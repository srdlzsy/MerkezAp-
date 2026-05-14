using FurpaMerkezApi.Application.Abstractions.Time;

namespace FurpaMerkezApi.Infrastructure.Services;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
