using StudentManagement.Models;

namespace StudentManagement.Services;

public class MedicalRecordService : IMedicalRecordService
{
    private readonly List<MedicalRecord> records = [];
    private readonly List<Prescription> prescriptions = [];
    private readonly IPatientManager patientManager;
    private readonly IDoctorService doctorService;

    public MedicalRecordService(IPatientManager patientManager, IDoctorService doctorService)
    {
        this.patientManager = patientManager;
        this.doctorService = doctorService;
    }

    public List<MedicalRecord> GetAll(int? patientId = null)
    {
        var query = records.AsEnumerable();
        if (patientId.HasValue) query = query.Where(item => item.PatientId == patientId.Value);
        return query.OrderByDescending(item => item.ExaminedAt).ToList();
    }

    public MedicalRecord GetById(int id) => records.FirstOrDefault(item => item.Id == id)
        ?? throw new KeyNotFoundException("Không tìm thấy hồ sơ bệnh án.");

    public MedicalRecord Add(MedicalRecord record)
    {
        patientManager.FindById(record.PatientId);
        doctorService.GetById(record.DoctorId);
        record.Id = records.Select(item => item.Id).DefaultIfEmpty(0).Max() + 1;
        record.ExaminedAt = DateTime.Now;
        records.Add(record);
        return record;
    }

    public List<Prescription> GetPrescriptions(int recordId)
    {
        GetById(recordId);
        return prescriptions.Where(item => item.MedicalRecordId == recordId).ToList();
    }

    public Prescription AddPrescription(Prescription prescription)
    {
        GetById(prescription.MedicalRecordId);
        prescription.Id = prescriptions.Select(item => item.Id).DefaultIfEmpty(0).Max() + 1;
        prescription.CreatedAt = DateTime.Now;
        prescriptions.Add(prescription);
        return prescription;
    }
}