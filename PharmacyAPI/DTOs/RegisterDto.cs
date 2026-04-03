namespace PharmacyAPI.DTOs
{
    public class RegisterDto
    {
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string OrganizationType { get; set; } = string.Empty;

        // ✅ بس لتحديد نوع الحساب (Pharmacist أو PharmaceuticalCompany)
        // السيرفر هو يحدد الـ Role الحقيقي، مش المستخدم
        public string Role { get; set; } = string.Empty;
    }
}