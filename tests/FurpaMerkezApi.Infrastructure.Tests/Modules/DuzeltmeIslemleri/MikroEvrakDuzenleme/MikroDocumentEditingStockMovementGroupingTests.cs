using FurpaMerkezApi.Infrastructure.Modules.DuzeltmeIslemleri.MikroEvrakDuzenleme;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.DuzeltmeIslemleri.MikroEvrakDuzenleme;

public sealed class MikroDocumentEditingStockMovementGroupingTests
{
    [Fact]
    public void GroupStockMovementDocuments_SeparatesSameSerieAndOrderByRealDocumentIdentity()
    {
        var rows = new[]
        {
            Movement(Guid.NewGuid(), documentType: 1, movementKind: 0, normalReturn: 0, lineNo: 0),
            Movement(Guid.NewGuid(), documentType: 1, movementKind: 0, normalReturn: 0, lineNo: 1),
            Movement(Guid.NewGuid(), documentType: 13, movementKind: 0, normalReturn: 0, lineNo: 0),
            Movement(Guid.NewGuid(), documentType: 1, movementKind: 1, normalReturn: 1, lineNo: 0)
        };

        var groups = MikroDocumentEditingService.GroupStockMovementDocuments(rows);

        Assert.Equal(3, groups.Count);
        Assert.Contains(groups, group =>
            group.Count == 2 &&
            group.All(row => row.sth_evraktip == 1 && row.sth_cins == 0 && row.sth_normal_iade == 0));
        Assert.Contains(groups, group =>
            group.Count == 1 && group.First().sth_evraktip == 13);
        Assert.Contains(groups, group =>
            group.Count == 1 && group.First().sth_cins == 1 && group.First().sth_normal_iade == 1);
    }

    [Fact]
    public void GroupStockMovementDocuments_KeepsDifferentMovementTypesInSameDocument()
    {
        var first = Movement(Guid.NewGuid(), documentType: 17, movementKind: 3, normalReturn: 0, lineNo: 0);
        var second = Movement(Guid.NewGuid(), documentType: 17, movementKind: 3, normalReturn: 0, lineNo: 1);
        first.sth_tip = 0;
        second.sth_tip = 1;

        var group = Assert.Single(MikroDocumentEditingService.GroupStockMovementDocuments([first, second]));

        Assert.Equal(2, group.Count);
    }

    private static STOK_HAREKETLERI Movement(
        Guid movementGuid,
        byte documentType,
        byte movementKind,
        byte normalReturn,
        int lineNo) =>
        new()
        {
            sth_Guid = movementGuid,
            sth_create_date = new DateTime(2026, 11, 2, 10, 0, lineNo),
            sth_evrakno_seri = "F100",
            sth_evrakno_sira = 1234,
            sth_evraktip = documentType,
            sth_tip = 1,
            sth_cins = movementKind,
            sth_normal_iade = normalReturn,
            sth_satirno = lineNo,
            sth_stok_kod = $"STOK-{lineNo}"
        };
}
