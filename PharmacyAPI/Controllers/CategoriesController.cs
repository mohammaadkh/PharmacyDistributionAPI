using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Data;
using PharmacyAPI.Models;

namespace PharmacyAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoriesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CategoriesController(AppDbContext context) { _context = context; }

        // --- 1. جلب الأصناف (متاحة للجميع) ---
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Category>>> GetCategories()
        {
            return await _context.Categories.AsNoTracking().ToListAsync();
        }

        // --- 2. إضافة صنف جديد (حماية يدوية برسالة فصحى) ---
        [HttpPost]
        [Authorize] // يشترط تسجيل الدخول فقط، والفحص يتم بالداخل
        public async Task<ActionResult<Category>> PostCategory(Category category)
        {
            // التحقق من صلاحية المشرف
            if (!User.IsInRole("Admin"))
            {
                return StatusCode(403, new { message = "عذراً، لا تمتلك الصلاحيات الكافية لإضافة أصناف جديدة؛ هذه العملية مخصصة للمشرفين فقط." });
            }

            if (string.IsNullOrWhiteSpace(category.Name))
                return BadRequest(new { message = "خطأ: اسم الصنف يعتبر حقلاً مطلوباً ولا يمكن تركه فارغاً." });

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();
            return Ok(category);
        }

        // --- 3. تعديل صنف (حماية يدوية برسالة فصحى) ---
        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> PutCategory(int id, Category category)
        {
            // التحقق من صلاحية المشرف
            if (!User.IsInRole("Admin"))
            {
                return StatusCode(403, new { message = "تنبيه: لا يمكن تعديل بيانات الأصناف إلا من قبل المشرف المسؤول عن النظام." });
            }

            if (id != category.Id) return BadRequest();

            _context.Entry(category).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CategoryExists(id)) return NotFound();
                else throw;
            }

            return NoContent();
        }

        // --- 4. حذف صنف (حماية يدوية برسالة فصحى) ---
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            // التحقق من صلاحية المشرف
            if (!User.IsInRole("Admin"))
            {
                return StatusCode(403, new { message = "نعتذر، صلاحية حذف الأصناف محصورة فقط بمدير النظام نظراً لخطورة هذه العملية." });
            }

            var category = await _context.Categories.FindAsync(id);
            if (category == null) return NotFound();

            var hasMedicines = await _context.Medicines.AnyAsync(m => m.CategoryId == id);
            if (hasMedicines)
                return BadRequest(new { message = "لا يمكن إتمام عملية الحذف؛ يوجد أدوية مرتبطة بهذا الصنف حالياً." });

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private bool CategoryExists(int id)
        {
            return _context.Categories.Any(e => e.Id == id);
        }
    }
}