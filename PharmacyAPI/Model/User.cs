using PharmacyAPI.Model;
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
        public string Role { get; set; } = string.Empty;
        public string OrganizationType { get; set; } = string.Empty;

        // Email Verification
        public bool IsEmailConfirmed { get; set; } = false;
        public string? EmailVerificationCode { get; set; }

        // Reset Password
        public string? ResetToken { get; set; }
        public DateTime? ResetTokenExpiry { get; set; }

        // Refresh Token
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiry { get; set; }

        // Settings & Identity
        public string? NpiNumber { get; set; }
        public string? DeaRegistration { get; set; }

        // Global Settings
        public string Language { get; set; } = "English (United States)";
        public string Timezone { get; set; } = "(GMT-05:00) Eastern Time";
        public bool EmailAlertsEnabled { get; set; } = true;
        public bool PushNotificationsEnabled { get; set; } = true;

        // Avatar
        public string? ImageUrl { get; set; } = "/avatars/default.png";

        // العلاقات
        public List<Order> Orders { get; set; } = new();
        public List<CartItem> CartItems { get; set; } = new();
        public List<Notification> Notifications { get; set; } = new();
    }
}