using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Data;
using PharmacyAPI.Model;
using PharmacyAPI.Models; // تأكد من وجود الموديلات هنا
using System.Security.Claims;

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
        // 1. تحويل السلة إلى طلب (العملية الأساسية)
        // POST /api/orders/place-order
        // ───────────────────────────────────────────
        [HttpPost("place-order")]
        [Authorize(Roles = "Pharmacist")]
        public async Task<IActionResult> PlaceOrder()
        {
            var userId = GetUserId();

            // 1. جلب عناصر السلة الخاصة بالصيدلي
            var cartItems = await _context.CartItems
                .Include(c => c.Medicine)
                .Where(c => c.UserId == userId)
                .ToListAsync();

            if (!cartItems.Any())
                return BadRequest(new { message = "السلة فارغة! لا يمكن إتمام الطلب." });

            // 2. التحقق من المخزون قبل البدء
            foreach (var item in cartItems)
            {
                if (item.Medicine.StockQuantity < item.Quantity)
                    return BadRequest(new { message = $"كمية الدواء {item.Medicine.Name} غير كافية في المستودع!" });
            }

            // 3. جلب رسوم الشحن والرسوم التنظيمية من الإعدادات (appsettings.json)
            var shippingFees = _configuration.GetValue<decimal>("CartSettings:EstimatedShipping");
            var regulatoryFees = _configuration.GetValue<decimal>("CartSettings:RegulatoryFees");

            decimal subtotal = cartItems.Sum(i => i.Quantity * i.Medicine.Price);

            // 4. إنشاء كائن الطلب الرئيسي
            var order = new Order
            {
                UserId = userId,
                OrderDate = DateTime.Now,
                Status = "Pending",
                Subtotal = subtotal,
                ShippingFees = shippingFees,
                RegulatoryFees = regulatoryFees,
                TotalAmount = subtotal + shippingFees + regulatoryFees,
                // توليد رقم طلب احترافي يشبه الواجهة
                OrderNumber = $"PL-{DateTime.Now.ToString("yyyyMMdd")}-{new Random().Next(1000, 9999)}"
            };

            // استخدام Transaction لضمان تنفيذ كل العمليات أو إلغائها معاً في حال حدوث خطأ
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Orders.Add(order);
                await _context.SaveChangesAsync(); // نحفظ الطلب أولاً لنحصل على الـ ID

                // 5. نقل العناصر وتحديث المخزون
                foreach (var item in cartItems)
                {
                    var orderDetail = new OrderDetail
                    {
                        OrderId = order.Id,
                        MedicineId = item.MedicineId,
                        Quantity = item.Quantity,
                        PriceAtPurchase = item.Medicine.Price // تثبيت السعر وقت الشراء
                    };

                    // خصم من المخزون الحقيقي
                    item.Medicine.StockQuantity -= item.Quantity;

                    _context.OrderDetails.Add(orderDetail);
                }

                // 6. تفريغ السلة بعد نجاح العملية
                _context.CartItems.RemoveRange(cartItems);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    message = "تم إنشاء الطلب بنجاح!",
                    orderId = order.Id,
                    orderNumber = order.OrderNumber
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "حدث خطأ أثناء معالجة الطلب", details = ex.Message });
            }
        }

        // ───────────────────────────────────────────
        // 2. الصيدلي يشوف طلباته (مع Pagination)
        // ───────────────────────────────────────────
        [HttpGet("my")]
        [Authorize(Roles = "Pharmacist")]
        public async Task<IActionResult> GetMyOrders(int page = 1, int pageSize = 10)
        {
            if (page < 1) page = 1;
            if (pageSize > 50) pageSize = 50;

            var userId = GetUserId();
            var query = _context.Orders.Where(o => o.UserId == userId);

            var total = await query.CountAsync();
            var orders = await query
                .AsNoTracking()
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
                    TotalItems = o.OrderDetails.Sum(d => d.Quantity)
                })
                .ToListAsync();

            return Ok(new { total, page, pageSize, orders });
        }

        // ───────────────────────────────────────────
        // 3. رؤية تفاصيل طلب معين (للصيدلي والأدمن)
        // ───────────────────────────────────────────
        [HttpGet("{id}")]
        [Authorize(Roles = "Pharmacist,Admin")]
        public async Task<IActionResult> GetOrderById(int id)
        {
            var userId = GetUserId();
            var userRole = GetUserRole();

            var order = await _context.Orders
                .AsNoTracking()
                .Include(o => o.OrderDetails)
                    .ThenInclude(d => d.Medicine)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                return NotFound(new { message = "الطلب غير موجود!" });

            if (userRole != "Admin" && order.UserId != userId)
                return Forbid();

            return Ok(order); // يفضل استخدام DTO هنا لتنظيف البيانات المرسلة للفرونت
        }

        // ───────────────────────────────────────────
        // 4. Admin يغير حالة الطلب
        // ───────────────────────────────────────────
        [HttpPatch("{id}/status")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusDto dto)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound(new { message = "الطلب غير موجود!" });

            if (order.Status == "Cancelled")
                return BadRequest(new { message = "لا يمكن تعديل طلب ملغي!" });

            order.Status = dto.Status;
            await _context.SaveChangesAsync();

            return Ok(new { message = "تم تحديث الحالة بنجاح", newStatus = order.Status });
        }

        // ───────────────────────────────────────────
        // 5. الصيدلي يلغي طلبه (في حال كان Pending فقط)
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

            if (order == null) return NotFound();
            if (order.Status != "Pending")
                return BadRequest(new { message = "لا يمكن إلغاء الطلب بعد البدء بمعالجته!" });

            // إرجاع الكميات للمخزون
            foreach (var detail in order.OrderDetails)
            {
                detail.Medicine.StockQuantity += detail.Quantity;
            }

            order.Status = "Cancelled";
            await _context.SaveChangesAsync();

            return Ok(new { message = "تم إلغاء الطلب وإرجاع المنتجات للمخزون." });
        }

        // Helpers
        private int GetUserId() => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
        private string GetUserRole() => User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
    }

    public class UpdateOrderStatusDto { public string Status { get; set; } = string.Empty; }
}