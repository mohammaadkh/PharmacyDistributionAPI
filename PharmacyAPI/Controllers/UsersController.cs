using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Data;
using PharmacyAPI.DTOs;
using PharmacyAPI.Models;
using PharmacyAPI.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using BC = BCrypt.Net.BCrypt;

namespace PharmacyAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        private readonly EmailService _emailService;

        public UsersController(AppDbContext context, IConfiguration config, EmailService emailService)
        {
            _context = context;
            _config = config;
            _emailService = emailService;
        }

        // ───────────────────────────────────────────
        // 1. تسجيل الدخول (المعدل ليشمل فحص التأكيد)
        // ───────────────────────────────────────────
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserLoginDto loginDto)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == loginDto.Email);

            if (user == null)
            {
                BC.Verify("dummy_password", "$2a$11$dummyhashXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX");
                return Unauthorized(new { message = "الإيميل أو كلمة السر خطأ!" });
            }

            if (!BC.Verify(loginDto.Password, user.PasswordHash))
                return Unauthorized(new { message = "الإيميل أو كلمة السر خطأ!" });

            // ⚠️ منع الدخول إذا لم يتم تأكيد الإيميل
            if (!user.IsEmailConfirmed)
            {
                return BadRequest(new { message = "يرجى تأكيد حسابك عبر الكود المرسل إلى إيميلك أولاً!" });
            }

            var accessToken = GenerateAccessToken(user);

            var refreshToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                token = accessToken,
                refreshToken = refreshToken,
                role = user.Role,
                fullName = user.FullName
            });
        }

        // ───────────────────────────────────────────
        // 2. ميثود جديدة: تأكيد الكود (Verify Email)
        // ───────────────────────────────────────────
        [HttpPost("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailDto dto)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == dto.Email && u.EmailVerificationCode == dto.Code);

            if (user == null)
            {
                return BadRequest(new { message = "الكود غير صحيح أو الإيميل خاطئ!" });
            }

            // تفعيل الحساب وتصفير الكود
            user.IsEmailConfirmed = true;
            user.EmailVerificationCode = null;
            await _context.SaveChangesAsync();

            return Ok(new { message = "تم تأكيد الحساب بنجاح، يمكنك الآن تسجيل الدخول." });
        }

        // ───────────────────────────────────────────
        // 3. تسجيل مستخدم جديد (المعدل لإرسال الكود)
        // ───────────────────────────────────────────
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            // 1. فحص إذا كان الإيميل موجوداً مسبقاً (لحماية الداتابيز من التكرار)
            if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
            {
                return BadRequest(new { message = "هذا البريد الإلكتروني مسجل مسبقاً!" });
            }

            // 2. التحقق من نوع الحساب المبعوث
            var allowedTypes = new[] { "Pharmacist", "PharmaceuticalCompany" };
            if (!allowedTypes.Contains(dto.Role, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "نوع الحساب غير صحيح!" });
            }

            // 3. توليد كود تأكيد عشوائي من 6 أرقام
            string verificationCode = new Random().Next(100000, 999999).ToString();

            var user = new User
            {
                FullName = dto.FullName,
                Email = dto.Email,
                PasswordHash = BC.HashPassword(dto.Password),
                PhoneNumber = dto.PhoneNumber,
                OrganizationType = dto.OrganizationType,
                Role = dto.Role.Equals("PharmaceuticalCompany", StringComparison.OrdinalIgnoreCase)
                        ? "PharmaceuticalCompany"
                        : "Pharmacist",

                // الحقول الجديدة اللي ضفناها بالموديل
                IsEmailConfirmed = false,
                EmailVerificationCode = verificationCode
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // 4. إرسال الكود بالإيميل باستخدام الميثود المخصصة اللي عملناها
            try
            {
                // استدعاء الميثود المخصصة للكود من الـ EmailService
                await _emailService.SendVerificationCodeEmail(user.Email, verificationCode);
            }
            catch (Exception ex)
            {
                // في حال فشل الإرسال (مثلاً مشكلة بالسيرفر أو بالإنترنت)
                // نخبر المستخدم إنو الحساب انعمل بس الكود ما وصله
                return Ok(new
                {
                    message = "تم إنشاء الحساب، ولكن فشل إرسال كود التأكيد. يرجى محاولة طلب كود جديد لاحقاً.",
                    debug = ex.Message // فيك تشيل هاد السطر بالإنتاج (Production)
                });
            }

            return Ok(new { message = "تم إنشاء الحساب بنجاح! يرجى إدخال الكود المرسل إلى إيميلك لتفعيل الحساب." });
        }
        [HttpPost("resend-verification-code")]
        public async Task<IActionResult> ResendVerificationCode([FromBody] ResendCodeDto dto)
        {
            // 1. البحث عن المستخدم عن طريق الإيميل
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);

            if (user == null)
            {
                // لأسباب أمنية، منفضل ما نقول "الإيميل مو موجود"
                // بس هون بمرحلة الكود فيك تقله مو موجود لحتى تعرف شو عم يصير
                return NotFound(new { message = "هذا البريد الإلكتروني غير مسجل!" });
            }

            // 2. فحص إذا كان الحساب مأكد أصلاً
            if (user.IsEmailConfirmed)
            {
                return BadRequest(new { message = "هذا الحساب مؤكد بالفعل، يمكنك تسجيل الدخول." });
            }

            // 3. توليد كود جديد (6 أرقام)
            string newVerificationCode = new Random().Next(100000, 999999).ToString();

            // 4. تحديث الكود في قاعدة البيانات
            user.EmailVerificationCode = newVerificationCode;
            await _context.SaveChangesAsync();

            // 5. إرسال الكود الجديد بالإيميل
            try
            {
                await _emailService.SendVerificationCodeEmail(user.Email, newVerificationCode);
                return Ok(new { message = "تم إعادة إرسال كود جديد إلى بريدك الإلكتروني." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "فشل إرسال الإيميل، حاول مرة أخرى لاحقاً.", error = ex.Message });
            }
        }
        // ───────────────────────────────────────────
        // الميثودات الباقية (Refresh, Forgot, Reset, Logout, Delete) تبقى كما هي
        // ───────────────────────────────────────────

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto dto)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.RefreshToken == dto.RefreshToken);

            if (user == null || user.RefreshTokenExpiry < DateTime.UtcNow)
                return Unauthorized(new { message = "انتهت الجلسة، سجل دخول مجدداً" });

            var newAccessToken = GenerateAccessToken(user);

            user.RefreshToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                token = newAccessToken,
                refreshToken = user.RefreshToken
            });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);

            if (user == null)
                return Ok(new { message = "إذا كان الإيميل مسجلاً، ستصلك رسالة خلال دقائق!" });

            user.ResetToken = Guid.NewGuid().ToString();
            user.ResetTokenExpiry = DateTime.UtcNow.AddMinutes(30);
            await _context.SaveChangesAsync();

            await _emailService.SendResetPasswordEmail(user.Email, user.ResetToken);

            return Ok(new { message = "إذا كان الإيميل مسجلاً، ستصلك رسالة خلال دقائق!" });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.ResetToken == dto.Token);

            if (user == null)
                return BadRequest(new { message = "الرابط غير صحيح!" });

            if (user.ResetTokenExpiry < DateTime.UtcNow)
                return BadRequest(new { message = "انتهت صلاحية الرابط، اطلب رابطاً جديداً!" });

            user.PasswordHash = BC.HashPassword(dto.NewPassword);
            user.ResetToken = null;
            user.ResetTokenExpiry = null;
            await _context.SaveChangesAsync();

            return Ok(new { message = "تم تغيير كلمة المرور بنجاح!" });
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _context.Users.FindAsync(userId);

            if (user != null)
            {
                user.RefreshToken = null;
                user.RefreshTokenExpiry = null;
                await _context.SaveChangesAsync();
            }

            return Ok(new { message = "تم تسجيل الخروج بنجاح!" });
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound(new { message = "المستخدم غير موجود!" });

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private string GenerateAccessToken(User user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("UserFullName", user.FullName)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}