namespace FurpaMerkezApi.Application.Modules.Common.CompanyMovements;

public sealed record CompanyMovementDetailDto(
    CompanyMovementHeaderDto Header,
    IReadOnlyCollection<CompanyMovementLineItemDto> Items);
