using StudentManagement.DTOs;

namespace StudentManagement.Services;

public interface IUserService
{
    List<UserDto> GetAll();
    UserDto? Login(LoginDto login);
    UserDto Register(RegisterDto register);
    UserDto ChangePassword(int userId, string currentPassword, string newPassword);
    void UpdateProfile(int userId, string fullName, string email);
    bool UserExists(string usernameOrEmail);
    void UpdateRole(int userId, string role);
}
