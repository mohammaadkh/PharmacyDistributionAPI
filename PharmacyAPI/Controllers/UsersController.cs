using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PharmacyAPI.Data;
using PharmacyAPI.DTOs;
using PharmacyAPI.Models;
using PharmacyAPI.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
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

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserLoginDto loginDto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == loginDto.Email);

            if (user == null)
            {
                BC.Verify("dummy_password", "$2a$11$dummyhashXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX");
                return Unauthorized(new { message = "الإيميل أو كلمة السر خطأ!" });
            }

            if (!BC.Verify(loginDto.Password, user.PasswordHash))
                return Unauthorized(new { message = "الإيميل أو كلمة السر خطأ!" });

            if (!user.IsEmailConfirmed)
                return BadRequest(new { message = "يرجى تأكيد حسابك عبر الكود المرسل إلى إيميلك أولاً!" });

            var accessToken = GenerateAccessToken(user);
            var refreshToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                token = accessToken,
                refreshToken,
                role = user.Role,
                fullName = user.FullName
            });
        }

        [HttpPost("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailDto dto)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == dto.Email && u.EmailVerificationCode == dto.Code);

            if (user == null)
                return BadRequest(new { message = "الكود غير صحيح أو الإيميل خاطئ!" });

            user.IsEmailConfirmed = true;
            user.EmailVerificationCode = null;
            await _context.SaveChangesAsync();

            return Ok(new { message = "تم تأكيد الحساب بنجاح، يمكنك الآن تسجيل الدخول." });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
                return BadRequest(new { message = "هذا البريد الإلكتروني مسجل مسبقاً!" });

            var allowedTypes = new[] { "Pharmacist", "PharmaceuticalCompany" };
            if (!allowedTypes.Contains(dto.Role, StringComparer.OrdinalIgnoreCase))
                return BadRequest(new { message = "نوع الحساب غير صحيح!" });

            string verificationCode = RandomNumberGenerator.GetInt32(100000, 999999).ToString();

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
                IsEmailConfirmed = false,
                EmailVerificationCode = verificationCode
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            try
            {
                await _emailService.SendVerificationCodeEmail(user.Email, verificationCode);
            }
            catch
            {
                return Ok(new { message = "تم إنشاء الحساب، ولكن فشل إرسال كود التأكيد. يرجى طلب كود جديد." });
            }

            return Ok(new { message = "تم إنشاء الحساب بنجاح! يرجى إدخال الكود المرسل لإيميلك." });
        }

        [HttpPost("resend-verification-code")]
        public async Task<IActionResult> ResendVerificationCode([FromBody] ResendCodeDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);

            if (user == null)
                return NotFound(new { message = "هذا البريد الإلكتروني غير مسجل!" });

            if (user.IsEmailConfirmed)
                return BadRequest(new { message = "الحساب مؤكد بالفعل." });

            string newCode = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
            user.EmailVerificationCode = newCode;
            await _context.SaveChangesAsync();

            try
            {
                await _emailService.SendVerificationCodeEmail(user.Email, newCode);
                return Ok(new { message = "تم إرسال كود جديد." });
            }
            catch
            {
                return StatusCode(500, new { message = "فشل إرسال الإيميل." });
            }
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.RefreshToken == dto.RefreshToken);

            if (user == null || user.RefreshTokenExpiry < DateTime.UtcNow)
                return Unauthorized(new { message = "انتهت الجلسة، سجل دخول مجدداً" });

            var newAccessToken = GenerateAccessToken(user);
            user.RefreshToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
            await _context.SaveChangesAsync();

            return Ok(new { token = newAccessToken, refreshToken = user.RefreshToken });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null) return Ok(new { message = "إذا كان الإيميل مسجلاً، ستصلك رسالة!" });

            user.ResetToken = Guid.NewGuid().ToString();
            user.ResetTokenExpiry = DateTime.UtcNow.AddMinutes(30);
            await _context.SaveChangesAsync();

            await _emailService.SendResetPasswordEmail(user.Email, user.ResetToken);
            return Ok(new { message = "إذا كان الإيميل مسجلاً، ستصلك رسالة!" });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.ResetToken == dto.Token);

            if (user == null || user.ResetTokenExpiry < DateTime.UtcNow)
                return BadRequest(new { message = "الرابط غير صحيح أو انتهت صلاحيته!" });

            user.PasswordHash = BC.HashPassword(dto.NewPassword);

            // ✅ تفعيل الحساب تلقائياً عند تغيير كلمة السر بنجاح
            user.IsEmailConfirmed = true;
            user.EmailVerificationCode = null;

            user.ResetToken = null;
            user.ResetTokenExpiry = null;
            await _context.SaveChangesAsync();

            return Ok(new { message = "تم تغيير كلمة المرور وتفعيل الحساب بنجاح!" });
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdStr != null)
            {
                var user = await _context.Users.FindAsync(int.Parse(userIdStr));
                if (user != null)
                {
                    user.RefreshToken = null;
                    user.RefreshTokenExpiry = null;
                    await _context.SaveChangesAsync();
                }
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