namespace FurpaMerkezApi.Application.Authentication.Contracts;

public sealed record WarehouseContextResponse(
    Guid UserId,
    string Username,
    string TokenWarehouseNo,
    string TokenWarehouseName,
    string? CurrentWarehouseNo,
    string? CurrentWarehouseName,
    bool IsTerminalUser,
    bool RequiresRelogin,
    string Reason,
    DateTime ServerTimeUtc);
