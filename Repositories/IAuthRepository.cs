using StudentManagement.Models;

namespace StudentManagement.Repositories;

public interface IAuthRepository
{
    User? GetByUsername(string username);
    User? GetById(int id);
    bool UsernameExists(string username);
    bool UserExists(string usernameOrEmail);
    Doctor? GetDoctorByUserId(int userId);
    Doctor? GetFirstAvailableDoctor();
    void Add(User user);
    void SaveChanges();
}
