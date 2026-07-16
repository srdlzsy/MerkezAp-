namespace FurpaMerkezApi.WebApi.Security;

public sealed class ForbiddenAccessException(string message) : Exception(message);
