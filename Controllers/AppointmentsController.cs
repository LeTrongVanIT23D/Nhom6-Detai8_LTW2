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
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentService appointmentService;

    public AppointmentsController(IAppointmentService appointmentService)
    {
        this.appointmentService = appointmentService;
    }

    [HttpGet]
    public ActionResult<List<Appointment>> GetAll([FromQuery] DateTime? date = null, [FromQuery] AppointmentStatus? status = null)
    {
        if (User.IsInRole("User"))
            return Ok(appointmentService.GetForUser(int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)));
        return Ok(appointmentService.GetAll(date, status));
    }

    [HttpGet("{id:int}")]
    public ActionResult<Appointment> GetById(int id)
    {
        try { return Ok(appointmentService.GetById(id)); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    [HttpPost]
    [Authorize(Roles = "User,Receptionist,Admin")]
    public ActionResult<Appointment> Create(AppointmentDto dto)
    {
        try
        {
            var patientId = dto.PatientId;
            if (User.IsInRole("User"))
                patientId = HttpContext.RequestServices.GetRequiredService<IPatientManager>().FindByUserId(int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)).Id;

            var appointment = appointmentService.Add(new Appointment
            {
                Id = appointmentService.GetAll().Select(item => item.Id).DefaultIfEmpty(0).Max() + 1,
                PatientId = patientId, DoctorId = dto.DoctorId, UserId = User.IsInRole("User") ? int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!) : dto.UserId, AppointmentDate = dto.AppointmentDate,
                Reason = dto.Reason.Trim(), Status = dto.Status
            });
            return CreatedAtAction(nameof(GetById), new { id = appointment.Id }, appointment);
        }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Doctor,Receptionist,Admin")]
    public IActionResult Update(int id, AppointmentDto dto)
    {
        try
        {
            appointmentService.Update(id, new Appointment
            {
                PatientId = dto.PatientId, DoctorId = dto.DoctorId, AppointmentDate = dto.AppointmentDate,
                Reason = dto.Reason.Trim(), Status = dto.Status
            });
            return NoContent();
        }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Receptionist,Admin")]
    public IActionResult Delete(int id)
    {
        try { appointmentService.Remove(id); return NoContent(); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    [HttpPatch("{id:int}/cancel")]
    [Authorize(Roles = "User")]
    public IActionResult Cancel(int id)
    {
        try { appointmentService.Cancel(id, int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)); return NoContent(); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
        catch (UnauthorizedAccessException exception) { return Forbid(exception.Message); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }
}
