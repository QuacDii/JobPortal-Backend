using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

namespace TKVL.Services
{
    // 1. Chuẩn hóa giao diện về duy nhất 1 hàm 3 tham số
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string noiDungHtml);
    }

    // 2. Đồng bộ lớp triển khai khớp chính xác với giao diện
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string noiDungHtml)
        {
            var emailSettings = _config.GetSection("EmailSettings");
            var email = new MimeMessage();
            email.From.Add(new MailboxAddress("JobsNow System", emailSettings["Email"]));
            email.To.Add(MailboxAddress.Parse(toEmail));
            email.Subject = subject;

            // Xây dựng nội dung thư từ tham số truyền vào
            var builder = new BodyBuilder { HtmlBody = noiDungHtml };
            email.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            try
            {
                // Kết nối tới server Gmail theo cấu hình appsettings
                await smtp.ConnectAsync(emailSettings["Host"], int.Parse(emailSettings["Port"]!), SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync(emailSettings["Email"], emailSettings["Password"]);

                // Thực hiện gửi thư đi
                await smtp.SendAsync(email);
            }
            finally
            {
                await smtp.DisconnectAsync(true);
            }
        }
    }
}