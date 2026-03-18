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

        // "Constructor" لجلب قاعدة البيانات للمتحكم
        public MedicinesController(AppDbContext context)
        {
            _context = context;
        }

        // 1. جلب كل الأدوية من قاعدة البيانات
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Medicine>>> GetMedicines()
        {
            return await _context.Medicines.ToListAsync();
        }

        // 2. إضافة دواء جديد للقاعدة
        [HttpPost]
        public async Task<ActionResult<Medicine>> PostMedicine(Medicine medicine)
        {
            _context.Medicines.Add(medicine);
            await _context.SaveChangesAsync();
            return Ok(medicine);
        }
    }
}