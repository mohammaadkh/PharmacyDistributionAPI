using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Data;
using PharmacyAPI.Model;
using PharmacyAPI.Models;
using System.Security.Claims;

namespace PharmacyAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CartController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CartController(AppDbContext context) { _context = context; }

        // ───────────────────────────────────────────
        // 1. جلب محتويات السلة
        // GET /api/cart
        // ───────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetUserCart()
        {
            var userId = GetUserId();

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
                    IsFdaApproved = item.Medicine.IsFdaApproved,
                    IsColdChain = item.Medicine.IsColdChain,
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
                    estimatedShipping = 145.50m,
                    regulatoryFees = 89.00m,
                    totalEstimate = subtotal + 145.50m + 89.00m
                }
            });
        }

        // ───────────────────────────────────────────
        // 2. إضافة دواء للسلة
        // POST /api/cart/add/{medicineId}?quantity=1
        // ───────────────────────────────────────────
        [HttpPost("add/{medicineId}")]
        public async Task<IActionResult> AddToCart(int medicineId, int quantity = 1)
        {
            if (quantity <= 0)
                return BadRequest(new { message = "الكمية يجب أن تكون أكبر من صفر!" });

            var userId = GetUserId();

            var medicine = await _context.Medicines.FindAsync(medicineId);
            if (medicine == null)
                return NotFound(new { message = $"عذراً، الدواء رقم {medicineId} غير موجود!" });

            // ✅ تعديل: تحقق من المخزون
            if (quantity > medicine.StockQuantity)
                return BadRequest(new { message = $"الكمية المطلوبة تتجاوز المخزون المتاح ({medicine.StockQuantity})!" });

            var existingItem = await _context.CartItems
                .FirstOrDefaultAsync(c => c.UserId == userId && c.MedicineId == medicineId);

            if (existingItem != null)
            {
                // ✅ تعديل: تحقق من المخزون بعد الإضافة
                if (existingItem.Quantity + quantity > medicine.StockQuantity)
                    return BadRequest(new { message = $"الكمية الإجمالية تتجاوز المخزون المتاح ({medicine.StockQuantity})!" });

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

        // ───────────────────────────────────────────
        // 3. تحديث الكمية
        // PUT /api/cart/update-quantity?cartItemId=&newQuantity=
        // ───────────────────────────────────────────
        [HttpPut("update-quantity")]
        public async Task<IActionResult> UpdateQuantity(int cartItemId, int newQuantity)
        {
            var userId = GetUserId();

            var item = await _context.CartItems
                .Include(c => c.Medicine)
                .FirstOrDefaultAsync(c => c.Id == cartItemId && c.UserId == userId);

            if (item == null)
                return NotFound(new { message = "العنصر غير موجود في سلتك الشخصية!" });

            if (newQuantity <= 0)
            {
                _context.CartItems.Remove(item);
            }
            else
            {
                // ✅ تعديل: تحقق من المخزون
                if (newQuantity > item.Medicine.StockQuantity)
                    return BadRequest(new { message = $"الكمية المطلوبة تتجاوز المخزون المتاح ({item.Medicine.StockQuantity})!" });

                item.Quantity = newQuantity;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "تم تحديث الكمية بنجاح!" });
        }

        // ───────────────────────────────────────────
        // 4. حذف عنصر من السلة
        // DELETE /api/cart/{id}
        // ───────────────────────────────────────────
        [HttpDelete("{id}")]
        public async Task<IActionResult> RemoveItem(int id)
        {
            var userId = GetUserId();

            var item = await _context.CartItems
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

            if (item == null)
                return NotFound(new { message = "العنصر غير موجود في سلتك الشخصية!" });

            _context.CartItems.Remove(item);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ───────────────────────────────────────────
        // 5. Checkout — تحويل السلة إلى فاتورة
        // POST /api/cart/checkout
        // ───────────────────────────────────────────
        [HttpPost("checkout")]
        public async Task<IActionResult> Checkout()
        {
            var userId = GetUserId();

            var cartItems = await _context.CartItems
                .Include(c => c.Medicine)
                .Where(c => c.UserId == userId)
                .ToListAsync();

            if (!cartItems.Any())
                return BadRequest(new { message = "السلة فارغة، لا يمكن إتمام الطلب!" });

            // ✅ تعديل: تحقق من المخزون قبل الـ Checkout
            foreach (var item in cartItems)
            {
                if (item.Quantity > item.Medicine.StockQuantity)
                    return BadRequest(new
                    {
                        message = $"الكمية المطلوبة من {item.Medicine.Name} تتجاوز المخزون المتاح ({item.Medicine.StockQuantity})!"
                    });
            }

            var subtotal = cartItems.Sum(item => item.Quantity * item.Medicine.Price);
            var shipping = 145.50m;
            var regulatory = 89.00m;

            // ✅ تعديل: Transaction لضمان سلامة البيانات
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var order = new Order
                {
                    UserId = userId,
                    OrderDate = DateTime.UtcNow, // ✅ تعديل: UTC
                    Subtotal = subtotal,
                    ShippingFees = shipping,
                    RegulatoryFees = regulatory,
                    TotalAmount = subtotal + shipping + regulatory,
                    Status = "Pending"
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                var orderDetails = cartItems.Select(item => new OrderDetail
                {
                    OrderId = order.Id,
                    MedicineId = item.MedicineId,
                    Quantity = item.Quantity,
                    PriceAtPurchase = item.Medicine.Price
                }).ToList();

                _context.OrderDetails.AddRange(orderDetails);
                _context.CartItems.RemoveRange(cartItems);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return Ok(new
                {
                    message = "تم تثبيت طلبك بنجاح!",
                    orderId = order.Id,
                    finalTotal = order.TotalAmount
                });
            }
            catch
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "حدث خطأ أثناء تثبيت الطلب، حاول مجدداً!" });
            }
        }

        // ───────────────────────────────────────────
        // Helper: جلب الـ UserId من الـ Token
        // ───────────────────────────────────────────
        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            // ✅ تعديل: بدل ما يرجع 0، بيرمي exception
            if (string.IsNullOrEmpty(userIdClaim))
                throw new UnauthorizedAccessException();
            return int.Parse(userIdClaim);
        }
    }
}