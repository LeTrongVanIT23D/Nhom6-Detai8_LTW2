using StudentManagement.Data;
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
        return context.Appointments.Where(item => item.UserId == userId).OrderBy(item => item.AppointmentDate).ToList();
    }

    public Appointment GetById(int id)
    {
        return context.Appointments.FirstOrDefault(item => item.Id == id)
            ?? throw new KeyNotFoundException($"Không tìm thấy lịch hẹn có mã {id}.");
    }

    public Appointment Add(Appointment appointment)
    {
        ValidateReferences(appointment);
        EnsureDoctorIsAvailable(appointment);

        if (appointment.Id > 0 && context.Appointments.Any(item => item.Id == appointment.Id))
            throw new InvalidOperationException($"Mã lịch hẹn {appointment.Id} đã tồn tại.");

        context.Appointments.Add(appointment);
        context.SaveChanges();
        return appointment;
    }

    public void Update(int id, Appointment appointment)
    {
        ValidateReferences(appointment);
        EnsureDoctorIsAvailable(appointment, id);

        var existingAppointment = GetById(id);
        existingAppointment.PatientId = appointment.PatientId;
        existingAppointment.DoctorId = appointment.DoctorId;
        existingAppointment.AppointmentDate = appointment.AppointmentDate;
        existingAppointment.Reason = appointment.Reason;
        existingAppointment.Status = appointment.Status;
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
        if (appointment.UserId != userId)
            throw new UnauthorizedAccessException("Bạn không có quyền thao tác lịch hẹn này.");
        if (appointment.Status != AppointmentStatus.Scheduled)
            throw new InvalidOperationException("Chỉ lịch hẹn đang chờ mới có thể hủy.");

        appointment.Status = AppointmentStatus.Cancelled;
        context.SaveChanges();
    }

    private void ValidateReferences(Appointment appointment)
    {
        patientManager.FindById(appointment.PatientId);
        doctorService.GetById(appointment.DoctorId);
        if (appointment.AppointmentDate <= DateTime.Now.AddMinutes(-1))
            throw new ArgumentException("Thời gian lịch hẹn phải ở hiện tại hoặc tương lai.");
    }

    private void EnsureDoctorIsAvailable(Appointment appointment, int? ignoredId = null)
    {
        var hasConflict = context.Appointments.Any(item =>
            item.Id != ignoredId &&
            item.DoctorId == appointment.DoctorId &&
            item.Status == AppointmentStatus.Scheduled &&
            item.AppointmentDate == appointment.AppointmentDate);

        if (hasConflict)
            throw new InvalidOperationException("Bác sĩ đã có lịch hẹn vào thời gian này.");
    }
}
