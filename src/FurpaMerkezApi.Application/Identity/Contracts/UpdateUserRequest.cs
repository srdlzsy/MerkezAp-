namespace FurpaMerkezApi.Application.Identity.Contracts;

public sealed record UpdateUserRequest(
    string Username,
    string Email,
    string FirstName,
    string LastName,
    string WarehouseNo,
    string WarehouseName,
    bool IsActive);
