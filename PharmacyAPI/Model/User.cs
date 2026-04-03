using PharmacyAPI.Models;

namespace PharmacyAPI.Models
{
    public class User
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;

        // ✅ تعديل: بدل "Customer" صار حسب الواجهة
        public string Role { get; set; } = string.Empty;

        // ✅ مضاف: نوع المنظمة
        public string OrganizationType { get; set; } = string.Empty;

        // ✅ مضاف حديثاً: تفعيل الإيميل (Email Verification)
        // بتبلش False وبس يدخل الكود بتصير True
        public bool IsEmailConfirmed { get; set; } = false;

        // لتخزين الكود المكون من 6 أرقام مثلاً
        public string? EmailVerificationCode { get; set; }

        // Reset Password
        public string? ResetToken { get; set; }
        public DateTime? ResetTokenExpiry { get; set; }

        // ✅ مضاف: Refresh Token
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiry { get; set; }

        // العلاقات - ما تغيرت
        public List<Order> Orders { get; set; } = new();
        public List<CartItem> CartItems { get; set; } = new();
    }
}