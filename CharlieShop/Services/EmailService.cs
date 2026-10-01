using CharlieShop.Models;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace CharlieShop.Services
{
    public class EmailService
    {
        private readonly EmailSettings _settings;

        public EmailService(IOptions<EmailSettings> options)
        {
            _settings = options.Value;
        }

        public async Task SendAsync(
            string destination,
            string subject,
            string body)
        {
            using var message = new MailMessage();

            message.From = new MailAddress(
                _settings.From,
                "Charlie Shop");

            message.To.Add(destination);

            message.Subject = subject;
            message.Body = body;
            message.IsBodyHtml = true;

            using var smtp = new SmtpClient(
                _settings.Host,
                _settings.Port);

            smtp.EnableSsl = true;

            smtp.Credentials = new NetworkCredential(
                _settings.Username,
                _settings.Password);

            await smtp.SendMailAsync(message);
        }
    }
}