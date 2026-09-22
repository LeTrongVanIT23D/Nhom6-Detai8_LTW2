using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentManagement.DTOs;
using StudentManagement.Models;
using StudentManagement.Services;

namespace StudentManagement.Controllers;

[ApiController]
[Route("api/support")]
[Authorize]
public class SupportController : ControllerBase
{
    private readonly ISupportService service;
    public SupportController(ISupportService service) { this.service = service; }

    [HttpGet]
    public ActionResult<List<SupportMessage>> GetAll()
    {
        var isStaff = User.IsInRole("Support") || User.IsInRole("Admin");
        return Ok(service.GetAll(isStaff ? null : int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)));
    }

    [HttpPost]
    [Authorize(Roles = "User")]
    public ActionResult<SupportMessage> Create(SupportMessageDto dto)
    {
        var message = service.Add(int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), new SupportMessage { Subject = dto.Subject.Trim(), Content = dto.Content.Trim() });
        return Created($"/api/support/{message.Id}", message);
    }

    [HttpPatch("{id:int}/reply")]
    [Authorize(Roles = "Support,Admin")]
    public IActionResult Reply(int id, SupportReplyDto dto)
    {
        try { service.Reply(id, dto.Reply, dto.Status); return NoContent(); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }
}
