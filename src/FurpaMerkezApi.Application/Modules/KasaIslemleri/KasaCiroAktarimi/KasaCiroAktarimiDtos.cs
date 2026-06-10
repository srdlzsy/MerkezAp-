namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaCiroAktarimi;

public sealed record KasaCiroBranchDto(
    int BranchNo,
    string BranchName,
    string Region);

public sealed record KasaCiroImportRequest(
    DateTime StartDate,
    DateTime EndDate,
    IReadOnlyCollection<int>? Branches,
    string? MovementRootPath,
    bool DryRun);

public sealed record KasaCiroImportResultDto(
    string RunId,
    string Status,
    DateTime StartDate,
    DateTime EndDate,
    int ProcessedDays,
    int ProcessedBranches,
    int ProcessedFiles,
    int SkippedEmptyBranches,
    int InsertedTotals,
    int UpdatedTotals,
    int InsertedDetails,
    int UpdatedDetails,
    int InsertedDiscountCards,
    int UpdatedDiscountCards,
    IReadOnlyCollection<KasaCiroImportIssueDto> Warnings,
    IReadOnlyCollection<KasaCiroImportIssueDto> Errors);

public sealed record KasaCiroImportIssueDto(
    DateTime? Date,
    int? BranchNo,
    int? CashRegisterNo,
    string? File,
    int? LineNo,
    string Message);
