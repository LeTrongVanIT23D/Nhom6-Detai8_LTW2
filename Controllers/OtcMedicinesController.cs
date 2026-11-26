using System.Security.Claims;
using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentManagement.Data;
using StudentManagement.DTOs;
using StudentManagement.Models;
using StudentManagement.Services;

namespace StudentManagement.Controllers;

[ApiController]
[Route("api/otc-medicines")]
public class OtcMedicinesController : ControllerBase
{
    private readonly AppDbContext context;
    private readonly IAuditLogService auditLogService;
    private readonly BankTransferService bankTransferService;
    private readonly IFileService fileService;

    public OtcMedicinesController(
        AppDbContext context,
        IAuditLogService auditLogService,
        BankTransferService bankTransferService,
        IFileService fileService)
    {
        this.context = context;
        this.auditLogService = auditLogService;
        this.bankTransferService = bankTransferService;
        this.fileService = fileService;
    }

    /// <summary>
    /// Lấy danh mục thuốc không kê đơn (OTC) cho nhà thuốc trực tuyến
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<OtcMedicineDto>>> GetMedicines([FromQuery] string? category = null, [FromQuery] string? search = null)
    {
        var query = context.OtcMedicines.AsNoTracking().Where(m => m.IsActive);

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(m => m.Category.ToLower() == category.Trim().ToLower());
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(m => m.Name.ToLower().Contains(term) || m.Description.ToLower().Contains(term));
        }

        var list = await query.OrderBy(m => m.Category).ThenBy(m => m.Name).ToListAsync();

        return Ok(list.Select(m => new OtcMedicineDto
        {
            Id = m.Id,
            Name = m.Name,
            Category = m.Category,
            Price = m.Price,
            Unit = m.Unit,
            Description = m.Description,
            UsageInstructions = m.UsageInstructions,
            ImageUrl = m.ImageUrl,
            StockQuantity = m.StockQuantity,
            IsActive = m.IsActive
        }).ToList());
    }

    /// <summary>
    /// Chi tiết một sản phẩm thuốc OTC
    /// </summary>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public async Task<ActionResult<OtcMedicineDto>> GetById(int id)
    {
        var m = await context.OtcMedicines.FindAsync(id);
        if (m == null || !m.IsActive) return NotFound(new { message = "Không tìm thấy sản phẩm thuốc OTC." });

        return Ok(new OtcMedicineDto
        {
            Id = m.Id,
            Name = m.Name,
            Category = m.Category,
            Price = m.Price,
            Unit = m.Unit,
            Description = m.Description,
            UsageInstructions = m.UsageInstructions,
            ImageUrl = m.ImageUrl,
            StockQuantity = m.StockQuantity,
            IsActive = m.IsActive
        });
    }

    [HttpPost("{id:int}/image")]
    [Authorize(Roles = "Admin")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadMedicineImage(int id, IFormFile file)
    {
        var medicine = await context.OtcMedicines.FirstOrDefaultAsync(item => item.Id == id);
        if (medicine == null)
            return NotFound(new { message = "Không tìm thấy thuốc OTC." });

        try
        {
            var uploadedFile = await fileService.UploadFileAsync(file);
            medicine.ImageUrl = $"/api/files/{uploadedFile.Id}";
            await context.SaveChangesAsync();
            await auditLogService.LogAsync(
                "OTC_MEDICINE_IMAGE_UPDATED",
                User.FindFirstValue(ClaimTypes.Name) ?? "Admin",
                User.FindFirstValue(ClaimTypes.Role) ?? "Admin",
                $"Cập nhật ảnh thuốc OTC #{medicine.Id}");
            return Ok(new { imageUrl = medicine.ImageUrl });
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
    }

    public class UpdateMedicineImageUrlRequest
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(500)]
        public string ImageUrl { get; set; } = string.Empty;
    }

    public class CreateMedicineRequest
    {
        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(100)]
        public string Category { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Range(typeof(decimal), "0.01", "1000000000")]
        public decimal Price { get; set; }

        [System.ComponentModel.DataAnnotations.Required]
        [System.ComponentModel.DataAnnotations.StringLength(100)]
        public string Unit { get; set; } = "Hộp";

        [System.ComponentModel.DataAnnotations.StringLength(1000)]
        public string Description { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.StringLength(1000)]
        public string UsageInstructions { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.StringLength(500)]
        public string ImageUrl { get; set; } = string.Empty;

        [System.ComponentModel.DataAnnotations.Range(0, int.MaxValue)]
        public int StockQuantity { get; set; }

        public IFormFile? ImageFile { get; set; }
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<OtcMedicineDto>> CreateMedicine([FromForm] CreateMedicineRequest request)
    {
        if (request.ImageFile is { Length: > 0 } && !string.IsNullOrWhiteSpace(request.ImageUrl))
            return BadRequest(new { message = "Chỉ chọn tải ảnh lên hoặc nhập URL ảnh, không dùng cả hai." });

        var imageUrl = string.Empty;
        if (!string.IsNullOrWhiteSpace(request.ImageUrl))
        {
            if (!Uri.TryCreate(request.ImageUrl.Trim(), UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps)
                return BadRequest(new { message = "URL ảnh phải là địa chỉ HTTPS hợp lệ." });
            imageUrl = uri.AbsoluteUri;
        }

        try
        {
            if (request.ImageFile is { Length: > 0 })
            {
                var uploadedFile = await fileService.UploadFileAsync(request.ImageFile);
                imageUrl = $"/api/files/{uploadedFile.Id}";
            }

            var medicine = new OtcMedicine
            {
                Name = request.Name.Trim(),
                Category = request.Category.Trim(),
                Price = request.Price,
                Unit = request.Unit.Trim(),
                Description = request.Description.Trim(),
                UsageInstructions = request.UsageInstructions.Trim(),
                ImageUrl = imageUrl,
                StockQuantity = request.StockQuantity
            };

            context.OtcMedicines.Add(medicine);
            await context.SaveChangesAsync();
            await auditLogService.LogAsync(
                "OTC_MEDICINE_CREATED",
                User.FindFirstValue(ClaimTypes.Name) ?? "Admin",
                User.FindFirstValue(ClaimTypes.Role) ?? "Admin",
                $"Thêm thuốc OTC #{medicine.Id}: {medicine.Name}");

            return CreatedAtAction(nameof(GetById), new { id = medicine.Id }, new OtcMedicineDto
            {
                Id = medicine.Id,
                Name = medicine.Name,
                Category = medicine.Category,
                Price = medicine.Price,
                Unit = medicine.Unit,
                Description = medicine.Description,
                UsageInstructions = medicine.UsageInstructions,
                ImageUrl = medicine.ImageUrl,
                StockQuantity = medicine.StockQuantity,
                IsActive = medicine.IsActive
            });
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return BadRequest(new { message = exception.Message }); }
    }

    [HttpPatch("{id:int}/image")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateMedicineImageUrl(
        int id,
        [FromBody] UpdateMedicineImageUrlRequest request)
    {
        if (!Uri.TryCreate(request.ImageUrl.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
            return BadRequest(new { message = "URL ảnh phải là địa chỉ HTTPS hợp lệ." });

        var medicine = await context.OtcMedicines.FirstOrDefaultAsync(item => item.Id == id);
        if (medicine == null)
            return NotFound(new { message = "Không tìm thấy thuốc OTC." });

        medicine.ImageUrl = uri.AbsoluteUri;
        await context.SaveChangesAsync();
        await auditLogService.LogAsync(
            "OTC_MEDICINE_IMAGE_UPDATED",
            User.FindFirstValue(ClaimTypes.Name) ?? "Admin",
            User.FindFirstValue(ClaimTypes.Role) ?? "Admin",
            $"Cập nhật URL ảnh thuốc OTC #{medicine.Id}");
        return Ok(new { imageUrl = medicine.ImageUrl });
    }

    /// <summary>
    /// Tạo đơn, giữ tồn kho và yêu cầu chuyển khoản ngay khi đặt.
    /// </summary>
    [HttpPost("order")]
    [Authorize(Roles = "User")]
    public async Task<ActionResult<OtcOrderResponseDto>> CreateOrder([FromBody] OtcOrderRequestDto request)
    {
        if (request.Items.Count == 0 || request.Items.Count > 20)
            return BadRequest(new { message = "Giỏ hàng rỗng. Vui lòng chọn ít nhất 1 sản phẩm thuốc OTC." });

        if (!Enum.IsDefined(request.FulfillmentMethod))
            return BadRequest(new { message = "Hình thức nhận thuốc không hợp lệ." });
        if (string.IsNullOrWhiteSpace(request.ReceiverName) ||
            string.IsNullOrWhiteSpace(request.ReceiverPhone))
            return BadRequest(new { message = "Vui lòng nhập họ tên và số điện thoại người nhận." });
        if (request.FulfillmentMethod == OtcFulfillmentMethod.Delivery &&
            string.IsNullOrWhiteSpace(request.DeliveryAddress))
            return BadRequest(new { message = "Vui lòng nhập địa chỉ giao thuốc." });

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var user = await context.Users.FindAsync(userId);
        if (user == null)
            return Unauthorized();
        var patient = await context.Patients.FirstOrDefaultAsync(item => item.UserId == userId);

        var orderItems = new List<OtcOrderItem>();
        decimal subTotal = 0;
        var groupedItems = request.Items
            .GroupBy(item => item.MedicineId)
            .Select(group => new { MedicineId = group.Key, Quantity = group.Sum(item => item.Quantity) })
            .ToList();

        foreach (var requestedItem in groupedItems)
        {
            if (requestedItem.Quantity is < 1 or > 20)
                return BadRequest(new { message = "Mỗi loại thuốc chỉ được đặt từ 1 đến 20 đơn vị." });

            var medicine = await context.OtcMedicines
                .FirstOrDefaultAsync(item => item.Id == requestedItem.MedicineId && item.IsActive);
            if (medicine == null)
                return BadRequest(new { message = $"Sản phẩm thuốc mã #{requestedItem.MedicineId} không tồn tại hoặc đã ngừng kinh doanh." });
            if (medicine.StockQuantity < requestedItem.Quantity)
                return Conflict(new { message = $"Sản phẩm {medicine.Name} chỉ còn {medicine.StockQuantity} {medicine.Unit} trong kho." });

            medicine.StockQuantity -= requestedItem.Quantity;
            var lineTotal = medicine.Price * requestedItem.Quantity;
            subTotal += lineTotal;
            orderItems.Add(new OtcOrderItem
            {
                MedicineId = medicine.Id,
                MedicineName = medicine.Name,
                MedicineUnit = medicine.Unit,
                UnitPrice = medicine.Price,
                Quantity = requestedItem.Quantity,
                LineTotal = lineTotal
            });
        }

        if (request.PointsToUse > user.RewardPoints)
            return BadRequest(new { message = $"Số điểm thưởng của bạn ({user.RewardPoints} điểm) không đủ để dùng {request.PointsToUse} điểm." });

        var pointsUsed = Math.Min(request.PointsToUse, (int)(subTotal / 1000m));
        var discountAmount = pointsUsed * 1000m;
        var finalAmount = subTotal - discountAmount;
        if (finalAmount > 0 && !bankTransferService.IsConfigured)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = "Chưa cấu hình tài khoản nhận chuyển khoản BIDV. Đơn chưa được tạo; vui lòng báo quản trị viên cấu hình BankTransfer."
            });

        user.RewardPoints -= pointsUsed;
        if (patient != null)
            patient.RewardPoints = user.RewardPoints;

        var order = new OtcOrder
        {
            UserId = userId,
            ReceiverName = request.ReceiverName.Trim(),
            ReceiverPhone = request.ReceiverPhone.Trim(),
            DeliveryAddress = request.FulfillmentMethod == OtcFulfillmentMethod.Delivery
                ? request.DeliveryAddress.Trim()
                : string.Empty,
            FulfillmentMethod = request.FulfillmentMethod,
            PaymentMethod = finalAmount > 0 ? "BankTransfer" : "RewardPoints",
            SubTotal = subTotal,
            DiscountAmount = discountAmount,
            FinalAmount = finalAmount,
            PointsUsed = pointsUsed,
            IsPaid = finalAmount == 0,
            PaymentConfirmedAt = finalAmount == 0 ? DateTime.Now : null,
            Items = orderItems
        };
        context.OtcOrders.Add(order);
        await context.SaveChangesAsync();
        await auditLogService.LogAsync(
            "OTC_MEDICINE_ORDER",
            user.Username,
            user.Role,
            $"Tạo đơn OTC #{order.Id}, tổng {order.SubTotal:N0}đ, giảm {order.DiscountAmount:N0}đ, cần chuyển khoản {order.FinalAmount:N0}đ");
        await transaction.CommitAsync();

        return Ok(ToDto(
            order,
            true,
            user.RewardPoints,
            order.IsPaid ? null : bankTransferService.CreateInstructions(order.FinalAmount, $"MEDIKA OTC {order.Id}")));
    }

    [HttpGet("orders/mine")]
    [Authorize(Roles = "User")]
    public async Task<ActionResult<List<OtcOrderResponseDto>>> GetMyOrders()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var orders = await context.OtcOrders.AsNoTracking()
            .Include(order => order.Items)
            .Where(order => order.UserId == userId)
            .OrderByDescending(order => order.CreatedAt)
            .ToListAsync();
        return Ok(orders.Select(order => ToDto(
            order,
            false,
            paymentInstructions: order.IsPaid || order.Status == OtcOrderStatus.Cancelled
                ? null
                : bankTransferService.CreateInstructions(order.FinalAmount, $"MEDIKA OTC {order.Id}"))).ToList());
    }

    [HttpGet("orders")]
    [Authorize(Roles = "Admin,Receptionist")]
    public async Task<ActionResult<List<OtcOrderResponseDto>>> GetOrders()
    {
        var orders = await context.OtcOrders.AsNoTracking()
            .Include(order => order.Items)
            .OrderBy(order => order.Status)
            .ThenByDescending(order => order.CreatedAt)
            .ToListAsync();
        return Ok(orders.Select(order => ToDto(order, false)).ToList());
    }

    [HttpPatch("orders/{id:int}/payment/confirm")]
    [Authorize(Roles = "Admin,Receptionist")]
    public async Task<ActionResult<OtcOrderResponseDto>> ConfirmPayment(
        int id,
        [FromBody] ConfirmBankTransferDto request)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var order = await context.OtcOrders.Include(item => item.Items).FirstOrDefaultAsync(item => item.Id == id);
        if (order == null)
            return NotFound(new { message = "Không tìm thấy đơn thuốc OTC." });
        if (order.IsPaid)
            return Ok(ToDto(order, false));
        if (order.Status != OtcOrderStatus.Pending)
            return Conflict(new { message = "Chỉ đối soát được đơn OTC đang chờ xử lý." });

        order.IsPaid = true;
        order.PaymentConfirmedAt = DateTime.Now;
        order.PaymentReference = request.TransactionReference.Trim();
        await context.SaveChangesAsync();
        await auditLogService.LogAsync(
            "OTC_PAYMENT_CONFIRMED",
            User.FindFirstValue(ClaimTypes.Name) ?? "Staff",
            User.FindFirstValue(ClaimTypes.Role) ?? "Staff",
            $"Đối soát chuyển khoản đơn OTC #{order.Id}; mã giao dịch: {order.PaymentReference}");
        await transaction.CommitAsync();
        return Ok(ToDto(order, false));
    }

    [HttpPatch("orders/{id:int}/status")]
    [Authorize(Roles = "Admin,Receptionist")]
    public Task<ActionResult<OtcOrderResponseDto>> UpdateOrderStatus(
        int id,
        [FromBody] UpdateOtcOrderStatusDto request) =>
        ChangeOrderStatus(id, request.Status, null);

    [HttpPatch("orders/{id:int}/cancel")]
    [Authorize(Roles = "User")]
    public Task<ActionResult<OtcOrderResponseDto>> CancelMyOrder(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return ChangeOrderStatus(id, OtcOrderStatus.Cancelled, userId);
    }

    private async Task<ActionResult<OtcOrderResponseDto>> ChangeOrderStatus(
        int id,
        OtcOrderStatus nextStatus,
        int? ownerUserId)
    {
        if (!Enum.IsDefined(nextStatus))
            return BadRequest(new { message = "Trạng thái đơn thuốc không hợp lệ." });

        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var order = await context.OtcOrders
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == id);
        if (order == null)
            return NotFound(new { message = "Không tìm thấy đơn thuốc OTC." });
        if (ownerUserId.HasValue && order.UserId != ownerUserId.Value)
            return Forbid();

        if (nextStatus == OtcOrderStatus.Cancelled)
        {
            if (order.Status != OtcOrderStatus.Pending)
                return Conflict(new { message = "Chỉ có thể hủy đơn đang chờ nhân viên xác nhận." });
            if (order.IsPaid)
                return Conflict(new { message = "Đơn đã thanh toán. Vui lòng liên hệ lễ tân để xử lý hoàn tiền trước khi hủy." });

            foreach (var item in order.Items)
            {
                var medicine = await context.OtcMedicines.FindAsync(item.MedicineId);
                if (medicine != null)
                    medicine.StockQuantity += item.Quantity;
            }

            var user = await context.Users.FindAsync(order.UserId);
            if (user != null)
            {
                user.RewardPoints += order.PointsUsed;
                var patient = await context.Patients.FirstOrDefaultAsync(item => item.UserId == order.UserId);
                if (patient != null)
                    patient.RewardPoints = user.RewardPoints;
            }
            order.Status = OtcOrderStatus.Cancelled;
        }
        else if (nextStatus == OtcOrderStatus.ReadyForPickup &&
                 order.Status == OtcOrderStatus.Pending &&
                 order.FulfillmentMethod == OtcFulfillmentMethod.Pickup)
        {
            if (!order.IsPaid)
                return Conflict(new { message = "Cần đối soát chuyển khoản trước khi xác nhận nhận thuốc." });
            order.Status = nextStatus;
        }
        else if (nextStatus == OtcOrderStatus.OutForDelivery &&
                 order.Status == OtcOrderStatus.Pending &&
                 order.FulfillmentMethod == OtcFulfillmentMethod.Delivery)
        {
            if (!order.IsPaid)
                return Conflict(new { message = "Cần đối soát chuyển khoản trước khi giao thuốc." });
            order.Status = nextStatus;
        }
        else if (nextStatus == OtcOrderStatus.Completed &&
                 ((order.Status == OtcOrderStatus.ReadyForPickup &&
                   order.FulfillmentMethod == OtcFulfillmentMethod.Pickup) ||
                  (order.Status == OtcOrderStatus.OutForDelivery &&
                   order.FulfillmentMethod == OtcFulfillmentMethod.Delivery)))
        {
            if (!order.IsPaid)
                return Conflict(new { message = "Đơn chưa được xác nhận thanh toán." });
            order.Status = nextStatus;
            order.CompletedAt = DateTime.Now;
            order.PointsEarned = (int)(order.FinalAmount / 100000m) * 10;
            var user = await context.Users.FindAsync(order.UserId);
            if (user != null)
            {
                user.RewardPoints += order.PointsEarned;
                var patient = await context.Patients.FirstOrDefaultAsync(item => item.UserId == order.UserId);
                if (patient != null)
                    patient.RewardPoints = user.RewardPoints;
            }
        }
        else
        {
            return Conflict(new { message = "Trạng thái mới không hợp lệ đối với bước hiện tại của đơn." });
        }

        await context.SaveChangesAsync();
        var actor = User.FindFirstValue(ClaimTypes.Name) ?? "Staff";
        var role = User.FindFirstValue(ClaimTypes.Role) ?? "Staff";
        await auditLogService.LogAsync(
            "OTC_ORDER_STATUS",
            actor,
            role,
            $"Cập nhật đơn OTC #{order.Id} sang {order.Status}");
        await transaction.CommitAsync();
        return Ok(ToDto(order, false));
    }

    private static OtcOrderResponseDto ToDto(
        OtcOrder order,
        bool success,
        int remainingPoints = 0,
        BankTransferInstructionsDto? paymentInstructions = null) => new()
    {
        Id = order.Id,
        Success = success,
        Message = success
            ? order.IsPaid
                ? "Đơn đã được thanh toán bằng điểm; nhân viên sẽ xác nhận giao hoặc nhận tại quầy."
                : "Đơn đã được tạo. Hãy chuyển khoản đúng nội dung và chờ nhân viên đối soát."
            : string.Empty,
        Status = order.Status,
        FulfillmentMethod = order.FulfillmentMethod,
        ReceiverName = order.ReceiverName,
        ReceiverPhone = order.ReceiverPhone,
        DeliveryAddress = order.DeliveryAddress,
        SubTotal = order.SubTotal,
        DiscountAmount = order.DiscountAmount,
        FinalAmount = order.FinalAmount,
        PointsUsed = order.PointsUsed,
        PointsEarned = order.PointsEarned,
        RemainingPoints = remainingPoints,
        IsPaid = order.IsPaid,
        PaymentConfirmedAt = order.PaymentConfirmedAt,
        PaymentReference = order.PaymentReference,
        PaymentInstructions = paymentInstructions,
        CreatedAt = order.CreatedAt,
        Items = order.Items.Select(item => new OtcOrderLineDto
        {
            MedicineId = item.MedicineId,
            MedicineName = item.MedicineName,
            MedicineUnit = item.MedicineUnit,
            UnitPrice = item.UnitPrice,
            Quantity = item.Quantity,
            LineTotal = item.LineTotal
        }).ToList()
    };
}
