namespace FurpaMerkezApi.Application.Authentication.Contracts;

public sealed record LoginRequest(string UsernameOrEmail, string Password ,string? IpAddress = null);
