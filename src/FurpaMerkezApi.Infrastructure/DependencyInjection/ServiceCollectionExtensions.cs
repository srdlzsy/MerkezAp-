using FurpaMerkezApi.Application.Abstractions.Time;
using FurpaMerkezApi.Application.Abstractions.Services;
using FurpaMerkezApi.Application.Modules.AyarIslemleri.Ayarlar;
using FurpaMerkezApi.Application.Modules.AramaIslemleri.ProductCustomerSuggestions;
using FurpaMerkezApi.Application.Modules.AramaIslemleri.ProductLatestTag;
using FurpaMerkezApi.Application.Modules.AramaIslemleri.ResolveBarcode;
using FurpaMerkezApi.Application.Modules.AramaIslemleri.SearchCustomers;
using FurpaMerkezApi.Application.Modules.DuzeltmeIslemleri.MikroEvrakDuzenleme;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.PosMuhasebeAktarimi;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.UyumsoftServisleri;
using FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGonderimi;
using FurpaMerkezApi.Application.Modules.FaturaIslemleri.FaturaGoruntuleme;
using FurpaMerkezApi.Application.Modules.AramaIslemleri.SearchProducts;
using FurpaMerkezApi.Application.Modules.AramaIslemleri.SearchWarehouses;
using FurpaMerkezApi.Application.Modules.GreenGrocer.Reports;
using FurpaMerkezApi.Application.Modules.IadeIslemleri.DepoIadeleri.Create;
using FurpaMerkezApi.Application.Modules.IadeIslemleri.DepoIadeleri.Detail;
using FurpaMerkezApi.Application.Modules.IadeIslemleri.DepoIadeleri.List;
using FurpaMerkezApi.Application.Modules.IadeIslemleri.FirmaIadeleri.Create;
using FurpaMerkezApi.Application.Modules.IadeIslemleri.FirmaIadeleri.Detail;
using FurpaMerkezApi.Application.Modules.IadeIslemleri.FirmaIadeleri.List;
using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.DepoMalKabulleri.Detail;
using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.DepoMalKabulleri.List;
using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.FirmaMalKabulleri.Detail;
using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.FirmaMalKabulleri.List;
using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.MalKabulFarklari;
using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.Common.EIrsaliyeLookup;
using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.MalKabuller.Accept;
using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.MalKabuller.CompanyReceiving;
using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.MalKabuller.CompanyReceiving.Offline;
using FurpaMerkezApi.Application.Modules.MobileSync.CustomerCatalog;
using FurpaMerkezApi.Application.Modules.MobileSync.ProductPriceCatalog;
using FurpaMerkezApi.Application.Modules.MobileSync.WarehouseCatalog;
using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.BelgeAkisTakibi;
using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.DepoOperasyonPaneli;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.BanknotTakipleri.Create;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.BanknotTakipleri.Detail;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.BanknotTakipleri.List;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaCirolari.Detail;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaCirolari.List;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaCirolari.Overview;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari.Commands;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari.Files;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari.Lookups;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari.Queries;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaHareketAktarimi;
using FurpaMerkezApi.Infrastructure.Modules.OperasyonIslemleri.BelgeAkisTakibi;
using FurpaMerkezApi.Infrastructure.Modules.OperasyonIslemleri.DepoOperasyonPaneli;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaCiroAktarimi;
using FurpaMerkezApi.Application.Modules.SevkIslemleri.DepolarArasiSevkler.Detail;
using FurpaMerkezApi.Application.Modules.SevkIslemleri.DepolarArasiSevkler.Create;
using FurpaMerkezApi.Application.Modules.SevkIslemleri.DepolarArasiSevkler.List;
using FurpaMerkezApi.Application.Modules.SevkIslemleri.FirmaSevkleri.Create;
using FurpaMerkezApi.Application.Modules.SevkIslemleri.FirmaSevkleri.Detail;
using FurpaMerkezApi.Application.Modules.SevkIslemleri.FirmaSevkleri.List;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.AlinanFirmaSiparisleri.Detail;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.AlinanFirmaSiparisleri.List;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.AlinanDepoSiparisleri.Detail;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.AlinanDepoSiparisleri.List;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.Create;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.Detail;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.List;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenDepoSiparisleri.Create;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenDepoSiparisleri.Detail;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenDepoSiparisleri.List;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri.Create;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri.Detail;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri.List;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri.Products;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri.Tags;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KunyeEtiketYazdirma;
using FurpaMerkezApi.Application.Modules.StokIslemleri.SayimSonuclari.Create;
using FurpaMerkezApi.Application.Modules.StokIslemleri.SayimSonuclari.Detail;
using FurpaMerkezApi.Application.Modules.StokIslemleri.SayimSonuclari.List;
using FurpaMerkezApi.Application.Modules.StokIslemleri.SayimSonuclari.Offline;
using FurpaMerkezApi.Application.Modules.StokIslemleri.MasrafFisleri.Create;
using FurpaMerkezApi.Application.Modules.StokIslemleri.MasrafFisleri.Detail;
using FurpaMerkezApi.Application.Modules.StokIslemleri.MasrafFisleri.List;
using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.Operations;
using FurpaMerkezApi.Application.Modules.OrtakIslemler.SikayetOneri;
using FurpaMerkezApi.Application.Modules.RaporIslemleri.SatisAnalizleri;
using FurpaMerkezApi.Application.Modules.StokIslemleri.Virmanlar.Create;
using FurpaMerkezApi.Application.Modules.StokIslemleri.Virmanlar.Detail;
using FurpaMerkezApi.Application.Modules.StokIslemleri.Virmanlar.List;
using FurpaMerkezApi.Application.Modules.StokIslemleri.ZayiatFisleri.Create;
using FurpaMerkezApi.Application.Modules.StokIslemleri.ZayiatFisleri.Detail;
using FurpaMerkezApi.Application.Modules.StokIslemleri.ZayiatFisleri.List;
using FurpaMerkezApi.Infrastructure.Authentication;
using FurpaMerkezApi.Infrastructure.Modules.AyarIslemleri.Ayarlar;
using FurpaMerkezApi.Infrastructure.Modules.AramaIslemleri.ProductCustomerSuggestions;
using FurpaMerkezApi.Infrastructure.Modules.AramaIslemleri.ProductLatestTag;
using FurpaMerkezApi.Infrastructure.Modules.AramaIslemleri.ResolveBarcode;
using FurpaMerkezApi.Infrastructure.Modules.AramaIslemleri.SearchCustomers;
using FurpaMerkezApi.Infrastructure.Modules.DuzeltmeIslemleri.MikroEvrakDuzenleme;
using FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;
using FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.PosMuhasebeAktarimi;
using FurpaMerkezApi.Infrastructure.Modules.FaturaIslemleri.FaturaGonderimi;
using FurpaMerkezApi.Infrastructure.Modules.FaturaIslemleri.FaturaGoruntuleme;
using FurpaMerkezApi.Infrastructure.Modules.AramaIslemleri.SearchProducts;
using FurpaMerkezApi.Infrastructure.Modules.AramaIslemleri.SearchWarehouses;
using FurpaMerkezApi.Infrastructure.Modules.GreenGrocer.Reports;
using FurpaMerkezApi.Infrastructure.Modules.Common.CompanyMovements;
using FurpaMerkezApi.Infrastructure.Modules.IadeIslemleri.DepoIadeleri.Create;
using FurpaMerkezApi.Infrastructure.Modules.IadeIslemleri.DepoIadeleri.Detail;
using FurpaMerkezApi.Infrastructure.Modules.IadeIslemleri.DepoIadeleri.List;
using FurpaMerkezApi.Infrastructure.Modules.IadeIslemleri.FirmaIadeleri.Create;
using FurpaMerkezApi.Infrastructure.Modules.IadeIslemleri.FirmaIadeleri.Detail;
using FurpaMerkezApi.Infrastructure.Modules.IadeIslemleri.FirmaIadeleri.List;
using FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.BanknotTakipleri;
using FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.BanknotTakipleri.Create;
using FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.BanknotTakipleri.Detail;
using FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.BanknotTakipleri.List;
using FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KasaCirolari;
using FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KasaCirolari.Detail;
using FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KasaCirolari.List;
using FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KasaCirolari.Overview;
using FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KasaSayimlari.Commands;
using FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KasaSayimlari.Files;
using FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KasaSayimlari.Lookups;
using FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KasaSayimlari.Queries;
using FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KasaHareketAktarimi;
using FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KasaCiroAktarimi;
using FurpaMerkezApi.Infrastructure.Modules.MalKabulIslemleri.Common.EIrsaliyeLookup;
using FurpaMerkezApi.Infrastructure.Modules.MalKabulIslemleri.DepoMalKabulleri.Detail;
using FurpaMerkezApi.Infrastructure.Modules.MalKabulIslemleri.DepoMalKabulleri.List;
using FurpaMerkezApi.Infrastructure.Modules.MalKabulIslemleri.FirmaMalKabulleri.Detail;
using FurpaMerkezApi.Infrastructure.Modules.MalKabulIslemleri.FirmaMalKabulleri.List;
using FurpaMerkezApi.Infrastructure.Modules.MalKabulIslemleri.MalKabulFarklari;
using FurpaMerkezApi.Infrastructure.Modules.MalKabulIslemleri.MalKabuller.Accept;
using FurpaMerkezApi.Infrastructure.Modules.MalKabulIslemleri.MalKabuller.CompanyReceiving;
using FurpaMerkezApi.Infrastructure.Modules.MalKabulIslemleri.MalKabuller.CompanyReceiving.Offline;
using FurpaMerkezApi.Infrastructure.Modules.MobileSync.CustomerCatalog;
using FurpaMerkezApi.Infrastructure.Modules.MobileSync.ProductPriceCatalog;
using FurpaMerkezApi.Infrastructure.Modules.MobileSync.WarehouseCatalog;
using FurpaMerkezApi.Infrastructure.Modules.SevkIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Modules.SevkIslemleri.DepolarArasiSevkler.Create;
using FurpaMerkezApi.Infrastructure.Modules.SevkIslemleri.DepolarArasiSevkler.Detail;
using FurpaMerkezApi.Infrastructure.Modules.SevkIslemleri.DepolarArasiSevkler.List;
using FurpaMerkezApi.Infrastructure.Modules.SevkIslemleri.FirmaSevkleri.Create;
using FurpaMerkezApi.Infrastructure.Modules.SevkIslemleri.FirmaSevkleri.Detail;
using FurpaMerkezApi.Infrastructure.Modules.SevkIslemleri.FirmaSevkleri.List;
using FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.AlinanFirmaSiparisleri.Detail;
using FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.AlinanFirmaSiparisleri.List;
using FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.AlinanDepoSiparisleri.Detail;
using FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.AlinanDepoSiparisleri.List;
using FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.Create;
using FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.Detail;
using FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.List;
using FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.VerilenDepoSiparisleri.Create;
using FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.VerilenDepoSiparisleri.Detail;
using FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.VerilenDepoSiparisleri.List;
using FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.EtiketBelgeleri;
using FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.EtiketBelgeleri.Create;
using FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.EtiketBelgeleri.Detail;
using FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.EtiketBelgeleri.List;
using FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.EtiketBelgeleri.Products;
using FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.EtiketBelgeleri.Tags;
using FurpaMerkezApi.Infrastructure.Modules.KasaIslemleri.KunyeEtiketYazdirma;
using FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.SayimSonuclari.Create;
using FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.SayimSonuclari.Detail;
using FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.SayimSonuclari.List;
using FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.SayimSonuclari.Offline;
using FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Common;
using FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.MasrafFisleri.Create;
using FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.MasrafFisleri.Detail;
using FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.MasrafFisleri.List;
using FurpaMerkezApi.Infrastructure.Modules.OperasyonIslemleri.Operations;
using FurpaMerkezApi.Infrastructure.Modules.OrtakIslemler.SikayetOneri;
using FurpaMerkezApi.Infrastructure.Modules.RaporIslemleri.SatisAnalizleri;
using FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Virmanlar;
using FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Virmanlar.Create;
using FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Virmanlar.Detail;
using FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.Virmanlar.List;
using FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.ZayiatFisleri.Create;
using FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.ZayiatFisleri.Detail;
using FurpaMerkezApi.Infrastructure.Modules.StokIslemleri.ZayiatFisleri.List;
using FurpaMerkezApi.Infrastructure.Persistence;
using FurpaMerkezApi.Infrastructure.Persistence.Axata;
using FurpaMerkezApi.Infrastructure.Persistence.Furpa;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.Infrastructure.Persistence.Shopigo;
using FurpaMerkezApi.Infrastructure.Services;
using FurpaMerkezApi.Infrastructure.Services.MikroApi;
using FurpaMerkezApi.Infrastructure.OfflineSync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.OnerilenDepoSiparisleri.List;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.OnerilenFirmaSiparisleri.List;
using FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.OnerilenDepoSiparisleri.List;
using FurpaMerkezApi.Infrastructure.Modules.SiparisIslemleri.OnerilenFirmaSiparisleri.List;

namespace FurpaMerkezApi.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var authConnection = configuration.GetConnectionString("AuthConnection");
        var (mikroConnectionName, mikroWriteConnectionName) = ResolveMikroConnectionNames(configuration);
        var mikroConnection = configuration.GetConnectionString(mikroConnectionName);
        var mikroWriteConnection = configuration.GetConnectionString(mikroWriteConnectionName);
        var furpaConnection = configuration.GetConnectionString("FurpaConnection");
        var axataConnection = configuration.GetConnectionString("AxataConnection");
        var shopigoCiroConnection = configuration.GetConnectionString("ShopigoCiroConnection");

        if (string.IsNullOrWhiteSpace(authConnection))
        {
            throw new InvalidOperationException("Connection string 'AuthConnection' was not found.");
        }

        if (string.IsNullOrWhiteSpace(mikroConnection))
        {
            throw new InvalidOperationException($"Connection string '{mikroConnectionName}' was not found.");
        }

        if (string.IsNullOrWhiteSpace(mikroWriteConnection))
        {
            throw new InvalidOperationException($"Connection string '{mikroWriteConnectionName}' was not found.");
        }

        if (string.IsNullOrWhiteSpace(furpaConnection))
        {
            throw new InvalidOperationException("Connection string 'FurpaConnection' was not found.");
        }

        var jwtSection = configuration.GetSection("Jwt");

        var jwtOptions = new JwtOptions
        {
            Issuer = jwtSection["Issuer"] ?? string.Empty,
            Audience = jwtSection["Audience"] ?? string.Empty,
            SecretKey = jwtSection["SecretKey"] ?? string.Empty,
            ExpiryMinutes = int.TryParse(jwtSection["ExpiryMinutes"], out var expiryMinutes) ? expiryMinutes : 120
        };

        services.AddHttpClient();
        services.AddHttpClient(nameof(UyumsoftConnectedQueryService), client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5);
        });
        services.Configure<MikroApiOptions>(configuration.GetSection(MikroApiOptions.SectionName));
        services.Configure<MikroWriteRoutingOptions>(configuration.GetSection(MikroWriteRoutingOptions.SectionName));
        services.Configure<DocumentFlowTrackingOptions>(configuration.GetSection(DocumentFlowTrackingOptions.SectionName));
        services.AddSingleton<MikroApiAuthBlockFactory>();
        services.AddHttpClient<MikroApiClient>((serviceProvider, client) =>
        {
            var mikroApiOptions = serviceProvider.GetRequiredService<IOptionsMonitor<MikroApiOptions>>().CurrentValue;

            if (Uri.TryCreate(mikroApiOptions.BaseUrl, UriKind.Absolute, out var baseUri) &&
                (baseUri.Scheme == Uri.UriSchemeHttp || baseUri.Scheme == Uri.UriSchemeHttps))
            {
                client.BaseAddress = baseUri;
            }

            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(mikroApiOptions.TimeoutSeconds, 1, 600));
        });
        services.Configure<AxataSynchronizationOptions>(configuration.GetSection("AxataSynchronization"));
        services.AddSingleton(Options.Create(jwtOptions));
        services.AddSingleton(Options.Create(new MikroWriteOptions(
            mikroWriteConnection,
            mikroWriteConnectionName)));
        services.AddSingleton(Options.Create(new EDespatchOptions(
            configuration["EDespatch:EndpointUrl"] ?? string.Empty,
            configuration["EDespatch:Username"] ?? string.Empty,
            configuration["EDespatch:Password"] ?? string.Empty,
            configuration["EDespatch:SupplierCustomerCode"] ?? string.Empty,
            configuration["EDespatch:ProfileId"] ?? string.Empty,
            configuration["EDespatch:DespatchAdviceTypeCode"] ?? string.Empty,
            configuration["EDespatch:CountryCode"] ?? string.Empty,
            configuration["EDespatch:CountryName"] ?? string.Empty)));
        var eDespatchUsername = configuration["EDespatch:Username"] ?? string.Empty;
        var eDespatchPassword = configuration["EDespatch:Password"] ?? string.Empty;
        var eDespatchEndpointUrl = configuration["EDespatch:EndpointUrl"] ?? string.Empty;
        var eDespatchWsdlUrl = configuration["EDespatch:WsdlUrl"] ?? string.Empty;
        var eInvoiceUsername = configuration["EInvoice:Username"];
        var eInvoicePassword = configuration["EInvoice:Password"];

        services.AddSingleton(Options.Create(new UyumsoftConnectedServicesOptions(
            new UyumsoftServiceEndpointOptions(
                configuration["EInvoice:EndpointUrl"] ?? string.Empty,
                configuration["EInvoice:WsdlUrl"] ?? string.Empty,
                string.IsNullOrWhiteSpace(eInvoiceUsername) ? eDespatchUsername : eInvoiceUsername,
                string.IsNullOrWhiteSpace(eInvoicePassword) ? eDespatchPassword : eInvoicePassword,
                configuration["EInvoice:ContractName"] ?? "IBasicIntegration"),
            new UyumsoftServiceEndpointOptions(
                eDespatchEndpointUrl,
                eDespatchWsdlUrl,
                eDespatchUsername,
                eDespatchPassword,
                configuration["EDespatch:ContractName"] ?? "IBasicDespatchIntegration"))));
        services.AddSingleton(Options.Create(new OperationsExportOptions(
            configuration["OperationsExport:BasePath"] ?? string.Empty)));

        services.AddDbContext<AuthDbContext>(options =>
        {
            if (IsSqlServerConnectionString(authConnection))
            {
                options.UseSqlServer(
                    authConnection,
                    sqlServer =>
                    {
                        sqlServer.EnableRetryOnFailure();
                        sqlServer.CommandTimeout(180);
                    });

                return;
            }

            options.UseNpgsql(authConnection, npgsql => npgsql.MigrationsAssembly(typeof(AuthDbContext).Assembly.FullName));
        });

        services.AddDbContext<MikroDbContext>(options =>
            options.UseSqlServer(
                mikroConnection,
                sqlServer =>
                {
                    sqlServer.EnableRetryOnFailure();
                    sqlServer.CommandTimeout(180);
                }));

        services.AddDbContext<MikroWriteDbContext>(options =>
            options.UseSqlServer(
                mikroWriteConnection,
                sqlServer =>
                {
                    sqlServer.EnableRetryOnFailure();
                    sqlServer.CommandTimeout(180);
                }));

        services.AddDbContext<FurpaDbContext>(options =>
            options.UseSqlServer(
                furpaConnection,
                sqlServer =>
                {
                    sqlServer.EnableRetryOnFailure();
                    sqlServer.CommandTimeout(180);
                }));

        if (!string.IsNullOrWhiteSpace(axataConnection))
        {
            services.AddDbContext<AxataDbContext>(options =>
                options.UseSqlServer(
                    axataConnection,
                    sqlServer =>
                    {
                        // AXATA SQL does not support EF's OPENJSON-based collection translation.
                        // Compatibility level 120 makes collection Contains generate classic IN clauses.
                        sqlServer.UseCompatibilityLevel(120);
                        sqlServer.EnableRetryOnFailure();
                        sqlServer.CommandTimeout(180);
                    }));
        }

        if (!string.IsNullOrWhiteSpace(shopigoCiroConnection))
        {
            services.AddDbContext<ShopigoCiroDbContext>(options =>
                options.UseSqlServer(
                    shopigoCiroConnection,
                    sqlServer =>
                    {
                        sqlServer.EnableRetryOnFailure();
                        sqlServer.CommandTimeout(180);
                    }));
        }

        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenFactory, JwtTokenFactory>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IEDespatchService, EDespatchService>();
        services.AddScoped<IEInvoiceDocumentRenderer, EInvoiceDocumentRenderer>();
        services.AddScoped<UblTrInvoiceBusinessRuleValidator>();
        services.AddScoped<UblTrInvoiceXmlValidator>();
        services.AddScoped<IUyumsoftConnectedQueryService, UyumsoftConnectedQueryService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<ISikayetOneriService, SikayetOneriService>();
        services.AddScoped<IAyarlarService, AyarlarService>();
        services.AddScoped<IMikroDocumentEditingService, MikroDocumentEditingService>();
        services.AddScoped<MobileOfflineSyncService>();
        services.AddScoped<ISearchProductsUseCase, SearchProductsUseCase>();
        services.AddScoped<ISearchCustomersUseCase, SearchCustomersUseCase>();
        services.AddScoped<ISearchWarehousesUseCase, SearchWarehousesUseCase>();
        services.AddScoped<IResolveBarcodeUseCase, ResolveBarcodeUseCase>();
        services.AddScoped<IGetProductCustomerSuggestionsUseCase, GetProductCustomerSuggestionsUseCase>();
        services.AddScoped<IGetProductLatestTagUseCase, GetProductLatestTagUseCase>();
        services.AddScoped<IGetMobileProductPriceCatalogUseCase, GetMobileProductPriceCatalogUseCase>();
        services.AddScoped<IGetMobileCustomerCatalogUseCase, GetMobileCustomerCatalogUseCase>();
        services.AddScoped<IGetMobileWarehouseCatalogUseCase, GetMobileWarehouseCatalogUseCase>();
        services.AddScoped<IGreenGrocerReportsUseCase, GreenGrocerReportsUseCase>();
        services.AddScoped<IDeleteGreenGrocerOrderUseCase, DeleteGreenGrocerOrderUseCase>();
        services.AddScoped<CompanyOrderDetailQueryExecutor>();
        services.AddScoped<CompanyOrderListQueryExecutor>();
        services.AddScoped<WarehouseOrderListQueryExecutor>();
        services.AddScoped<WarehouseOrderDetailQueryExecutor>();
        services.AddScoped<IListSuggestedWarehouseOrdersUseCase, ListSuggestedWarehouseOrdersUseCase>();
        services.AddScoped<IListSuggestedCompanyOrdersUseCase, ListSuggestedCompanyOrdersUseCase>();
        services.AddScoped<CompanyMovementDetailQueryExecutor>();
        services.AddScoped<CompanyMovementListQueryExecutor>();
        services.AddScoped<CompanyMovementWriteService>();
        services.AddScoped<InvoiceSendingService>();
        services.AddScoped<InvoiceViewingService>();
        services.AddScoped<UyumsoftInboxInvoiceSyncService>();
        services.AddScoped<InvoiceViewingQueryExecutor>();
        services.AddScoped<InventoryCountDetailQueryExecutor>();
        services.AddScoped<InventoryCountListQueryExecutor>();
        services.AddScoped<InventoryCountWriteService>();
        services.AddScoped<StockReceiptDetailQueryExecutor>();
        services.AddScoped<StockReceiptListQueryExecutor>();
        services.AddScoped<StockReceiptWriteService>();
        services.AddScoped<VirmanDetailQueryExecutor>();
        services.AddScoped<VirmanListQueryExecutor>();
        services.AddScoped<VirmanWriteService>();
        services.AddScoped<LabelDocumentQueryExecutor>();
        services.AddScoped<LabelDocumentWriteService>();
        services.AddScoped<LabelProductQueryExecutor>();
        services.AddScoped<LabelTagQueryExecutor>();
        services.AddScoped<BanknoteTrackQueryExecutor>();
        services.AddScoped<BanknoteTrackWriteService>();
        services.AddScoped<WarehouseShippingDetailQueryExecutor>();
        services.AddScoped<WarehouseShippingListQueryExecutor>();
        services.AddScoped<IListReceivedCompanyOrdersUseCase, ListReceivedCompanyOrdersUseCase>();
        services.AddScoped<IGetReceivedCompanyOrderDetailUseCase, GetReceivedCompanyOrderDetailUseCase>();
        services.AddScoped<IListReceivedWarehouseOrdersUseCase, ListReceivedWarehouseOrdersUseCase>();
        services.AddScoped<IGetReceivedWarehouseOrderDetailUseCase, GetReceivedWarehouseOrderDetailUseCase>();
        services.AddScoped<IListIssuedCompanyOrdersUseCase, ListIssuedCompanyOrdersUseCase>();
        services.AddScoped<IGetIssuedCompanyOrderDetailUseCase, GetIssuedCompanyOrderDetailUseCase>();
        services.AddScoped<ICreateIssuedCompanyOrderUseCase, CreateIssuedCompanyOrderUseCase>();
        services.AddScoped<IListIssuedWarehouseOrdersUseCase, ListIssuedWarehouseOrdersUseCase>();
        services.AddScoped<IGetIssuedWarehouseOrderDetailUseCase, GetIssuedWarehouseOrderDetailUseCase>();
        services.AddScoped<ICreateIssuedWarehouseOrderUseCase, CreateIssuedWarehouseOrderUseCase>();
        services.AddScoped<IGetInterWarehouseShipmentDetailUseCase, GetInterWarehouseShipmentDetailUseCase>();
        services.AddScoped<IListInterWarehouseShipmentsUseCase, ListInterWarehouseShipmentsUseCase>();
        services.AddScoped<ICreateInterWarehouseShipmentUseCase, CreateInterWarehouseShipmentUseCase>();
        services.AddScoped<ICreateCompanyShipmentUseCase, CreateCompanyShipmentUseCase>();
        services.AddScoped<IGetCompanyShipmentDetailUseCase, GetCompanyShipmentDetailUseCase>();
        services.AddScoped<IListCompanyShipmentsUseCase, ListCompanyShipmentsUseCase>();
        services.AddScoped<ICreateWarehouseReturnUseCase, CreateWarehouseReturnUseCase>();
        services.AddScoped<IGetWarehouseReturnDetailUseCase, GetWarehouseReturnDetailUseCase>();
        services.AddScoped<IListWarehouseReturnsUseCase, ListWarehouseReturnsUseCase>();
        services.AddScoped<ICreateCompanyReturnUseCase, CreateCompanyReturnUseCase>();
        services.AddScoped<IGetCompanyReturnDetailUseCase, GetCompanyReturnDetailUseCase>();
        services.AddScoped<IListCompanyReturnsUseCase, ListCompanyReturnsUseCase>();
        services.AddScoped<IListPendingWarehouseReceivingsUseCase, ListPendingWarehouseReceivingsUseCase>();
        services.AddScoped<IGetPendingWarehouseReceivingDetailUseCase, GetPendingWarehouseReceivingDetailUseCase>();
        services.AddScoped<IListCompanyReceivingDocumentsUseCase, ListCompanyReceivingDocumentsUseCase>();
        services.AddScoped<IGetCompanyReceivingDocumentDetailUseCase, GetCompanyReceivingDocumentDetailUseCase>();
        services.AddScoped<IGetInboundDespatchLookupUseCase, GetInboundDespatchLookupUseCase>();
        services.AddScoped<IAcceptWarehouseReceivingUseCase, AcceptWarehouseReceivingUseCase>();
        services.AddScoped<IListWarehouseReceivingDifferencesUseCase, ListWarehouseReceivingDifferencesUseCase>();
        services.AddScoped<CreateCompanyReceivingUseCase>();
        services.AddScoped<ICreateCompanyReceivingUseCase>(serviceProvider =>
            serviceProvider.GetRequiredService<CreateCompanyReceivingUseCase>());
        services.AddScoped<IGetCompanyReceivingOfflineSyncStatusUseCase, GetCompanyReceivingOfflineSyncStatusUseCase>();
        services.AddScoped<IListInvoiceSendingDocumentsUseCase, ListInvoiceSendingDocumentsUseCase>();
        services.AddScoped<IGetInvoiceSendingDocumentUseCase, GetInvoiceSendingDocumentUseCase>();
        services.AddScoped<IRenderInvoiceSendingDocumentUseCase, RenderInvoiceSendingDocumentUseCase>();
        services.AddScoped<IValidateInvoiceSendingDocumentsUseCase, ValidateInvoiceSendingDocumentsUseCase>();
        services.AddScoped<ISendInvoiceSendingDocumentsUseCase, SendInvoiceSendingDocumentsUseCase>();
        services.AddScoped<IListInvoiceReturnReferenceCandidatesUseCase, ListInvoiceReturnReferenceCandidatesUseCase>();
        services.AddScoped<IUpdateInvoiceReturnReferenceUseCase, UpdateInvoiceReturnReferenceUseCase>();
        services.AddScoped<IListInvoiceViewingDocumentsUseCase, ListInvoiceViewingDocumentsUseCase>();
        services.AddScoped<ISynchronizeInvoiceViewingDocumentsUseCase, SynchronizeInvoiceViewingDocumentsUseCase>();
        services.AddScoped<IRenderInvoiceViewingDocumentUseCase, RenderInvoiceViewingDocumentUseCase>();
        services.AddScoped<IGetInvoiceViewingDocumentUseCase, GetInvoiceViewingDocumentUseCase>();
        services.AddScoped<ISetInvoiceViewingPrintedStateUseCase, SetInvoiceViewingPrintedStateUseCase>();
        services.AddScoped<IListOutageReceiptsUseCase, ListOutageReceiptsUseCase>();
        services.AddScoped<IGetOutageReceiptDetailUseCase, GetOutageReceiptDetailUseCase>();
        services.AddScoped<ICreateOutageReceiptUseCase, CreateOutageReceiptUseCase>();
        services.AddScoped<IListExpenseReceiptsUseCase, ListExpenseReceiptsUseCase>();
        services.AddScoped<IGetExpenseReceiptDetailUseCase, GetExpenseReceiptDetailUseCase>();
        services.AddScoped<ICreateExpenseReceiptUseCase, CreateExpenseReceiptUseCase>();
        services.AddScoped<IListInventoryCountsUseCase, ListInventoryCountsUseCase>();
        services.AddScoped<IGetInventoryCountDetailUseCase, GetInventoryCountDetailUseCase>();
        services.AddScoped<ICreateInventoryCountUseCase, CreateInventoryCountUseCase>();
        services.AddScoped<IGetInventoryCountOfflineSyncStatusUseCase, GetInventoryCountOfflineSyncStatusUseCase>();
        services.AddScoped<ISalesAnalysisReportsUseCase, SalesAnalysisReportsUseCase>();
        services.AddScoped<CashTurnoverQueryExecutor>();
        services.AddScoped<IListCashTurnoversUseCase, ListCashTurnoversUseCase>();
        services.AddScoped<IGetCashTurnoverDetailUseCase, GetCashTurnoverDetailUseCase>();
        services.AddScoped<IGetCashTurnoverOverviewUseCase, GetCashTurnoverOverviewUseCase>();
        services.AddScoped<ICashSummaryQueriesUseCase, CashSummaryQueriesUseCase>();
        services.AddScoped<ICashSummaryLookupsUseCase, CashSummaryLookupsUseCase>();
        services.AddScoped<ICashSummaryCommandsUseCase, CashSummaryCommandsUseCase>();
        services.AddScoped<IGetCashSummaryZReportTotalUseCase, GetCashSummaryZReportTotalUseCase>();
        services.AddScoped<IKasaHareketAktarimiService, KasaHareketAktarimiService>();
        services.AddScoped<IKasaCiroAktarimiService, KasaCiroAktarimiService>();
        services.AddScoped<IListBanknoteTracksUseCase, ListBanknoteTracksUseCase>();
        services.AddScoped<IGetBanknoteTrackDetailUseCase, GetBanknoteTrackDetailUseCase>();
        services.AddScoped<ICreateBanknoteTrackUseCase, CreateBanknoteTrackUseCase>();
        services.AddScoped<IListLabelDocumentsUseCase, ListLabelDocumentsUseCase>();
        services.AddScoped<IGetLabelDocumentProductsUseCase, GetLabelDocumentProductsUseCase>();
        services.AddScoped<ICreateLabelDocumentUseCase, CreateLabelDocumentUseCase>();
        services.AddScoped<IListLabelPriceChangedProductsUseCase, ListLabelPriceChangedProductsUseCase>();
        services.AddScoped<IListLabelTagsUseCase, ListLabelTagsUseCase>();
        services.AddScoped<IListKunyeLabelTagsUseCase, ListKunyeLabelTagsUseCase>();
        services.AddScoped<IListVirmansUseCase, ListVirmansUseCase>();
        services.AddScoped<IGetVirmanDetailUseCase, GetVirmanDetailUseCase>();
        services.AddScoped<ICreateVirmanUseCase, CreateVirmanUseCase>();
        services.AddSingleton<AxataSynchronizationQueue>();
        services.AddScoped<AxataSynchronizationExecutionCoordinator>();
        services.AddScoped<AxataSynchronizationManualDocumentService>();
        services.AddScoped<AxataSynchronizationOutboxWriter>();
        services.AddScoped<AxataSynchronizationLiveTransportService>();
        services.AddScoped<IAxataProductSynchronizationService, AxataProductSynchronizationService>();
        services.AddScoped<AxataOutboundDeliveryImportService>();
        services.AddScoped<IAxataOutboundDeliveryImportService>(serviceProvider =>
            serviceProvider.GetRequiredService<AxataOutboundDeliveryImportService>());
        services.AddScoped<IAxataIntegrationAuditService>(serviceProvider =>
            serviceProvider.GetRequiredService<AxataOutboundDeliveryImportService>());
        services.AddScoped<AxataSynchronizationConnectionProbeService>();
        services.AddScoped<IAxataSynchronizationService, AxataSynchronizationService>();
        services.AddScoped<IPosMuhasebeAktarimiService, PosMuhasebeAktarimiService>();
        services.AddScoped<IAxataSynchronizationTaskHandler, FirmMasterSyncTaskHandler>();
        services.AddScoped<IAxataSynchronizationTaskHandler, ProductMasterSyncTaskHandler>();
        services.AddScoped<IAxataSynchronizationTaskHandler, IssuedWarehouseOrderSyncTaskHandler>();
        services.AddScoped<IAxataSynchronizationTaskHandler, CompanyReceivingSyncTaskHandler>();
        services.AddScoped<IAxataSynchronizationTaskHandler, InventoryCountSyncTaskHandler>();
        services.AddHostedService<AxataSynchronizationWorker>();
        services.AddHostedService<AxataSynchronizationScheduler>();
        services.AddSingleton<OperationsJobQueue>();
        services.AddHostedService<OperationsJobWorker>();
        services.AddScoped<IOperationsService, OperationsService>();
        services.AddScoped<IDocumentFlowService, DocumentFlowService>();
        services.AddScoped<IWarehouseOperationsDashboardService, WarehouseOperationsDashboardService>();
        services.AddScoped<OperationsFileGenerationService>();

        return services;
    }

    private static (string ReadConnectionName, string WriteConnectionName) ResolveMikroConnectionNames(
        IConfiguration configuration)
    {
        var profile = configuration["MikroDatabase:Profile"];

        if (string.IsNullOrWhiteSpace(profile) || profile.Equals("Split", StringComparison.OrdinalIgnoreCase))
        {
            var writeConnectionName = string.IsNullOrWhiteSpace(configuration.GetConnectionString("MikroWriteConnection"))
                ? "testMikroConnection"
                : "MikroWriteConnection";

            return ("MikroConnection", writeConnectionName);
        }

        if (profile.Equals("Test", StringComparison.OrdinalIgnoreCase))
        {
            return ("testMikroConnection", "testMikroConnection");
        }

        if (profile.Equals("Live", StringComparison.OrdinalIgnoreCase))
        {
            return ("MikroConnection", "MikroConnection");
        }

        throw new InvalidOperationException(
            "Configuration value 'MikroDatabase:Profile' must be one of: Split, Test, Live.");
    }

    private static bool IsSqlServerConnectionString(string connectionString) =>
        connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
        connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase);
}
