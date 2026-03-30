using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Data;
using PharmacyAPI.Models;
using PharmacyAPI.Models.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.IO; // ضرورية للتعامل مع الملفات

namespace PharmacyAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MedicinesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment; // لإدارة مسارات السيرفر

        public MedicinesController(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // --- 1. جلب الأدوية (متاحة للجميع) ---
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Medicine>>> GetMedicines(string? search, int? categoryId)
        {
            var query = _context.Medicines.Include(m => m.Category).AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(m => m.Name.Contains(search)
                                      || m.SKU.Contains(search)
                                      || m.Manufacturer.Contains(search));
            }

            if (categoryId.HasValue)
            {
                query = query.Where(m => m.CategoryId == categoryId.Value);
            }

            return await query.ToListAsync();
        }

        // --- 2. جلب تفاصيل المنتج العميقة (للواجهة الاحترافية) ---
        [HttpGet("{id}/details")]
        [AllowAnonymous]
        public async Task<ActionResult<ProductDetailsDto>> GetProductDetails(int id)
        {
            if (id <= 0) return BadRequest(new { message = "معرف المنتج غير صالح" });

            var medicine = await _context.Medicines
                .AsNoTracking()
                .Include(m => m.Category)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (medicine == null) return NotFound(new { message = "عذراً، هذا المنتج غير متوفر حالياً" });

            var details = new ProductDetailsDto
            {
                Id = medicine.Id,
                Name = medicine.Name,
                Description = medicine.Description ?? "لا يوجد وصف طبي متوفر حالياً.",
                NdcNumber = "50458-578-01",
                ManufacturerName = medicine.Manufacturer,
                CategoryName = medicine.Category?.Name ?? "General",
                StorageCondition = medicine.IsColdChain ? "2°C - 8°C" : "20°C - 25°C",
                Price = medicine.Price,
                StockQuantity = medicine.StockQuantity,
                ImageUrl = medicine.ImageUrl,
                IsFdaApproved = medicine.IsFdaApproved,
                IsColdChain = medicine.IsColdChain,
                BlackBoxWarning = "تحذير: زيادة خطر الوفاة لدى المرضى المسنين المصابين بذهان متعلق بالخرف.",
                ClinicalSpecs = new List<string> {
                    "Pharmacotherapeutic group: Psycholeptics",
                    "Active ingredient: Paliperidone Palmitate",
                    "Extended-release injectable suspension"
                }
            };

            return Ok(details);
        }

        // --- 3. إضافة دواء جديد مع رفع صورة حقيقية (للمشرف فقط) ---
        [HttpPost]
        [Authorize]
        public async Task<ActionResult<Medicine>> PostMedicine([FromForm] Medicine medicine)
        {
            if (!User.IsInRole("Admin"))
                return StatusCode(403, new { message = "عذراً، لا يمكن إلا للمشرف (Admin) إضافة أدوية جديدة." });

            if (medicine.Price <= 0)
                return BadRequest(new { message = "خطأ: السعر يجب أن يكون قيمة موجبة!" });

            // منطق رفع الصورة الحقيقية
            if (medicine.ImageFile != null)
            {
                medicine.ImageUrl = await SaveImage(medicine.ImageFile);
            }

            _context.Medicines.Add(medicine);
            await _context.SaveChangesAsync();
            return CreatedAtAction("GetMedicine", new { id = medicine.Id }, medicine);
        }

        // --- 4. تحديث بيانات دواء وصورته (للمشرف فقط) ---
        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> PutMedicine(int id, [FromForm] Medicine medicine)
        {
            if (!User.IsInRole("Admin"))
                return StatusCode(403, new { message = "تعديل البيانات متاح فقط للمشرف المسؤول." });

            if (id != medicine.Id) return BadRequest();

            var existingMedicine = await _context.Medicines.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
            if (existingMedicine == null) return NotFound();

            // إذا رفع المستخدم صورة جديدة، نحذف القديمة ونرفع الجديدة
            if (medicine.ImageFile != null)
            {
                DeleteOldImage(existingMedicine.ImageUrl);
                medicine.ImageUrl = await SaveImage(medicine.ImageFile);
            }
            else
            {
                medicine.ImageUrl = existingMedicine.ImageUrl; // الحفاظ على الرابط القديم إذا لم تُرفع صورة
            }

            _context.Entry(medicine).State = EntityState.Modified;

            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException)
            {
                if (!MedicineExists(id)) return NotFound();
                else throw;
            }
            return NoContent();
        }

        // --- 5. حذف دواء وحذف صورته من السيرفر ---
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteMedicine(int id)
        {
            if (!User.IsInRole("Admin"))
                return StatusCode(403, new { message = "عملية الحذف نهائية ومتاحة للإدارة فقط." });

            var medicine = await _context.Medicines.FindAsync(id);
            if (medicine == null) return NotFound();

            DeleteOldImage(medicine.ImageUrl); // حذف الملف من المجلد عند حذف الدواء

            _context.Medicines.Remove(medicine);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // --- وظائف مساعدة (Helper Methods) لرفع الصور ---
        private async Task<string> SaveImage(IFormFile file)
        {
            string folderPath = Path.Combine(_environment.WebRootPath, "images");
            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

            string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            string filePath = Path.Combine(folderPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
            return "/images/" + fileName;
        }

        private void DeleteOldImage(string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl) || imageUrl.Contains("default.png")) return;

            string filePath = Path.Combine(_environment.WebRootPath, imageUrl.TrimStart('/'));
            if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Medicine>> GetMedicine(int id)
        {
            var medicine = await _context.Medicines.Include(m => m.Category).FirstOrDefaultAsync(m => m.Id == id);
            if (medicine == null) return NotFound();
            return medicine;
        }

        private bool MedicineExists(int id)
        {
            return _context.Medicines.Any(e => e.Id == id);
        }
    }
}