namespace FurpaMerkezApi.Application.Modules.StokIslemleri.Virmanlar;

public sealed record VirmanDetailRequest(
    int WarehouseNo,
    string DocumentSerie,
    int DocumentOrderNo);
