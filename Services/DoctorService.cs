using StudentManagement.Data;
using StudentManagement.Models;

namespace StudentManagement.Services;

public class DoctorService : IDoctorService
{
    private readonly AppDbContext context;

    public DoctorService(AppDbContext context)
    {
        this.context = context;
    }

    public List<Doctor> GetAll(string? specialty = null)
    {
        var query = context.Doctors.AsQueryable();
        if (!string.IsNullOrWhiteSpace(specialty))
            query = query.Where(doctor => doctor.Specialty.ToLower().Contains(specialty.Trim().ToLower()));

        return query.OrderBy(doctor => doctor.FullName).ToList();
    }

    public Doctor GetById(int id)
    {
        return context.Doctors.FirstOrDefault(doctor => doctor.Id == id)
            ?? throw new KeyNotFoundException($"Không tìm thấy bác sĩ có mã {id}.");
    }

    public Doctor Add(Doctor doctor)
    {
        if (doctor.Id > 0 && context.Doctors.Any(item => item.Id == doctor.Id))
            throw new InvalidOperationException($"Mã bác sĩ {doctor.Id} đã tồn tại.");

        context.Doctors.Add(doctor);
        context.SaveChanges();
        return doctor;
    }

    public void Update(int id, Doctor doctor)
    {
        var existingDoctor = GetById(id);
        existingDoctor.FullName = doctor.FullName;
        existingDoctor.Specialty = doctor.Specialty;
        existingDoctor.Phone = doctor.Phone;
        existingDoctor.Room = doctor.Room;
        context.SaveChanges();
    }

    public void Remove(int id)
    {
        var doctor = GetById(id);
        context.Doctors.Remove(doctor);
        context.SaveChanges();
    }
}
