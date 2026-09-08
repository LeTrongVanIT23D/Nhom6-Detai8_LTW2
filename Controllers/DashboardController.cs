using Microsoft.AspNetCore.Mvc;
using StudentManagement.Models;
using StudentManagement.Services;

namespace StudentManagement.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IPatientManager patientManager;
    private readonly IDoctorService doctorService;
    private readonly IAppointmentService appointmentService;

    public DashboardController(IPatientManager patientManager, IDoctorService doctorService, IAppointmentService appointmentService)
    {
        this.patientManager = patientManager;
        this.doctorService = doctorService;
        this.appointmentService = appointmentService;
    }

    [HttpGet]
    public IActionResult GetSummary()
    {
        var today = DateTime.Today;
        var appointments = appointmentService.GetAll(today);
        return Ok(new
        {
            totalPatients = patientManager.GetAll().Count,
            totalDoctors = doctorService.GetAll().Count,
            todayAppointments = appointments.Count,
            scheduledAppointments = appointments.Count(item => item.Status == AppointmentStatus.Scheduled),
            completedAppointments = appointments.Count(item => item.Status == AppointmentStatus.Completed),
            cancelledAppointments = appointments.Count(item => item.Status == AppointmentStatus.Cancelled)
        });
    }
}
