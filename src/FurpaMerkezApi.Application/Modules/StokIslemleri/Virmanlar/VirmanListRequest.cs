namespace FurpaMerkezApi.Application.Modules.StokIslemleri.Virmanlar;

public sealed record VirmanListRequest(
    int WarehouseNo,
    DateTime StartDate,
    DateTime EndDate);
