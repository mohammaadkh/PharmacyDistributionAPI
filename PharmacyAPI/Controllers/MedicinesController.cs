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

        // ───────────────────────────────────────────
        // 1. جلب الأدوية مع البحث والفلترة والـ Pagination
        // GET /api/medicines?search=&categoryId=&page=1&pageSize=20
        // ───────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetMedicines(
            string? search,
            int? categoryId,
            int page = 1,
            int pageSize = 20)
        {
            // ✅ تعديل: حماية من pageSize كبير جداً
            if (pageSize > 100) pageSize = 100;
            if (page < 1) page = 1;

            var query = _context.Medicines
                .Include(m => m.Category)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(m => m.Name.Contains(search)
                                      || m.SKU.Contains(search)
                                      || m.Manufacturer.Contains(search));

            if (categoryId.HasValue)
                query = query.Where(m => m.CategoryId == categoryId.Value);

            // ✅ تعديل: Pagination بدل إرجاع كل الأدوية مرة وحدة
            var total = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new { total, page, pageSize, items });
        }

        // ───────────────────────────────────────────
        // 2. جلب تفاصيل دواء معين
        // GET /api/medicines/{id}
        // ───────────────────────────────────────────
        [HttpGet("{id}")]
        public async Task<ActionResult<Medicine>> GetMedicine(int id)
        {
            var medicine = await _context.Medicines
                .Include(m => m.Category)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);

            if (medicine == null)
                return NotFound(new { message = "عذراً، هذا الدواء غير موجود!" });

            return medicine;
        }

        // ───────────────────────────────────────────
        // 3. جلب التفاصيل العميقة للواجهة
        // GET /api/medicines/{id}/details
        // ───────────────────────────────────────────
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

        // ───────────────────────────────────────────
        // 4. إضافة دواء جديد (Admin فقط)
        // POST /api/medicines
        // ───────────────────────────────────────────
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<Medicine>> PostMedicine([FromForm] MedicineDto dto)
        {
            // ✅ تعديل: استقبال MedicineDto بدل Medicine مباشرة
            if (dto.Price <= 0)
                return BadRequest(new { message = "خطأ: السعر يجب أن يكون قيمة موجبة!" });

            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { message = "خطأ: اسم الدواء مطلوب!" });

            var medicine = new Medicine
            {
                Name = dto.Name,
                Description = dto.Description,
                Price = dto.Price,
                StockQuantity = dto.StockQuantity,
                CategoryId = dto.CategoryId,
                Manufacturer = dto.Manufacturer,
                SKU = dto.SKU,
                NdcNumber = dto.NdcNumber,
                Dosage = dto.Dosage,
                PackSize = dto.PackSize,
                IsFdaApproved = dto.IsFdaApproved,
                IsColdChain = dto.IsColdChain,
                BlackBoxWarning = dto.BlackBoxWarning,
                ClinicalSpecs = dto.ClinicalSpecs
            };

            // ✅ تعديل: SaveImage فيها validation على النوع والحجم
            if (dto.ImageFile != null)
            {
                try { medicine.ImageUrl = await SaveImage(dto.ImageFile); }
                catch (InvalidOperationException ex)
                { return BadRequest(new { message = ex.Message }); }
            }

            _context.Medicines.Add(medicine);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetMedicine), new { id = medicine.Id }, medicine);
        }

        // ───────────────────────────────────────────
        // 5. تعديل دواء (Admin فقط)
        // PUT /api/medicines/{id}
        // ───────────────────────────────────────────
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> PutMedicine(int id, [FromForm] MedicineDto dto)
        {
            var existingMedicine = await _context.Medicines
                .FirstOrDefaultAsync(m => m.Id == id);

            if (existingMedicine == null)
                return NotFound(new { message = "عذراً، الدواء المطلوب تعديله غير موجود!" });

            // ✅ تعديل: بنعدل على الـ existing بدل ما نحط entity جديدة
            existingMedicine.Name = dto.Name;
            existingMedicine.Description = dto.Description;
            existingMedicine.Price = dto.Price;
            existingMedicine.StockQuantity = dto.StockQuantity;
            existingMedicine.CategoryId = dto.CategoryId;
            existingMedicine.Manufacturer = dto.Manufacturer;
            existingMedicine.SKU = dto.SKU;
            existingMedicine.NdcNumber = dto.NdcNumber;
            existingMedicine.Dosage = dto.Dosage;
            existingMedicine.PackSize = dto.PackSize;
            existingMedicine.IsFdaApproved = dto.IsFdaApproved;
            existingMedicine.IsColdChain = dto.IsColdChain;
            existingMedicine.BlackBoxWarning = dto.BlackBoxWarning;
            existingMedicine.ClinicalSpecs = dto.ClinicalSpecs;

            if (dto.ImageFile != null)
            {
                try
                {
                    DeleteOldImage(existingMedicine.ImageUrl);
                    existingMedicine.ImageUrl = await SaveImage(dto.ImageFile);
                }
                catch (InvalidOperationException ex)
                { return BadRequest(new { message = ex.Message }); }
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ───────────────────────────────────────────
        // 6. حذف دواء (Admin فقط)
        // DELETE /api/medicines/{id}
        // ───────────────────────────────────────────
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

        // ───────────────────────────────────────────
        // Helper: حفظ الصورة مع validation
        // ───────────────────────────────────────────
        private async Task<string> SaveImage(IFormFile file)
        {
            // ✅ تعديل: التحقق من نوع الملف
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLower();
            if (!allowedExtensions.Contains(extension))
                throw new InvalidOperationException("نوع الملف غير مسموح! المسموح: jpg, jpeg, png, webp");

            // ✅ تعديل: التحقق من الحجم (5MB)
            if (file.Length > 5 * 1024 * 1024)
                throw new InvalidOperationException("حجم الصورة يتجاوز 5MB!");

            string folderPath = Path.Combine(_environment.WebRootPath, "images");
            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

            string fileName = Guid.NewGuid().ToString() + extension;
            string filePath = Path.Combine(folderPath, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            return "/images/" + fileName;
        }

        // ───────────────────────────────────────────
        // Helper: حذف الصورة القديمة
        // ───────────────────────────────────────────
        private void DeleteOldImage(string? imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl) || imageUrl.Contains("default.png")) return;

            string filePath = Path.Combine(_environment.WebRootPath, imageUrl.TrimStart('/'));
            if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
        }

        private bool MedicineExists(int id) => _context.Medicines.Any(e => e.Id == id);
    }
}