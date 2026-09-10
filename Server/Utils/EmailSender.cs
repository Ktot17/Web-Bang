using System.Net;
using System.Net.Mail;

namespace Server.Utils;

public interface IEmailSender
{
    Task Send(string email, string subject, string message);
}

public class EmailSender(IConfiguration config) : IEmailSender
{
    public async Task Send(string email, string subject, string message)
    {
        using var client = new SmtpClient();
        client.Host = config["SMTP:Host"]!;
        client.Port = int.Parse(config["SMTP:Port"]!);
        client.UseDefaultCredentials = config.GetValue<bool>("SMTP:UseDefaultCredentials");
        client.EnableSsl = config.GetValue<bool>("SMTP:EnableSsl");
        client.DeliveryMethod = SmtpDeliveryMethod.Network;
        client.Credentials = client.UseDefaultCredentials ? null :
            new NetworkCredential(config["SMTP:User"]!, config["SMTP:Password"]!);

        var mail = new MailMessage(config["Smtp:Email"]!, email, subject, message)
        { IsBodyHtml = false };

        await client.SendMailAsync(mail).ConfigureAwait(false);
        mail.Dispose();
    }
}

public static class TwoFactorService
{
    private const int MinCode = 100000;
    private const int MaxCode = 999999;

    public static string GenerateCode()
    {
        return Random.Shared.Next(MinCode, MaxCode).ToString();
    }
}
