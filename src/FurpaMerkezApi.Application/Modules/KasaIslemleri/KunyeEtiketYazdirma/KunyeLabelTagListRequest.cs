namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.KunyeEtiketYazdirma;

public sealed record KunyeLabelTagListRequest(
    int WarehouseNo,
    DateTime? DateToGet);
