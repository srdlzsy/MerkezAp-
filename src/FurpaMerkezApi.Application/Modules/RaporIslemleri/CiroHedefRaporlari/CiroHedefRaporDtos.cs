namespace FurpaMerkezApi.Application.Modules.RaporIslemleri.CiroHedefRaporlari;

public sealed record CiroHedefKarsilastirmaRequest(
    DateOnly StartDate,
    DateOnly EndDate,
    int? WarehouseNo,
    string? Region);

public sealed record HedefTanimDto(
    int Id,
    int Year,
    int Month,
    string MonthName,
    decimal CiroGoalPercent,
    decimal CustomerGoalPercent,
    decimal ExpenseGoalPercent,
    int? WarehouseNo);

public sealed record CiroHedefSubeKarsilastirmaDto(
    int BranchNo,
    string BranchName,
    string Region,
    decimal CurrentCashTotal,
    decimal CurrentCreditTotal,
    decimal CurrentGiftTotal,
    decimal CurrentFuturesTotal,
    decimal CurrentExpenseTotal,
    decimal CurrentOverallTotal,
    int CurrentCustomerCount,
    decimal CurrentAverageBasketAmount,
    decimal PreviousCashTotal,
    decimal PreviousCreditTotal,
    decimal PreviousGiftTotal,
    decimal PreviousFuturesTotal,
    decimal PreviousExpenseTotal,
    decimal PreviousOverallTotal,
    int PreviousCustomerCount,
    decimal PreviousAverageBasketAmount,
    decimal TargetTurnover,
    decimal TurnoverAchievementRate,
    decimal TargetCustomerCount,
    decimal CustomerAchievementRate);

public sealed record CiroHedefKarsilastirmaRaporDto(
    DateOnly StartDate,
    DateOnly EndDate,
    decimal CiroGoalPercent,
    decimal CustomerGoalPercent,
    decimal TotalCurrentTurnover,
    decimal TotalPreviousTurnover,
    decimal TotalTargetTurnover,
    decimal OverallTurnoverAchievementRate,
    int TotalCurrentCustomers,
    int TotalPreviousCustomers,
    decimal TotalTargetCustomers,
    decimal OverallCustomerAchievementRate,
    decimal CurrentOverallAverageBasket,
    decimal PreviousOverallAverageBasket,
    IReadOnlyCollection<CiroHedefSubeKarsilastirmaDto> Branches);
