using System.ComponentModel.DataAnnotations;
using StudentManagement.Models;

namespace StudentManagement.DTOs;

public class AppointmentDto
{
    [Range(1, int.MaxValue)]
    public int PatientId { get; set; }

    [Range(1, int.MaxValue)]
    public int DoctorId { get; set; }

    public DateTime AppointmentDate { get; set; }

    [Required, StringLength(250)]
    public string Reason { get; set; } = string.Empty;

    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;
}
