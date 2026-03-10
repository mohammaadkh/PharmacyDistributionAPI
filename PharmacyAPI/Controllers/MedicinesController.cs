using Microsoft.AspNetCore.Mvc;
using PharmacyAPI.Models; // أضف حرف الـ s هنا لتطابق اسم المجلد والـ Namespace الجديد

namespace PharmacyAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MedicinesController : ControllerBase
    {
        // قائمة تجريبية للأدوية
        // لاحظ أننا أضفنا الحقول الجديدة (Description, ImageUrl, CategoryId) لتطابق الكلاس المطور
        private static List<Medicine> myMedicines = new List<Medicine>
        {
            new Medicine { Id = 1, Name = "Panadol", Price = 15.5m, Quantity = 100, Description = "Pain killer", CategoryId = 1 },
            new Medicine { Id = 2, Name = "Aspirin", Price = 8.0m, Quantity = 50, Description = "Blood thinner", CategoryId = 1 }
        };

        [HttpGet]
        public IActionResult Get()
        {
            return Ok(myMedicines);
        }
    }
}
 