namespace FurpaMerkezApi.Application.Modules.StokIslemleri.Virmanlar;

public sealed record VirmanDetailDto(
    VirmanHeaderDto Header,
    IReadOnlyCollection<VirmanLineItemDto> Items);
