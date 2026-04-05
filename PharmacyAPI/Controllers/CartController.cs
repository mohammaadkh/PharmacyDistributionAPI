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
        private readonly IConfiguration _config;

        public CartController(AppDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        // ───────────────────────────────────────────
        // 1. جلب محتويات السلة
        // GET /api/cart
        // ───────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetUserCart()
        {
            var userId = GetUserId();

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
                return NotFound(new { message = "العنصر غير موجود!" });

            _context.CartItems.Remove(item);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ───────────────────────────────────────────
        // Helper
        // ───────────────────────────────────────────
        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                throw new UnauthorizedAccessException();
            return int.Parse(userIdClaim);
        }
    }
}