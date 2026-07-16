namespace FurpaMerkezApi.Application.Modules.MalKabulIslemleri.MalKabulFarklari;

public sealed record WarehouseReceivingDifferenceListRequest(
    int? WarehouseNo,
    DateTime StartDate,
    DateTime EndDate,
    WarehouseReceivingDifferenceScope Scope);
