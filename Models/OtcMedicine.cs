namespace StudentManagement.Models;

public class OtcMedicine
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // Hạ sốt - Giảm đau, Tiêu hóa, Tai mũi họng, Bổ sung vitamin, Da liễu
    public decimal Price { get; set; }
    public string Unit { get; set; } = "Hộp";
    public string Description { get; set; } = string.Empty;
    public string UsageInstructions { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int StockQuantity { get; set; } = 100;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
