namespace StudentManagement.Models;

public class Doctor
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Specialty { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public string Degree { get; set; } = "Bác sĩ CKI";
    public int ExperienceYears { get; set; } = 5;
    public string ProfileImageUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    [System.Text.Json.Serialization.JsonIgnore]
    public User? User { get; set; }
}
