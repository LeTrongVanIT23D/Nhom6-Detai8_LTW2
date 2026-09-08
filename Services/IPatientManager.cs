using StudentManagement.Models;

namespace StudentManagement.Services;

public interface IPatientManager
{
    List<Patient> GetAll();
    Patient FindById(int id);
    void Add(Patient patient);
    void Update(Patient patient);
    void Remove(int id);
}