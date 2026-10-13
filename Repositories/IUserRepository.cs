using StudentManagement.Models;

namespace StudentManagement.Repositories;

public interface IUserRepository
{
    List<User> GetAll();
    User? GetById(int id);
    bool UsernameExists(string username);
    void Add(User user);
    void Remove(User user);
    void SaveChanges();
}
