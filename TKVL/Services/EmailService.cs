using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace TKVL.Services 
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string htmlMessage);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlMessage)
        {
            var emailSettings = _config.GetSection("EmailSettings");

            var email = new MimeMessage();
            email.From.Add(new MailboxAddress("JobsNow System", emailSettings["Email"]));
            email.To.Add(MailboxAddress.Parse(toEmail));
            email.Subject = subject;

            // Xây dựng nội dung Email hỗ trợ định dạng HTML cho đẹp
            var builder = new BodyBuilder { HtmlBody = htmlMessage };
            email.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            try
            {
                // Kết nối tới server Gmail
                await smtp.ConnectAsync(emailSettings["Host"], int.Parse(emailSettings["Port"]!), SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync(emailSettings["Email"], emailSettings["Password"]);

                // Bắn mail đi
                await smtp.SendAsync(email);
            }
            finally
            {
                await smtp.DisconnectAsync(true);
            }
        }
    }
}