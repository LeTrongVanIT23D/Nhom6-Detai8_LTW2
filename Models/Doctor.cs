using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace StudentManagement.Models;

public class Doctor
{
    [Key]
    public int Id { get; set; }

    public int? UserId { get; set; }
    public User? User { get; set; }

    public int SpecialtyId { get; set; }
    public Specialty? SpecialtyEntity { get; set; }

    [Required, MaxLength(120)]
    public string FullName { get; set; } = string.Empty;

    [NotMapped]
    public string Specialty { get; set; } = string.Empty;

    [Required, Phone, MaxLength(20)]
    public string Phone { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Room { get; set; } = string.Empty;

    [MaxLength(150)]
    public string Qualification { get; set; } = string.Empty;

    public int Experience { get; set; }
    public bool IsActive { get; set; } = true;
}
