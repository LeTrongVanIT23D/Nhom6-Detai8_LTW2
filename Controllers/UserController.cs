using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentManagement.DTOs;
using StudentManagement.Models;
using StudentManagement.Services;

namespace StudentManagement.Controllers;

[ApiController]
[Route("api/user")]
[Authorize(Roles = "User")]
public class UserController : ControllerBase
{
    private readonly IPatientManager patientManager;
    private readonly IUserService userService;
    private readonly IAppointmentService appointmentService;
    private readonly IMedicalRecordService medicalRecordService;
    private readonly IDoctorService doctorService;

    public UserController(IPatientManager patientManager, IUserService userService, IAppointmentService appointmentService, IMedicalRecordService medicalRecordService, IDoctorService doctorService)
    {
        this.patientManager = patientManager;
        this.userService = userService;
        this.appointmentService = appointmentService;
        this.medicalRecordService = medicalRecordService;
        this.doctorService = doctorService;
    }

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private int PatientId => patientManager.FindByUserId(UserId).Id;

    [HttpGet("profile")]
    public ActionResult<object> Profile()
    {
        var patient = patientManager.FindByUserId(UserId);
        var user = userService.GetAll().First(item => item.Id == UserId);
        return Ok(new { user, patient });
    }

    [HttpPut("profile")]
    public ActionResult<object> UpdateProfile(ProfileDto dto)
    {
        var patient = patientManager.FindByUserId(UserId);
        patientManager.Update(new() { Id = patient.Id, UserId = UserId, FullName = dto.FullName.Trim(), DateOfBirth = dto.DateOfBirth, Phone = dto.Phone.Trim(), Address = dto.Address.Trim(), MedicalHistory = patient.MedicalHistory });
        userService.UpdateProfile(UserId, dto.FullName.Trim(), dto.Email.Trim());
        return Profile();
    }

    [HttpGet("records")]
    public ActionResult<object> Records()
    {
        var records = medicalRecordService.GetAll(PatientId).Select(record => new
        {
            record,
            doctor = doctorService.GetById(record.DoctorId),
            prescriptions = medicalRecordService.GetPrescriptions(record.Id)
        });
        return Ok(records);
    }

    [HttpGet("invoices")]
    public ActionResult<object> Invoices()
    {
        var invoices = appointmentService.GetForUser(UserId).Where(item => item.Status == AppointmentStatus.Completed).Select(item => new
        {
            id = $"HD-{item.Id:0000}", appointmentId = item.Id, date = item.AppointmentDate,
            description = item.Reason, amount = 0, status = "Chưa phát hành"
        });
        return Ok(invoices);
    }
}