using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudentManagement.Data;
using StudentManagement.DTOs;
using StudentManagement.Models;

namespace StudentManagement.Services;

public class UserService : IUserService
{
    private readonly AppDbContext context;
    private readonly PasswordHasher<User> passwordHasher = new();

    public UserService(AppDbContext context, IConfiguration configuration)
    {
        this.context = context;
        SeedSpecialAccount(configuration, "AdminPassword", "MEDIKA_ADMIN_PASSWORD", "admin", "Quản trị viên", "Admin", "admin@medika.local", "admin123");
        SeedSpecialAccount(configuration, "DoctorPassword", "MEDIKA_DOCTOR_PASSWORD", "doctor", "Bác sĩ trực", "Doctor", "doctor@medika.local", "doctor123");
        SeedSpecialAccount(configuration, "ReceptionistPassword", "MEDIKA_RECEPTIONIST_PASSWORD", "letu", "Lê Thu", "Receptionist", "receptionist@medika.local", "receptionist123");
        SeedSpecialAccount(configuration, "SupportPassword", "MEDIKA_SUPPORT_PASSWORD", "support", "Nhân viên chăm sóc khách hàng", "Support", "support@medika.local", "support123");
        SeedSpecialAccount(configuration, "UserPassword", "MEDIKA_USER_PASSWORD", "user", "Người dùng demo", "User", "user@medika.local", "user123");
    }

    public List<UserDto> GetAll()
    {
        return context.Users.AsNoTracking()
            .OrderBy(user => user.Id)
            .Select(user => new UserDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Username = user.Username,
                Role = user.Role,
                Email = user.Email
            })
            .ToList();
    }

    public UserDto? Login(LoginDto login)
    {
        var normalizedUsername = login.Username.Trim();
        var user = context.Users.FirstOrDefault(item =>
            item.Username.ToLower() == normalizedUsername.ToLower());

        if (user is null)
            return null;

        var passwordMatches = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, login.Password) == PasswordVerificationResult.Success;

        return passwordMatches ? ToDto(user) : null;
    }

    public UserDto Register(RegisterDto register)
    {
        var username = register.Username.Trim();
        if (context.Users.Any(item => item.Username.ToLower() == username.ToLower()))
            throw new InvalidOperationException("Tên đăng nhập đã tồn tại.");

        var user = new User
        {
            FullName = register.FullName.Trim(),
            Username = username,
            Email = register.Email.Trim(),
            PasswordHash = passwordHasher.HashPassword(new User(), register.Password),
            Role = "User"
        };

        context.Users.Add(user);
        context.SaveChanges();
        return ToDto(user);
    }

    public UserDto ChangePassword(int userId, string currentPassword, string newPassword)
    {
        var user = context.Users.FirstOrDefault(item => item.Id == userId)
            ?? throw new KeyNotFoundException("Không tìm thấy tài khoản.");

        if (passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword) != PasswordVerificationResult.Success)
            throw new UnauthorizedAccessException("Mật khẩu hiện tại không đúng.");

        user.PasswordHash = passwordHasher.HashPassword(user, newPassword);
        context.SaveChanges();
        return ToDto(user);
    }

    public bool UserExists(string usernameOrEmail)
    {
        var value = usernameOrEmail.Trim();
        var normalized = value.ToLower();
        return context.Users.Any(item => item.Username.ToLower() == normalized ||
            item.Email.ToLower() == normalized);
    }

    public void UpdateRole(int userId, string role)
    {
        var allowedRoles = new[] { "User", "Doctor", "Receptionist", "Support", "Admin" };
        if (!allowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Role không hợp lệ.");

        var user = context.Users.FirstOrDefault(item => item.Id == userId)
            ?? throw new KeyNotFoundException("Không tìm thấy tài khoản.");

        user.Role = allowedRoles.First(item => item.Equals(role, StringComparison.OrdinalIgnoreCase));
        context.SaveChanges();
    }

    private void SeedSpecialAccount(IConfiguration configuration, string secretKey, string passwordVariable, string username, string fullName, string role, string email, string fallbackPassword)
    {
        var configuredPassword = configuration[secretKey] ?? Environment.GetEnvironmentVariable(passwordVariable);
        var password = string.IsNullOrWhiteSpace(configuredPassword) ? fallbackPassword : configuredPassword;

        if (context.Users.Any(item => item.Username.ToLower() == username.ToLower()))
            return;

        var user = new User
        {
            FullName = fullName,
            Username = username,
            Email = email,
            Role = role,
            PasswordHash = passwordHasher.HashPassword(new User(), password)
        };

        context.Users.Add(user);
        context.SaveChanges();
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
