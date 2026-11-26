using StudentManagement.Data;
using StudentManagement.DTOs;
using StudentManagement.Models;

namespace StudentManagement.Services;

public class AppointmentService : IAppointmentService
{
    private readonly AppDbContext context;
    private readonly IPatientManager patientManager;
    private readonly IDoctorService doctorService;

    public AppointmentService(
        AppDbContext context,
        IPatientManager patientManager,
        IDoctorService doctorService)
    {
        this.context = context;
        this.patientManager = patientManager;
        this.doctorService = doctorService;
    }

    public List<Appointment> GetAll(DateTime? date = null, AppointmentStatus? status = null)
    {
        var query = context.Appointments.AsQueryable();
        if (date.HasValue)
            query = query.Where(item => item.AppointmentDate.Date == date.Value.Date);
        if (status.HasValue)
            query = query.Where(item => item.Status == status.Value);

        return query.OrderBy(item => item.AppointmentDate).ToList();
    }

    public List<Appointment> GetForUser(int userId)
    {
        return context.Appointments
            .Where(item => item.UserId == userId ||
                context.Patients.Any(patient => patient.UserId == userId && patient.Id == item.PatientId))
            .OrderByDescending(item => item.AppointmentDate)
            .ToList();
    }

    public List<Appointment> GetForDoctor(int doctorId, DateTime? date = null, AppointmentStatus? status = null)
    {
        var query = context.Appointments.Where(item => item.DoctorId == doctorId);
        if (date.HasValue)
            query = query.Where(item => item.AppointmentDate.Date == date.Value.Date);
        if (status.HasValue)
            query = query.Where(item => item.Status == status.Value);
        return query.OrderBy(item => item.AppointmentDate).ToList();
    }

    public List<Appointment> GetDoctorTodayQueue(int doctorId, DateTime? date = null)
    {
        var targetDate = (date ?? DateTime.Today).Date;
        return context.Appointments
            .Where(item => item.DoctorId == doctorId &&
                           item.Status == AppointmentStatus.CheckedIn &&
                           item.AppointmentDate.Date == targetDate)
            .OrderBy(item => item.QueueNumber ?? 999)
            .ThenBy(item => item.AppointmentDate)
            .ToList();
    }

    public List<Appointment> GetForPatientByDoctor(int patientId, int doctorId) =>
        context.Appointments
            .Where(item => item.PatientId == patientId && item.DoctorId == doctorId)
            .OrderByDescending(item => item.AppointmentDate)
            .ToList();

    public Appointment GetById(int id)
    {
        return context.Appointments.FirstOrDefault(item => item.Id == id)
            ?? throw new KeyNotFoundException($"Không tìm thấy lịch hẹn có mã {id}.");
    }

    public Appointment Add(Appointment appointment)
    {
        if (appointment.AppointmentDate <= DateTime.Now)
            throw new ArgumentException("Thời gian khám phải sau thời điểm hiện tại.");
        if (string.IsNullOrWhiteSpace(appointment.Reason))
            throw new ArgumentException("Vui lòng nhập lý do khám.");
        ValidateReferences(appointment);
        EnsureDoctorIsAvailable(appointment);

        context.Appointments.Add(appointment);
        context.SaveChanges();
        return appointment;
    }

    public void Update(int id, Appointment appointment)
    {
        if (appointment.AppointmentDate <= DateTime.Now)
            throw new ArgumentException("Thời gian khám phải sau thời điểm hiện tại.");
        if (string.IsNullOrWhiteSpace(appointment.Reason))
            throw new ArgumentException("Vui lòng nhập lý do khám.");
        ValidateReferences(appointment);
        EnsureDoctorIsAvailable(appointment, id);

        var existingAppointment = GetById(id);
        if (existingAppointment.Status is not (AppointmentStatus.Pending or AppointmentStatus.Confirmed or AppointmentStatus.NeedReschedule or AppointmentStatus.Scheduled))
            throw new InvalidOperationException("Không thể cập nhật lịch hẹn đã check-in, hoàn thành hoặc đã hủy.");
        existingAppointment.PatientId = appointment.PatientId;
        existingAppointment.DoctorId = appointment.DoctorId;
        existingAppointment.AppointmentDate = appointment.AppointmentDate;
        existingAppointment.Reason = appointment.Reason;
        context.SaveChanges();
    }

    public void Remove(int id)
    {
        var appointment = GetById(id);
        context.Appointments.Remove(appointment);
        context.SaveChanges();
    }

    public void Cancel(int id, int userId)
    {
        var appointment = GetById(id);
        if (!GetForUser(userId).Any(item => item.Id == appointment.Id))
            throw new UnauthorizedAccessException("Bạn không có quyền thao tác lịch hẹn này.");
        if (appointment.Status is not (AppointmentStatus.Pending or AppointmentStatus.Confirmed or AppointmentStatus.NeedReschedule))
            throw new InvalidOperationException("Chỉ lịch hẹn đang chờ hoặc đã được duyệt mới có thể hủy.");

        RestoreBookingPoints(appointment);
        ApplyPatientCancellationRefundPolicy(appointment);

        appointment.Status = AppointmentStatus.Cancelled;
        context.SaveChanges();
    }

    public void CancelByClinic(int id)
    {
        var appointment = GetById(id);
        if (appointment.Status is not (AppointmentStatus.Pending or AppointmentStatus.Confirmed or AppointmentStatus.NeedReschedule))
            throw new InvalidOperationException("Chỉ lịch chưa khám mới có thể bị hủy bởi phòng khám.");

        RestoreBookingPoints(appointment);
        if (appointment.BookingFeeIsPaid && appointment.BookingFeePaymentMethod == "BankTransfer")
            appointment.BookingFeeRefundDue = true;

        appointment.Status = AppointmentStatus.Cancelled;
        context.SaveChanges();
    }

    public void MarkNoShow(int id)
    {
        var appointment = GetById(id);
        if (appointment.AppointmentDate > DateTime.Now)
            throw new InvalidOperationException("Chỉ được ghi nhận bỏ hẹn sau giờ khám.");
        if (appointment.Status is not (AppointmentStatus.Pending or AppointmentStatus.Confirmed))
            throw new InvalidOperationException("Chỉ lịch đang chờ hoặc đã xác nhận mới có thể ghi nhận bỏ hẹn.");

        appointment.Status = AppointmentStatus.NoShow;
        appointment.BookingFeeForfeited = appointment.BookingFeeIsPaid;
        appointment.BookingFeeRefundDue = false;
        context.SaveChanges();
    }

    public void ConfirmBookingFeeRefund(int id)
    {
        var appointment = GetById(id);
        if (appointment.Status != AppointmentStatus.Cancelled || !appointment.BookingFeeRefundDue)
            throw new InvalidOperationException("Lịch hẹn này không có khoản phí giữ chỗ đang chờ hoàn.");

        appointment.BookingFeeRefundDue = false;
        appointment.BookingFeeRefunded = true;
        context.SaveChanges();
    }

    private void RestoreBookingPoints(Appointment appointment)
    {
        if (appointment.PointsUsed <= 0) return;

        var patient = patientManager.FindById(appointment.PatientId);
        var ownerId = appointment.UserId ?? patient.UserId;
        var user = ownerId.HasValue ? context.Users.Find(ownerId.Value) : null;
        var restoredPoints = (user?.RewardPoints ?? patient.RewardPoints) + appointment.PointsUsed;
        if (user != null)
            user.RewardPoints = restoredPoints;
        patient.RewardPoints = restoredPoints;
    }

    private static void ApplyPatientCancellationRefundPolicy(Appointment appointment)
    {
        if (!appointment.BookingFeeIsPaid) return;
        if (appointment.BookingFeePaymentMethod == "RewardPoints") return;
        if (appointment.BookingFeePaymentMethod == "BankTransfer" &&
            appointment.AppointmentDate - DateTime.Now >= TimeSpan.FromHours(24))
        {
            appointment.BookingFeeRefundDue = true;
            return;
        }

        appointment.BookingFeeForfeited = true;
    }

    public Appointment Confirm(int id, int? receptionistUserId = null)
    {
        var appointment = GetById(id);
        if (appointment.Status != AppointmentStatus.Pending)
            throw new InvalidOperationException("Chỉ lịch hẹn đang chờ mới được duyệt.");
        if (appointment.AppointmentDate <= DateTime.Now)
            throw new InvalidOperationException("Không thể duyệt lịch hẹn đã qua thời gian khám.");
        EnsureDoctorIsAvailable(appointment, id);
        appointment.Status = AppointmentStatus.Confirmed;
        if (receptionistUserId.HasValue)
            appointment.ReceptionistId = receptionistUserId.Value;
        context.SaveChanges();
        return appointment;
    }

    public Appointment CheckIn(int id, int? receptionistUserId = null)
    {
        var appointment = GetById(id);
        if (appointment.Status != AppointmentStatus.Confirmed && appointment.Status != AppointmentStatus.Pending)
            throw new InvalidOperationException("Chỉ lịch hẹn đang chờ hoặc đã được duyệt mới được check-in.");
        if (appointment.AppointmentDate.Date != DateTime.Today)
            throw new InvalidOperationException("Chỉ có thể check-in vào ngày hẹn khám.");
        appointment.Status = AppointmentStatus.CheckedIn;
        if (receptionistUserId.HasValue)
            appointment.ReceptionistId = receptionistUserId.Value;

        if (!appointment.QueueNumber.HasValue)
        {
            var maxQueue = context.Appointments
                .Where(a => a.DoctorId == appointment.DoctorId &&
                            a.AppointmentDate.Date == appointment.AppointmentDate.Date &&
                            a.QueueNumber.HasValue)
                .Max(a => (int?)a.QueueNumber) ?? 0;
            appointment.QueueNumber = maxQueue + 1;
        }

        context.SaveChanges();
        return appointment;
    }

    public MedicalRecord CompleteExam(int id, CompleteExamDto dto, int? doctorId = null)
    {
        using var transaction = context.Database.BeginTransaction();
        var appointment = GetById(id);
        if (doctorId.HasValue && appointment.DoctorId != doctorId.Value)
            throw new UnauthorizedAccessException("Ca khám này được phân công cho bác sĩ khác.");
        if (appointment.Status != AppointmentStatus.CheckedIn)
            throw new InvalidOperationException("Chỉ ca khám đã check-in mới được hoàn tất.");
        if (context.MedicalRecords.Any(record => record.AppointmentId == appointment.Id))
            throw new InvalidOperationException("Ca khám này đã có hồ sơ bệnh án.");
        appointment.Status = AppointmentStatus.Completed;

        var record = new MedicalRecord
        {
            PatientId = appointment.PatientId,
            DoctorId = appointment.DoctorId,
            AppointmentId = appointment.Id,
            ExaminedAt = DateTime.Now,
            Symptoms = (dto.Symptoms ?? "").Trim(),
            Diagnosis = (dto.Diagnosis ?? "").Trim(),
            Notes = (dto.Notes ?? "").Trim(),
            BloodPressure = (dto.BloodPressure ?? "").Trim(),
            HeartRate = dto.HeartRate,
            Temperature = dto.Temperature,
            Weight = dto.Weight,
            TreatmentPlan = (dto.TreatmentPlan ?? "").Trim(),
            FollowUpDate = dto.FollowUpDate,
            LabTests = (dto.LabTests ?? "").Trim()
        };
        context.MedicalRecords.Add(record);
        context.SaveChanges();

        if (!string.IsNullOrWhiteSpace(dto.Medicines))
        {
            var prescription = new Prescription
            {
                MedicalRecordId = record.Id,
                CreatedAt = DateTime.Now,
                Medicines = dto.Medicines.Trim(),
                Instructions = (dto.Instructions ?? "").Trim()
            };
            context.Prescriptions.Add(prescription);
            context.SaveChanges();
        }

        transaction.Commit();
        return record;
    }

    public Appointment Reschedule(int id, DateTime newDate, int? newDoctorId = null, string? reason = null, int? userId = null, string? role = null)
    {
        var appointment = GetById(id);

        if (role == "User" && userId.HasValue && appointment.UserId != userId.Value)
        {
            var patient = patientManager.FindByUserId(userId.Value);
            if (patient == null || patient.Id != appointment.PatientId)
                throw new UnauthorizedAccessException("Bạn không có quyền đổi lịch hẹn này.");
        }

        if (appointment.Status is not (AppointmentStatus.Pending or AppointmentStatus.Confirmed or AppointmentStatus.NeedReschedule or AppointmentStatus.Scheduled))
        {
            throw new InvalidOperationException("Chỉ lịch hẹn đang chờ, đã duyệt hoặc cần đổi lịch mới có thể đổi.");
        }

        if (newDate <= DateTime.Now)
        {
            throw new ArgumentException("Thời gian khám mới phải sau thời điểm hiện tại.");
        }

        var targetDoctorId = newDoctorId ?? appointment.DoctorId;
        doctorService.GetById(targetDoctorId);

        EnsureDoctorIsAvailable(new Appointment
        {
            DoctorId = targetDoctorId,
            AppointmentDate = newDate
        }, id);

        appointment.AppointmentDate = newDate;
        appointment.DoctorId = targetDoctorId;
        if (!string.IsNullOrWhiteSpace(reason))
            appointment.Reason = reason.Trim();

        // Nếu trước đó đang chờ đổi lịch (NeedReschedule), chuyển về Confirmed
        if (appointment.Status == AppointmentStatus.NeedReschedule)
            appointment.Status = AppointmentStatus.Confirmed;

        appointment.QueueNumber = null;
        context.SaveChanges();
        return appointment;
    }

    private void ValidateReferences(Appointment appointment)
    {
        patientManager.FindById(appointment.PatientId);
        doctorService.GetById(appointment.DoctorId);
    }

    private void EnsureDoctorIsAvailable(Appointment appointment, int? ignoredId = null)
    {
        var doctor = doctorService.GetById(appointment.DoctorId);
        if (!doctor.IsActive)
            throw new InvalidOperationException("Bác sĩ hiện không nhận lịch khám.");

        var appointmentTime = TimeOnly.FromDateTime(appointment.AppointmentDate);
        var hasWorkingSchedule = context.DoctorSchedules.Any(schedule =>
            schedule.DoctorId == appointment.DoctorId &&
            schedule.DayOfWeek == appointment.AppointmentDate.DayOfWeek &&
            !schedule.IsDayOff &&
            schedule.StartTime <= appointmentTime &&
            schedule.EndTime > appointmentTime);
        if (!hasWorkingSchedule)
            throw new InvalidOperationException("Bác sĩ không có lịch làm việc vào thời gian đã chọn.");

        var hasConflict = context.Appointments.Any(item =>
            item.Id != ignoredId &&
            item.DoctorId == appointment.DoctorId &&
            (item.Status == AppointmentStatus.Pending ||
             item.Status == AppointmentStatus.Confirmed ||
             item.Status == AppointmentStatus.CheckedIn ||
             item.Status == AppointmentStatus.Scheduled) &&
            item.AppointmentDate == appointment.AppointmentDate);

        if (hasConflict)
            throw new InvalidOperationException("Bác sĩ đã có lịch hẹn trùng thời gian này.");

        var hasApprovedLeave = context.DoctorLeaveRequests.Any(leave =>
            leave.DoctorId == appointment.DoctorId &&
            leave.Status == LeaveRequestStatus.Approved &&
            leave.LeaveDate.Date == appointment.AppointmentDate.Date &&
            (leave.Shift == "allday" ||
             (leave.Shift == "morning" && appointment.AppointmentDate.Hour >= 6 && appointment.AppointmentDate.Hour < 12) ||
             (leave.Shift == "afternoon" && appointment.AppointmentDate.Hour >= 12 && appointment.AppointmentDate.Hour < 17) ||
             (leave.Shift == "evening" && appointment.AppointmentDate.Hour >= 17 && appointment.AppointmentDate.Hour < 22)));
        if (hasApprovedLeave)
            throw new InvalidOperationException("Bác sĩ đã có lịch nghỉ phép vào ca này.");
    }
}
