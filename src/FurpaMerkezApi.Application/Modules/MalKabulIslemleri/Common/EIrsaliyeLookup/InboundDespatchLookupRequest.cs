namespace FurpaMerkezApi.Application.Modules.MalKabulIslemleri.Common.EIrsaliyeLookup;

public sealed record InboundDespatchLookupRequest(
    int WarehouseNo,
    string ReceivingContext,
    string Ettn);
