using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using StudentManagement.DTOs;
using StudentManagement.Services;

namespace StudentManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/users")]
[Authorize(Roles = "Admin")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IAuthService _authService;
    private readonly IAuditLogService _auditLogService;
    private readonly IJwtTokenService _jwtTokenService;

    public UserController(IUserService userService, IAuthService authService, IAuditLogService auditLogService, IJwtTokenService jwtTokenService)
    {
        _userService = userService;
        _authService = authService;
        _auditLogService = auditLogService;
        _jwtTokenService = jwtTokenService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<UserResponseDto>>> GetAll([FromQuery] UserQueryParameters queryParams)
    {
        var result = await _userService.GetAllAsync(queryParams);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> CreateUser([FromBody] CreateUserDto dto)
    {
        try
        {
            var user = _userService.CreateUser(dto);
            await _auditLogService.LogAsync("CREATE_USER", User.Identity?.Name ?? "Admin", "Admin", $"Tạo tài khoản @{user.Username} với vai trò {user.Role}");
            return Ok(user);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UserDto>> UpdateUser(int id, [FromBody] UpdateUserDto dto)
    {
        try
        {
            var user = _userService.UpdateUser(id, dto);
            await _auditLogService.LogAsync("UPDATE_USER", User.Identity?.Name ?? "Admin", "Admin", $"Cập nhật tài khoản ID #{id} (@{user.Username})");
            return Ok(user);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        try
        {
            _userService.DeleteUser(id);
            await _auditLogService.LogAsync("DELETE_USER", User.Identity?.Name ?? "Admin", "Admin", $"Xóa tài khoản ID #{id}");
            return Ok(new { message = "Xóa tài khoản thành công." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    public class LockRequest
    {
        public bool IsLocked { get; set; }
    }

    [HttpPatch("{id:int}/lock")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> LockUser(int id, [FromBody] LockRequest req)
    {
        try
        {
            _userService.SetLocked(id, req.IsLocked);
            await _auditLogService.LogAsync(req.IsLocked ? "LOCK_USER" : "UNLOCK_USER", User.Identity?.Name ?? "Admin", "Admin", $"{(req.IsLocked ? "Khóa" : "Mở khóa")} tài khoản ID #{id}");
            return Ok(new { message = req.IsLocked ? "Đã khóa tài khoản thành công." : "Đã mở khóa tài khoản thành công." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    public class ResetPasswordRequest
    {
        public string NewPassword { get; set; } = "123456";
    }

    [HttpPost("{id:int}/reset-password")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetPasswordRequest req)
    {
        try
        {
            _userService.ResetPassword(id, req.NewPassword);
            await _auditLogService.LogAsync("RESET_PASSWORD", User.Identity?.Name ?? "Admin", "Admin", $"Reset mật khẩu cho tài khoản ID #{id}");
            return Ok(new { message = "Đã đặt lại mật khẩu thành công (Mặc định: 123456)." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("{id:int}/role")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateRole(int id, [FromBody] string role)
    {
        try
        {
            _userService.UpdateRole(id, role);
            await _auditLogService.LogAsync("CHANGE_ROLE", User.Identity?.Name ?? "Admin", "Admin", $"Đổi vai trò tài khoản ID #{id} sang {role}");
            return Ok(new { message = "Cập nhật phân quyền thành công." });
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    [HttpPatch("{id:int}/doctor")]
    public IActionResult AssignDoctor(int id, [FromBody] AssignDoctorDto dto)
    {
        try
        {
            _userService.AssignDoctor(id, dto.DoctorId);
            return NoContent();
        }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public ActionResult<UserDto> Login(LoginDto login)
    {
        try
        {
            var user = _authService.Login(login);
            if (user is null)
                return Unauthorized(new { message = "Tên đăng nhập hoặc mật khẩu không đúng." });

            user.AccessToken = _jwtTokenService.CreateToken(user);
            return Ok(user);
        }
        catch (InvalidOperationException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }
}

