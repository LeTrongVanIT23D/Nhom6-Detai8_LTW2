using StudentManagement.DTOs;
using StudentManagement.Models;
using StudentManagement.Repositories;

namespace StudentManagement.Services;

public class AuthService : IAuthService
{
    private readonly IAuthRepository authRepository;

    public AuthService(IAuthRepository authRepository, IConfiguration configuration)
    {
        this.authRepository = authRepository;
        SeedSpecialAccount(configuration, "AdminPassword", "MEDIKA_ADMIN_PASSWORD", "admin", "Quản trị viên", "Admin", "admin@medika.local");
        SeedSpecialAccount(configuration, "DoctorPassword", "MEDIKA_DOCTOR_PASSWORD", "doctor", "Bác sĩ trực", "Doctor", "doctor@medika.local");
        SeedSpecialAccount(configuration, "ReceptionistPassword", "MEDIKA_RECEPTIONIST_PASSWORD", "letu", "Lê Thu", "Receptionist", "receptionist@medika.local");
        SeedSpecialAccount(configuration, "SupportPassword", "MEDIKA_SUPPORT_PASSWORD", "support", "Nhân viên CSKH", "Support", "support@medika.local");
        SeedSpecialAccount(configuration, "UserPassword", "MEDIKA_USER_PASSWORD", "user", "Long-Luân-Văn", "User", "user@medika.local");
    }

    public UserDto? Login(LoginDto login)
    {
        var user = authRepository.GetByUsername(login.Username);
        if (user is null)
            return null;

        if (user.IsLocked)
            throw new InvalidOperationException("Tài khoản của bạn đã bị khóa bởi Quản trị viên.");

        if (!PasswordHashing.Verify(user, login.Password))
            return null;

        if (!PasswordHashing.IsBcryptHash(user.PasswordHash))
        {
            user.PasswordHash = PasswordHashing.Hash(login.Password);
            authRepository.SaveChanges();
        }

        // Tự động ánh xạ UserId ra DoctorId khi Role = Doctor đăng nhập
        if (string.Equals(user.Role, "Doctor", StringComparison.OrdinalIgnoreCase))
        {
            if (!user.DoctorId.HasValue)
            {
                var doc = authRepository.GetDoctorByUserId(user.Id) ?? authRepository.GetFirstAvailableDoctor();
                if (doc != null)
                {
                    user.DoctorId = doc.Id;
                    doc.UserId = user.Id;
                    authRepository.SaveChanges();
                }
            }
            else
            {
                var doc = authRepository.GetDoctorByUserId(user.Id);
                if (doc != null && doc.Id != user.DoctorId.Value)
                {
                    user.DoctorId = doc.Id;
                    authRepository.SaveChanges();
                }
            }
        }

        return ToDto(user);
    }

    public UserDto Register(RegisterDto register)
    {
        var username = register.Username.Trim();
        if (authRepository.UsernameExists(username))
            throw new InvalidOperationException("Tên đăng nhập đã tồn tại.");

        var user = new User
        {
            FullName = register.FullName.Trim(),
            Username = username,
            Email = register.Email.Trim(),
            PasswordHash = PasswordHashing.Hash(register.Password),
            Role = "User",
            CreatedAt = DateTime.Now
        };

        authRepository.Add(user);
        authRepository.SaveChanges();
        return ToDto(user);
    }

    public UserDto ChangePassword(int userId, string currentPassword, string newPassword)
    {
        var user = authRepository.GetById(userId)
            ?? throw new KeyNotFoundException("Không tìm thấy tài khoản.");

        if (!PasswordHashing.Verify(user, currentPassword))
            throw new UnauthorizedAccessException("Mật khẩu hiện tại không đúng.");

        user.PasswordHash = PasswordHashing.Hash(newPassword);
        authRepository.SaveChanges();
        return ToDto(user);
    }

    public bool UserExists(string usernameOrEmail) => authRepository.UserExists(usernameOrEmail);

    private void SeedSpecialAccount(
        IConfiguration configuration,
        string secretKey,
        string passwordVariable,
        string username,
        string fullName,
        string role,
        string email)
    {
        var configuredPassword = configuration[secretKey] 
            ?? Environment.GetEnvironmentVariable(passwordVariable) 
            ?? "123";
        if (string.IsNullOrWhiteSpace(configuredPassword))
            return;

        var existingUser = authRepository.GetByUsername(username);

        if (existingUser is not null)
        {
            if (!PasswordHashing.Verify(existingUser, configuredPassword) ||
                !PasswordHashing.IsBcryptHash(existingUser.PasswordHash))
            {
                existingUser.PasswordHash = PasswordHashing.Hash(configuredPassword);
                authRepository.SaveChanges();
            }
            return;
        }

        authRepository.Add(new User
        {
            FullName = fullName,
            Username = username,
            Email = email,
            Role = role,
            PasswordHash = PasswordHashing.Hash(configuredPassword),
            CreatedAt = DateTime.Now
        });
        authRepository.SaveChanges();
    }

    private static UserDto ToDto(User user) => new()
    {
        Id = user.Id,
        FullName = user.FullName,
        Username = user.Username,
        Role = user.Role,
        Email = user.Email,
        IsLocked = user.IsLocked,
        DoctorId = user.DoctorId,
        RewardPoints = user.RewardPoints,
        CreatedAt = user.CreatedAt
    };
}
