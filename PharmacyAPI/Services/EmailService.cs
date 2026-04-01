using MailKit.Net.Smtp;
using MimeKit;

namespace PharmacyAPI.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendResetPasswordEmail(string toEmail, string token)
        {
            var message = new MimeMessage();
            // سطر المرسل (تأكد إن الإيميل بملف الإعدادات ما فيه فراغات)
            message.From.Add(new MailboxAddress("PharmaLink", _config["EmailSettings:From"]?.Trim()));

            // سطر المستقبل (أضفنا Trim ليمسح أي فراغ دخلته أنت بالسواجر)
            message.To.Add(MailboxAddress.Parse(toEmail.Trim()));
            message.Subject = "إعادة تعيين كلمة المرور";

            message.Body = new TextPart("html")
            {
                Text = $@"
                <div style='font-family: Arial; padding: 20px;'>
                    <h2 style='color: #005EB8;'>PharmaLink</h2>
                    <p>اضغط على الرابط التالي لإعادة تعيين كلمة المرور:</p>
                    <a href='http://localhost:3000/reset-password?token={token}'
                       style='background:#005EB8; color:white; padding:10px 20px;
                              text-decoration:none; border-radius:5px;'>
                        إعادة تعيين كلمة المرور
                    </a>
                    <p style='color:red;'>الرابط صالح لمدة 30 دقيقة فقط.</p>
                </div>"
            };

            using var client = new SmtpClient();
            await client.ConnectAsync(
                _config["EmailSettings:Host"],
                int.Parse(_config["EmailSettings:Port"]),
                false);
            await client.AuthenticateAsync(
                _config["EmailSettings:From"],
                _config["EmailSettings:Password"]);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }
}