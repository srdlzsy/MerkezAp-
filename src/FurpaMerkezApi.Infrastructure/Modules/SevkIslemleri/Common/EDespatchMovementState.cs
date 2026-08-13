using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;

namespace FurpaMerkezApi.Infrastructure.Modules.SevkIslemleri.Common;

internal static class EDespatchMovementState
{
    private const string CommonEDespatchDocumentPrefix = "FRM";

    public static bool HasSentEDespatch(STOK_HAREKETLERI movement)
    {
        var documentNo = movement.sth_belge_no?.Trim();
        var uuid = movement.sth_aciklama?.Trim();

        return movement.sth_kilitli == true &&
               !string.IsNullOrWhiteSpace(documentNo) &&
               documentNo.StartsWith(CommonEDespatchDocumentPrefix, StringComparison.OrdinalIgnoreCase) &&
               Guid.TryParse(uuid, out _);
    }
}
