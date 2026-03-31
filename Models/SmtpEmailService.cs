using System.Net;
using System.Net.Mail;

namespace SOS.Models;

public interface IEmailService
{
    Task SendEmailAsync(string email, string subject, string message, byte[]? attachmentData = null, string? attachmentName = null);
}

public class SmtpEmailService : IEmailService
{
    private IConfiguration _configuration;
    public SmtpEmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    public async Task SendEmailAsync(string email, string subject, string message, byte[]? attachmentData = null, string? attachmentName = null)
    {
        // 1. Validation
        if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
            throw new ArgumentException("Geçersiz e-posta adresi.");

        if (attachmentData != null && attachmentData.Length > 10 * 1024 * 1024) // 10MB Limit
            throw new ArgumentException("Dosya boyutu çok büyük (Max 10MB).");

        using (var client = new SmtpClient(_configuration["Email:Host"]))
        {
            client.UseDefaultCredentials = false;
            client.Credentials = new NetworkCredential(_configuration["Email:Username"], _configuration["Email:Password"]);
            
            client.Port = 587;
            client.EnableSsl = true;
            client.Timeout = 15000; // 15 Seconds Timeout

            using (var mailMessage = new MailMessage())
            {
                mailMessage.From = new MailAddress(_configuration["Email:Username"]!);
                mailMessage.Subject = subject;
                mailMessage.Body = message;
                mailMessage.IsBodyHtml = true;
                mailMessage.To.Add(email);

                // Use a MemoryStream that is disposed when the message is disposed (or explicitly)
                MemoryStream? ms = null;
                if (attachmentData != null && attachmentData.Length > 0 && !string.IsNullOrEmpty(attachmentName))
                {
                    ms = new MemoryStream(attachmentData);
                    mailMessage.Attachments.Add(new Attachment(ms, attachmentName));
                }

                try 
                {
                    await client.SendMailAsync(mailMessage);
                }
                finally
                {
                    // Ensure the stream is closed if it was opened
                    if (ms != null) await ms.DisposeAsync();
                }
            }
        }
    }
}

