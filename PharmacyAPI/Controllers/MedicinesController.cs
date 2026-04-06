using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Data;
using PharmacyAPI.Models;
using PharmacyAPI.Models.DTOs;
using System.Text;

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
        // GET /api/medicines?search=&categoryId=&manufacturer=&
        //     isFdaApproved=&inStockOnly=&minPrice=&maxPrice=&
        //     sortBy=price&page=1&pageSize=20
        // ───────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetMedicines(
            string? search = null,
            int? categoryId = null,
            string? manufacturer = null,
            bool? isFdaApproved = null,
            bool? inStockOnly = null,
            decimal? minPrice = null,
            decimal? maxPrice = null,
            string? sortBy = null,
            int page = 1,
            int pageSize = 20)
        {
            if (pageSize > 100) pageSize = 100;
            if (page < 1) page = 1;

            var query = _context.Medicines
                .Include(m => m.Category)
                .AsNoTracking()
                .AsQueryable();

            // ✅ فلتر البحث
            if (!string.IsNullOrEmpty(search))
                query = query.Where(m =>
                    m.Name.Contains(search) ||
                    m.SKU.Contains(search) ||
                    m.Manufacturer.Contains(search));

            // ✅ فلتر الكاتيغوري
            if (categoryId.HasValue)
                query = query.Where(m => m.CategoryId == categoryId.Value);

            // ✅ فلتر المصنع
            if (!string.IsNullOrEmpty(manufacturer))
                query = query.Where(m => m.Manufacturer.Contains(manufacturer));

            // ✅ فلتر FDA Approved
            if (isFdaApproved.HasValue)
                query = query.Where(m => m.IsFdaApproved == isFdaApproved.Value);

            // ✅ فلتر المتوفر بالمخزون فقط
            if (inStockOnly == true)
                query = query.Where(m => m.StockQuantity > 0);

            // ✅ فلتر السعر
            if (minPrice.HasValue)
                query = query.Where(m => m.Price >= minPrice.Value);

            if (maxPrice.HasValue)
                query = query.Where(m => m.Price <= maxPrice.Value);

            // ✅ الترتيب
            query = sortBy switch
            {
                "price_asc" => query.OrderBy(m => m.Price),
                "price_desc" => query.OrderByDescending(m => m.Price),
                "name" => query.OrderBy(m => m.Name),
                "stock" => query.OrderByDescending(m => m.StockQuantity),
                _ => query.OrderBy(m => m.Name)
            };

            var total = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new
                {
                    m.Id,
                    m.Name,
                    m.Description,
                    m.Price,
                    m.StockQuantity,
                    m.SKU,
                    m.Dosage,
                    m.Manufacturer,
                    m.PackSize,
                    m.ImageUrl,
                    m.IsFdaApproved,
                    m.IsGmpCertified,
                    m.IsColdChain,
                    CategoryName = m.Category != null ? m.Category.Name : "General",
                    InStock = m.StockQuantity > 0,
                    IsLowStock = m.StockQuantity < 50 && m.StockQuantity > 0
                })
                .ToListAsync();

            return Ok(new { total, page, pageSize, items });
        }

        // ───────────────────────────────────────────
        // 2. جلب تفاصيل دواء معين
        // GET /api/medicines/{id}
        // ───────────────────────────────────────────
        [HttpGet("{id}")]
        public async Task<IActionResult> GetMedicine(int id)
        {
            var medicine = await _context.Medicines
                .Include(m => m.Category)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);

            if (medicine == null)
                return NotFound(new { message = "عذراً، هذا الدواء غير موجود!" });

            return Ok(medicine);
        }

        // ───────────────────────────────────────────
        // 3. جلب التفاصيل العميقة للواجهة
        // GET /api/medicines/{id}/details
        // ───────────────────────────────────────────
        [HttpGet("{id}/details")]
        [AllowAnonymous]
        public async Task<IActionResult> GetProductDetails(int id)
        {
            if (id <= 0)
                return BadRequest(new { message = "معرف المنتج غير صالح!" });

            var medicine = await _context.Medicines
                .AsNoTracking()
                .Include(m => m.Category)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (medicine == null)
                return NotFound(new { message = "هذا المنتج غير متوفر حالياً!" });

            var details = new ProductDetailsDto
            {
                Id = medicine.Id,
                Name = medicine.Name,
                Description = medicine.Description ?? "لا يوجد وصف طبي متوفر.",
                NdcNumber = medicine.NdcNumber ?? "غير متوفر",
                ManufacturerName = medicine.Manufacturer,
                CategoryName = medicine.Category?.Name ?? "General",
                StorageCondition = medicine.IsColdChain ? "2°C - 8°C" : "20°C - 25°C",
                Price = medicine.Price,
                StockQuantity = medicine.StockQuantity,
                ImageUrl = medicine.ImageUrl,
                IsFdaApproved = medicine.IsFdaApproved,
                IsColdChain = medicine.IsColdChain,
                BlackBoxWarning = medicine.BlackBoxWarning ?? "لا يوجد تحذير خاص",
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
        public async Task<IActionResult> PostMedicine([FromForm] MedicineDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { message = "اسم الدواء مطلوب!" });

            if (dto.Price <= 0)
                return BadRequest(new { message = "السعر يجب أن يكون قيمة موجبة!" });

            // التحقق من عدم تكرار الـ SKU
            var skuExists = await _context.Medicines
                .AnyAsync(m => m.SKU == dto.SKU);
            if (skuExists)
                return BadRequest(new { message = "هذا الـ SKU مستخدم مسبقاً!" });

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

            if (dto.ImageFile != null)
            {
                try { medicine.ImageUrl = await SaveImage(dto.ImageFile); }
                catch (InvalidOperationException ex)
                { return BadRequest(new { message = ex.Message }); }
            }

            _context.Medicines.Add(medicine);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetMedicine), new { id = medicine.Id }, new
            {
                message = "تم إضافة الدواء بنجاح!",
                medicine.Id,
                medicine.Name,
                medicine.SKU
            });
        }

        // ───────────────────────────────────────────
        // 5. تعديل دواء (Admin فقط)
        // PUT /api/medicines/{id}
        // ───────────────────────────────────────────
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> PutMedicine(int id, [FromForm] MedicineDto dto)
        {
            var medicine = await _context.Medicines.FindAsync(id);
            if (medicine == null)
                return NotFound(new { message = "الدواء غير موجود!" });

            // التحقق من عدم تكرار الـ SKU مع دواء آخر
            var skuExists = await _context.Medicines
                .AnyAsync(m => m.SKU == dto.SKU && m.Id != id);
            if (skuExists)
                return BadRequest(new { message = "هذا الـ SKU مستخدم من دواء آخر!" });

            medicine.Name = dto.Name;
            medicine.Description = dto.Description;
            medicine.Price = dto.Price;
            medicine.StockQuantity = dto.StockQuantity;
            medicine.CategoryId = dto.CategoryId;
            medicine.Manufacturer = dto.Manufacturer;
            medicine.SKU = dto.SKU;
            medicine.NdcNumber = dto.NdcNumber;
            medicine.Dosage = dto.Dosage;
            medicine.PackSize = dto.PackSize;
            medicine.IsFdaApproved = dto.IsFdaApproved;
            medicine.IsColdChain = dto.IsColdChain;
            medicine.BlackBoxWarning = dto.BlackBoxWarning;
            medicine.ClinicalSpecs = dto.ClinicalSpecs;

            if (dto.ImageFile != null)
            {
                try
                {
                    DeleteOldImage(medicine.ImageUrl);
                    medicine.ImageUrl = await SaveImage(dto.ImageFile);
                }
                catch (InvalidOperationException ex)
                { return BadRequest(new { message = ex.Message }); }
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "تم تعديل الدواء بنجاح!" });
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
                return NotFound(new { message = "الدواء غير موجود!" });

            var isInCart = await _context.CartItems.AnyAsync(c => c.MedicineId == id);
            if (isInCart)
                return BadRequest(new { message = "لا يمكن حذف هذا الدواء لأنه موجود في سلة مستخدم!" });

            var isInOrder = await _context.OrderDetails.AnyAsync(o => o.MedicineId == id);
            if (isInOrder)
                return BadRequest(new { message = "لا يمكن حذف هذا الدواء لأنه مرتبط بفاتورة سابقة!" });

            DeleteOldImage(medicine.ImageUrl);
            _context.Medicines.Remove(medicine);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ───────────────────────────────────────────
        // 7. Export CSV (Admin فقط)
        // GET /api/medicines/export
        // ───────────────────────────────────────────
        [HttpGet("export")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ExportMedicines()
        {
            var medicines = await _context.Medicines
                .AsNoTracking()
                .Include(m => m.Category)
                .OrderBy(m => m.Name)
                .ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("Name,SKU,Category,Price,Stock,FDA Approved,Cold Chain,Manufacturer");

            foreach (var m in medicines)
            {
                csv.AppendLine(
                    $"{m.Name}," +
                    $"{m.SKU}," +
                    $"{m.Category?.Name ?? "General"}," +
                    $"{m.Price}," +
                    $"{m.StockQuantity}," +
                    $"{m.IsFdaApproved}," +
                    $"{m.IsColdChain}," +
                    $"{m.Manufacturer}"
                );
            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"medicines_{DateTime.UtcNow:yyyyMMdd}.csv");
        }

        // ───────────────────────────────────────────
        // 8. جلب المصنعين للفلتر
        // GET /api/medicines/manufacturers
        // ───────────────────────────────────────────
        [HttpGet("manufacturers")]
        public async Task<IActionResult> GetManufacturers()
        {
            var manufacturers = await _context.Medicines
                .AsNoTracking()
                .Select(m => m.Manufacturer)
                .Distinct()
                .OrderBy(m => m)
                .ToListAsync();

            return Ok(manufacturers);
        }

        // ───────────────────────────────────────────
        // Helpers
        // ───────────────────────────────────────────
        private async Task<string> SaveImage(IFormFile file)
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLower();

            if (!allowedExtensions.Contains(extension))
                throw new InvalidOperationException("نوع الملف غير مسموح! المسموح: jpg, jpeg, png, webp");

            if (file.Length > 5 * 1024 * 1024)
                throw new InvalidOperationException("حجم الصورة يتجاوز 5MB!");

            string folderPath = Path.Combine(_environment.WebRootPath, "images");
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            string fileName = Guid.NewGuid().ToString() + extension;
            string filePath = Path.Combine(folderPath, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            return "/images/" + fileName;
        }

        private void DeleteOldImage(string? imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl) || imageUrl.Contains("default.png"))
                return;

            string filePath = Path.Combine(_environment.WebRootPath, imageUrl.TrimStart('/'));
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);
        }
    }
}