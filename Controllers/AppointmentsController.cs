using Microsoft.AspNetCore.Mvc;
using StudentManagement.DTOs;
using StudentManagement.Models;
using StudentManagement.Services;

namespace StudentManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
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
        return Ok(appointmentService.GetAll(date, status));
    }

    [HttpGet("{id:int}")]
    public ActionResult<Appointment> GetById(int id)
    {
        try { return Ok(appointmentService.GetById(id)); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    [HttpPost]
    public ActionResult<Appointment> Create(AppointmentDto dto)
    {
        try
        {
            var appointment = appointmentService.Add(new Appointment
            {
                Id = appointmentService.GetAll().Select(item => item.Id).DefaultIfEmpty(0).Max() + 1,
                PatientId = dto.PatientId, DoctorId = dto.DoctorId, AppointmentDate = dto.AppointmentDate,
                Reason = dto.Reason.Trim(), Status = dto.Status
            });
            return CreatedAtAction(nameof(GetById), new { id = appointment.Id }, appointment);
        }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpPut("{id:int}")]
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
    public IActionResult Delete(int id)
    {
        try { appointmentService.Remove(id); return NoContent(); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }
}
