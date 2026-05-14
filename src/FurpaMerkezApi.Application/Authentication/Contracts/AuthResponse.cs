using FurpaMerkezApi.Application.Identity.Contracts;

namespace FurpaMerkezApi.Application.Authentication.Contracts;

public sealed record AuthResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    UserDto User);
