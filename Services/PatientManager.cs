using StudentManagement.Models;

namespace StudentManagement.Services;

public class PatientManager : IPatientManager
{
    private readonly List<Patient> patients;

    public PatientManager(IEnumerable<Patient> initialPatients)
    {
        patients = initialPatients.ToList();
    }

    public List<Patient> GetAll()
    {
        return patients.OrderBy(patient => patient.Id).ToList();
    }

    public Patient FindById(int id)
    {
        return patients.FirstOrDefault(patient => patient.Id == id)
            ?? throw new KeyNotFoundException($"Không tìm thấy bệnh nhân có mã {id}.");
    }

    public void Add(Patient patient)
    {
        if (patients.Any(item => item.Id == patient.Id))
            throw new InvalidOperationException($"Mã bệnh nhân {patient.Id} đã tồn tại.");

        patients.Add(patient);
    }

    public void Update(Patient patient)
    {
        var existingPatient = FindById(patient.Id);
        existingPatient.FullName = patient.FullName;
        existingPatient.DateOfBirth = patient.DateOfBirth;
        existingPatient.Phone = patient.Phone;
        existingPatient.Address = patient.Address;
        existingPatient.MedicalHistory = patient.MedicalHistory;
    }

    public void Remove(int id)
    {
        patients.Remove(FindById(id));
    }
}