using System.ComponentModel.DataAnnotations;

namespace StudentManagement.DTOs;

public class DoctorDto
{
    [Required, StringLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Specialty { get; set; } = string.Empty;

    [Required, Phone]
    public string Phone { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string Room { get; set; } = string.Empty;
}
