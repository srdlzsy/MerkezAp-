using System.ComponentModel.DataAnnotations;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.EntegrasyonIslemleri.Common;

public sealed class UyumsoftOperationHttpRequest
{
    public IReadOnlyCollection<UyumsoftOperationParameterHttpRequest> Parameters { get; init; } =
        Array.Empty<UyumsoftOperationParameterHttpRequest>();
}

public sealed class UyumsoftOperationParameterHttpRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(100)]
    public string Name { get; init; } = string.Empty;

    public string? Value { get; init; }
}
