using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using StudentManagement.DTOs;
using StudentManagement.Models;
using StudentManagement.Services;

namespace StudentManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "User,Doctor,Receptionist,Admin")]
public class DoctorsController : ControllerBase
{
    private readonly IDoctorService doctorService;
    private readonly IFileService fileService;

    public DoctorsController(IDoctorService doctorService, IFileService fileService)
    {
        this.doctorService = doctorService;
        this.fileService = fileService;
    }

    [HttpGet]
    [AllowAnonymous]
    public ActionResult<List<Doctor>> GetAll(
        [FromQuery] string? specialty = null,
        [FromQuery] bool includeInactive = false)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(doctorService.GetAll(specialty, includeInactive && User.IsInRole("Admin")));
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public ActionResult<Doctor> GetById(int id)
    {
        Response.Headers.CacheControl = "no-store";
        try { return Ok(doctorService.GetById(id)); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public ActionResult<Doctor> Create(DoctorDto dto)
    {
        if (!IsValidProfileImageUrl(dto.ProfileImageUrl))
            return BadRequest(new { message = "Bác sĩ phải có ảnh hồ sơ. Hãy tải ảnh lên và thử lại." });

        try
        {
            var doctor = doctorService.Add(new Doctor
            {
                FullName = dto.FullName.Trim(),
                Specialty = dto.Specialty.Trim(),
                Phone = dto.Phone.Trim(),
                Room = dto.Room.Trim(),
                Degree = string.IsNullOrWhiteSpace(dto.Degree) ? "Bác sĩ CKI" : dto.Degree.Trim(),
                ExperienceYears = dto.ExperienceYears > 0 ? dto.ExperienceYears : 5,
                ProfileImageUrl = dto.ProfileImageUrl.Trim(),
                IsActive = dto.IsActive
            });
            return CreatedAtAction(nameof(GetById), new { id = doctor.Id }, doctor);
        }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public IActionResult Update(int id, DoctorDto dto)
    {
        if (!IsValidProfileImageUrl(dto.ProfileImageUrl))
            return BadRequest(new { message = "Bác sĩ phải có ảnh hồ sơ. Hãy tải ảnh lên và thử lại." });

        try
        {
            doctorService.Update(id, new Doctor
            {
                FullName = dto.FullName.Trim(),
                Specialty = dto.Specialty.Trim(),
                Phone = dto.Phone.Trim(),
                Room = dto.Room.Trim(),
                Degree = dto.Degree,
                ExperienceYears = dto.ExperienceYears,
                ProfileImageUrl = dto.ProfileImageUrl.Trim(),
                IsActive = dto.IsActive
            });
            return NoContent();
        }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    public class ToggleDoctorStatusRequest
    {
        public bool IsActive { get; set; }
    }

    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = "Admin")]
    public IActionResult ToggleStatus(int id, [FromBody] ToggleDoctorStatusRequest req)
    {
        try
        {
            var doc = doctorService.GetById(id);
            doc.IsActive = req.IsActive;
            doctorService.Update(id, doc);
            return Ok(new { message = $"Đã cập nhật trạng thái bác sĩ: {(req.IsActive ? "Đang làm việc" : "Tạm nghỉ công tác")}" });
        }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public IActionResult Delete(int id)
    {
        try { doctorService.Remove(id); return NoContent(); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    public class DoctorProfilePhotoRequest
    {
        public string ProfileImageUrl { get; set; } = string.Empty;
    }

    [HttpPost("my-profile/photo")]
    [Authorize(Roles = "Doctor")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadMyProfilePhoto(IFormFile file)
    {
        if (!int.TryParse(User.FindFirstValue("doctor_id"), out var doctorId))
            return Conflict(new { message = "Tài khoản bác sĩ chưa được gắn với hồ sơ bác sĩ." });

        try
        {
            var doctor = doctorService.GetById(doctorId);
            var uploadedFile = await fileService.UploadFileAsync(file);
            doctor.ProfileImageUrl = $"/api/files/{uploadedFile.Id}";
            doctorService.Update(doctorId, doctor);
            return Ok(new { profileImageUrl = doctor.ProfileImageUrl });
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    [HttpPost("{id:int}/profile-photo/upload")]
    [Authorize(Roles = "Admin")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadProfilePhoto(int id, IFormFile file)
    {
        try
        {
            var doctor = doctorService.GetById(id);
            var uploadedFile = await fileService.UploadFileAsync(file);
            doctor.ProfileImageUrl = $"/api/files/{uploadedFile.Id}";
            doctorService.Update(id, doctor);
            return Ok(new { profileImageUrl = doctor.ProfileImageUrl });
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    [HttpPatch("{id:int}/profile-photo")]
    [Authorize(Roles = "Admin")]
    public IActionResult UpdateProfilePhoto(int id, [FromBody] DoctorProfilePhotoRequest request)
    {
        if (!IsValidProfileImageUrl(request.ProfileImageUrl))
            return BadRequest(new { message = "Nhập URL ảnh hợp lệ (https://...) hoặc đường dẫn ảnh nội bộ /api/files/{id}." });

        try
        {
            var doctor = doctorService.GetById(id);
            doctor.ProfileImageUrl = request.ProfileImageUrl.Trim();
            doctorService.Update(id, doctor);
            return NoContent();
        }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    private static bool IsValidProfileImageUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var imageUrl = value.Trim();
        if (imageUrl.StartsWith("/api/files/", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(imageUrl["/api/files/".Length..], out var fileId) && fileId > 0)
            return true;

        return Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) &&
            string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }
}
