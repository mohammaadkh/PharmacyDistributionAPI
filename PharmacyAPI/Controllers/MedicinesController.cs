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

        // GET: api/Medicines
        // أضفت معاملات (search) و (categoryId) لخدمة الواجهة
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Medicine>>> GetMedicines(string? search, int? categoryId)
        {
            var query = _context.Medicines.Include(m => m.Category).AsQueryable();

            // إذا رفيقك بعت كلمة بحث
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(m => m.Name.Contains(search));
            }

            // إذا رفيقك ضغط على صنف معين
            if (categoryId.HasValue)
            {
                query = query.Where(m => m.CategoryId == categoryId.Value);
            }

            return await query.ToListAsync();
        }

        // الباقي بضل متل ما هو (GetByID, Post, Put, Delete)
        [HttpGet("{id}")]
        public async Task<ActionResult<Medicine>> GetMedicine(int id)
        {
            // استخدمت Include هون كمان عشان تفاصيل الدواء تطلع مع اسم الصنف
            var medicine = await _context.Medicines.Include(m => m.Category).FirstOrDefaultAsync(m => m.Id == id);

            if (medicine == null) return NotFound();
            return medicine;
        }

        [HttpPost]
        public async Task<ActionResult<Medicine>> PostMedicine(Medicine medicine)
        {
            _context.Medicines.Add(medicine);
            await _context.SaveChangesAsync();
            return CreatedAtAction("GetMedicine", new { id = medicine.Id }, medicine);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutMedicine(int id, Medicine medicine)
        {
            if (id != medicine.Id) return BadRequest();
            _context.Entry(medicine).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMedicine(int id)
        {
            var medicine = await _context.Medicines.FindAsync(id);
            if (medicine == null) return NotFound();
            _context.Medicines.Remove(medicine);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}