namespace FurpaMerkezApi.Application.Modules.AramaIslemleri.ResolveBarcode;

public sealed record BarcodeResolutionRequest(
    int WarehouseNo,
    string Barcode,
    string? ScreenCode,
    string? OperationType = null,
    int? TargetWarehouseNo = null,
    string? SupplierCode = null,
    bool? IsRefund = null);
