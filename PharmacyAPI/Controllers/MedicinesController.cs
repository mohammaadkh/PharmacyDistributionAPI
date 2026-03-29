using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Data;
using PharmacyAPI.Models;

namespace PharmacyAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MedicinesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public MedicinesController(AppDbContext context)
        {
            _context = context;
        }

        // 1. جلب الأدوية (متاحة للجميع)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Medicine>>> GetMedicines(string? search, int? categoryId)
        {
            var query = _context.Medicines.Include(m => m.Category).AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(m => m.Name.Contains(search) || m.SKU.Contains(search));
            }

            if (categoryId.HasValue)
            {
                query = query.Where(m => m.CategoryId == categoryId.Value);
            }

            return await query.ToListAsync();
        }

        // GET: api/Medicines/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Medicine>> GetMedicine(int id)
        {
            var medicine = await _context.Medicines.Include(m => m.Category).FirstOrDefaultAsync(m => m.Id == id);
            if (medicine == null) return NotFound();
            return medicine;
        }

        // 2. إضافة دواء جديد (مع رسالة خطأ مخصصة للمشرف)
        [HttpPost]
        [Authorize] // يجب تسجيل الدخول أولاً
        public async Task<ActionResult<Medicine>> PostMedicine(Medicine medicine)
        {
            // فحص الصلاحية يدوياً لإرجاع رسالة عربية
            if (!User.IsInRole("Admin"))
            {
                return StatusCode(403, new { message = "عذراً  ، لا يمكن إلا للمشرف (Admin) إضافة أدوية جديدة." });
            }

            if (medicine.Price <= 0)
                return BadRequest(new { message = "خطأ: السعر يجب أن يكون قيمة موجبة أكبر من صفر!" });

            var skuExists = await _context.Medicines.AnyAsync(m => m.SKU == medicine.SKU);
            if (skuExists)
                return BadRequest(new { message = "خطأ: كود الـ SKU هذا مستخدم مسبقاً لدواء آخر!" });

            _context.Medicines.Add(medicine);
            await _context.SaveChangesAsync();
            return CreatedAtAction("GetMedicine", new { id = medicine.Id }, medicine);
        }

        // 3. تحديث بيانات دواء (مع رسالة خطأ مخصصة للمشرف)
        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> PutMedicine(int id, Medicine medicine)
        {
            if (!User.IsInRole("Admin"))
            {
                return StatusCode(403, new { message = "تنبيه: تعديل أسعار أو بيانات الأدوية متاح فقط للمشرف المسؤول." });
            }

            if (id != medicine.Id) return BadRequest();

            if (medicine.Price <= 0)
                return BadRequest(new { message = "خطأ: لا يمكن تعديل السعر لقيمة صفر أو أقل!" });

            _context.Entry(medicine).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!MedicineExists(id)) return NotFound();
                else throw;
            }

            return NoContent();
        }

        // 4. حذف دواء (مع رسالة خطأ مخصصة للمشرف)
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteMedicine(int id)
        {
            if (!User.IsInRole("Admin"))
            {
                return StatusCode(403, new { message = "   ، عملية الحذف نهائية ولا يمكن القيام بها إلا من قبل الإدارة." });
            }

            var medicine = await _context.Medicines.FindAsync(id);
            if (medicine == null) return NotFound();

            _context.Medicines.Remove(medicine);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private bool MedicineExists(int id)
        {
            return _context.Medicines.Any(e => e.Id == id);
        }
    }
}