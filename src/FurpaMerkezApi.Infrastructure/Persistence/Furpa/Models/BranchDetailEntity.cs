namespace FurpaMerkezApi.Infrastructure.Persistence.Furpa.Models;

public sealed class BranchDetailEntity
{
    public int BranchNo { get; set; }

    public string BranchIpAddress { get; set; } = string.Empty;

    public string PosGenelFolderPath { get; set; } = string.Empty;

    public string PoskonFolderPath { get; set; } = string.Empty;

    public string BranchScalesFolderPath { get; set; } = string.Empty;

    public byte ScalesType { get; set; }
}
