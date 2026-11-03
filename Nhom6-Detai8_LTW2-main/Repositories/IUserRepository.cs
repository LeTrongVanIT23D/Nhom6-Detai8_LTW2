using StudentManagement.DTOs;
using StudentManagement.Models;

namespace StudentManagement.Repositories;

public interface IUserRepository
{
    List<User> GetAll();
    Task<PagedResult<User>> GetAllAsync(UserQueryParameters queryParams);
    User? GetById(int id);
    bool UsernameExists(string username);
    bool DoctorExists(int doctorId);
    bool DoctorAssignedToAnotherUser(int doctorId, int userId);
    void Add(User user);
    void Remove(User user);
    void SaveChanges();
}
