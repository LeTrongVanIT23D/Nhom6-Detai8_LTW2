using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using StudentManagement.Models;
using StudentManagement.Services;

namespace StudentManagement.Controllers;

[ApiController]
[Route("api/invoices")]
[Authorize]
public class InvoicesController : ControllerBase
{
    private readonly IInvoiceService invoiceService;

    public InvoicesController(IInvoiceService invoiceService)
    {
        this.invoiceService = invoiceService;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Receptionist")]
    public async Task<ActionResult<List<InvoiceDto>>> GetInvoices()
    {
        var list = await invoiceService.GetInvoicesAsync();
        return Ok(list);
    }

    [HttpGet("appointment/{appointmentId}")]
    public async Task<ActionResult<InvoiceDto>> GetByAppointmentId(int appointmentId)
    {
        var inv = await invoiceService.GetInvoiceByAppointmentIdAsync(appointmentId);
        if (inv == null) return NotFound(new { message = "Chưa có hóa đơn cho ca khám này." });
        if (!User.IsInRole("Admin") && !User.IsInRole("Receptionist"))
        {
            if (!User.IsInRole("User"))
                return Forbid();

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            try
            {
                var patient = HttpContext.RequestServices.GetRequiredService<IPatientManager>().FindByUserId(userId);
                if (patient.Id != inv.PatientId)
                    return Forbid();
            }
            catch (KeyNotFoundException)
            {
                return Forbid();
            }
        }
        return Ok(inv);
    }

    public class CreateInvoiceRequest
    {
        public int AppointmentId { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = "Cash";
        public string Notes { get; set; } = string.Empty;
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Receptionist")]
    public async Task<ActionResult<Invoice>> CreateInvoice([FromBody] CreateInvoiceRequest req)
    {
        try
        {
            var invoice = await invoiceService.CreateInvoiceAsync(req.AppointmentId, req.Amount, req.PaymentMethod, req.Notes);
            return Ok(invoice);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    public class ConfirmPaymentRequest
    {
        public string PaymentMethod { get; set; } = "Cash";
    }

    [HttpPatch("{id}/confirm")]
    [Authorize(Roles = "Admin,Receptionist")]
    public async Task<ActionResult> ConfirmPayment(int id, [FromBody] ConfirmPaymentRequest? req = null)
    {
        var method = req?.PaymentMethod ?? "Cash";
        try
        {
            var success = await invoiceService.ConfirmPaymentAsync(id, method);
            if (!success) return NotFound(new { message = "Không tìm thấy hóa đơn." });
            return Ok(new { message = "Xác nhận thanh toán thành công." });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }
}
