using System.Data;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari.Commands;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KasaSayimlari.Commands;

public sealed class CashSummaryCommandsUseCase(
    MikroWriteDbContext mikroWriteDbContext,
    IOptions<MikroWriteOptions> mikroWriteOptions)
    : ICashSummaryCommandsUseCase
{
    private const short MikroUserNo = 39;
    private const short CustomerMovementFileId = 1;
    private const byte CustomerMovementDocumentType = 0;
    private const byte CustomerMovementType = 0;
    private const byte CustomerMovementGenre = 0;
    private const byte CustomerMovementNormalReturn = 0;
    private const byte CustomerMovementTpoz = 0;
    private const byte CustomerMovementTradeType = 0;
    private const int FirstDocumentOrderNo = 1;
    private static readonly DateTime MikroEmptyDate = new(1899, 12, 30);

    public async Task<CreateCashSummaryResponse> CreateAsync(
        CreateCashSummaryRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        var options = mikroWriteOptions.Value;
        var now = DateTime.Now;
        var summaryDate = request.SummaryDate.Date;
        var documentSerie = $"KS{request.WarehouseNo}";
        var summaryLines = request.PaymentTypes
            .Select(line => CreateSummaryEntity(request, line, now))
            .Concat(request.StoreExpenses.Select(line => CreateSummaryEntity(request, line, now)))
            .ToArray();
        var banknoteLines = request.BanknoteMovements.ToArray();
        var giftCheckLines = request.GiftCheckMovements.ToArray();
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

                foreach (var summary in summaryLines)
                {
                    summary.DocumentSerie = documentSerie;
                    summary.DocumentOrderNo = documentOrderNo;
                }

                var banknoteEntities = banknoteLines
                    .Select(line => CreateBanknoteMovementEntity(request, line, documentSerie, documentOrderNo, now))
                    .ToArray();
                var giftCheckEntities = giftCheckLines
                    .Select(line => CreateGiftCheckMovementEntity(request, line, documentSerie, documentOrderNo, now))
                    .ToArray();
                var customerMovement = CreateCustomerMovementEntity(
                    request,
                    summaryDate,
                    documentSerie,
                    documentOrderNo,
                    now);

                await mikroWriteDbContext.Summaries.AddRangeAsync(summaryLines, cancellationToken);
                await mikroWriteDbContext.BanknoteMovements.AddRangeAsync(banknoteEntities, cancellationToken);
                await mikroWriteDbContext.GiftCheckMovements.AddRangeAsync(giftCheckEntities, cancellationToken);
                await mikroWriteDbContext.CARI_HESAP_HAREKETLERIs.AddAsync(customerMovement, cancellationToken);
                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new CreateCashSummaryResponse(
                    documentSerie,
                    documentOrderNo,
                    summaryDate,
                    request.WarehouseNo,
                    summaryLines.Length,
                    request.Total,
                    options.ConnectionStringName);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<UpdateCashSummaryDetailsResponse> UpdateDetailsAsync(
        UpdateCashSummaryDetailsRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        var executionStrategy = mikroWriteDbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            mikroWriteDbContext.ChangeTracker.Clear();
            await using var transaction = await mikroWriteDbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            try
            {
                var existingSummaries = await mikroWriteDbContext.Summaries
                    .Where(item =>
                        item.WarehouseNo == request.WarehouseNo &&
                        item.DocumentSerie == request.DocumentSerie &&
                        item.DocumentOrderNo == request.DocumentOrderNo)
                    .OrderBy(item => item.Id)
                    .ToListAsync(cancellationToken);

                if (existingSummaries.Count == 0)
                {
                    throw new KeyNotFoundException("Cash summary detail was not found.");
                }

                var header = existingSummaries[0];
                var now = DateTime.Now;
                var totalAmount = request.Details.Sum(item => item.Amount);

                mikroWriteDbContext.Summaries.RemoveRange(existingSummaries);

                var updatedSummaries = request.Details
                    .Select(detail => new SummaryEntity
                    {
                        DocumentSerie = header.DocumentSerie,
                        DocumentOrderNo = header.DocumentOrderNo,
                        CashNo = header.CashNo,
                        ZReportNo = header.ZReportNo,
                        CashierNo = header.CashierNo,
                        ManagerNo = header.ManagerNo,
                        SummaryDate = header.SummaryDate,
                        Total = totalAmount,
                        PaymentTypeId = detail.PaymentTypeId,
                        Amount = detail.Amount,
                        WarehouseNo = header.WarehouseNo,
                        TypeName = NormalizeText(detail.TypeName),
                        AccountCode = NormalizeText(detail.AccountCode),
                        SlipNumber = detail.SlipNumber,
                        TerminalId = NormalizeText(detail.TerminalId),
                        Description = NormalizeText(detail.Description),
                        StoreExpenseType = null,
                        CreateDate = now
                    })
                    .ToArray();

                await mikroWriteDbContext.Summaries.AddRangeAsync(updatedSummaries, cancellationToken);
                await UpdateCustomerMovementTotalsAsync(
                    request.DocumentSerie,
                    request.DocumentOrderNo,
                    totalAmount,
                    now,
                    cancellationToken);
                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new UpdateCashSummaryDetailsResponse(
                    request.DocumentSerie,
                    request.DocumentOrderNo,
                    updatedSummaries.Length,
                    totalAmount);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<UpdateCashSummaryBanknotesResponse> UpdateBanknotesAsync(
        UpdateCashSummaryBanknotesRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        var executionStrategy = mikroWriteDbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            mikroWriteDbContext.ChangeTracker.Clear();
            await using var transaction = await mikroWriteDbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            try
            {
                var summaryHeader = await mikroWriteDbContext.Summaries
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item =>
                        item.WarehouseNo == request.WarehouseNo &&
                        item.DocumentSerie == request.DocumentSerie &&
                        item.DocumentOrderNo == request.DocumentOrderNo,
                        cancellationToken);

                if (summaryHeader is null)
                {
                    throw new KeyNotFoundException("Cash summary was not found.");
                }

                var existingBanknotes = await mikroWriteDbContext.BanknoteMovements
                    .Where(item =>
                        item.WarehouseNo == request.WarehouseNo &&
                        item.DocumentSerie == request.DocumentSerie &&
                        item.DocumentOrderNo == request.DocumentOrderNo)
                    .ToListAsync(cancellationToken);

                mikroWriteDbContext.BanknoteMovements.RemoveRange(existingBanknotes);

                var now = DateTime.Now;
                var updatedBanknotes = request.BanknoteMovements
                    .Select(item => new BanknoteMovementEntity
                    {
                        DocumentSerie = request.DocumentSerie,
                        DocumentOrderNo = request.DocumentOrderNo,
                        SummaryDate = summaryHeader.SummaryDate,
                        WarehouseNo = request.WarehouseNo,
                        Value = item.Value,
                        BanknoteType = item.BanknoteType,
                        Quantity = item.Quantity,
                        Total = item.Total,
                        CreateDate = now
                    })
                    .ToArray();

                await mikroWriteDbContext.BanknoteMovements.AddRangeAsync(updatedBanknotes, cancellationToken);
                await TouchCustomerMovementAsync(request.DocumentSerie, request.DocumentOrderNo, now, cancellationToken);
                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new UpdateCashSummaryBanknotesResponse(
                    request.DocumentSerie,
                    request.DocumentOrderNo,
                    updatedBanknotes.Length,
                    updatedBanknotes.Sum(item => item.Total));
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<DeleteCashSummaryResponse> DeleteAsync(
        DeleteCashSummaryRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request);

        var executionStrategy = mikroWriteDbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            mikroWriteDbContext.ChangeTracker.Clear();
            await using var transaction = await mikroWriteDbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);

            try
            {
                var summaries = await mikroWriteDbContext.Summaries
                    .Where(item =>
                        item.WarehouseNo == request.WarehouseNo &&
                        item.DocumentSerie == request.DocumentSerie &&
                        item.DocumentOrderNo == request.DocumentOrderNo)
                    .ToListAsync(cancellationToken);
                var banknotes = await mikroWriteDbContext.BanknoteMovements
                    .Where(item =>
                        item.WarehouseNo == request.WarehouseNo &&
                        item.DocumentSerie == request.DocumentSerie &&
                        item.DocumentOrderNo == request.DocumentOrderNo)
                    .ToListAsync(cancellationToken);
                var giftChecks = await mikroWriteDbContext.GiftCheckMovements
                    .Where(item =>
                        item.WarehouseNo == request.WarehouseNo &&
                        item.DocumentSerie == request.DocumentSerie &&
                        item.DocumentOrderNo == request.DocumentOrderNo)
                    .ToListAsync(cancellationToken);
                var customerMovements = await mikroWriteDbContext.CARI_HESAP_HAREKETLERIs
                    .Where(item =>
                        item.cha_evrakno_seri == request.DocumentSerie &&
                        item.cha_evrakno_sira == request.DocumentOrderNo)
                    .ToListAsync(cancellationToken);

                mikroWriteDbContext.Summaries.RemoveRange(summaries);
                mikroWriteDbContext.BanknoteMovements.RemoveRange(banknotes);
                mikroWriteDbContext.GiftCheckMovements.RemoveRange(giftChecks);
                mikroWriteDbContext.CARI_HESAP_HAREKETLERIs.RemoveRange(customerMovements);
                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new DeleteCashSummaryResponse(
                    request.DocumentSerie,
                    request.DocumentOrderNo,
                    summaries.Count,
                    banknotes.Count,
                    giftChecks.Count,
                    customerMovements.Count);
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
        var currentMax = await mikroWriteDbContext.Summaries
            .Where(item => item.DocumentSerie == documentSerie)
            .MaxAsync(item => (int?)item.DocumentOrderNo, cancellationToken);

        return currentMax.HasValue ? currentMax.Value + 1 : FirstDocumentOrderNo;
    }

    private async Task UpdateCustomerMovementTotalsAsync(
        string documentSerie,
        int documentOrderNo,
        double totalAmount,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var customerMovements = await mikroWriteDbContext.CARI_HESAP_HAREKETLERIs
            .Where(item =>
                item.cha_evrakno_seri == documentSerie &&
                item.cha_evrakno_sira == documentOrderNo)
            .ToListAsync(cancellationToken);

        foreach (var movement in customerMovements)
        {
            movement.cha_meblag = totalAmount;
            movement.cha_aratoplam = totalAmount;
            movement.cha_lastup_user = MikroUserNo;
            movement.cha_lastup_date = now;
        }
    }

    private async Task TouchCustomerMovementAsync(
        string documentSerie,
        int documentOrderNo,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var customerMovements = await mikroWriteDbContext.CARI_HESAP_HAREKETLERIs
            .Where(item =>
                item.cha_evrakno_seri == documentSerie &&
                item.cha_evrakno_sira == documentOrderNo)
            .ToListAsync(cancellationToken);

        foreach (var movement in customerMovements)
        {
            movement.cha_lastup_user = MikroUserNo;
            movement.cha_lastup_date = now;
        }
    }

    private static SummaryEntity CreateSummaryEntity(
        CreateCashSummaryRequest request,
        CreateCashSummaryPaymentLineRequest line,
        DateTime now) =>
        new()
        {
            CashNo = request.CashNo,
            ZReportNo = request.ZReportNo,
            CashierNo = request.CashierNo,
            ManagerNo = request.ManagerNo,
            SummaryDate = request.SummaryDate.Date,
            Total = request.Total,
            PaymentTypeId = line.PaymentTypeNo,
            Amount = line.AmountValue,
            WarehouseNo = request.WarehouseNo,
            TypeName = NormalizeText(line.PaymentName),
            AccountCode = NormalizeText(line.AccountCode),
            SlipNumber = line.SlipNumber,
            TerminalId = NormalizeText(line.TerminalId),
            Description = string.Empty,
            StoreExpenseType = null,
            CreateDate = now
        };

    private static SummaryEntity CreateSummaryEntity(
        CreateCashSummaryRequest request,
        CreateCashSummaryStoreExpenseLineRequest line,
        DateTime now) =>
        new()
        {
            CashNo = request.CashNo,
            ZReportNo = request.ZReportNo,
            CashierNo = request.CashierNo,
            ManagerNo = request.ManagerNo,
            SummaryDate = request.SummaryDate.Date,
            Total = request.Total,
            PaymentTypeId = 0,
            Amount = line.AmountValue,
            WarehouseNo = request.WarehouseNo,
            TypeName = NormalizeText(string.IsNullOrWhiteSpace(line.Description) ? "StoreExpense" : line.Description),
            AccountCode = string.Empty,
            SlipNumber = 0,
            TerminalId = string.Empty,
            Description = NormalizeText(line.Description),
            StoreExpenseType = line.StoreExpenseType,
            CreateDate = now
        };

    private static BanknoteMovementEntity CreateBanknoteMovementEntity(
        CreateCashSummaryRequest request,
        CreateCashSummaryBanknoteLineRequest line,
        string documentSerie,
        int documentOrderNo,
        DateTime now) =>
        new()
        {
            DocumentSerie = documentSerie,
            DocumentOrderNo = documentOrderNo,
            SummaryDate = request.SummaryDate.Date,
            WarehouseNo = request.WarehouseNo,
            Value = line.Value,
            BanknoteType = line.BanknoteType,
            Quantity = line.Quantity,
            Total = line.Total,
            CreateDate = now
        };

    private static GiftCheckMovementEntity CreateGiftCheckMovementEntity(
        CreateCashSummaryRequest request,
        CreateCashSummaryGiftCheckLineRequest line,
        string documentSerie,
        int documentOrderNo,
        DateTime now) =>
        new()
        {
            DocumentSerie = documentSerie,
            DocumentOrderNo = documentOrderNo,
            SummaryDate = request.SummaryDate.Date,
            WarehouseNo = request.WarehouseNo,
            Value = line.Value,
            GiftCheckType = line.GiftCheckType,
            Quantity = line.Quantity,
            Total = line.Total,
            CreateDate = now
        };

    private static CARI_HESAP_HAREKETLERI CreateCustomerMovementEntity(
        CreateCashSummaryRequest request,
        DateTime summaryDate,
        string documentSerie,
        int documentOrderNo,
        DateTime now) =>
        new()
        {
            cha_Guid = Guid.NewGuid(),
            cha_DBCno = 0,
            cha_SpecRecNo = 0,
            cha_iptal = false,
            cha_fileid = CustomerMovementFileId,
            cha_hidden = false,
            cha_kilitli = false,
            cha_degisti = false,
            cha_CheckSum = 0,
            cha_create_user = MikroUserNo,
            cha_create_date = now,
            cha_lastup_user = MikroUserNo,
            cha_lastup_date = now,
            cha_special1 = string.Empty,
            cha_special2 = string.Empty,
            cha_special3 = string.Empty,
            cha_firmano = 0,
            cha_subeno = 0,
            cha_evrak_tip = CustomerMovementDocumentType,
            cha_evrakno_seri = documentSerie,
            cha_evrakno_sira = documentOrderNo,
            cha_satir_no = 0,
            cha_tarihi = summaryDate,
            cha_tip = CustomerMovementType,
            cha_cinsi = CustomerMovementGenre,
            cha_normal_Iade = CustomerMovementNormalReturn,
            cha_tpoz = CustomerMovementTpoz,
            cha_ticaret_turu = CustomerMovementTradeType,
            cha_belge_no = $"{request.CashNo}-{request.ZReportNo}",
            cha_belge_tarih = summaryDate,
            cha_aciklama = $"Kasa sayimi {documentSerie}/{documentOrderNo}",
            cha_satici_kodu = request.CashierNo.ToString(),
            cha_cari_cins = 0,
            cha_kod = $"KASA-{request.WarehouseNo}",
            cha_d_cins = 0,
            cha_d_kur = 1d,
            cha_altd_kur = 0d,
            cha_grupno = 0,
            cha_srmrkkodu = string.Empty,
            cha_kasa_hizmet = 0,
            cha_kasa_hizkod = request.CashNo.ToString(),
            cha_karsidcinsi = 0,
            cha_karsid_kur = 1d,
            cha_karsidgrupno = 0,
            cha_karsisrmrkkodu = string.Empty,
            cha_miktari = 1d,
            cha_meblag = request.Total,
            cha_aratoplam = request.Total,
            cha_vade = 0,
            cha_Vade_Farki_Yuz = 0d,
            cha_ft_iskonto1 = 0d,
            cha_ft_iskonto2 = 0d,
            cha_ft_iskonto3 = 0d,
            cha_ft_iskonto4 = 0d,
            cha_ft_iskonto5 = 0d,
            cha_ft_iskonto6 = 0d,
            cha_ft_masraf1 = 0d,
            cha_ft_masraf2 = 0d,
            cha_ft_masraf3 = 0d,
            cha_ft_masraf4 = 0d,
            cha_isk_mas1 = 0,
            cha_isk_mas2 = 0,
            cha_isk_mas3 = 0,
            cha_isk_mas4 = 0,
            cha_isk_mas5 = 0,
            cha_isk_mas6 = 0,
            cha_isk_mas7 = 0,
            cha_isk_mas8 = 0,
            cha_isk_mas9 = 0,
            cha_isk_mas10 = 0,
            cha_sat_iskmas1 = false,
            cha_sat_iskmas2 = false,
            cha_sat_iskmas3 = false,
            cha_sat_iskmas4 = false,
            cha_sat_iskmas5 = false,
            cha_sat_iskmas6 = false,
            cha_sat_iskmas7 = false,
            cha_sat_iskmas8 = false,
            cha_sat_iskmas9 = false,
            cha_sat_iskmas10 = false,
            cha_yuvarlama = 0d,
            cha_StFonPntr = 0,
            cha_stopaj = 0d,
            cha_savsandesfonu = 0d,
            cha_avansmak_damgapul = 0d,
            cha_vergipntr = 0,
            cha_vergisiz_fl = false,
            cha_otvtutari = 0d,
            cha_otvvergisiz_fl = false,
            cha_oiv_pntr = 0,
            cha_oivtutari = 0d,
            cha_oiv_vergi = 0d,
            cha_oivergisiz_fl = false,
            cha_fis_tarih = MikroEmptyDate,
            cha_fis_sirano = 0,
            cha_trefno = string.Empty,
            cha_sntck_poz = 0,
            cha_reftarihi = summaryDate,
            cha_istisnakodu = 0,
            cha_pos_hareketi = 0,
            cha_meblag_ana_doviz_icin_gecersiz_fl = 0,
            cha_meblag_alt_doviz_icin_gecersiz_fl = 0,
            cha_meblag_orj_doviz_icin_gecersiz_fl = 0,
            cha_sip_uid = Guid.Empty,
            cha_kirahar_uid = Guid.Empty,
            cha_vardiya_tarihi = summaryDate,
            cha_vardiya_no = Convert.ToByte(Math.Clamp(request.CashNo, 0, byte.MaxValue)),
            cha_vardiya_evrak_ti = 0,
            cha_ebelge_turu = 0,
            cha_tevkifat_toplam = 0d,
            cha_e_islem_turu = 0,
            cha_fatura_belge_turu = 0,
            cha_diger_belge_adi = string.Empty,
            cha_uuid = string.Empty,
            cha_adres_no = 0,
            cha_vergifon_toplam = 0d,
            cha_ilk_belge_tarihi = summaryDate,
            cha_ilk_belge_doviz_kuru = 1d,
            cha_HareketGrupKodu1 = string.Empty,
            cha_HareketGrupKodu2 = string.Empty,
            cha_HareketGrupKodu3 = string.Empty,
            cha_ebelgeno_seri = string.Empty,
            cha_ebelgeno_sira = 0,
            cha_hubid = string.Empty,
            cha_hubglbid = string.Empty,
            cha_disyazilimid = string.Empty,
            cha_disyazilim_tip = 0,
            cha_bsba_e_belge_mi = 0,
            cha_eticaret_kanal_kodu = string.Empty,
            cha_hizli_satis_kasa_no = Convert.ToInt16(Math.Clamp(request.CashNo, 0, short.MaxValue)),
            cha_ebelge_Islemturu = 0,
            cha_tevkifat_sifirlandi_fl = false,
            cha_vergi1 = 0d,
            cha_vergi2 = 0d,
            cha_vergi3 = 0d,
            cha_vergi4 = 0d,
            cha_vergi5 = 0d,
            cha_vergi6 = 0d,
            cha_vergi7 = 0d,
            cha_vergi8 = 0d,
            cha_vergi9 = 0d,
            cha_vergi10 = 0d,
            cha_vergi11 = 0d,
            cha_vergi12 = 0d,
            cha_vergi13 = 0d,
            cha_vergi14 = 0d,
            cha_vergi15 = 0d,
            cha_vergi16 = 0d,
            cha_vergi17 = 0d,
            cha_vergi18 = 0d,
            cha_vergi19 = 0d,
            cha_vergi20 = 0d,
            cha_ilave_edilecek_kdv1 = 0d,
            cha_ilave_edilecek_kdv2 = 0d,
            cha_ilave_edilecek_kdv3 = 0d,
            cha_ilave_edilecek_kdv4 = 0d,
            cha_ilave_edilecek_kdv5 = 0d,
            cha_ilave_edilecek_kdv6 = 0d,
            cha_ilave_edilecek_kdv7 = 0d,
            cha_ilave_edilecek_kdv8 = 0d,
            cha_ilave_edilecek_kdv9 = 0d,
            cha_ilave_edilecek_kdv10 = 0d,
            cha_ilave_edilecek_kdv11 = 0d,
            cha_ilave_edilecek_kdv12 = 0d,
            cha_ilave_edilecek_kdv13 = 0d,
            cha_ilave_edilecek_kdv14 = 0d,
            cha_ilave_edilecek_kdv15 = 0d,
            cha_ilave_edilecek_kdv16 = 0d,
            cha_ilave_edilecek_kdv17 = 0d,
            cha_ilave_edilecek_kdv18 = 0d,
            cha_ilave_edilecek_kdv19 = 0d,
            cha_ilave_edilecek_kdv20 = 0d,
            cha_efatura_belge_tipi = 0
        };

    private static void Validate(CreateCashSummaryRequest request)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        if (request.CashNo <= 0 || request.ZReportNo < 0 || request.CashierNo <= 0 || request.ManagerNo <= 0)
        {
            throw new ArgumentException("Cash, Z report, cashier and manager values must be valid.");
        }

        if (request.SummaryDate == default)
        {
            throw new ArgumentException("Summary date is required.", nameof(request.SummaryDate));
        }

        if (request.PaymentTypes.Count == 0 && request.StoreExpenses.Count == 0)
        {
            throw new ArgumentException("At least one summary detail line is required.");
        }
    }

    private static void Validate(UpdateCashSummaryDetailsRequest request)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        if (string.IsNullOrWhiteSpace(request.DocumentSerie))
        {
            throw new ArgumentException("Document serie is required.", nameof(request.DocumentSerie));
        }

        if (request.DocumentOrderNo < 0)
        {
            throw new ArgumentException("Document order no can not be negative.", nameof(request.DocumentOrderNo));
        }

        if (request.Details.Count == 0)
        {
            throw new ArgumentException("At least one detail line is required.", nameof(request.Details));
        }
    }

    private static void Validate(UpdateCashSummaryBanknotesRequest request)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        if (string.IsNullOrWhiteSpace(request.DocumentSerie))
        {
            throw new ArgumentException("Document serie is required.", nameof(request.DocumentSerie));
        }

        if (request.DocumentOrderNo < 0)
        {
            throw new ArgumentException("Document order no can not be negative.", nameof(request.DocumentOrderNo));
        }
    }

    private static void Validate(DeleteCashSummaryRequest request)
    {
        if (request.WarehouseNo <= 0)
        {
            throw new ArgumentException("Warehouse no must be greater than zero.", nameof(request.WarehouseNo));
        }

        if (string.IsNullOrWhiteSpace(request.DocumentSerie))
        {
            throw new ArgumentException("Document serie is required.", nameof(request.DocumentSerie));
        }

        if (request.DocumentOrderNo < 0)
        {
            throw new ArgumentException("Document order no can not be negative.", nameof(request.DocumentOrderNo));
        }
    }

    private static string NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
