namespace FurpaMerkezApi.Infrastructure.Persistence.Furpa.Models;

public sealed class LabelDocumentDetailEntity
{
    public int DetailId { get; set; }

    public int DocumentId { get; set; }

    public string ProductCode { get; set; } = string.Empty;

    public LabelDocumentEntity? Document { get; set; }
}
