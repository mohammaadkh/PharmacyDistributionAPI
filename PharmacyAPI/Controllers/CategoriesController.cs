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

        public CategoriesController(AppDbContext context)
        {
            _context = context;
        }

        // ───────────────────────────────────────────
        // 1. جلب الأصناف مع Pagination
        // GET /api/categories?page=1&pageSize=50
        // ───────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetCategories(int page = 1, int pageSize = 50)
        {
            if (page < 1) page = 1;
            if (pageSize > 100) pageSize = 100;

            var total = await _context.Categories.CountAsync();
            var items = await _context.Categories
                .AsNoTracking()
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new { total, page, pageSize, items });
        }

        // ───────────────────────────────────────────
        // 2. إضافة صنف جديد (Admin فقط)
        // POST /api/categories
        // ───────────────────────────────────────────
        [HttpPost]
        [Authorize(Roles = "Admin")] // ✅ تعديل: بدل الـ if بالداخل
        public async Task<ActionResult<Category>> PostCategory(Category category)
        {
            if (string.IsNullOrWhiteSpace(category.Name))
                return BadRequest(new { message = "خطأ: اسم الصنف حقل مطلوب ولا يمكن تركه فارغاً." });

            // ✅ تعديل: التحقق من عدم تكرار الاسم
            var exists = await _context.Categories
                .AnyAsync(c => c.Name.ToLower() == category.Name.ToLower());
            if (exists)
                return BadRequest(new { message = "خطأ: هذا الصنف موجود مسبقاً!" });

            _context.Categories.Add(category);
            await _context.SaveChangesAsync();
            return Ok(category);
        }

        // ───────────────────────────────────────────
        // 3. تعديل صنف (Admin فقط)
        // PUT /api/categories/{id}
        // ───────────────────────────────────────────
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")] // ✅ تعديل: بدل الـ if بالداخل
        public async Task<IActionResult> PutCategory(int id, Category category)
        {
            if (id != category.Id)
                return BadRequest(new { message = "خطأ: معرف الصنف غير متطابق!" });

            // ✅ تعديل: بنعدل على الـ existing بدل ما نحط entity جديدة
            var existing = await _context.Categories.FindAsync(id);
            if (existing == null)
                return NotFound(new { message = "عذراً، الصنف المطلوب تعديله غير موجود!" });

            existing.Name = category.Name;
            existing.Description = category.Description;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ───────────────────────────────────────────
        // 4. حذف صنف (Admin فقط)
        // DELETE /api/categories/{id}
        // ───────────────────────────────────────────
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")] // ✅ تعديل: بدل الـ if بالداخل
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null)
                return NotFound(new { message = "عذراً، الصنف المطلوب حذفه غير موجود!" });

            var hasMedicines = await _context.Medicines.AnyAsync(m => m.CategoryId == id);
            if (hasMedicines)
                return BadRequest(new { message = "لا يمكن حذف هذا الصنف لأنه يحتوي على أدوية مرتبطة به!" });

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ───────────────────────────────────────────
        // Helper
        // ───────────────────────────────────────────
        private bool CategoryExists(int id) =>
            _context.Categories.Any(e => e.Id == id);
    }
}