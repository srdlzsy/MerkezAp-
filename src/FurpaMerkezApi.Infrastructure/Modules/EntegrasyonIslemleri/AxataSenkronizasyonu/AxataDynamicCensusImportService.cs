using System.Globalization;
using System.ServiceModel;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using AxataExt = FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu.ServiceReferences.Ext;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

internal sealed class AxataDynamicCensusImportService(
    IOptionsMonitor<AxataSynchronizationOptions> options,
    MikroWriteDbContext mikroWriteDbContext)
    : IAxataDynamicCensusImportService
{
    private const string ViewName = "vw_stok_duzeltme";
    private const string PendingStatus = "0";
    private const string CompletedStatus = "1";
    private const string AckOperationName = "updIntegrationTable";
    private const string DynamicDocumentSerie = "X";
    private const string DynamicExpenseCode = "0998";
    private const int DefaultTake = 50;
    private const int MaxTake = 500;
    private const short MikroUserNo = 39;
    private const byte MovementGenre = 10;
    private const byte NormalReturn = 0;
    private const byte InboundMovementType = 0;
    private const byte OutboundMovementType = 1;
    private const byte InboundDocumentType = 12;
    private const byte OutboundDocumentType = 0;
    private const int InboundInputWarehouseNo = 50;
    private const int InboundOutputWarehouseNo = 1;
    private const int OutboundInputWarehouseNo = 1;
    private const int OutboundOutputWarehouseNo = 50;
    private const double QuantityTolerance = 0.000001d;

    public async Task<AxataDynamicCensusPreviewDto> PreviewAsync(
        AxataDynamicCensusPreviewRequest request,
        CancellationToken cancellationToken)
    {
        var take = NormalizeTake(request.Take);
        var lines = await FetchPendingLinesAsync(cancellationToken);
        var selectedLines = lines.Take(take).ToArray();
        var analyses = await AnalyzeLinesAsync(selectedLines, cancellationToken);

        return new AxataDynamicCensusPreviewDto(
            ViewName,
            PendingStatus,
            DateTime.UtcNow,
            lines.Count,
            analyses.Count,
            analyses.Count(analysis => analysis.Dto.CanImport),
            analyses.Count(analysis => analysis.Dto.ExistingMovementExists),
            analyses.Sum(analysis => analysis.Dto.Quantity),
            analyses.Select(analysis => analysis.Dto).ToArray(),
            [
                "AXATA EXT getViewDataAsync ile vw_stok_duzeltme pending satirlari okunur.",
                "S11STIP=1 giris duzeltmesi olarak, diger tipler cikis duzeltmesi olarak Mikro STOK_HAREKETLERI'ne yazilir.",
                "Duplicate kontrolu icin S11SIRA Mikro sth_HareketGrupKodu1 alanina AXATA-S11:{rowNo} formatinda yazilir."
            ]);
    }

    public async Task<AxataDynamicCensusExecuteDto> ExecuteAsync(
        AxataDynamicCensusExecuteRequest request,
        Guid requestedByUserId,
        CancellationToken cancellationToken)
    {
        var take = NormalizeTake(request.Take);
        var lines = await FetchPendingLinesAsync(cancellationToken);
        var selectedLines = lines.Take(take).ToArray();
        var analyses = await AnalyzeLinesAsync(selectedLines, cancellationToken);
        var results = new List<AxataDynamicCensusResultDto>(analyses.Count);
        var failures = new List<AxataDynamicCensusFailureDto>();
        var skippedLineCount = 0;

        var importableAnalyses = new List<DynamicCensusAnalysis>();
        foreach (var analysis in analyses)
        {
            if (analysis.Dto.CanImport)
            {
                importableAnalyses.Add(analysis);
                continue;
            }

            skippedLineCount++;
            failures.Add(new AxataDynamicCensusFailureDto(
                analysis.Line.RowNo,
                analysis.Line.StockCode,
                analysis.Dto.Warning ?? "AXATA dynamic census line can not be imported safely."));

            if (!request.ContinueOnError)
            {
                break;
            }
        }

        if (failures.Count > 0 && !request.ContinueOnError)
        {
            return BuildExecuteResponse(
                analyses.Count,
                results,
                failures,
                skippedLineCount,
                requestedByUserId);
        }

        foreach (var group in importableAnalyses.GroupBy(analysis => new
        {
            analysis.Dto.MovementType,
            analysis.Dto.DocumentType,
            analysis.Dto.InputWarehouseNo,
            analysis.Dto.OutputWarehouseNo
        }))
        {
            var documentOrderNo = await GetNextDocumentOrderNoAsync(
                group.Key.MovementType,
                group.Key.DocumentType,
                cancellationToken);
            var now = DateTime.Now;
            var movementDate = DateTime.Today;
            var rowNo = 0;
            var groupResults = new List<AxataDynamicCensusResultDto>();

            await using var transaction = await mikroWriteDbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                foreach (var analysis in group)
                {
                    var movement = CreateMovement(
                        analysis,
                        documentOrderNo,
                        rowNo,
                        now,
                        movementDate);

                    mikroWriteDbContext.STOK_HAREKETLERIs.Add(movement);
                    groupResults.Add(new AxataDynamicCensusResultDto(
                        analysis.Line.RowNo,
                        analysis.Line.StockCode,
                        DynamicDocumentSerie,
                        documentOrderNo,
                        rowNo,
                        analysis.Line.Quantity,
                        false,
                        "Mikro dynamic census movement created."));
                    rowNo++;
                }

                await mikroWriteDbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                await transaction.RollbackAsync(cancellationToken);

                foreach (var analysis in group)
                {
                    failures.Add(new AxataDynamicCensusFailureDto(
                        analysis.Line.RowNo,
                        analysis.Line.StockCode,
                        exception.Message));
                }

                if (!request.ContinueOnError)
                {
                    break;
                }

                continue;
            }

            foreach (var result in groupResults)
            {
                if (!request.Acknowledge)
                {
                    results.Add(result with
                    {
                        Message = "Mikro dynamic census movement created; AXATA status was not changed."
                    });
                    continue;
                }

                try
                {
                    await AcknowledgeAsync(result.RowNo, cancellationToken);
                    results.Add(result with
                    {
                        Acknowledged = true,
                        Message = "Mikro dynamic census movement created and AXATA ENT011.S11STAT=1 acknowledged."
                    });
                }
                catch (Exception exception)
                {
                    results.Add(result with
                    {
                        Message = "Mikro dynamic census movement created; AXATA ack failed."
                    });
                    failures.Add(new AxataDynamicCensusFailureDto(
                        result.RowNo,
                        result.StockCode,
                        exception.Message));

                    if (!request.ContinueOnError)
                    {
                        break;
                    }
                }
            }

            if (failures.Count > 0 && !request.ContinueOnError)
            {
                break;
            }
        }

        return BuildExecuteResponse(
            analyses.Count,
            results,
            failures,
            skippedLineCount,
            requestedByUserId);
    }

    private AxataDynamicCensusExecuteDto BuildExecuteResponse(
        int requestedLineCount,
        IReadOnlyCollection<AxataDynamicCensusResultDto> results,
        IReadOnlyCollection<AxataDynamicCensusFailureDto> failures,
        int skippedLineCount,
        Guid requestedByUserId) =>
        new(
            ViewName,
            PendingStatus,
            DateTime.UtcNow,
            requestedLineCount,
            results.Count(result => result.MovementOrderNo >= 0),
            failures.Count,
            skippedLineCount,
            results
                .Where(result => result.MovementOrderNo >= 0)
                .Select(result => result.MovementOrderNo)
                .Distinct()
                .Count(),
            results.Count,
            results.Sum(result => result.Quantity),
            results,
            failures,
            [
                "AXATA ack islemi Mikro dynamic census hareketi basariyla yazildiktan sonra yapilir.",
                "S11SIRA duplicate kontrol anahtari olarak Mikro sth_HareketGrupKodu1 alaninda saklanir.",
                $"Talep eden kullanici: {requestedByUserId}"
            ]);

    private async Task<IReadOnlyCollection<DynamicCensusAnalysis>> AnalyzeLinesAsync(
        IReadOnlyCollection<DynamicCensusLine> lines,
        CancellationToken cancellationToken)
    {
        if (lines.Count == 0)
        {
            return Array.Empty<DynamicCensusAnalysis>();
        }

        var rowKeys = lines
            .Select(line => BuildMovementGroupCode(line.RowNo))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var existingRowKeys = rowKeys.Length == 0
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : (await mikroWriteDbContext.STOK_HAREKETLERIs
                .AsNoTracking()
                .Where(movement =>
                    movement.sth_cins == MovementGenre &&
                    movement.sth_HareketGrupKodu1 != null &&
                    rowKeys.Contains(movement.sth_HareketGrupKodu1))
                .Select(movement => movement.sth_HareketGrupKodu1 ?? string.Empty)
                .Distinct()
                .ToArrayAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return lines
            .Select(line =>
            {
                var isInbound = string.Equals(line.Type, "1", StringComparison.OrdinalIgnoreCase);
                var movementType = isInbound ? InboundMovementType : OutboundMovementType;
                var documentType = isInbound ? InboundDocumentType : OutboundDocumentType;
                var inputWarehouseNo = isInbound ? InboundInputWarehouseNo : OutboundInputWarehouseNo;
                var outputWarehouseNo = isInbound ? InboundOutputWarehouseNo : OutboundOutputWarehouseNo;
                var existingMovementExists = existingRowKeys.Contains(BuildMovementGroupCode(line.RowNo));
                var warning = BuildWarning(line, existingMovementExists);

                return new DynamicCensusAnalysis(
                    line,
                    new AxataDynamicCensusLineDto(
                        line.RowNo,
                        line.StockCode,
                        line.Quantity,
                        line.Type,
                        movementType,
                        MovementGenre,
                        documentType,
                        DynamicDocumentSerie,
                        inputWarehouseNo,
                        outputWarehouseNo,
                        string.IsNullOrWhiteSpace(warning),
                        existingMovementExists,
                        warning));
            })
            .ToArray();
    }

    private async Task<IReadOnlyCollection<DynamicCensusLine>> FetchPendingLinesAsync(CancellationToken cancellationToken)
    {
        var configuration = GetRequiredConfiguration();
        var client = CreateExtClient(configuration.ExtendedEndpointUrl);
        AxataExt.getViewData_Res response;

        try
        {
            response = await client
                .getViewDataAsync(
                    new AxataExt.getViewData_Req(
                        configuration.Username,
                        configuration.Password,
                        ViewName,
                        Array.Empty<AxataExt.Field>()))
                .WaitAsync(cancellationToken);

            CloseWcfClient(client);
        }
        catch
        {
            AbortWcfClient(client);
            throw;
        }

        if (response.state != 0)
        {
            throw new InvalidOperationException(
                $"AXATA getViewDataAsync failed: {NormalizeText(response.message)}");
        }

        return (response.ViewData ?? Array.Empty<AxataExt.Row>())
            .Select(ParseLine)
            .Where(line => line is not null)
            .Select(line => line!)
            .Where(line =>
                string.IsNullOrWhiteSpace(line.Status) ||
                string.Equals(line.Status, PendingStatus, StringComparison.OrdinalIgnoreCase))
            .OrderBy(line => ParseLong(line.RowNo) ?? long.MaxValue)
            .ToArray();
    }

    private async Task<int> GetNextDocumentOrderNoAsync(
        byte movementType,
        byte documentType,
        CancellationToken cancellationToken)
    {
        var currentMax = await mikroWriteDbContext.STOK_HAREKETLERIs
            .Where(movement =>
                movement.sth_tip == movementType &&
                movement.sth_evraktip == documentType &&
                movement.sth_normal_iade == NormalReturn &&
                movement.sth_evrakno_seri == DynamicDocumentSerie)
            .MaxAsync(movement => movement.sth_evrakno_sira, cancellationToken);

        return currentMax.HasValue ? currentMax.Value + 1 : 0;
    }

    private async Task AcknowledgeAsync(
        string rowNo,
        CancellationToken cancellationToken)
    {
        var configuration = GetRequiredConfiguration();
        var client = CreateExtClient(configuration.ExtendedEndpointUrl);
        AxataExt.updIntegrationTable_Res response;

        try
        {
            response = await client
                .updIntegrationTableAsync(
                    new AxataExt.updIntegrationTable_Req(
                        configuration.Username,
                        configuration.Password,
                        new AxataExt.IntegrationTable
                        {
                            TableName = "ENT011",
                            UpdateField = "S11STAT",
                            UpdateValue = CompletedStatus,
                            IDField = "S11SIRA",
                            IDValues = new AxataExt.IDList
                            {
                                rowNo
                            }
                        }))
                .WaitAsync(cancellationToken);

            CloseWcfClient(client);
        }
        catch
        {
            AbortWcfClient(client);
            throw;
        }

        if (response.state != 0)
        {
            throw new InvalidOperationException(
                $"AXATA {AckOperationName} failed: {NormalizeText(response.message)}");
        }
    }

    private static DynamicCensusLine? ParseLine(AxataExt.Row row)
    {
        var values = (row.Columns ?? Array.Empty<AxataExt.Column>())
            .Where(column => !string.IsNullOrWhiteSpace(column.Name))
            .ToDictionary(
                column => column.Name.Trim(),
                column => column.Value,
                StringComparer.OrdinalIgnoreCase);

        var rowNo = ToText(values.GetValueOrDefault("S11SIRA"));
        var stockCode = ToText(values.GetValueOrDefault("S11MALK"));
        var quantity = ToDouble(values.GetValueOrDefault("S11MIKT")) ?? 0d;
        var type = ToText(values.GetValueOrDefault("S11STIP"));
        var status = ToText(values.GetValueOrDefault("S11STAT"));

        if (string.IsNullOrWhiteSpace(rowNo) &&
            string.IsNullOrWhiteSpace(stockCode) &&
            Math.Abs(quantity) <= QuantityTolerance)
        {
            return null;
        }

        return new DynamicCensusLine(rowNo, stockCode, quantity, type, status);
    }

    private static string? BuildWarning(DynamicCensusLine line, bool existingMovementExists)
    {
        if (string.IsNullOrWhiteSpace(line.RowNo))
        {
            return "AXATA S11SIRA bos; ENT011 ack ve duplicate kontrolu guvenli degil.";
        }

        if (string.IsNullOrWhiteSpace(line.StockCode))
        {
            return "AXATA S11MALK stok kodu bos.";
        }

        if (Math.Abs(line.Quantity) <= QuantityTolerance)
        {
            return "AXATA S11MIKT miktari sifir.";
        }

        if (string.IsNullOrWhiteSpace(line.Type))
        {
            return "AXATA S11STIP hareket tipi bos.";
        }

        if (existingMovementExists)
        {
            return "Bu AXATA S11SIRA icin Mikro dynamic census hareketi zaten var; duplicate fis olusturulmaz.";
        }

        return null;
    }

    private static STOK_HAREKETLERI CreateMovement(
        DynamicCensusAnalysis analysis,
        int documentOrderNo,
        int rowNo,
        DateTime now,
        DateTime movementDate)
    {
        var line = analysis.Line;
        var dto = analysis.Dto;

        return new STOK_HAREKETLERI
        {
            sth_Guid = Guid.NewGuid(),
            sth_DBCno = 0,
            sth_SpecRECno = 0,
            sth_iptal = false,
            sth_fileid = 16,
            sth_hidden = false,
            sth_kilitli = false,
            sth_degisti = false,
            sth_checksum = 0,
            sth_create_user = MikroUserNo,
            sth_create_date = now,
            sth_lastup_user = MikroUserNo,
            sth_lastup_date = now,
            sth_special1 = string.Empty,
            sth_special2 = string.Empty,
            sth_special3 = string.Empty,
            sth_firmano = 0,
            sth_subeno = 0,
            sth_tarih = movementDate,
            sth_tip = dto.MovementType,
            sth_cins = dto.MovementGenre,
            sth_normal_iade = NormalReturn,
            sth_evraktip = dto.DocumentType,
            sth_evrakno_seri = DynamicDocumentSerie,
            sth_evrakno_sira = documentOrderNo,
            sth_satirno = rowNo,
            sth_belge_no = string.Empty,
            sth_belge_tarih = movementDate,
            sth_stok_kod = line.StockCode.Trim(),
            sth_isk_mas1 = 0,
            sth_isk_mas2 = 1,
            sth_isk_mas3 = 1,
            sth_isk_mas4 = 1,
            sth_isk_mas5 = 1,
            sth_isk_mas6 = 1,
            sth_isk_mas7 = 1,
            sth_isk_mas8 = 1,
            sth_isk_mas9 = 1,
            sth_isk_mas10 = 1,
            sth_sat_iskmas1 = false,
            sth_sat_iskmas2 = false,
            sth_sat_iskmas3 = false,
            sth_sat_iskmas4 = false,
            sth_sat_iskmas5 = false,
            sth_sat_iskmas6 = false,
            sth_sat_iskmas7 = false,
            sth_sat_iskmas8 = false,
            sth_sat_iskmas9 = false,
            sth_sat_iskmas10 = false,
            sth_pos_satis = 0,
            sth_promosyon_fl = false,
            sth_cari_cinsi = 0,
            sth_cari_kodu = string.Empty,
            sth_cari_grup_no = 0,
            sth_isemri_gider_kodu = DynamicExpenseCode,
            sth_plasiyer_kodu = string.Empty,
            sth_har_doviz_cinsi = 0,
            sth_har_doviz_kuru = 1d,
            sth_alt_doviz_kuru = 0d,
            sth_stok_doviz_cinsi = 0,
            sth_stok_doviz_kuru = 1d,
            sth_miktar = line.Quantity,
            sth_miktar2 = 0d,
            sth_birim_pntr = 1,
            sth_tutar = 0d,
            sth_iskonto1 = 0d,
            sth_iskonto2 = 0d,
            sth_iskonto3 = 0d,
            sth_iskonto4 = 0d,
            sth_iskonto5 = 0d,
            sth_iskonto6 = 0d,
            sth_masraf1 = 0d,
            sth_masraf2 = 0d,
            sth_masraf3 = 0d,
            sth_masraf4 = 0d,
            sth_vergi_pntr = 2,
            sth_vergi = 0d,
            sth_masraf_vergi_pntr = 0,
            sth_masraf_vergi = 0d,
            sth_netagirlik = 0d,
            sth_odeme_op = 0,
            sth_aciklama = string.Empty,
            sth_sip_uid = Guid.Empty,
            sth_fat_uid = Guid.Empty,
            sth_giris_depo_no = dto.InputWarehouseNo,
            sth_cikis_depo_no = dto.OutputWarehouseNo,
            sth_malkbl_sevk_tarihi = movementDate,
            sth_cari_srm_merkezi = string.Empty,
            sth_stok_srm_merkezi = string.Empty,
            sth_fis_tarihi = new DateTime(1899, 12, 30),
            sth_fis_sirano = 0,
            sth_vergisiz_fl = false,
            sth_maliyet_ana = 0d,
            sth_maliyet_alternatif = 0d,
            sth_maliyet_orjinal = 0d,
            sth_adres_no = 1,
            sth_parti_kodu = string.Empty,
            sth_lot_no = 0,
            sth_kons_uid = Guid.Empty,
            sth_proje_kodu = string.Empty,
            sth_exim_kodu = string.Empty,
            sth_otv_pntr = 0,
            sth_otv_vergi = 0d,
            sth_brutagirlik = 0d,
            sth_disticaret_turu = 0,
            sth_otvtutari = 0d,
            sth_otvvergisiz_fl = false,
            sth_oiv_pntr = 0,
            sth_oiv_vergi = 0d,
            sth_oivvergisiz_fl = false,
            sth_fiyat_liste_no = 0,
            sth_oivtutari = 0d,
            sth_Tevkifat_turu = 0,
            sth_nakliyedeposu = 0,
            sth_nakliyedurumu = 0,
            sth_yetkili_uid = Guid.Empty,
            sth_taxfree_fl = false,
            sth_ilave_edilecek_kdv = 0d,
            sth_ismerkezi_kodu = string.Empty,
            sth_HareketGrupKodu1 = BuildMovementGroupCode(line.RowNo),
            sth_HareketGrupKodu2 = string.Empty,
            sth_HareketGrupKodu3 = string.Empty,
            sth_Olcu1 = 0d,
            sth_Olcu2 = 0d,
            sth_Olcu3 = 0d,
            sth_Olcu4 = 0d,
            sth_Olcu5 = 0d,
            sth_FormulMiktarNo = 0,
            sth_FormulMiktar = 0d,
            sth_eirs_senaryo = 0,
            sth_eirs_tipi = 0,
            sth_teslim_tarihi = movementDate,
            sth_matbu_fl = false,
            sth_satis_fiyat_doviz_cinsi = 0,
            sth_satis_fiyat_doviz_kuru = 1d,
            sth_eticaret_kanal_kodu = string.Empty,
            sth_bagli_ithalat_kodu = string.Empty,
            sth_tevkifat_sifirlandi_fl = false
        };
    }

    private AxataDynamicCensusConfiguration GetRequiredConfiguration()
    {
        var currentOptions = options.CurrentValue;

        if (!currentOptions.Enabled)
        {
            throw new InvalidOperationException("AXATA synchronization is disabled.");
        }

        if (string.IsNullOrWhiteSpace(currentOptions.ExtendedEndpointUrl))
        {
            throw new InvalidOperationException(
                "AXATA extended endpoint URL is not configured. Dynamic census import requires AxataSynchronization:ExtendedEndpointUrl.");
        }

        if (string.IsNullOrWhiteSpace(currentOptions.Username))
        {
            throw new InvalidOperationException("AXATA username is not configured.");
        }

        if (string.IsNullOrWhiteSpace(currentOptions.Password))
        {
            throw new InvalidOperationException("AXATA password is not configured.");
        }

        return new AxataDynamicCensusConfiguration(
            currentOptions.ExtendedEndpointUrl,
            currentOptions.Username,
            currentOptions.Password);
    }

    private static AxataExt.AxataServicePoolEXTClient CreateExtClient(string endpointUrl) =>
        new(
            AxataExt.AxataServicePoolEXTClient.EndpointConfiguration.BasicHttpBinding_IAxataServicePoolEXT,
            endpointUrl);

    private static void CloseWcfClient(ICommunicationObject client)
    {
        if (client.State == CommunicationState.Faulted)
        {
            client.Abort();
            return;
        }

        client.Close();
    }

    private static void AbortWcfClient(ICommunicationObject client)
    {
        if (client.State != CommunicationState.Closed)
        {
            client.Abort();
        }
    }

    private static int NormalizeTake(int? value)
    {
        if (!value.HasValue || value.Value <= 0)
        {
            return DefaultTake;
        }

        return Math.Min(value.Value, MaxTake);
    }

    private static string BuildMovementGroupCode(string rowNo) =>
        string.IsNullOrWhiteSpace(rowNo) ? string.Empty : $"AXATA-S11:{rowNo.Trim()}";

    private static string ToText(object? value) =>
        Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;

    private static double? ToDouble(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is double doubleValue)
        {
            return doubleValue;
        }

        if (value is decimal decimalValue)
        {
            return (double)decimalValue;
        }

        if (value is int intValue)
        {
            return intValue;
        }

        if (value is long longValue)
        {
            return longValue;
        }

        var text = ToText(value);
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariantValue))
        {
            return invariantValue;
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.GetCultureInfo("tr-TR"), out var turkishValue))
        {
            return turkishValue;
        }

        return null;
    }

    private static long? ParseLong(string value) =>
        long.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static string NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "AXATA response received." : value.Trim();
}

internal sealed record DynamicCensusLine(
    string RowNo,
    string StockCode,
    double Quantity,
    string Type,
    string Status);

internal sealed record DynamicCensusAnalysis(
    DynamicCensusLine Line,
    AxataDynamicCensusLineDto Dto);

internal sealed record AxataDynamicCensusConfiguration(
    string ExtendedEndpointUrl,
    string Username,
    string Password);
