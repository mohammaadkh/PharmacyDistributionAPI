using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Data;
using PharmacyAPI.Models;
using System.Security.Claims;

namespace PharmacyAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // حماية السلة عشان كل مستخدم يشوف سلته بس
    public class CartController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CartController(AppDbContext context) { _context = context; }

        // --- 1. جلب محتويات السلة ---
        [HttpGet]
        public async Task<IActionResult> GetUserCart()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

            var cartItems = await _context.CartItems
                .Include(c => c.Medicine)
                .Where(c => c.UserId == userId)
                .Select(item => new {
                    item.Id,
                    item.MedicineId,
                    ProductName = item.Medicine.Name,
                    item.Medicine.ImageUrl,
                    UnitPrice = item.Medicine.Price,
                    item.Quantity,
                    Total = item.Quantity * item.Medicine.Price
                })
                .ToListAsync();

            var subtotal = cartItems.Sum(x => x.Total);

            return Ok(new
            {
                items = cartItems,
                summary = new
                {
                    subtotal = subtotal,
                    estimatedShipping = 145.50m,
                    regulatoryFees = 89.00m,
                    totalEstimate = subtotal + 145.50m + 89.00m
                }
            });
        }

        // --- 2. إضافة دواء للسلة (تم التعديل لمنع خطأ 500) ---
        [HttpPost("add/{medicineId}")]
        public async Task<IActionResult> AddToCart(int medicineId, int quantity = 1)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);

            // ** التحقق من وجود الدواء أولاً لمنع تعارض الـ Foreign Key **
            var medicineExists = await _context.Medicines.AnyAsync(m => m.Id == medicineId);
            if (!medicineExists)
            {
                return NotFound(new { message = $"عذراً، الدواء صاحب الرقم {medicineId} غير موجود في قاعدة البيانات!" });
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

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { message = "تمت الإضافة للسلة بنجاح" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "حدث خطأ أثناء الحفظ", error = ex.Message });
            }
        }

        // --- 3. تحديث الكمية ---
        [HttpPut("update-quantity")]
        public async Task<IActionResult> UpdateQuantity(int cartItemId, int newQuantity)
        {
            var item = await _context.CartItems.FindAsync(cartItemId);
            if (item == null) return NotFound();

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

        // --- 4. حذف عنصر من السلة ---
        [HttpDelete("{id}")]
        public async Task<IActionResult> RemoveItem(int id)
        {
            var item = await _context.CartItems.FindAsync(id);
            if (item == null) return NotFound();

            _context.CartItems.Remove(item);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}