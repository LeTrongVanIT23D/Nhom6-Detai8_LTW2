using System.ComponentModel.DataAnnotations;

namespace StudentManagement.Models;

public class Specialty
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public ICollection<Doctor> Doctors { get; set; } = [];
}