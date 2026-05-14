using System.Data;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenDepoSiparisleri.Create;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.VerilenDepoSiparisleri.Create;

public sealed class CreateIssuedWarehouseOrderUseCase(
    MikroWriteDbContext mikroWriteDbContext,
    IOptions<MikroWriteOptions> mikroWriteOptions)
    : ICreateIssuedWarehouseOrderUseCase
{
    private const short FileId = 86;
    private const short MikroUserNo = 39;
    private const int FirstDocumentOrderNo = 0;
    private static readonly DateTime MikroEmptyDate = new(1900, 1, 1);

    public async Task<CreateIssuedWarehouseOrderResponse> ExecuteAsync(
        CreateIssuedWarehouseOrderRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        var options = mikroWriteOptions.Value;
        var now = DateTime.Now;
        var orderDate = (request.OrderDate ?? DateTime.Today).Date;
        var deliveryDate = (request.DeliveryDate ?? orderDate).Date;
        var documentSerie = $"F{request.InWarehouseNo}";
        var lines = request.Lines.ToArray();
        var executionStrategy = mikroWriteDbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            mikroWriteDbContext.ChangeTracker.Clear();
            await using var transaction = await mikroWriteDbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            try
            {
                var documentOrderNo = await GetNextDocumentOrderNoAsync(documentSerie, cancellationToken);
                var entities = lines
                    .Select((line, rowNo) => CreateOrderLine(
                        request,
                        line,
                        rowNo,
                        now,
                        orderDate,
                        deliveryDate,
                        documentSerie,
                        documentOrderNo))
                    .ToArray();

                await mikroWriteDbContext.DEPOLAR_ARASI_SIPARISLERs.AddRangeAsync(entities, cancellationToken);
                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new CreateIssuedWarehouseOrderResponse(
                    documentSerie,
                    documentOrderNo,
                    orderDate,
                    deliveryDate,
                    request.InWarehouseNo,
                    request.OutWarehouseNo,
                    entities.Length,
                    lines.Sum(line => line.Quantity),
                    options.ConnectionStringName);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private async Task<int> GetNextDocumentOrderNoAsync(
        string documentSerie,
        CancellationToken cancellationToken)
    {
        var currentMax = await mikroWriteDbContext.DEPOLAR_ARASI_SIPARISLERs
            .Where(order => order.ssip_evrakno_seri == documentSerie)
            .MaxAsync(order => order.ssip_evrakno_sira, cancellationToken);

        return currentMax.HasValue ? currentMax.Value + 1 : FirstDocumentOrderNo;
    }

    private static DEPOLAR_ARASI_SIPARISLER CreateOrderLine(
        CreateIssuedWarehouseOrderRequest request,
        CreateIssuedWarehouseOrderLineRequest line,
        int rowNo,
        DateTime now,
        DateTime orderDate,
        DateTime deliveryDate,
        string documentSerie,
        int documentOrderNo)
    {
        var unitPrice = line.UnitPrice;
        var amount = line.Quantity * unitPrice;

        return new DEPOLAR_ARASI_SIPARISLER
        {
            ssip_Guid = Guid.NewGuid(),
            ssip_DBCno = 0,
            ssip_SpecRECno = 0,
            ssip_iptal = false,
            ssip_fileid = FileId,
            ssip_hidden = false,
            ssip_kilitli = false,
            ssip_degisti = false,
            ssip_checksum = 0,
            ssip_create_user = MikroUserNo,
            ssip_create_date = now,
            ssip_lastup_user = MikroUserNo,
            ssip_lastup_date = now,
            ssip_special1 = "0",
            ssip_special2 = string.Empty,
            ssip_special3 = string.Empty,
            ssip_firmano = 0,
            ssip_subeno = 0,
            ssip_tarih = orderDate,
            ssip_teslim_tarih = deliveryDate,
            ssip_evrakno_seri = documentSerie,
            ssip_evrakno_sira = documentOrderNo,
            ssip_satirno = rowNo,
            ssip_belgeno = string.Empty,
            ssip_belge_tarih = orderDate,
            ssip_stok_kod = line.StockCode.Trim(),
            ssip_miktar = line.Quantity,
            ssip_b_fiyat = unitPrice,
            ssip_tutar = amount,
            ssip_teslim_miktar = 0d,
            ssip_aciklama = NormalizeText(line.Description ?? request.Description),
            ssip_girdepo = request.InWarehouseNo,
            ssip_cikdepo = request.OutWarehouseNo,
            ssip_kapat_fl = false,
            ssip_birim_pntr = Convert.ToByte(line.UnitPointer),
            ssip_fiyat_liste_no = 0,
            ssip_stal_uid = Guid.Empty,
            ssip_paket_kod = NormalizeText(line.PackageCode),
            ssip_kapatmanedenkod = string.Empty,
            ssip_projekodu = NormalizeText(line.ProjectCode),
            ssip_sormerkezi = NormalizeText(line.ResponsibilityCenter),
            ssip_gecerlilik_tarihi = MikroEmptyDate,
            ssip_rezervasyon_miktari = line.RecommendedQuantity ?? 0d,
            ssip_rezerveden_teslim_edilen = 0d
        };
    }

    private static void Validate(CreateIssuedWarehouseOrderRequest request)
    {
        if (request.InWarehouseNo <= 0)
        {
            throw new ArgumentException("In warehouse no must be greater than zero.", nameof(request.InWarehouseNo));
        }

        if (request.OutWarehouseNo <= 0)
        {
            throw new ArgumentException("Out warehouse no must be greater than zero.", nameof(request.OutWarehouseNo));
        }

        if (request.InWarehouseNo == request.OutWarehouseNo)
        {
            throw new ArgumentException("In warehouse and out warehouse can not be the same.");
        }

        if (request.Lines is null || request.Lines.Count == 0)
        {
            throw new ArgumentException("At least one order line is required.", nameof(request.Lines));
        }

        if (request.DeliveryDate.HasValue &&
            request.OrderDate.HasValue &&
            request.DeliveryDate.Value.Date < request.OrderDate.Value.Date)
        {
            throw new ArgumentException("Delivery date can not be earlier than order date.", nameof(request.DeliveryDate));
        }

        foreach (var line in request.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.StockCode))
            {
                throw new ArgumentException("Stock code is required.", nameof(request.Lines));
            }

            if (line.Quantity <= 0)
            {
                throw new ArgumentException("Line quantity must be greater than zero.", nameof(request.Lines));
            }

            if (line.UnitPrice < 0)
            {
                throw new ArgumentException("Line unit price can not be negative.", nameof(request.Lines));
            }

            if (line.UnitPointer is < 1 or > byte.MaxValue)
            {
                throw new ArgumentException("Line unit pointer must be between 1 and 255.", nameof(request.Lines));
            }

            if (line.RecommendedQuantity is < 0)
            {
                throw new ArgumentException("Line recommended quantity can not be negative.", nameof(request.Lines));
            }
        }
    }

    private static string NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
