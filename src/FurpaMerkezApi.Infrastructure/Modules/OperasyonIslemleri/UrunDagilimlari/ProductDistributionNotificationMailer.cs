using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.OperasyonIslemleri.UrunDagilimlari;

public interface IProductDistributionNotificationMailer
{
    bool IsEnabled { get; }

    Task SendAsync(ProductDistributionMailRequest request, CancellationToken cancellationToken);
}

public sealed record ProductDistributionMailRequest(
    string To,
    string Subject,
    string HtmlBody);

internal sealed class ProductDistributionNotificationMailer(
    IOptionsMonitor<ProductDistributionMailOptions> options)
    : IProductDistributionNotificationMailer
{
    public bool IsEnabled => options.CurrentValue.Enabled;

    public async Task SendAsync(ProductDistributionMailRequest request, CancellationToken cancellationToken)
    {
        var currentOptions = options.CurrentValue;
        if (!currentOptions.Enabled)
        {
            return;
        }

        var host = RequireValue(currentOptions.Host, "ProductDistributionMail:Host");
        var fromAddress = NormalizeOptionalText(currentOptions.FromAddress)
            ?? RequireValue(currentOptions.Username, "ProductDistributionMail:Username");
        var username = NormalizeOptionalText(currentOptions.Username);

        using var mail = new MailMessage
        {
            From = new MailAddress(fromAddress, NormalizeOptionalText(currentOptions.FromName) ?? fromAddress),
            Subject = request.Subject,
            Body = request.HtmlBody,
            IsBodyHtml = true,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8
        };
        mail.To.Add(new MailAddress(request.To));

        foreach (var cc in currentOptions.Cc.Select(NormalizeOptionalText).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            mail.CC.Add(new MailAddress(cc));
        }

        using var client = new SmtpClient(host, currentOptions.Port > 0 ? currentOptions.Port : 587)
        {
            EnableSsl = currentOptions.EnableSsl,
            UseDefaultCredentials = false,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = Math.Max(1, currentOptions.TimeoutSeconds) * 1000
        };

        if (username is not null)
        {
            var password = RequireValue(currentOptions.Password, "ProductDistributionMail:Password");
            client.Credentials = new NetworkCredential(username, password);
        }

        await client.SendMailAsync(mail, cancellationToken);
    }

    private static string RequireValue(string? value, string settingName) =>
        NormalizeOptionalText(value)
        ?? throw new InvalidOperationException($"{settingName} ayari zorunludur.");

    private static string? NormalizeOptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
