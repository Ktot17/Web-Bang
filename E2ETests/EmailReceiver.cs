using System.Text.Json;
using MailKit.Net.Imap;
using MailKit.Security;

namespace E2ETests;

public interface IEmailReceiver
{
    public Task<string> LastEmailBody();
}

public class MailHogReceiver(Uri host) : IEmailReceiver
{
    private readonly HttpClient _client = new()
    {
        BaseAddress = host
    };

    public async Task<string> LastEmailBody()
    {
        var response = await _client.GetStringAsync("api/v2/messages");
        using var messagesJson = JsonDocument.Parse(response);
        var code = messagesJson.RootElement.GetProperty("items")[0]
            .GetProperty("Content").GetProperty("Body").GetString()!.TrimEnd();
        var messageId = messagesJson.RootElement.GetProperty("items")[0]
            .GetProperty("ID").GetString()!;
        await _client.DeleteAsync($"/api/v1/messages/{messageId}");
        return code;
    }
}

public class ImitateEmailReceiver(string username, string password) : IEmailReceiver
{
    private readonly ImapClient _client = new();

    public async Task<string> LastEmailBody()
    {
        await _client.ConnectAsync(
            "imap.imitate.email",
            993,
            SecureSocketOptions.SslOnConnect
        );

        await _client.AuthenticateAsync(username, password);

        var inbox = _client.Inbox;
        await inbox.OpenAsync(MailKit.FolderAccess.ReadOnly);

        var message = await inbox.GetMessageAsync(inbox.Count - 1);
        return message.TextBody.TrimEnd();
    }
}
