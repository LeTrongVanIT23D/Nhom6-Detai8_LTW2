using Microsoft.AspNetCore.Mvc;
using StudentManagement.DTOs;
using StudentManagement.Models;
using StudentManagement.Services;

namespace StudentManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DoctorsController : ControllerBase
{
    private readonly IDoctorService doctorService;

    public DoctorsController(IDoctorService doctorService)
    {
        this.doctorService = doctorService;
    }

    [HttpGet]
    public ActionResult<List<Doctor>> GetAll([FromQuery] string? specialty = null)
    {
        return Ok(doctorService.GetAll(specialty));
    }

    [HttpGet("{id:int}")]
    public ActionResult<Doctor> GetById(int id)
    {
        try { return Ok(doctorService.GetById(id)); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    [HttpPost]
    public ActionResult<Doctor> Create(DoctorDto dto)
    {
        try
        {
            var doctor = doctorService.Add(new Doctor
            {
                Id = doctorService.GetAll().Select(item => item.Id).DefaultIfEmpty(0).Max() + 1,
                FullName = dto.FullName.Trim(), Specialty = dto.Specialty.Trim(),
                Phone = dto.Phone.Trim(), Room = dto.Room.Trim()
            });
            return CreatedAtAction(nameof(GetById), new { id = doctor.Id }, doctor);
        }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpPut("{id:int}")]
    public IActionResult Update(int id, DoctorDto dto)
    {
        try
        {
            doctorService.Update(id, new Doctor { FullName = dto.FullName.Trim(), Specialty = dto.Specialty.Trim(), Phone = dto.Phone.Trim(), Room = dto.Room.Trim() });
            return NoContent();
        }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id)
    {
        try { doctorService.Remove(id); return NoContent(); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }
}
