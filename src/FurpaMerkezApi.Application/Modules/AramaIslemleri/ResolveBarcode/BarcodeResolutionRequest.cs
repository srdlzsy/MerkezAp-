namespace FurpaMerkezApi.Application.Modules.AramaIslemleri.ResolveBarcode;

public sealed record BarcodeResolutionRequest(
    int WarehouseNo,
    string Barcode,
    string? ScreenCode);
