using System.ComponentModel.DataAnnotations;

namespace StudentManagement.Models;

public class Patient
{
    [Key]
    public int Id { get; set; }

    public User? User { get; set; }
    public int? UserId { get; set; }

    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    public DateOnly DateOfBirth { get; set; }

    [Required, Phone, MaxLength(20)]
    public string Phone { get; set; } = string.Empty;

    [Required, MaxLength(250)]
    public string Address { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string MedicalHistory { get; set; } = string.Empty;
}