namespace FurpaMerkezApi.Infrastructure.Modules.OperasyonIslemleri.UrunDagilimlari;

public sealed class ProductDistributionMailOptions
{
    public const string SectionName = "ProductDistributionMail";

    public bool Enabled { get; set; }

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public bool EnableSsl { get; set; } = true;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "Dedektif";

    public string[] Cc { get; set; } = [];

    public int TimeoutSeconds { get; set; } = 30;
}
