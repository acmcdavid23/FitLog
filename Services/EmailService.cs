using System.Net;
using System.Net.Mail;

namespace FitLog.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string htmlBody);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            try
            {
                var host = _config["Email__SmtpHost"] ?? "smtp.gmail.com";
                var port = int.Parse(_config["Email__SmtpPort"] ?? "587");
                var user = _config["Email__SmtpUser"] ?? "";
                var pass = _config["Email__SmtpPassword"] ?? "";
                var fromAddress = _config["Email__FromAddress"] ?? user;
                var fromName = _config["Email__FromName"] ?? "FitLog";

                if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass)) return;

                using var client = new SmtpClient(host, port)
                {
                    Credentials = new NetworkCredential(user, pass),
                    EnableSsl = true
                };

                var message = new MailMessage
                {
                    From = new MailAddress(fromAddress, fromName),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true
                };
                message.To.Add(toEmail);

                await client.SendMailAsync(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Email send failed: {ex.Message}");
            }
        }
    }
}