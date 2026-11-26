using System.ComponentModel.DataAnnotations;

namespace StudentManagement.DTOs;

public class BankTransferInstructionsDto
{
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string TransferDescription { get; set; } = string.Empty;
    public string QrCodeUrl { get; set; } = string.Empty;
}

public class ConfirmBankTransferDto
{
    [StringLength(100)]
    public string TransactionReference { get; set; } = string.Empty;
}
