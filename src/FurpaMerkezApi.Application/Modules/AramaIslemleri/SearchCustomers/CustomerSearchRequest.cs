namespace FurpaMerkezApi.Application.Modules.AramaIslemleri.SearchCustomers;

public sealed record CustomerSearchRequest(
    string SearchText,
    int Take);
