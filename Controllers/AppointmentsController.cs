using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Claims;
using StudentManagement.DTOs;
using StudentManagement.Models;
using StudentManagement.Services;

namespace StudentManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "User,Doctor,Receptionist,Admin,Support")]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentService appointmentService;
    private readonly INotificationService notificationService;
    private readonly IPatientManager patientManager;
    private readonly IDoctorService doctorService;
    private readonly StudentManagement.Data.AppDbContext context;
    private readonly BankTransferService bankTransferService;
    private readonly IAuditLogService auditLogService;

    public AppointmentsController(
        IAppointmentService appointmentService,
        INotificationService notificationService,
        IPatientManager patientManager,
        IDoctorService doctorService,
        StudentManagement.Data.AppDbContext context,
        BankTransferService bankTransferService,
        IAuditLogService auditLogService)
    {
        this.appointmentService = appointmentService;
        this.notificationService = notificationService;
        this.patientManager = patientManager;
        this.doctorService = doctorService;
        this.context = context;
        this.bankTransferService = bankTransferService;
        this.auditLogService = auditLogService;
    }

    [HttpGet]
    public ActionResult<List<Appointment>> GetAll([FromQuery] DateTime? date = null, [FromQuery] AppointmentStatus? status = null)
    {
        if (User.IsInRole("User"))
        {
            var appointments = appointmentService.GetForUser(int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!));
            AddPaymentInstructions(appointments);
            return Ok(appointments);
        }
        if (User.IsInRole("Doctor"))
        {
            if (!TryGetDoctorId(out var doctorId))
                return Conflict(new { message = "Tài khoản bác sĩ chưa được quản trị viên gắn với hồ sơ bác sĩ." });

            // Nếu bác sĩ lọc bệnh nhân CheckedIn mà không truyền ngày, bắt buộc lọc ngày hôm nay
            var filterDate = (status == AppointmentStatus.CheckedIn && !date.HasValue) ? DateTime.Today : date;
            return Ok(appointmentService.GetForDoctor(doctorId, filterDate, status));
        }
        return Ok(appointmentService.GetAll(date, status));
    }

    /// <summary>
    /// API Hàng đợi khám bệnh dành riêng cho Bác sĩ.
    /// BẮT BUỘC có bộ lọc: WHERE DoctorId = @MyDoctorId AND Status = 'CheckedIn' AND Date = @Today.
    /// Bác sĩ tuyệt đối không thấy bệnh nhân của khoa khác/người khác.
    /// </summary>
    [HttpGet("queue")]
    [Authorize(Roles = "Doctor")]
    public ActionResult<List<Appointment>> GetDoctorQueue([FromQuery] DateTime? date = null)
    {
        if (!TryGetDoctorId(out var doctorId))
            return Conflict(new { message = "Tài khoản bác sĩ chưa được quản trị viên gắn với hồ sơ bác sĩ." });

        var targetDate = (date ?? DateTime.Today).Date;
        var queue = appointmentService.GetDoctorTodayQueue(doctorId, targetDate);
        return Ok(queue);
    }

    [HttpGet("{id:int}")]
    public ActionResult<Appointment> GetById(int id)
    {
        try
        {
            var appointment = appointmentService.GetById(id);
            if (User.IsInRole("User") &&
                !appointmentService.GetForUser(int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!))
                    .Any(item => item.Id == appointment.Id))
                return Forbid();
            if (User.IsInRole("Doctor") &&
                (!TryGetDoctorId(out var doctorId) || appointment.DoctorId != doctorId))
                return Forbid();
            if (User.IsInRole("User"))
                AddPaymentInstructions(appointment);
            return Ok(appointment);
        }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    [HttpPost]
    [Authorize(Roles = "User,Receptionist,Admin,Support")]
    public async Task<ActionResult<Appointment>> Create(AppointmentDto dto)
    {
        try
        {
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var patientId = dto.PatientId;
            int? apptUserId = null;

            if (User.IsInRole("User"))
            {
                var uid = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                patientId = patientManager.FindByUserId(uid).Id;
                apptUserId = uid;
            }
            else
            {
                if (patientId <= 0 && dto.UserId.HasValue)
                {
                    try
                    {
                        var p = patientManager.FindByUserId(dto.UserId.Value);
                        if (p != null) patientId = p.Id;
                    }
                    catch { /* fallback to direct patientId */ }
                }
                apptUserId = dto.UserId;
            }

            const decimal baseBookingFee = 100000m;
            decimal discount = 0;
            if (dto.PointsUsed > 0 && !User.IsInRole("User"))
                throw new InvalidOperationException("Chỉ bệnh nhân đặt lịch trực tuyến mới có thể dùng điểm thưởng.");
            int pointsUsed = Math.Max(0, dto.PointsUsed);
            StudentManagement.Models.Patient? patientObj = null;
            StudentManagement.Models.User? userObj = null;
            var actualDeduction = 0;

            if (pointsUsed > 0)
            {
                patientObj = patientManager.FindById(patientId);
                userObj = apptUserId.HasValue ? await context.Users.FindAsync(apptUserId.Value) : null;
                int currentPoints = userObj?.RewardPoints ?? patientObj.RewardPoints;

                if (pointsUsed > currentPoints)
                {
                    throw new InvalidOperationException($"Số điểm tích lũy hiện có ({currentPoints} điểm) không đủ để dùng {pointsUsed} điểm.");
                }

                // Cứ 1 điểm = 1.000 VNĐ giảm giá trực tiếp vào BookingFee (tối đa bằng phí giữ chỗ)
                discount = Math.Min(pointsUsed * 1000m, baseBookingFee);
                actualDeduction = (int)(discount / 1000m);
                pointsUsed = actualDeduction;
            }

            var feeDue = User.IsInRole("User") ? Math.Max(0, baseBookingFee - discount) : 0m;
            if (feeDue > 0 && !bankTransferService.IsConfigured)
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    message = "Chưa cấu hình tài khoản nhận chuyển khoản BIDV. Lịch chưa được tạo; vui lòng báo quản trị viên cấu hình BankTransfer."
                });

            var appointment = appointmentService.Add(new Appointment
            {
                PatientId = patientId,
                DoctorId = dto.DoctorId,
                UserId = apptUserId,
                AppointmentDate = dto.AppointmentDate,
                BookingSource = User.IsInRole("User")
                    ? AppointmentBookingSource.Online
                    : AppointmentBookingSource.InPerson,
                Reason = dto.Reason.Trim(),
                Status = AppointmentStatus.Pending,
                BookingFee = User.IsInRole("User") ? baseBookingFee : 0m,
                DiscountAmount = discount,
                PointsUsed = pointsUsed,
                BookingFeeIsPaid = User.IsInRole("User") && feeDue == 0,
                BookingFeePaymentMethod = feeDue > 0 ? "BankTransfer" : User.IsInRole("User") ? "RewardPoints" : string.Empty,
                BookingFeePaidAt = User.IsInRole("User") && feeDue == 0 ? DateTime.Now : null
            });

            if (actualDeduction > 0 && patientObj != null)
            {
                var remainingPoints = (userObj?.RewardPoints ?? patientObj.RewardPoints) - actualDeduction;
                if (userObj != null)
                    userObj.RewardPoints = remainingPoints;
                patientObj.RewardPoints = remainingPoints;
                await context.SaveChangesAsync();
            }

            await transaction.CommitAsync();
            AddPaymentInstructions(appointment);

            try
            {
                var patient = patientManager.FindById(appointment.PatientId);
                await notificationService.NotifyAppointmentCreatedAsync(appointment, patient.FullName);
            }
            catch { /* Notification failure should not break appointment creation */ }

            return CreatedAtAction(nameof(GetById), new { id = appointment.Id }, appointment);
        }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Receptionist,Admin")]
    public IActionResult Update(int id, AppointmentDto dto)
    {
        try
        {
            appointmentService.Update(id, new Appointment
            {
                PatientId = dto.PatientId, DoctorId = dto.DoctorId, AppointmentDate = dto.AppointmentDate,
                Reason = dto.Reason.Trim(), Status = dto.Status
            });
            return NoContent();
        }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpPatch("{id:int}/confirm")]
    [Authorize(Roles = "Receptionist,Admin")]
    public async Task<ActionResult<Appointment>> Confirm(int id)
    {
        try
        {
            var current = appointmentService.GetById(id);
            if (RequiresOnlineBookingPayment(current) && !current.BookingFeeIsPaid)
                return Conflict(new { message = "Hãy đối soát phí giữ chỗ trước khi xác nhận lịch hẹn." });

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var appointment = appointmentService.Confirm(id, userId);

            try
            {
                var patient = patientManager.FindById(appointment.PatientId);
                var doctor = doctorService.GetById(appointment.DoctorId);
                await notificationService.NotifyAppointmentConfirmedAsync(appointment, patient.FullName, doctor.FullName);
            }
            catch { /* Keep transaction clean */ }

            return Ok(appointment);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPatch("{id:int}/check-in")]
    [Authorize(Roles = "Receptionist,Admin")]
    public async Task<ActionResult<Appointment>> CheckIn(int id)
    {
        try
        {
            var current = appointmentService.GetById(id);
            if (RequiresOnlineBookingPayment(current) && !current.BookingFeeIsPaid)
                return Conflict(new { message = "Bệnh nhân chưa thanh toán phí giữ chỗ. Hãy đối soát trước khi check-in." });

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var appointment = appointmentService.CheckIn(id, userId);

            try
            {
                var patient = patientManager.FindById(appointment.PatientId);
                await notificationService.NotifyPatientCheckedInAsync(appointment, patient.FullName);
            }
            catch { /* Keep transaction clean */ }

            return Ok(appointment);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPost("{id:int}/complete")]
    [Authorize(Roles = "Doctor,Admin")]
    public async Task<ActionResult<MedicalRecord>> Complete(int id, [FromBody] CompleteExamDto dto)
    {
        try
        {
            int? doctorId = null;
            if (User.IsInRole("Doctor"))
            {
                if (!TryGetDoctorId(out var assignedDoctorId))
                    return Conflict(new { message = "Tài khoản bác sĩ chưa được quản trị viên gắn với hồ sơ bác sĩ." });
                doctorId = assignedDoctorId;
            }
            var record = appointmentService.CompleteExam(id, dto, doctorId);

            try
            {
                var appointment = appointmentService.GetById(id);
                var patient = patientManager.FindById(appointment.PatientId);
                var doctor = doctorService.GetById(appointment.DoctorId);
                await notificationService.NotifyExamCompletedAsync(appointment, patient.FullName, doctor.FullName);
            }
            catch { /* Keep transaction clean */ }

            return Ok(record);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Receptionist,Admin")]
    public IActionResult Delete(int id)
    {
        try { appointmentService.Remove(id); return NoContent(); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    [HttpPatch("{id:int}/cancel")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> Cancel(int id)
    {
        try
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            appointmentService.Cancel(id, userId);

            try
            {
                var appointment = appointmentService.GetById(id);
                var patient = patientManager.FindById(appointment.PatientId);
                var userName = User.FindFirstValue(ClaimTypes.Name) ?? patient.FullName;
                await notificationService.NotifyAppointmentCancelledAsync(appointment, patient.FullName, userName);
            }
            catch { /* Notification failure should not break cancel */ }

            return NoContent();
        }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
        catch (UnauthorizedAccessException exception) { return StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpPatch("{id:int}/payment/confirm")]
    [Authorize(Roles = "Receptionist,Admin")]
    public async Task<ActionResult<Appointment>> ConfirmBookingPayment(int id, [FromBody] ConfirmBankTransferDto request)
    {
        try
        {
            var appointment = appointmentService.GetById(id);
            if (!RequiresOnlineBookingPayment(appointment))
                return Conflict(new { message = "Lịch hẹn này không có khoản phí giữ chỗ cần đối soát." });
            if (appointment.BookingFeeIsPaid)
                return Conflict(new { message = "Phí giữ chỗ của lịch này đã được xác nhận thanh toán." });
            if (appointment.Status != AppointmentStatus.Pending)
                return Conflict(new { message = "Chỉ đối soát được phí của lịch đang chờ xác nhận." });

            appointment.BookingFeeIsPaid = true;
            appointment.BookingFeePaidAt = DateTime.Now;
            appointment.BookingFeePaymentReference = request.TransactionReference.Trim();
            await context.SaveChangesAsync();
            await auditLogService.LogAsync(
                "BOOKING_FEE_CONFIRMED",
                User.FindFirstValue(ClaimTypes.Name) ?? "Staff",
                User.FindFirstValue(ClaimTypes.Role) ?? "Staff",
                $"Đối soát phí giữ chỗ lịch #{id}; mã giao dịch: {appointment.BookingFeePaymentReference}");
            return Ok(appointment);
        }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    [HttpPatch("{id:int}/cancel-by-clinic")]
    [Authorize(Roles = "Receptionist,Admin")]
    public IActionResult CancelByClinic(int id)
    {
        try
        {
            appointmentService.CancelByClinic(id);
            return NoContent();
        }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpPatch("{id:int}/no-show")]
    [Authorize(Roles = "Receptionist,Admin")]
    public IActionResult MarkNoShow(int id)
    {
        try
        {
            appointmentService.MarkNoShow(id);
            return NoContent();
        }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpPatch("{id:int}/refund/confirm")]
    [Authorize(Roles = "Receptionist,Admin")]
    public async Task<IActionResult> ConfirmBookingFeeRefund(int id)
    {
        try
        {
            appointmentService.ConfirmBookingFeeRefund(id);
            await auditLogService.LogAsync(
                "BOOKING_FEE_REFUNDED",
                User.FindFirstValue(ClaimTypes.Name) ?? "Staff",
                User.FindFirstValue(ClaimTypes.Role) ?? "Staff",
                $"Xác nhận đã chuyển hoàn phí giữ chỗ lịch #{id}");
            return NoContent();
        }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpPatch("{id:int}/reschedule")]
    [Authorize(Roles = "User,Doctor,Receptionist,Admin")]
    public async Task<ActionResult<Appointment>> Reschedule(int id, [FromBody] RescheduleAppointmentDto dto)
    {
        try
        {
            if (User.IsInRole("Doctor") &&
                (!TryGetDoctorId(out var doctorId) || appointmentService.GetById(id).DoctorId != doctorId))
                return Forbid();
            int? userId = null;
            if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid))
            {
                userId = uid;
            }
            var role = User.FindFirstValue(ClaimTypes.Role);

            var appointment = appointmentService.Reschedule(
                id,
                dto.NewAppointmentDate,
                dto.DoctorId,
                dto.Reason,
                userId,
                role);

            try
            {
                var patient = patientManager.FindById(appointment.PatientId);
                var doctor = doctorService.GetById(appointment.DoctorId);
                await notificationService.NotifyAppointmentRescheduledAsync(appointment, patient.FullName, doctor.FullName);
            }
            catch { /* Notification failure should not break reschedule */ }

            return Ok(appointment);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    private bool TryGetDoctorId(out int doctorId) =>
        int.TryParse(User.FindFirstValue("doctor_id"), out doctorId);

    private static bool RequiresOnlineBookingPayment(Appointment appointment) =>
        appointment.BookingSource == AppointmentBookingSource.Online &&
        appointment.BookingFee > appointment.DiscountAmount;

    private void AddPaymentInstructions(IEnumerable<Appointment> appointments)
    {
        foreach (var appointment in appointments)
            AddPaymentInstructions(appointment);
    }

    private void AddPaymentInstructions(Appointment appointment)
    {
        if (RequiresOnlineBookingPayment(appointment) &&
            !appointment.BookingFeeIsPaid &&
            appointment.Status != AppointmentStatus.Cancelled)
        {
            appointment.PaymentInstructions = bankTransferService.CreateDynamicInstructions(
                appointment.BookingFee - appointment.DiscountAmount,
                $"MEDIKA LICH {appointment.Id}");
        }
    }
}
