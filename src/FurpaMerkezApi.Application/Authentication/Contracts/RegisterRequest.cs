namespace FurpaMerkezApi.Application.Authentication.Contracts;

public sealed record RegisterRequest(
    string Username,
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string WarehouseNo,
    string WarehouseName);
