using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PharmacyAPI.Data;
using PharmacyAPI.Models;
using PharmacyAPI.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
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

        // --- 1. تسجيل الدخول ---
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserLoginDto loginDto)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == loginDto.Email);

            if (user == null || !BC.Verify(loginDto.Password, user.PasswordHash))
                return Unauthorized(new { message = "الإيميل أو كلمة السر خطأ!" });

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("UserFullName", user.FullName)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddHours(3),
                signingCredentials: creds
            );

            return Ok(new
            {
                token = new JwtSecurityTokenHandler().WriteToken(token),
                role = user.Role,
                fullName = user.FullName
            });
        }

        // --- 2. نسيان كلمة المرور ---
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null) return NotFound(new { message = "هذا الإيميل غير مسجل!" });

            user.ResetToken = Guid.NewGuid().ToString();
            user.ResetTokenExpiry = DateTime.Now.AddMinutes(30);
            await _context.SaveChangesAsync();

            await _emailService.SendResetPasswordEmail(user.Email, user.ResetToken);

            return Ok(new { message = "تم إرسال رابط إعادة التعيين على بريدك الإلكتروني!" });
        }

        // --- 3. إعادة تعيين كلمة المرور ---
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.ResetToken == dto.Token);

            if (user == null)
                return BadRequest(new { message = "الرابط غير صحيح!" });

            if (user.ResetTokenExpiry < DateTime.Now)
                return BadRequest(new { message = "انتهت صلاحية الرابط، اطلب رابطاً جديداً!" });

            user.PasswordHash = BC.HashPassword(dto.NewPassword);
            user.ResetToken = null;
            user.ResetTokenExpiry = null;
            await _context.SaveChangesAsync();

            return Ok(new { message = "تم تغيير كلمة المرور بنجاح!" });
        }

        // --- 4. تسجيل مستخدم جديد ---
        [HttpPost("register")]
        public async Task<ActionResult<User>> Register([FromBody] RegisterDto dto)
        {
            if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
                return BadRequest(new { message = "هذا الإيميل مسجل مسبقاً!" });

            var user = new User
            {
                FullName = dto.FullName,
                Email = dto.Email,
                PasswordHash = BC.HashPassword(dto.Password),
                PhoneNumber = dto.PhoneNumber,
                Role = "Customer"
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return Ok(new { message = "تم التسجيل بنجاح!" });
        }

        // --- 5. حذف مستخدم (Admin فقط) ---
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
    }

    // --- DTOs ---
    public class UserLoginDto { public string Email { get; set; } = string.Empty; public string Password { get; set; } = string.Empty; }
    public class ForgotPasswordDto { public string Email { get; set; } = string.Empty; }
    public class ResetPasswordDto { public string Token { get; set; } = string.Empty; public string NewPassword { get; set; } = string.Empty; }
    public class RegisterDto
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
    }
}