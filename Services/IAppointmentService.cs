using StudentManagement.Models;

namespace StudentManagement.Services;

public interface IAppointmentService
{
    List<Appointment> GetAll(DateTime? date = null, AppointmentStatus? status = null);
    Appointment GetById(int id);
    Appointment Add(Appointment appointment);
    void Update(int id, Appointment appointment);
    void Remove(int id);
}
