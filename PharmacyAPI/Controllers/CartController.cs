using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Data;
using PharmacyAPI.Model;
using PharmacyAPI.Models; // تأكد أن هذا هو اسم الـ Namespace الصحيح لموديلاتك
using System.Security.Claims;

namespace PharmacyAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CartController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;

        public CartController(AppDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        // 1. جلب محتويات السلة
        [HttpGet]
        public async Task<IActionResult> GetUserCart()
        {
            var userId = GetUserId();

            // جلب الإعدادات من appsettings.json
            var estimatedShipping = _config.GetValue<decimal>("CartSettings:EstimatedShipping");
            var regulatoryFees = _config.GetValue<decimal>("CartSettings:RegulatoryFees");

            var cartItems = await _context.CartItems
                .AsNoTracking()
                .Include(c => c.Medicine)
                .Where(c => c.UserId == userId)
                .Select(item => new
                {
                    item.Id,
                    item.MedicineId,
                    ProductName = item.Medicine.Name,
                    SKU = item.Medicine.SKU,
                    item.Medicine.ImageUrl,
                    item.Medicine.IsFdaApproved,
                    item.Medicine.IsColdChain,
                    UnitPrice = item.Medicine.Price,
                    item.Quantity,
                    Total = item.Quantity * item.Medicine.Price
                })
                .ToListAsync();

            var subtotal = cartItems.Sum(x => x.Total);
            var totalUnits = cartItems.Sum(x => x.Quantity);

            return Ok(new
            {
                items = cartItems,
                summary = new
                {
                    totalUnits,
                    subtotal,
                    estimatedShipping,
                    regulatoryFees,
                    totalEstimate = subtotal + estimatedShipping + regulatoryFees
                }
            });
        }

        // 2. إضافة دواء للسلة
        [HttpPost("add/{medicineId}")]
        public async Task<IActionResult> AddToCart(int medicineId, int quantity = 1)
        {
            if (quantity <= 0)
                return BadRequest(new { message = "الكمية يجب أن تكون أكبر من صفر!" });

            var userId = GetUserId();

            var medicine = await _context.Medicines.FindAsync(medicineId);
            if (medicine == null)
                return NotFound(new { message = "الدواء غير موجود!" });

            if (quantity > medicine.StockQuantity)
                return BadRequest(new { message = $"المخزون غير كافٍ، المتاح: {medicine.StockQuantity}" });

            var existingItem = await _context.CartItems
                .FirstOrDefaultAsync(c => c.UserId == userId && c.MedicineId == medicineId);

            if (existingItem != null)
            {
                if (existingItem.Quantity + quantity > medicine.StockQuantity)
                    return BadRequest(new { message = "الكمية الإجمالية تتجاوز المخزون!" });

                existingItem.Quantity += quantity;
            }
            else
            {
                _context.CartItems.Add(new CartItem
                {
                    UserId = userId,
                    MedicineId = medicineId,
                    Quantity = quantity
                });
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "تمت الإضافة للسلة بنجاح" });
        }

        // 3. تحديث الكمية
        [HttpPut("update-quantity")]
        public async Task<IActionResult> UpdateQuantity(int cartItemId, int newQuantity)
        {
            var userId = GetUserId();

            var item = await _context.CartItems
                .Include(c => c.Medicine)
                .FirstOrDefaultAsync(c => c.Id == cartItemId && c.UserId == userId);

            if (item == null)
                return NotFound(new { message = "العنصر غير موجود!" });

            if (newQuantity <= 0)
            {
                _context.CartItems.Remove(item);
            }
            else
            {
                if (newQuantity > item.Medicine.StockQuantity)
                    return BadRequest(new { message = $"المخزون المتاح فقط: {item.Medicine.StockQuantity}" });

                item.Quantity = newQuantity;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "تم تحديث الكمية بنجاح!" });
        }

        // 4. حذف عنصر من السلة
        [HttpDelete("{id}")]
        public async Task<IActionResult> RemoveItem(int id)
        {
            var userId = GetUserId();
            var item = await _context.CartItems
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

            if (item == null)
                return NotFound(new { message = "العنصر غير موجود!" });

            _context.CartItems.Remove(item);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // 5. Checkout — تحويل السلة إلى طلب فعلي وخصم المخزون
        [HttpPost("checkout")]
        public async Task<IActionResult> Checkout()
        {
            var userId = GetUserId();

            var cartItems = await _context.CartItems
                .Include(c => c.Medicine)
                .Where(c => c.UserId == userId)
                .ToListAsync();

            if (!cartItems.Any())
                return BadRequest(new { message = "السلة فارغة!" });

            // فحص أولي للمخزون قبل بدء العملية
            foreach (var item in cartItems)
            {
                if (item.Quantity > item.Medicine.StockQuantity)
                    return BadRequest(new { message = $"الكمية المطلوبة من {item.Medicine.Name} غير متوفرة حالياً!" });
            }

            var subtotal = cartItems.Sum(item => item.Quantity * item.Medicine.Price);
            var shipping = _config.GetValue<decimal>("CartSettings:EstimatedShipping");
            var regulatory = _config.GetValue<decimal>("CartSettings:RegulatoryFees");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // إنشاء الطلب الرئيسي
                var order = new Order
                {
                    UserId = userId,
                    OrderDate = DateTime.UtcNow,
                    Subtotal = subtotal,
                    ShippingFees = shipping,
                    RegulatoryFees = regulatory,
                    TotalAmount = subtotal + shipping + regulatory,
                    Status = "Pending"
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync(); // لنحصل على Id الطلب

                // تجهيز تفاصيل الطلب وخصم المخزون
                var orderDetails = new List<OrderDetail>();
                foreach (var item in cartItems)
                {
                    // ✅ التعديل المهم: خصم الكمية من الدواء نفسه
                    item.Medicine.StockQuantity -= item.Quantity;

                    orderDetails.Add(new OrderDetail
                    {
                        OrderId = order.Id,
                        MedicineId = item.MedicineId,
                        Quantity = item.Quantity,
                        PriceAtPurchase = item.Medicine.Price
                    });
                }

                _context.OrderDetails.AddRange(orderDetails);
                _context.CartItems.RemoveRange(cartItems); // تفريغ السلة

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    message = "تم تثبيت طلبك بنجاح وخصم الكميات من المخزون!",
                    orderId = order.Id,
                    finalTotal = order.TotalAmount
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "حدث خطأ فني أثناء المعالجة!", error = ex.Message });
            }
        }

        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                throw new UnauthorizedAccessException();
            return int.Parse(userIdClaim);
        }
    }
}