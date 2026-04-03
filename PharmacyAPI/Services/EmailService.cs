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

        // 1. ميثود عامة لإرسال أي إيميل (لحتى ما نكرر كود الـ Connect و Authenticate)
        public async Task SendEmailAsync(string toEmail, string subject, string htmlMessage)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("PharmaLink", _config["EmailSettings:From"]?.Trim()));
            message.To.Add(MailboxAddress.Parse(toEmail.Trim()));
            message.Subject = subject;

            message.Body = new TextPart("html") { Text = htmlMessage };

            using var client = new SmtpClient();
            try
            {
                await client.ConnectAsync(
                    _config["EmailSettings:Host"],
                    int.Parse(_config["EmailSettings:Port"]!),
                    false);

                await client.AuthenticateAsync(
                    _config["EmailSettings:From"],
                    _config["EmailSettings:Password"]);

                await client.SendAsync(message);
            }
            finally
            {
                await client.DisconnectAsync(true);
            }
        }

        // 2. ميثود مخصصة لإعادة تعيين كلمة المرور (بتستخدم الميثود العامة)
        public async Task SendResetPasswordEmail(string toEmail, string token)
        {
            var subject = "إعادة تعيين كلمة المرور";
            var body = $@"
                <div style='font-family: Arial; padding: 20px; border: 1px solid #eee; border-radius: 10px;'>
                    <h2 style='color: #005EB8;'>PharmaLink</h2>
                    <p>أهلاً بك، اضغط على الزر التالي لإعادة تعيين كلمة المرور الخاصة بك:</p>
                    <div style='text-align: center; margin: 30px 0;'>
                        <a href='http://localhost:3000/reset-password?token={token}'
                           style='background:#005EB8; color:white; padding:12px 25px;
                                  text-decoration:none; border-radius:5px; font-weight: bold;'>
                            إعادة تعيين كلمة المرور
                        </a>
                    </div>
                    <p style='color:red; font-size: 12px;'>ملاحظة: هذا الرابط صالح لمدة 30 مديقة فقط.</p>
                </div>";

            await SendEmailAsync(toEmail, subject, body);
        }

        // 3. ميثود مخصصة لإرسال كود تأكيد الحساب (اللي عم نشتغل عليها هلق)
        public async Task SendVerificationCodeEmail(string toEmail, string code)
        {
            var subject = "كود تأكيد حساب PharmaLink";
            var body = $@"
                <div style='font-family: Arial; padding: 20px; border: 1px solid #eee; border-radius: 10px; direction: rtl;'>
                    <h2 style='color: #28a745;'>PharmaLink</h2>
                    <p>شكراً لتسجيلك معنا! لتفعيل حسابك، يرجى استخدام الكود التالي:</p>
                    <div style='background: #f8f9fa; padding: 20px; text-align: center; border-radius: 5px; margin: 20px 0;'>
                        <span style='font-size: 32px; font-weight: bold; letter-spacing: 5px; color: #333;'>{code}</span>
                    </div>
                    <p>إذا لم تكن أنت من قام بهذا الطلب، يرجى تجاهل هذا الإيميل.</p>
                </div>";

            await SendEmailAsync(toEmail, subject, body);
        }
    }
}