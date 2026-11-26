using System.Globalization;
using System.Text;
using QRCoder;
using StudentManagement.DTOs;

namespace StudentManagement.Services;

public class BankTransferService
{
    private readonly IConfiguration configuration;

    public BankTransferService(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(configuration["BankTransfer:AccountNumber"]) &&
        !string.IsNullOrWhiteSpace(configuration["BankTransfer:AccountName"]);

    public BankTransferInstructionsDto? CreateInstructions(decimal amount, string transferDescription)
        => CreateInstructions(amount, transferDescription, useDynamicQr: false);

    public BankTransferInstructionsDto? CreateDynamicInstructions(decimal amount, string transferDescription)
        => CreateInstructions(amount, transferDescription, useDynamicQr: true);

    private BankTransferInstructionsDto? CreateInstructions(
        decimal amount,
        string transferDescription,
        bool useDynamicQr)
    {
        if (!IsConfigured) return null;

        var bankName = configuration["BankTransfer:BankName"] ?? "BIDV";
        var accountNumber = configuration["BankTransfer:AccountNumber"]!;
        var accountName = configuration["BankTransfer:AccountName"]!;
        var qrCodeUrl = useDynamicQr
            ? CreateDynamicQrCodeUrl(bankName, accountNumber, amount, transferDescription)
            : configuration["BankTransfer:QrCodeImagePath"] ?? "/images/medika-payment-qr.png";

        return new BankTransferInstructionsDto
        {
            BankName = bankName,
            AccountNumber = accountNumber,
            AccountName = accountName,
            Amount = amount,
            TransferDescription = transferDescription,
            QrCodeUrl = qrCodeUrl
        };
    }

    private string CreateDynamicQrCodeUrl(
        string bankName,
        string accountNumber,
        decimal amount,
        string transferDescription)
    {
        var bankBin = configuration["BankTransfer:BankBin"] ?? GetBankBin(bankName);
        if (string.IsNullOrWhiteSpace(bankBin))
            return configuration["BankTransfer:QrCodeImagePath"] ?? "/images/medika-payment-qr.png";

        var amountText = decimal.ToInt64(decimal.Round(amount, 0)).ToString(CultureInfo.InvariantCulture);
        var merchantAccountInfo =
            Tlv("00", "A000000727") +
            Tlv("01", Tlv("00", bankBin) + Tlv("01", accountNumber)) +
            Tlv("02", "QRIBFTTA");
        var payload =
            Tlv("00", "01") +
            Tlv("01", "12") +
            Tlv("38", merchantAccountInfo) +
            Tlv("53", "704") +
            Tlv("54", amountText) +
            Tlv("58", "VN") +
            Tlv("62", Tlv("08", transferDescription));
        var payloadWithCrcPlaceholder = $"{payload}6304";
        var vietQrPayload = $"{payloadWithCrcPlaceholder}{CalculateCrc16(payloadWithCrcPlaceholder):X4}";

        using var generator = new QRCodeGenerator();
        using var qrData = generator.CreateQrCode(vietQrPayload, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(qrData).GetGraphic(8);
        return $"data:image/png;base64,{Convert.ToBase64String(png)}";
    }

    private static string Tlv(string tag, string value)
    {
        var length = Encoding.UTF8.GetByteCount(value);
        if (length > 99)
            throw new InvalidOperationException($"VietQR field {tag} exceeds the supported length.");
        return $"{tag}{length:D2}{value}";
    }

    private static ushort CalculateCrc16(string value)
    {
        ushort crc = 0xFFFF;
        foreach (var valueByte in Encoding.UTF8.GetBytes(value))
        {
            crc ^= (ushort)(valueByte << 8);
            for (var bit = 0; bit < 8; bit++)
                crc = (ushort)((crc & 0x8000) != 0 ? (crc << 1) ^ 0x1021 : crc << 1);
        }
        return crc;
    }

    private static string? GetBankBin(string bankName) =>
        bankName.Trim().ToUpperInvariant() switch
        {
            "BIDV" => "970418",
            "VIETCOMBANK" or "VCB" => "970436",
            "VIETINBANK" or "CTG" => "970415",
            "AGRIBANK" => "970405",
            "MB" or "MBBANK" => "970422",
            "ACB" => "970416",
            "TECHCOMBANK" or "TCB" => "970407",
            "VPBANK" or "VPB" => "970432",
            _ => null
        };
}
