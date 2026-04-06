using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Data;
using System.Security.Claims;

namespace PharmacyAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class GlobalSettingsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public GlobalSettingsController(AppDbContext context)
        {
            _context = context;
        }

        // ───────────────────────────────────────────
        // 1. جلب الإعدادات
        // GET /api/globalsettings
        // ───────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetSettings()
        {
            var userId = GetUserId();

            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return NotFound(new { message = "المستخدم غير موجود!" });

            return Ok(new
            {
                user.Language,
                user.Timezone,
                user.EmailAlertsEnabled,
                user.PushNotificationsEnabled
            });
        }

        // ───────────────────────────────────────────
        // 2. تحديث الإعدادات
        // PUT /api/globalsettings
        // ───────────────────────────────────────────
        [HttpPut]
        public async Task<IActionResult> UpdateSettings([FromBody] GlobalSettingsDto dto)
        {
            var userId = GetUserId();

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound(new { message = "المستخدم غير موجود!" });

            if (!string.IsNullOrEmpty(dto.Language))
                user.Language = dto.Language;

            if (!string.IsNullOrEmpty(dto.Timezone))
                user.Timezone = dto.Timezone;

            if (dto.EmailAlertsEnabled.HasValue)
                user.EmailAlertsEnabled = dto.EmailAlertsEnabled.Value;

            if (dto.PushNotificationsEnabled.HasValue)
                user.PushNotificationsEnabled = dto.PushNotificationsEnabled.Value;

            await _context.SaveChangesAsync();

            return Ok(new { message = "تم تحديث الإعدادات بنجاح!" });
        }

        // ───────────────────────────────────────────
        // Helper
        // ───────────────────────────────────────────
        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                throw new UnauthorizedAccessException();
            return int.Parse(userIdClaim);
        }
    }

    // ───────────────────────────────────────────
    // DTO
    // ───────────────────────────────────────────
    public class GlobalSettingsDto
    {
        public string? Language { get; set; }
        public string? Timezone { get; set; }
        public bool? EmailAlertsEnabled { get; set; }
        public bool? PushNotificationsEnabled { get; set; }
    }
}