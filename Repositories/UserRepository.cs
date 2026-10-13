using Microsoft.EntityFrameworkCore;
using StudentManagement.Data;
using StudentManagement.Models;

namespace StudentManagement.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext context;

    public UserRepository(AppDbContext context)
    {
        this.context = context;
    }

    public List<User> GetAll() => context.Users.AsNoTracking().OrderBy(user => user.Id).ToList();

    public User? GetById(int id) => context.Users.FirstOrDefault(user => user.Id == id);

    public bool UsernameExists(string username)
    {
        var normalizedUsername = username.Trim().ToLower();
        return context.Users.Any(user => user.Username.ToLower() == normalizedUsername);
    }

    public void Add(User user) => context.Users.Add(user);

    public void Remove(User user) => context.Users.Remove(user);

    public void SaveChanges() => context.SaveChanges();
}
