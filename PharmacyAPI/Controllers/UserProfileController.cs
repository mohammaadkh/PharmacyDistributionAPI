using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Data;
using PharmacyAPI.Models;
using System.Security.Claims;
using BC = BCrypt.Net.BCrypt;

namespace PharmacyAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ProfileController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public ProfileController(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // ───────────────────────────────────────────
        // 1. جلب بيانات البروفايل
        // GET /api/profile
        // ───────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetProfile()
        {
            var userId = GetUserId();

            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                return NotFound(new { message = "المستخدم غير موجود!" });

            return Ok(new
            {
                user.Id,
                user.FullName,
                user.Email,
                user.PhoneNumber,
                user.Role,
                user.OrganizationType,
                user.IsEmailConfirmed
            });
        }

        // ───────────────────────────────────────────
        // 2. تعديل البيانات الأساسية
        // PUT /api/profile
        // ───────────────────────────────────────────
        [HttpPut]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
        {
            var userId = GetUserId();

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound(new { message = "المستخدم غير موجود!" });

            // التحقق إذا الإيميل الجديد مستخدم من حدا ثاني
            if (!string.IsNullOrEmpty(dto.Email) && dto.Email != user.Email)
            {
                var emailExists = await _context.Users
                    .AnyAsync(u => u.Email == dto.Email && u.Id != userId);
                if (emailExists)
                    return BadRequest(new { message = "هذا الإيميل مستخدم من حساب آخر!" });

                user.Email = dto.Email;
            }

            if (!string.IsNullOrEmpty(dto.FullName))
                user.FullName = dto.FullName;

            if (!string.IsNullOrEmpty(dto.PhoneNumber))
                user.PhoneNumber = dto.PhoneNumber;

            if (!string.IsNullOrEmpty(dto.OrganizationType))
                user.OrganizationType = dto.OrganizationType;

            await _context.SaveChangesAsync();

            return Ok(new { message = "تم تحديث البيانات بنجاح!" });
        }

        // ───────────────────────────────────────────
        // 3. تغيير كلمة المرور
        // PUT /api/profile/change-password
        // ───────────────────────────────────────────
        [HttpPut("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            var userId = GetUserId();

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound(new { message = "المستخدم غير موجود!" });

            // التحقق من كلمة المرور القديمة
            if (!BC.Verify(dto.OldPassword, user.PasswordHash))
                return BadRequest(new { message = "كلمة المرور القديمة غير صحيحة!" });

            // التحقق إن الجديدة مختلفة عن القديمة
            if (BC.Verify(dto.NewPassword, user.PasswordHash))
                return BadRequest(new { message = "كلمة المرور الجديدة يجب أن تكون مختلفة عن القديمة!" });

            user.PasswordHash = BC.HashPassword(dto.NewPassword);

            // إلغاء كل الجلسات الأخرى بعد تغيير كلمة المرور
            user.RefreshToken = null;
            user.RefreshTokenExpiry = null;

            await _context.SaveChangesAsync();

            return Ok(new { message = "تم تغيير كلمة المرور بنجاح! يرجى تسجيل الدخول مجدداً." });
        }

        // ───────────────────────────────────────────
        // 4. رفع صورة البروفايل
        // POST /api/profile/avatar
        // ───────────────────────────────────────────
        [HttpPost("avatar")]
        public async Task<IActionResult> UploadAvatar(IFormFile imageFile)
        {
            var userId = GetUserId();

            if (imageFile == null || imageFile.Length == 0)
                return BadRequest(new { message = "يرجى اختيار صورة!" });

            // التحقق من نوع الصورة
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(imageFile.FileName).ToLower();
            if (!allowedExtensions.Contains(extension))
                return BadRequest(new { message = "نوع الصورة غير مسموح! المسموح: jpg, jpeg, png, webp" });

            // التحقق من الحجم (3MB للأفاتار)
            if (imageFile.Length > 3 * 1024 * 1024)
                return BadRequest(new { message = "حجم الصورة يتجاوز 3MB!" });

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return NotFound(new { message = "المستخدم غير موجود!" });

            // حذف الصورة القديمة
            DeleteOldAvatar(user.ImageUrl);

            // حفظ الصورة الجديدة
            string folderPath = Path.Combine(_environment.WebRootPath, "avatars");
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            string fileName = $"avatar_{userId}_{Guid.NewGuid()}{extension}";
            string filePath = Path.Combine(folderPath, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await imageFile.CopyToAsync(stream);

            user.ImageUrl = "/avatars/" + fileName;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "تم رفع الصورة بنجاح!",
                imageUrl = user.ImageUrl
            });
        }

        // ───────────────────────────────────────────
        // Helpers
        // ───────────────────────────────────────────
        private int GetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                throw new UnauthorizedAccessException();
            return int.Parse(userIdClaim);
        }

        private void DeleteOldAvatar(string? imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl) || imageUrl.Contains("default"))
                return;

            string filePath = Path.Combine(_environment.WebRootPath, imageUrl.TrimStart('/'));
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);
        }
    }

    // ───────────────────────────────────────────
    // DTOs
    // ───────────────────────────────────────────
    public class UpdateProfileDto
    {
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? OrganizationType { get; set; }
    }

    public class ChangePasswordDto
    {
        public string OldPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}