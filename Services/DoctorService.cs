using StudentManagement.Models;

namespace StudentManagement.Services;

public class DoctorService : IDoctorService
{
    private readonly List<Doctor> doctors;

    public DoctorService(IEnumerable<Doctor> initialDoctors)
    {
        doctors = initialDoctors.ToList();
    }

    public List<Doctor> GetAll(string? specialty = null)
    {
        var query = doctors.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(specialty))
            query = query.Where(doctor => doctor.Specialty.Contains(specialty.Trim(), StringComparison.OrdinalIgnoreCase));

        return query.OrderBy(doctor => doctor.FullName).ToList();
    }

    public Doctor GetById(int id)
    {
        return doctors.FirstOrDefault(doctor => doctor.Id == id)
            ?? throw new KeyNotFoundException($"Không tìm thấy bác sĩ có mã {id}.");
    }

    public Doctor Add(Doctor doctor)
    {
        if (doctors.Any(item => item.Id == doctor.Id))
            throw new InvalidOperationException($"Mã bác sĩ {doctor.Id} đã tồn tại.");

        doctors.Add(doctor);
        return doctor;
    }

    public void Update(int id, Doctor doctor)
    {
        var existingDoctor = GetById(id);
        existingDoctor.FullName = doctor.FullName;
        existingDoctor.Specialty = doctor.Specialty;
        existingDoctor.Phone = doctor.Phone;
        existingDoctor.Room = doctor.Room;
    }

    public void Remove(int id)
    {
        doctors.Remove(GetById(id));
    }
}
