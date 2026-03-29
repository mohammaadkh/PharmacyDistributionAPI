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
    [Authorize] // حماية السلة لضمان الخصوصية
    public class CartController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CartController(AppDbContext context) { _context = context; }

        // --- 1. جلب محتويات السلة (تم تحسين الأداء وإضافة تفاصيل الواجهة) ---
        [HttpGet]
        public async Task<IActionResult> GetUserCart()
        {
            var userId = GetUserId();

            var cartItems = await _context.CartItems
                .AsNoTracking() // تحسين سرعة الأداء للجلب فقط
                .Include(c => c.Medicine)
                .Where(c => c.UserId == userId)
                .Select(item => new
                {
                    item.Id,
                    item.MedicineId,
                    ProductName = item.Medicine.Name,
                    SKU = item.Medicine.SKU, // إظهار كود المنتج الفريد
                    item.Medicine.ImageUrl,
                    IsFdaApproved = item.Medicine.IsFdaApproved, // للعلامة الخضراء
                    IsColdChain = item.Medicine.IsColdChain,     // للعلامة الزرقاء
                    UnitPrice = item.Medicine.Price,
                    item.Quantity,
                    Total = item.Quantity * item.Medicine.Price
                })
                .ToListAsync();

            var subtotal = cartItems.Sum(x => x.Total);
            var totalUnits = cartItems.Sum(x => x.Quantity); // حساب عدد القطع الكلي

            return Ok(new
            {
                items = cartItems,
                summary = new
                {
                    totalUnits = totalUnits, // إرسال إجمالي الوحدات للفرونت-إند
                    subtotal = subtotal,
                    estimatedShipping = 145.50m,
                    regulatoryFees = 89.00m,
                    totalEstimate = subtotal + 145.50m + 89.00m
                }
            });
        }

        // --- 2. إضافة دواء للسلة ---
        [HttpPost("add/{medicineId}")]
        public async Task<IActionResult> AddToCart(int medicineId, int quantity = 1)
        {
            var userId = GetUserId();

            var medicineExists = await _context.Medicines.AnyAsync(m => m.Id == medicineId);
            if (!medicineExists)
            {
                return NotFound(new { message = $"عذراً، الدواء رقم {medicineId} غير موجود!" });
            }

            var existingItem = await _context.CartItems
                .FirstOrDefaultAsync(c => c.UserId == userId && c.MedicineId == medicineId);

            if (existingItem != null)
            {
                existingItem.Quantity += quantity;
            }
            else
            {
                var cartItem = new CartItem
                {
                    UserId = userId,
                    MedicineId = medicineId,
                    Quantity = quantity
                };
                _context.CartItems.Add(cartItem);
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "تمت الإضافة للسلة بنجاح" });
        }

        // --- 3. تحديث الكمية (تم إضافة حماية الـ UserId) ---
        [HttpPut("update-quantity")]
        public async Task<IActionResult> UpdateQuantity(int cartItemId, int newQuantity)
        {
            var userId = GetUserId();

            // التأكد أن العنصر المطلوب تعديله يخص المستخدم المسجل حالياً
            var item = await _context.CartItems
                .FirstOrDefaultAsync(c => c.Id == cartItemId && c.UserId == userId);

            if (item == null) return NotFound(new { message = "العنصر غير موجود في سلتك الشخصية" });

            if (newQuantity <= 0)
            {
                _context.CartItems.Remove(item);
            }
            else
            {
                item.Quantity = newQuantity;
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        // --- 4. حذف عنصر (تم إضافة حماية الـ UserId) ---
        [HttpDelete("{id}")]
        public async Task<IActionResult> RemoveItem(int id)
        {
            var userId = GetUserId();

            // التأكد أن الحذف يتم فقط من سلة المستخدم الحالي
            var item = await _context.CartItems
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

            if (item == null) return NotFound();

            _context.CartItems.Remove(item);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ميثود مساعدة لجلب معرف المستخدم من التوكن بشكل آمن
        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.Parse(userIdClaim ?? "0");
        }

        // --- 5. تثبيت الطلب وتحويل السلة إلى فاتورة (Checkout) ---
        [HttpPost("checkout")]
        public async Task<IActionResult> Checkout()
        {
            var userId = GetUserId();

            // 1. جلب عناصر السلة مع بيانات الأدوية
            var cartItems = await _context.CartItems
                .Include(c => c.Medicine)
                .Where(c => c.UserId == userId)
                .ToListAsync();

            if (!cartItems.Any())
                return BadRequest(new { message = "السلة فارغة، لا يمكن إتمام الطلب!" });

            // 2. حساب القيم المالية (مطابقة للواجهة)
            var subtotal = cartItems.Sum(item => item.Quantity * item.Medicine.Price);
            var shipping = 145.50m;
            var regulatory = 89.00m;

            // 3. إنشاء رأس الفاتورة (Order)
            var order = new Order
            {
                UserId = userId,
                OrderDate = DateTime.Now,
                Subtotal = subtotal,
                ShippingFees = shipping,
                RegulatoryFees = regulatory,
                TotalAmount = subtotal + shipping + regulatory,
                Status = "Pending"
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync(); // حفظ لتوليد الـ OrderId

            // 4. تحويل عناصر السلة إلى تفاصيل فاتورة (OrderDetails)
            foreach (var item in cartItems)
            {
                var detail = new OrderDetail
                {
                    OrderId = order.Id,
                    MedicineId = item.MedicineId,
                    Quantity = item.Quantity,
                    PriceAtPurchase = item.Medicine.Price // حفظ السعر وقت الشراء لضمان دقة التقارير
                };
                _context.OrderDetails.Add(detail);
            }

            // 5. تفريغ السلة بعد نجاح الطلب
            _context.CartItems.RemoveRange(cartItems);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "تم تثبيت طلبك بنجاح!",
                orderId = order.Id,
                finalTotal = order.TotalAmount
            });
        }
    }
}