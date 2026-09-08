using StudentManagement.DTOs;
using StudentManagement.Models;
using Microsoft.AspNetCore.Identity;

namespace StudentManagement.Services;

public class UserService : IUserService
{
    private readonly PasswordHasher<User> passwordHasher = new();
    private readonly List<User> users =
    [
        new() { Id = 1, FullName = "Lê Thu", Username = "letu", Password = "123456", Role = "Receptionist" },
        new() { Id = 2, FullName = "Nguyễn Minh", Username = "admin", Password = "admin123", Role = "Admin" },
        new() { Id = 3, FullName = "Bác sĩ Nguyễn Minh Tuấn", Username = "doctor", Password = "doctor123", Role = "Doctor" }
    ];

    public List<UserDto> GetAll()
    {
        return users.Select(ToDto).ToList();
    }

    public UserDto? Login(LoginDto login)
    {
        var user = users.FirstOrDefault(item =>
            item.Username.Equals(login.Username.Trim(), StringComparison.OrdinalIgnoreCase));

        if (user is null)
            return null;

        var passwordMatches = user.Password == login.Password ||
            passwordHasher.VerifyHashedPassword(user, user.Password, login.Password) == PasswordVerificationResult.Success;

        return passwordMatches ? ToDto(user) : null;
    }

    public UserDto Register(RegisterDto register)
    {
        var username = register.Username.Trim();
        if (users.Any(item => item.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Tên đăng nhập đã tồn tại.");

        var user = new User
        {
            Id = users.Select(item => item.Id).DefaultIfEmpty(0).Max() + 1,
            FullName = register.FullName.Trim(),
            Username = username,
            Password = passwordHasher.HashPassword(new User(), register.Password),
            Role = "User"
        };

        users.Add(user);
        return ToDto(user);
    }

    private static UserDto ToDto(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Username = user.Username,
            Role = user.Role
        };
    }
}
