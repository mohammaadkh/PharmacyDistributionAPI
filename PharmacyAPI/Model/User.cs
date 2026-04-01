namespace PharmacyAPI.Models
{
    public class User
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Role { get; set; } = "Customer";

        public string? ResetToken { get; set; }
        public DateTime? ResetTokenExpiry { get; set; }

        public List<Order> Orders { get; set; } = new();
        public List<CartItem> CartItems { get; set; } = new();
    }
}