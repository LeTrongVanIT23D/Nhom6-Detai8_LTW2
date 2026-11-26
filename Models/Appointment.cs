using System.Text.Json.Serialization;
using System.ComponentModel.DataAnnotations.Schema;
using StudentManagement.DTOs;

namespace StudentManagement.Models;

public enum AppointmentStatus
{
    Pending,
    Confirmed,
    CheckedIn,
    Completed,
    Cancelled,
    NeedReschedule,
    Scheduled,
    NoShow
}

public enum AppointmentBookingSource
{
    InPerson,
    Online
}

public class Appointment
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public int DoctorId { get; set; }
    public int? UserId { get; set; }
    public int? ReceptionistId { get; set; }
    public int? QueueNumber { get; set; }
    public DateTime AppointmentDate { get; set; }
    public AppointmentBookingSource BookingSource { get; set; } = AppointmentBookingSource.InPerson;
    public string Reason { get; set; } = string.Empty;
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;
    public decimal BookingFee { get; set; } = 100000;
    public decimal DiscountAmount { get; set; } = 0;
    public int PointsUsed { get; set; } = 0;
    public bool BookingFeeIsPaid { get; set; }
    public string BookingFeePaymentMethod { get; set; } = string.Empty;
    public DateTime? BookingFeePaidAt { get; set; }
    public string BookingFeePaymentReference { get; set; } = string.Empty;
    public bool BookingFeeRefundDue { get; set; }
    public bool BookingFeeRefunded { get; set; }
    public bool BookingFeeForfeited { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [NotMapped]
    public BankTransferInstructionsDto? PaymentInstructions { get; set; }

    [JsonIgnore]
    public Patient? Patient { get; set; }

    [JsonIgnore]
    public Doctor? Doctor { get; set; }

    [JsonIgnore]
    public User? Receptionist { get; set; }

    [JsonIgnore]
    public User? User { get; set; }
}
