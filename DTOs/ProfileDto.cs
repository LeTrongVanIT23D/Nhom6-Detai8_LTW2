using System.ComponentModel.DataAnnotations;

namespace StudentManagement.DTOs;

public class ProfileDto
{
    [Required, MaxLength(100)] public string FullName { get; set; } = string.Empty;
    [Required, EmailAddress, MaxLength(150)] public string Email { get; set; } = string.Empty;
    [Required] public DateOnly DateOfBirth { get; set; }
    [Required, MaxLength(20)] public string Phone { get; set; } = string.Empty;
    [Required, MaxLength(250)] public string Address { get; set; } = string.Empty;
}