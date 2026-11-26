namespace StudentManagement.Models;

public enum OtcOrderStatus
{
    Pending,
    ReadyForPickup,
    OutForDelivery,
    Completed,
    Cancelled
}

public enum OtcFulfillmentMethod
{
    Pickup,
    Delivery
}

public class OtcOrder
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string ReceiverName { get; set; } = string.Empty;
    public string ReceiverPhone { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
    public OtcFulfillmentMethod FulfillmentMethod { get; set; }
    public OtcOrderStatus Status { get; set; } = OtcOrderStatus.Pending;
    public string PaymentMethod { get; set; } = "CashOnDelivery";
    public bool IsPaid { get; set; }
    public DateTime? PaymentConfirmedAt { get; set; }
    public string PaymentReference { get; set; } = string.Empty;
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public int PointsUsed { get; set; }
    public int PointsEarned { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? CompletedAt { get; set; }
    public List<OtcOrderItem> Items { get; set; } = [];
}

public class OtcOrderItem
{
    public int Id { get; set; }
    public int OtcOrderId { get; set; }
    public int MedicineId { get; set; }
    public string MedicineName { get; set; } = string.Empty;
    public string MedicineUnit { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}
