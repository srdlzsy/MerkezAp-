namespace FurpaMerkezApi.Infrastructure.Persistence.Furpa.Models;

public sealed class DeviceDetailEntity
{
    public int Id { get; set; }

    public int DeviceTypeId { get; set; }

    public int BranchNo { get; set; }

    public string IpAddress { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}
