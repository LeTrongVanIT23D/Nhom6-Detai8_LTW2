using StudentManagement.Data;
using StudentManagement.Models;

namespace StudentManagement.Repositories;

public class AuthRepository : IAuthRepository
{
    private readonly AppDbContext context;

    public AuthRepository(AppDbContext context)
    {
        this.context = context;
    }

    public User? GetByUsername(string username)
    {
        var normalizedUsername = username.Trim().ToLower();
        return context.Users.FirstOrDefault(user => user.Username.ToLower() == normalizedUsername);
    }

    public User? GetById(int id) => context.Users.FirstOrDefault(user => user.Id == id);

    public bool UsernameExists(string username)
    {
        var normalizedUsername = username.Trim().ToLower();
        return context.Users.Any(user => user.Username.ToLower() == normalizedUsername);
    }

    public bool UserExists(string usernameOrEmail)
    {
        var normalizedValue = usernameOrEmail.Trim().ToLower();
        return context.Users.Any(user =>
            user.Username.ToLower() == normalizedValue ||
            user.Email.ToLower() == normalizedValue);
    }

    public Doctor? GetDoctorByUserId(int userId) =>
        context.Doctors.FirstOrDefault(doctor => doctor.UserId == userId);

    public Doctor? GetFirstAvailableDoctor() =>
        context.Doctors.FirstOrDefault(doctor => doctor.IsActive && doctor.UserId == null)
        ?? context.Doctors.FirstOrDefault(doctor => doctor.IsActive);

    public void Add(User user) => context.Users.Add(user);

    public void SaveChanges() => context.SaveChanges();
}
