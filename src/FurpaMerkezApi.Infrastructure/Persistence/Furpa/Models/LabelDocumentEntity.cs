namespace FurpaMerkezApi.Infrastructure.Persistence.Furpa.Models;

public sealed class LabelDocumentEntity
{
    public int Id { get; set; }

    public DateTime CreateDate { get; set; }

    public int BranchNo { get; set; }

    public ICollection<LabelDocumentDetailEntity> Details { get; set; } = new List<LabelDocumentDetailEntity>();
}
