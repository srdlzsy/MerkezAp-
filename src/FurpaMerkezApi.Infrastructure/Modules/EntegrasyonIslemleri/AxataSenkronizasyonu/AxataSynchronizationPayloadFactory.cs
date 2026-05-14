using System.Text.Json;
using FurpaMerkezApi.Application.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Application.Modules.StokIslemleri.SayimSonuclari;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

internal static class AxataSynchronizationPayloadFactory
{
    public static object BuildWarehouseOrderDocument(WarehouseOrderDetailDto detail) => new
    {
        header = detail.Header,
        lines = detail.Items
    };

    public static object BuildCompanyReceivingDocument(CompanyMovementDetailDto detail) => new
    {
        header = detail.Header,
        lines = detail.Items
    };

    public static object BuildInventoryCountDocument(InventoryCountDetailDto detail) => new
    {
        header = detail.Header,
        lines = detail.Items
    };

    public static string Serialize(object payload) =>
        JsonSerializer.Serialize(payload, AxataSynchronizationJson.Options);
}
