using StudentManagement.DTOs;
using StudentManagement.Models;

namespace StudentManagement.Services;

public class UserService : IUserService
{
    private readonly List<User> users =
    [
        new() { Id = 1, FullName = "Lê Thu", Username = "letu", Password = "123456", Role = "Receptionist" },
        new() { Id = 2, FullName = "Nguyễn Minh", Username = "admin", Password = "admin123", Role = "Admin" }
    ];

    public List<UserDto> GetAll()
    {
        return users.Select(ToDto).ToList();
    }

    public UserDto? Login(LoginDto login)
    {
        var user = users.FirstOrDefault(item =>
            item.Username == login.Username && item.Password == login.Password);

        return user is null ? null : ToDto(user);
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
