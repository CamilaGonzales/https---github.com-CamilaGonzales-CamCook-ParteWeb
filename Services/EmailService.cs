using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace CamCook.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _s;

    public EmailService(IOptions<EmailSettings> settings)
    {
        _s = settings.Value;
    }

    public async Task SendAsync(string to, string subject, string body)
    {
        // 🔒 Asegurar que el FROM nunca esté vacío
        var from = string.IsNullOrWhiteSpace(_s.FromEmail)
            ? _s.User
            : _s.FromEmail;

        if (string.IsNullOrWhiteSpace(from))
            throw new Exception("Email emisor no configurado (FromEmail/User).");

        var message = new MailMessage();
        message.From = new MailAddress(from, _s.FromName);
        message.To.Add(to);
        message.Subject = subject;
        message.Body = body;
        message.IsBodyHtml = true;

        var smtp = new SmtpClient(_s.Host, _s.Port)
        {
            Credentials = new NetworkCredential(_s.User, _s.Password),
            EnableSsl = _s.EnableSsl
        };

        await smtp.SendMailAsync(message);
    }
}
