using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Data;
using PharmacyAPI.Model;
using PharmacyAPI.Models;
using System.Security.Claims;
using System.Text;

namespace PharmacyAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public OrdersController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // ───────────────────────────────────────────
        // 1. تحويل السلة إلى طلب
        // POST /api/orders/place-order
        // ───────────────────────────────────────────
        [HttpPost("place-order")]
        [Authorize(Roles = "Pharmacist")]
        public async Task<IActionResult> PlaceOrder()
        {
            var userId = GetUserId();

            var cartItems = await _context.CartItems
                .Include(c => c.Medicine)
                .Where(c => c.UserId == userId)
                .ToListAsync();

            if (!cartItems.Any())
                return BadRequest(new { message = "السلة فارغة! لا يمكن إتمام الطلب." });

            foreach (var item in cartItems)
            {
                if (item.Medicine.StockQuantity < item.Quantity)
                    return BadRequest(new { message = $"كمية {item.Medicine.Name} غير كافية في المستودع!" });
            }

            var shippingFees = _configuration.GetValue<decimal>("CartSettings:EstimatedShipping");
            var regulatoryFees = _configuration.GetValue<decimal>("CartSettings:RegulatoryFees");
            var subtotal = cartItems.Sum(i => i.Quantity * i.Medicine.Price);

            var order = new Order
            {
                UserId = userId,
                OrderDate = DateTime.UtcNow,
                Status = "Pending",
                Subtotal = subtotal,
                ShippingFees = shippingFees,
                RegulatoryFees = regulatoryFees,
                TotalAmount = subtotal + shippingFees + regulatoryFees,
                OrderNumber = $"PL-{DateTime.UtcNow:yyyyMMdd}-{new Random().Next(1000, 9999)}"
            };

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                foreach (var item in cartItems)
                {
                    item.Medicine.StockQuantity -= item.Quantity;
                    _context.OrderDetails.Add(new OrderDetail
                    {
                        OrderId = order.Id,
                        MedicineId = item.MedicineId,
                        Quantity = item.Quantity,
                        PriceAtPurchase = item.Medicine.Price
                    });
                }

                _context.CartItems.RemoveRange(cartItems);
                await _context.SaveChangesAsync();

                // ✅ إشعار للصيدلي عند تثبيت الطلب
                await NotificationsController.AddNotification(
                    _context,
                    userId,
                    "تم تثبيت طلبك",
                    $"تم استلام طلبك رقم {order.OrderNumber} بنجاح — الإجمالي: ${order.TotalAmount}",
                    "Orders"
                );

                await transaction.CommitAsync();

                return Ok(new
                {
                    message = "تم إنشاء الطلب بنجاح!",
                    orderId = order.Id,
                    orderNumber = order.OrderNumber,
                    totalAmount = order.TotalAmount
                });
            }
            catch
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "حدث خطأ أثناء معالجة الطلب، حاول مجدداً!" });
            }
        }

        // ───────────────────────────────────────────
        // 2. الصيدلي يشوف طلباته
        // GET /api/orders/my?page=1&pageSize=10&status=Pending
        // ───────────────────────────────────────────
        [HttpGet("my")]
        [Authorize(Roles = "Pharmacist")]
        public async Task<IActionResult> GetMyOrders(
            int page = 1,
            int pageSize = 10,
            string? status = null)
        {
            if (page < 1) page = 1;
            if (pageSize > 50) pageSize = 50;

            var userId = GetUserId();

            var query = _context.Orders
                .AsNoTracking()
                .Where(o => o.UserId == userId);

            if (!string.IsNullOrEmpty(status))
                query = query.Where(o => o.Status == status);

            var total = await query.CountAsync();

            var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new
                {
                    o.Id,
                    o.OrderNumber,
                    o.OrderDate,
                    o.Status,
                    o.Subtotal,
                    o.ShippingFees,
                    o.RegulatoryFees,
                    o.TotalAmount,
                    TotalItems = o.OrderDetails.Sum(d => d.Quantity)
                })
                .ToListAsync();

            return Ok(new { total, page, pageSize, orders });
        }

        // ───────────────────────────────────────────
        // 3. تفاصيل طلب معين
        // GET /api/orders/{id}
        // ───────────────────────────────────────────
        [HttpGet("{id}")]
        [Authorize(Roles = "Pharmacist,Admin,PharmaceuticalCompany")]
        public async Task<IActionResult> GetOrderById(int id)
        {
            var userId = GetUserId();
            var userRole = GetUserRole();

            var order = await _context.Orders
                .AsNoTracking()
                .Include(o => o.OrderDetails)
                    .ThenInclude(d => d.Medicine)
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                return NotFound(new { message = "الطلب غير موجود!" });

            if (userRole == "Pharmacist" && order.UserId != userId)
                return Forbid();

            return Ok(new
            {
                order.Id,
                order.OrderNumber,
                order.OrderDate,
                order.Status,
                order.Subtotal,
                order.ShippingFees,
                order.RegulatoryFees,
                order.TotalAmount,
                Buyer = new
                {
                    order.User.FullName,
                    order.User.Email,
                    order.User.OrganizationType
                },
                Items = order.OrderDetails.Select(d => new
                {
                    d.Id,
                    d.MedicineId,
                    MedicineName = d.Medicine.Name,
                    SKU = d.Medicine.SKU,
                    ImageUrl = d.Medicine.ImageUrl,
                    IsFdaApproved = d.Medicine.IsFdaApproved,
                    IsColdChain = d.Medicine.IsColdChain,
                    d.Quantity,
                    d.PriceAtPurchase,
                    Total = d.Quantity * d.PriceAtPurchase
                })
            });
        }

        // ───────────────────────────────────────────
        // 4. Admin يشوف كل الطلبات
        // GET /api/orders?page=1&pageSize=10&status=Pending
        // ───────────────────────────────────────────
        [HttpGet]
        [Authorize(Roles = "Admin,PharmaceuticalCompany")]
        public async Task<IActionResult> GetAllOrders(
            int page = 1,
            int pageSize = 10,
            string? status = null)
        {
            if (page < 1) page = 1;
            if (pageSize > 50) pageSize = 50;

            var query = _context.Orders
                .AsNoTracking()
                .Include(o => o.User)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(o => o.Status == status);

            var total = await query.CountAsync();

            // إحصائيات سريعة للـ Incoming Orders page
            var awaitingReview = await _context.Orders
                .CountAsync(o => o.Status == "Pending");
            var processing = await _context.Orders
                .CountAsync(o => o.Status == "Processing");
            var shippedToday = await _context.Orders
                .CountAsync(o => o.Status == "Shipped"
                    && o.OrderDate.Date == DateTime.UtcNow.Date);
            var dailyVolume = await _context.Orders
                .Where(o => o.OrderDate.Date == DateTime.UtcNow.Date
                    && o.Status != "Cancelled")
                .SumAsync(o => o.TotalAmount);

            var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new
                {
                    o.Id,
                    o.OrderNumber,
                    o.OrderDate,
                    o.Status,
                    o.TotalAmount,
                    TotalItems = o.OrderDetails.Sum(d => d.Quantity),
                    Buyer = new
                    {
                        o.User.Id,
                        o.User.FullName,
                        o.User.Email,
                        o.User.OrganizationType
                    }
                })
                .ToListAsync();

            return Ok(new
            {
                stats = new
                {
                    awaitingReview,
                    processing,
                    shippedToday,
                    dailyVolume
                },
                total,
                page,
                pageSize,
                orders
            });
        }

        // ───────────────────────────────────────────
        // 5. Admin يغير Status الطلب
        // PATCH /api/orders/{id}/status
        // ───────────────────────────────────────────
        [HttpPatch("{id}/status")]
        [Authorize(Roles = "Admin,PharmaceuticalCompany")]
        public async Task<IActionResult> UpdateOrderStatus(
            int id,
            [FromBody] UpdateOrderStatusDto dto)
        {
            var allowedStatuses = new[]
            {
                "Pending", "Processing", "Shipped", "Delivered", "Cancelled"
            };

            if (!allowedStatuses.Contains(dto.Status))
                return BadRequest(new { message = $"الحالة غير صحيحة! المسموح: {string.Join(", ", allowedStatuses)}" });

            var order = await _context.Orders.FindAsync(id);
            if (order == null)
                return NotFound(new { message = "الطلب غير موجود!" });

            if (order.Status == "Cancelled")
                return BadRequest(new { message = "لا يمكن تعديل طلب ملغي!" });

            order.Status = dto.Status;
            await _context.SaveChangesAsync();

            // ✅ إشعار للصيدلي عند تغيير الحالة
            await NotificationsController.AddNotification(
                _context,
                order.UserId,
                "تحديث حالة طلبك",
                $"طلبك رقم {order.OrderNumber} أصبح {dto.Status}",
                "Orders"
            );

            return Ok(new
            {
                message = "تم تحديث حالة الطلب بنجاح!",
                orderId = order.Id,
                newStatus = order.Status
            });
        }

        // ───────────────────────────────────────────
        // 6. Admin يقبل طلب
        // PATCH /api/orders/{id}/accept
        // ───────────────────────────────────────────
        [HttpPatch("{id}/accept")]
        [Authorize(Roles = "Admin,PharmaceuticalCompany")]
        public async Task<IActionResult> AcceptOrder(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null)
                return NotFound(new { message = "الطلب غير موجود!" });

            if (order.Status != "Pending")
                return BadRequest(new { message = "يمكن قبول الطلبات الـ Pending فقط!" });

            order.Status = "Processing";
            await _context.SaveChangesAsync();

            // ✅ إشعار للصيدلي
            await NotificationsController.AddNotification(
                _context,
                order.UserId,
                "تم قبول طلبك",
                $"طلبك رقم {order.OrderNumber} تم قبوله وجاري المعالجة!",
                "Orders"
            );

            return Ok(new
            {
                message = "تم قبول الطلب بنجاح!",
                orderId = order.Id,
                newStatus = order.Status
            });
        }

        // ───────────────────────────────────────────
        // 7. Admin يرفض طلب
        // PATCH /api/orders/{id}/reject
        // ───────────────────────────────────────────
        [HttpPatch("{id}/reject")]
        [Authorize(Roles = "Admin,PharmaceuticalCompany")]
        public async Task<IActionResult> RejectOrder(int id, [FromBody] RejectOrderDto dto)
        {
            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(d => d.Medicine)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                return NotFound(new { message = "الطلب غير موجود!" });

            if (order.Status == "Shipped" || order.Status == "Delivered")
                return BadRequest(new { message = "لا يمكن رفض طلب تم شحنه أو تسليمه!" });

            // إرجاع المخزون
            foreach (var detail in order.OrderDetails)
            {
                detail.Medicine.StockQuantity += detail.Quantity;
            }

            order.Status = "Cancelled";
            await _context.SaveChangesAsync();

            // ✅ إشعار للصيدلي مع سبب الرفض
            await NotificationsController.AddNotification(
                _context,
                order.UserId,
                "تم رفض طلبك",
                $"طلبك رقم {order.OrderNumber} تم رفضه. السبب: {dto.Reason}",
                "Orders"
            );

            return Ok(new
            {
                message = "تم رفض الطلب وإرجاع المخزون!",
                orderId = order.Id
            });
        }

        // ───────────────────────────────────────────
        // 8. الصيدلي يلغي طلبه
        // PATCH /api/orders/{id}/cancel
        // ───────────────────────────────────────────
        [HttpPatch("{id}/cancel")]
        [Authorize(Roles = "Pharmacist")]
        public async Task<IActionResult> CancelOrder(int id)
        {
            var userId = GetUserId();

            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                    .ThenInclude(d => d.Medicine)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null)
                return NotFound(new { message = "الطلب غير موجود!" });

            if (order.Status != "Pending")
                return BadRequest(new { message = "لا يمكن إلغاء الطلب بعد البدء بمعالجته!" });

            foreach (var detail in order.OrderDetails)
            {
                detail.Medicine.StockQuantity += detail.Quantity;
            }

            order.Status = "Cancelled";
            await _context.SaveChangesAsync();

            return Ok(new { message = "تم إلغاء الطلب وإرجاع المنتجات للمخزون." });
        }

        // ───────────────────────────────────────────
        // 9. Export Ledger
        // GET /api/orders/export
        // ───────────────────────────────────────────
        [HttpGet("export")]
        [Authorize(Roles = "Pharmacist")]
        public async Task<IActionResult> ExportOrders()
        {
            var userId = GetUserId();

            var orders = await _context.Orders
                .AsNoTracking()
                .Include(o => o.OrderDetails)
                    .ThenInclude(d => d.Medicine)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("Order Number,Date,Status,Subtotal,Shipping,Regulatory,Total");

            foreach (var order in orders)
            {
                csv.AppendLine(
                    $"{order.OrderNumber}," +
                    $"{order.OrderDate:yyyy-MM-dd}," +
                    $"{order.Status}," +
                    $"{order.Subtotal}," +
                    $"{order.ShippingFees}," +
                    $"{order.RegulatoryFees}," +
                    $"{order.TotalAmount}"
                );
            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"orders_{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        // ───────────────────────────────────────────
        // Helpers
        // ───────────────────────────────────────────
        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                throw new UnauthorizedAccessException();
            return int.Parse(userIdClaim);
        }

        private string GetUserRole()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        }
    }

    // ───────────────────────────────────────────
    // DTOs
    // ───────────────────────────────────────────
    public class UpdateOrderStatusDto
    {
        public string Status { get; set; } = string.Empty;
    }

    public class RejectOrderDto
    {
        public string Reason { get; set; } = string.Empty;
    }
}