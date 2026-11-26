using System.Text.Json.Serialization;

namespace StudentManagement.DTOs;

public class UserDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int? DoctorId { get; set; }
    public int RewardPoints { get; set; } = 0;
    public string Email { get; set; } = string.Empty;
    public bool IsLocked { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AccessToken { get; set; }
}

public class CreateUserDto
{
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
}

public class UpdateUserDto
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
}

public class AssignDoctorDto
{
    public int? DoctorId { get; set; }
}

public class StaffProfileDto
{
    [System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(120)]
    public string FullName { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.EmailAddress, System.ComponentModel.DataAnnotations.StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Phone, System.ComponentModel.DataAnnotations.StringLength(30)]
    public string Phone { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
