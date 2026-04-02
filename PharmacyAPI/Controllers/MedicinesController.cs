using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Data;
using PharmacyAPI.Models;
using PharmacyAPI.Models.DTOs;

namespace PharmacyAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MedicinesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public MedicinesController(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // --- 1. جلب الأدوية مع البحث والفلترة ---
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
                query = query.Where(m => m.CategoryId == categoryId.Value);

            return await query.ToListAsync();
        }

        // --- 2. جلب تفاصيل دواء معين ---
        [HttpGet("{id}")]
        public async Task<ActionResult<Medicine>> GetMedicine(int id)
        {
            var medicine = await _context.Medicines
                .Include(m => m.Category)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (medicine == null)
                return NotFound(new { message = "عذراً، هذا الدواء غير موجود!" });

            return medicine;
        }

        // --- 3. جلب التفاصيل العميقة للواجهة ---
        [HttpGet("{id}/details")]
        [AllowAnonymous]
        public async Task<ActionResult<ProductDetailsDto>> GetProductDetails(int id)
        {
            if (id <= 0)
                return BadRequest(new { message = "خطأ: معرف المنتج غير صالح!" });

            var medicine = await _context.Medicines
                .AsNoTracking()
                .Include(m => m.Category)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (medicine == null)
                return NotFound(new { message = "عذراً، هذا المنتج غير متوفر حالياً!" });

            var details = new ProductDetailsDto
            {
                Id = medicine.Id,
                Name = medicine.Name,
                Description = medicine.Description ?? "لا يوجد وصف طبي متوفر حالياً.",
                NdcNumber = medicine.NdcNumber ?? "غير متوفر",
                ManufacturerName = medicine.Manufacturer,
                CategoryName = medicine.Category?.Name ?? "General",
                StorageCondition = medicine.IsColdChain ? "2°C - 8°C" : "20°C - 25°C",
                Price = medicine.Price,
                StockQuantity = medicine.StockQuantity,
                ImageUrl = medicine.ImageUrl,
                IsFdaApproved = medicine.IsFdaApproved,
                IsColdChain = medicine.IsColdChain,
                BlackBoxWarning = medicine.BlackBoxWarning ?? "لا يوجد تحذير خاص بهذا الدواء",
                ClinicalSpecs = string.IsNullOrEmpty(medicine.ClinicalSpecs)
                    ? new List<string>()
                    : medicine.ClinicalSpecs.Split(',').ToList()
            };

            return Ok(details);
        }

        // --- 4. إضافة دواء جديد (Admin فقط) ---
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<Medicine>> PostMedicine([FromForm] Medicine medicine)
        {
            if (medicine.Price <= 0)
                return BadRequest(new { message = "خطأ: السعر يجب أن يكون قيمة موجبة!" });

            if (string.IsNullOrWhiteSpace(medicine.Name))
                return BadRequest(new { message = "خطأ: اسم الدواء مطلوب!" });

            if (medicine.ImageFile != null)
                medicine.ImageUrl = await SaveImage(medicine.ImageFile);

            _context.Medicines.Add(medicine);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetMedicine), new { id = medicine.Id }, medicine);
        }

        // --- 5. تعديل دواء (Admin فقط) ---
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> PutMedicine(int id, [FromForm] Medicine medicine)
        {
            if (id != medicine.Id)
                return BadRequest(new { message = "خطأ: معرف الدواء غير متطابق!" });

            var existingMedicine = await _context.Medicines
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);

            if (existingMedicine == null)
                return NotFound(new { message = "عذراً، الدواء المطلوب تعديله غير موجود!" });

            if (medicine.ImageFile != null)
            {
                DeleteOldImage(existingMedicine.ImageUrl);
                medicine.ImageUrl = await SaveImage(medicine.ImageFile);
            }
            else
            {
                medicine.ImageUrl = existingMedicine.ImageUrl;
            }

            _context.Entry(medicine).State = EntityState.Modified;

            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException)
            {
                if (!MedicineExists(id))
                    return NotFound(new { message = "عذراً، الدواء لم يعد موجوداً!" });
                else throw;
            }

            return NoContent();
        }

        // --- 6. حذف دواء (Admin فقط) ---
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteMedicine(int id)
        {
            var medicine = await _context.Medicines.FindAsync(id);
            if (medicine == null)
                return NotFound(new { message = "عذراً، الدواء المطلوب حذفه غير موجود!" });

            var isInCart = await _context.CartItems.AnyAsync(c => c.MedicineId == id);
            if (isInCart)
                return BadRequest(new { message = "لا يمكن حذف هذا الدواء لأنه موجود في سلة مستخدم حالياً!" });

            var isInOrder = await _context.OrderDetails.AnyAsync(o => o.MedicineId == id);
            if (isInOrder)
                return BadRequest(new { message = "لا يمكن حذف هذا الدواء لأنه مرتبط بفاتورة سابقة!" });

            DeleteOldImage(medicine.ImageUrl);
            _context.Medicines.Remove(medicine);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // --- Helper Methods ---
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

        private void DeleteOldImage(string? imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl) || imageUrl.Contains("default.png")) return;

            string filePath = Path.Combine(_environment.WebRootPath, imageUrl.TrimStart('/'));
            if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
        }

        private bool MedicineExists(int id) => _context.Medicines.Any(e => e.Id == id);
    }
}