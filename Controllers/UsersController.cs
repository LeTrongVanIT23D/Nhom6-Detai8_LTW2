using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using StudentManagement.DTOs;
using StudentManagement.Services;

namespace StudentManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly IUserService userService;

    public UsersController(IUserService userService)
    {
        this.userService = userService;
    }

    [HttpGet]
    public ActionResult<List<UserDto>> GetAll()
    {
        return Ok(userService.GetAll());
    }

    [HttpPost("login")]
    public ActionResult<UserDto> Login(LoginDto login)
    {
        var user = userService.Login(login);

        if (user is null)
            return Unauthorized(new { message = "Tên đăng nhập hoặc mật khẩu không đúng." });

        return Ok(user);
    }
}
