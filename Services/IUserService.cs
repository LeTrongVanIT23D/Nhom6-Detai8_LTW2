using StudentManagement.DTOs;

namespace StudentManagement.Services;

public interface IUserService
{
    List<UserDto> GetAll();
    UserDto? Login(LoginDto login);
}
