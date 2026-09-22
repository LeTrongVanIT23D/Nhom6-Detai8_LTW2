using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using StudentManagement.DTOs;
using StudentManagement.Models;
using StudentManagement.Services;

namespace StudentManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Receptionist,Admin")]
public class PatientsController : ControllerBase
{
    private readonly IPatientManager patientManager;

    public PatientsController(IPatientManager patientManager)
    {
        this.patientManager = patientManager;
    }

    [HttpGet]
    public ActionResult<List<Patient>> GetAll()
    {
        return Ok(patientManager.GetAll());
    }

    [HttpGet("{id:int}")]
    public ActionResult<Patient> GetById(int id)
    {
        try
        {
            return Ok(patientManager.FindById(id));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
    }

    [HttpPost]
    public ActionResult<Patient> Create(PatientDto dto)
    {
        var patient = new Patient
        {
            FullName = dto.FullName,
            DateOfBirth = dto.DateOfBirth,
            Phone = dto.Phone,
            Address = dto.Address,
            MedicalHistory = dto.MedicalHistory
        };

        try
        {
            patientManager.Add(patient);
            return CreatedAtAction(nameof(GetById), new { id = patient.Id }, patient);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpPut("{id:int}")]
    public IActionResult Update(int id, PatientDto dto)
    {
        var patient = new Patient
        {
            Id = id,
            FullName = dto.FullName,
            DateOfBirth = dto.DateOfBirth,
            Phone = dto.Phone,
            Address = dto.Address,
            MedicalHistory = dto.MedicalHistory
        };

        try
        {
            patientManager.Update(patient);
            return NoContent();
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
    }

    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id)
    {
        try
        {
            patientManager.Remove(id);
            return NoContent();
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
    }
}