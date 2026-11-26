using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using StudentManagement.Services;
using static StudentManagement.DTOs.FileUploadDbDto;

namespace StudentManagement.Controllers;

[Route("api/[controller]")]
[ApiController]
public class FilesController : ControllerBase
{
    private readonly IFileService _fileService;

    public FilesController(IFileService fileService)
    {
        _fileService = fileService;
    }

    [HttpPost("upload")]
    [Authorize(Roles = "Admin")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload([FromForm] UploadFileDto dto)
    {
        var result = await _fileService.UploadFileAsync(dto.File);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> Download(int id)
    {
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        var fileDto = await _fileService.GetFileForDownloadAsync(id);

        if (fileDto == null)
            return NotFound("Không tìm thấy file yêu cầu.");

        Response.Headers.XContentTypeOptions = "nosniff";
        if (fileDto.ContentType is "image/jpeg" or "image/png" or "image/webp")
            return File(fileDto.Content, fileDto.ContentType);

        return File(fileDto.Content, fileDto.ContentType, fileDto.FileName);
    }
}
