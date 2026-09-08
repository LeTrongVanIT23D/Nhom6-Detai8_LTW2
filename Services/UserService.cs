using StudentManagement.DTOs;
using StudentManagement.Models;
using Microsoft.AspNetCore.Identity;

namespace StudentManagement.Services;

public class UserService : IUserService
{
    private readonly PasswordHasher<User> passwordHasher = new();
    private readonly List<User> users =
    [];

    public UserService()
    {
        SeedSpecialAccount("MEDIKA_RECEPTIONIST_PASSWORD", "letu", "Lê Thu", "Receptionist", "receptionist@medika.local");
        SeedSpecialAccount("MEDIKA_ADMIN_PASSWORD", "admin", "Quản trị viên", "Admin", "admin@medika.local");
        SeedSpecialAccount("MEDIKA_DOCTOR_PASSWORD", "doctor", "Bác sĩ trực", "Doctor", "doctor@medika.local");
        SeedSpecialAccount("MEDIKA_SUPPORT_PASSWORD", "support", "Nhân viên chăm sóc khách hàng", "Support", "support@medika.local");
    }

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

        var passwordMatches = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, login.Password) == PasswordVerificationResult.Success;

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
            Email = register.Email.Trim(),
            PasswordHash = passwordHasher.HashPassword(new User(), register.Password),
            Role = "User"
        };

        users.Add(user);
        return ToDto(user);
    }

    public UserDto ChangePassword(int userId, string currentPassword, string newPassword)
    {
        var user = users.FirstOrDefault(item => item.Id == userId)
            ?? throw new KeyNotFoundException("Không tìm thấy tài khoản.");

        if (passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword) != PasswordVerificationResult.Success)
            throw new UnauthorizedAccessException("Mật khẩu hiện tại không đúng.");

        user.PasswordHash = passwordHasher.HashPassword(user, newPassword);
        return ToDto(user);
    }

    public bool UserExists(string usernameOrEmail)
    {
        return users.Any(item => item.Username.Equals(usernameOrEmail.Trim(), StringComparison.OrdinalIgnoreCase) ||
            item.Email.Equals(usernameOrEmail.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public void UpdateRole(int userId, string role)
    {
        var allowedRoles = new[] { "User", "Doctor", "Receptionist", "Support", "Admin" };
        if (!allowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Role không hợp lệ.");

        var user = users.FirstOrDefault(item => item.Id == userId)
            ?? throw new KeyNotFoundException("Không tìm thấy tài khoản.");
        user.Role = allowedRoles.First(item => item.Equals(role, StringComparison.OrdinalIgnoreCase));
    }

    private void SeedSpecialAccount(string passwordVariable, string username, string fullName, string role, string email)
    {
        var password = Environment.GetEnvironmentVariable(passwordVariable);
        if (string.IsNullOrWhiteSpace(password))
            return;

        var user = new User { Id = users.Count + 1, Username = username, FullName = fullName, Email = email, Role = role };
        user.PasswordHash = passwordHasher.HashPassword(user, password);
        users.Add(user);
    }

    private static UserDto ToDto(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Username = user.Username,
            Role = user.Role,
            Email = user.Email
        };
    }
}
