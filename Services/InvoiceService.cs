using Microsoft.EntityFrameworkCore;
using StudentManagement.Data;
using StudentManagement.Models;

namespace StudentManagement.Services;

public class InvoiceService : IInvoiceService
{
    private readonly AppDbContext context;
    private readonly IAuditLogService auditLogService;

    public InvoiceService(AppDbContext context, IAuditLogService auditLogService)
    {
        this.context = context;
        this.auditLogService = auditLogService;
    }

    public async Task<Invoice> CreateInvoiceAsync(int appointmentId, decimal amount, string paymentMethod, string notes)
    {
        var appt = await context.Appointments.FindAsync(appointmentId);
        if (appt == null) throw new InvalidOperationException("Không tìm thấy lịch hẹn.");
        if (appt.Status != AppointmentStatus.Completed)
            throw new InvalidOperationException("Chỉ có thể lập viện phí sau khi ca khám hoàn thành.");
        if (amount <= 0)
            throw new InvalidOperationException("Số tiền hóa đơn phải lớn hơn 0.");
        if (paymentMethod is not ("Cash" or "Transfer"))
            throw new InvalidOperationException("Phương thức thanh toán không hợp lệ.");

        var existing = await context.Invoices.FirstOrDefaultAsync(i => i.AppointmentId == appointmentId);
        if (existing != null)
        {
            if (existing.Status == "Paid")
                throw new InvalidOperationException("Ca khám này đã được thanh toán.");
            existing.Amount = amount;
            existing.PaymentMethod = paymentMethod;
            existing.Notes = notes;
            await context.SaveChangesAsync();
            return existing;
        }

        var invoice = new Invoice
        {
            AppointmentId = appointmentId,
            PatientId = appt.PatientId,
            Amount = amount,
            PaymentMethod = paymentMethod,
            Status = "Pending",
            Notes = notes,
            CreatedAt = DateTime.Now
        };

        context.Invoices.Add(invoice);
        await context.SaveChangesAsync();

        await auditLogService.LogAsync("CREATE_INVOICE", "System/Receptionist", "Receptionist", $"Tạo hóa đơn #{invoice.Id} cho lịch hẹn #{appointmentId}, số tiền {amount:N0} VNĐ");

        return invoice;
    }

    public async Task<List<InvoiceDto>> GetInvoicesAsync()
    {
        var list = await context.Invoices
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        var apptIds = list.Select(i => i.AppointmentId).ToList();
        var appointments = await context.Appointments.Where(a => apptIds.Contains(a.Id)).ToListAsync();
        var patientIds = list.Select(i => i.PatientId).ToList();
        var patients = await context.Patients.Where(p => patientIds.Contains(p.Id)).ToListAsync();
        var doctorIds = appointments.Select(a => a.DoctorId).ToList();
        var doctors = await context.Doctors.Where(d => doctorIds.Contains(d.Id)).ToListAsync();

        return list.Select(inv =>
        {
            var appt = appointments.FirstOrDefault(a => a.Id == inv.AppointmentId);
            var pat = patients.FirstOrDefault(p => p.Id == inv.PatientId);
            var doc = appt != null ? doctors.FirstOrDefault(d => d.Id == appt.DoctorId) : null;

            return new InvoiceDto
            {
                Id = inv.Id,
                AppointmentId = inv.AppointmentId,
                PatientId = inv.PatientId,
                PatientName = pat?.FullName ?? $"Bệnh nhân #{inv.PatientId}",
                DoctorName = doc?.FullName ?? "N/A",
                Amount = inv.Amount,
                PaymentMethod = inv.PaymentMethod,
                Status = inv.Status,
                Notes = inv.Notes,
                CreatedAt = inv.CreatedAt,
                PaidAt = inv.PaidAt
            };
        }).ToList();
    }

    public async Task<InvoiceDto?> GetInvoiceByAppointmentIdAsync(int appointmentId)
    {
        var inv = await context.Invoices.FirstOrDefaultAsync(i => i.AppointmentId == appointmentId);
        if (inv == null) return null;

        var pat = await context.Patients.FindAsync(inv.PatientId);
        var appt = await context.Appointments.FindAsync(inv.AppointmentId);
        Doctor? doc = null;
        if (appt != null) doc = await context.Doctors.FindAsync(appt.DoctorId);

        return new InvoiceDto
        {
            Id = inv.Id,
            AppointmentId = inv.AppointmentId,
            PatientId = inv.PatientId,
            PatientName = pat?.FullName ?? $"Bệnh nhân #{inv.PatientId}",
            DoctorName = doc?.FullName ?? "N/A",
            Amount = inv.Amount,
            PaymentMethod = inv.PaymentMethod,
            Status = inv.Status,
            Notes = inv.Notes,
            CreatedAt = inv.CreatedAt,
            PaidAt = inv.PaidAt
        };
    }

    public async Task<bool> ConfirmPaymentAsync(int invoiceId, string paymentMethod)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var inv = await context.Invoices.FindAsync(invoiceId);
        if (inv == null) return false;
        if (paymentMethod is not ("Cash" or "Transfer"))
            throw new InvalidOperationException("Phương thức thanh toán không hợp lệ.");

        var pointsEarned = 0;
        if (inv.Status != "Paid")
        {
            inv.Status = "Paid";
            inv.PaymentMethod = paymentMethod;
            inv.PaidAt = DateTime.Now;

            pointsEarned = (int)(inv.Amount / 100000m) * 10;
            if (pointsEarned > 0)
            {
                var patient = await context.Patients.FindAsync(inv.PatientId);
                if (patient != null)
                {
                    var user = patient.UserId.HasValue
                        ? await context.Users.FindAsync(patient.UserId.Value)
                        : null;
                    var updatedPoints = (user?.RewardPoints ?? patient.RewardPoints) + pointsEarned;
                    if (user != null)
                        user.RewardPoints = updatedPoints;
                    patient.RewardPoints = updatedPoints;
                }
            }

            await context.SaveChangesAsync();
        }

        await transaction.CommitAsync();

        await auditLogService.LogAsync("CONFIRM_PAYMENT", "Receptionist", "Receptionist", $"Xác nhận thanh toán hóa đơn #{invoiceId} qua {paymentMethod} (Tích lũy +{pointsEarned} điểm)");
        return true;
    }
}
