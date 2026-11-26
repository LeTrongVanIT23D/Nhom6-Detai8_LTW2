using System.ComponentModel.DataAnnotations;
using StudentManagement.Models;

namespace StudentManagement.DTOs;

public class OtcMedicineDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Unit { get; set; } = "Hộp";
    public string Description { get; set; } = string.Empty;
    public string UsageInstructions { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int StockQuantity { get; set; }
    public bool IsActive { get; set; }
}

public class OtcOrderRequestDto
{
    [Required, MinLength(1)]
    public List<OtcOrderItemDto> Items { get; set; } = [];

    [Range(0, int.MaxValue)]
    public int PointsToUse { get; set; } = 0;

    [StringLength(500)]
    public string DeliveryAddress { get; set; } = string.Empty;

    [Required, StringLength(30)]
    public string ReceiverPhone { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string ReceiverName { get; set; } = string.Empty;

    public OtcFulfillmentMethod FulfillmentMethod { get; set; } = OtcFulfillmentMethod.Delivery;
}

public class OtcOrderItemDto
{
    [Range(1, int.MaxValue)]
    public int MedicineId { get; set; }

    [Range(1, 20)]
    public int Quantity { get; set; }
}

public class OtcOrderResponseDto
{
    public int Id { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public OtcOrderStatus Status { get; set; }
    public OtcFulfillmentMethod FulfillmentMethod { get; set; }
    public string ReceiverName { get; set; } = string.Empty;
    public string ReceiverPhone { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public int PointsUsed { get; set; }
    public int PointsEarned { get; set; }
    public int RemainingPoints { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaymentConfirmedAt { get; set; }
    public string PaymentReference { get; set; } = string.Empty;
    public BankTransferInstructionsDto? PaymentInstructions { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<OtcOrderLineDto> Items { get; set; } = [];
}

public class OtcOrderLineDto
{
    public int MedicineId { get; set; }
    public string MedicineName { get; set; } = string.Empty;
    public string MedicineUnit { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}

public class UpdateOtcOrderStatusDto
{
    public OtcOrderStatus Status { get; set; }
}
