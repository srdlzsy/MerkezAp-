using FurpaMerkezApi.Application.Abstractions.Services;
using FurpaMerkezApi.Infrastructure.Persistence.Mikro;
using FurpaMerkezApi.WebApi.Configuration;
using FurpaMerkezApi.WebApi.Controllers.Legacy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Legacy;

public sealed class LegacyEDespatchBridgeControllerTests
{
    [Fact]
    public void Controller_AllowsAnonymousOnlyThroughLegacyGate()
    {
        var allowAnonymousAttribute = typeof(LegacyEDespatchBridgeController)
            .GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: false)
            .Cast<AllowAnonymousAttribute>()
            .Single();

        Assert.NotNull(allowAnonymousAttribute);
    }

    [Fact]
    public async Task SendEDespatch_ReturnsForbidden_WhenOriginIsNotAllowed()
    {
        var service = new CapturingEDespatchService();
        var controller = CreateController(service, origin: "http://unexpected.local");

        var result = await controller.SendEDespatch(
            "depolar-arasi-sevkler",
            "F56",
            86102,
            56,
            new LegacySendEDespatchHttpRequest
            {
                DriverId = Guid.NewGuid()
            },
            CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        Assert.Null(service.LastRequest);
    }

    [Fact]
    public async Task SendEDespatch_ReturnsForbidden_WhenWarehouseIsNotAllowed()
    {
        var service = new CapturingEDespatchService();
        var controller = CreateController(
            service,
            new LegacyEDespatchBridgeOptions
            {
                Enabled = true,
                AllowedWarehouseNos = [56]
            });

        var result = await controller.SendEDespatch(
            "depolar-arasi-sevkler",
            "F120",
            10,
            120,
            new LegacySendEDespatchHttpRequest
            {
                DriverId = Guid.NewGuid()
            },
            CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        Assert.Null(service.LastRequest);
    }

    [Fact]
    public async Task SendEDespatch_ReturnsBadRequest_WhenDocumentKindIsNotSupported()
    {
        var service = new CapturingEDespatchService();
        var controller = CreateController(service, origin: "http://legacy.local");

        var result = await controller.SendEDespatch(
            "bilinmeyen",
            "F56",
            86102,
            56,
            new LegacySendEDespatchHttpRequest
            {
                DriverId = Guid.NewGuid()
            },
            CancellationToken.None);

        var objectResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        Assert.Null(service.LastRequest);
    }

    [Theory]
    [InlineData("depolar-arasi-sevkler", EDespatchDocumentType.InterWarehouseShipment)]
    [InlineData("depo-iadeleri", EDespatchDocumentType.WarehouseReturn)]
    [InlineData("firma-sevkleri", EDespatchDocumentType.OutgoingCompanyShipment)]
    [InlineData("firma-iadeleri", EDespatchDocumentType.CompanyReturn)]
    public async Task SendEDespatch_ForwardsRequest_WhenLegacyGatePasses(
        string documentKind,
        EDespatchDocumentType expectedDocumentType)
    {
        var service = new CapturingEDespatchService();
        var driverId = Guid.NewGuid();
        var controller = CreateController(service, origin: "http://legacy.local");

        var result = await controller.SendEDespatch(
            documentKind,
            "F56",
            86102,
            56,
            new LegacySendEDespatchHttpRequest
            {
                DriverId = driverId
            },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<SendEDespatchResponse>(ok.Value);
        Assert.NotNull(service.LastRequest);
        Assert.Equal(expectedDocumentType, service.LastRequest.DocumentType);
        Assert.Equal(56, service.LastRequest.WarehouseNo);
        Assert.Equal("F56", service.LastRequest.DocumentSerie);
        Assert.Equal(86102, service.LastRequest.DocumentOrderNo);
        Assert.Equal(driverId, service.LastRequest.DriverId);
    }

    [Fact]
    public async Task SendEDespatch_ReturnsNotFound_WhenBridgeIsDisabled()
    {
        var service = new CapturingEDespatchService();
        var controller = CreateController(
            service,
            new LegacyEDespatchBridgeOptions
            {
                Enabled = false
            });

        var result = await controller.SendEDespatch(
            "depolar-arasi-sevkler",
            "F56",
            86102,
            56,
            new LegacySendEDespatchHttpRequest
            {
                DriverId = Guid.NewGuid()
            },
            CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Null(service.LastRequest);
    }

    [Fact]
    public async Task GetEDespatchStatus_ForwardsLegacyDocumentWhenGatePasses()
    {
        var service = new CapturingEDespatchService();
        var controller = CreateController(service, origin: "http://legacy.local");

        var result = await controller.GetEDespatchStatus(
            "depolar-arasi-sevkler", "F56", 88015, 56, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<GetEDespatchStatusResponse>(ok.Value);
        Assert.NotNull(service.LastStatusRequest);
        Assert.Equal(EDespatchDocumentType.InterWarehouseShipment, service.LastStatusRequest.DocumentType);
        Assert.Equal(56, service.LastStatusRequest.WarehouseNo);
    }

    [Fact]
    public async Task GetEDespatchPdf_ReturnsInlinePdfWhenGatePasses()
    {
        var service = new CapturingEDespatchService();
        var controller = CreateController(service, origin: "http://legacy.local");

        var result = await controller.GetEDespatchPdf(
            "depolar-arasi-sevkler", "F56", 88015, 56, CancellationToken.None);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal("inline; filename=\"FRM2026600113344.pdf\"", controller.Response.Headers.ContentDisposition);
        Assert.NotNull(service.LastPdfRequest);
    }

    private static LegacyEDespatchBridgeController CreateController(
        CapturingEDespatchService service,
        LegacyEDespatchBridgeOptions? options = null,
        string? origin = null)
    {
        var controller = new LegacyEDespatchBridgeController(
            service,
            CreateMikroDbContext(),
            new StaticOptionsMonitor<LegacyEDespatchBridgeOptions>(options ?? new LegacyEDespatchBridgeOptions
            {
                Enabled = true,
                AllowedOrigins = ["http://legacy.local"],
                AllowedWarehouseNos = [56]
            }),
            NullLogger<LegacyEDespatchBridgeController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        if (!string.IsNullOrWhiteSpace(origin))
        {
            controller.Request.Headers.Origin = origin;
        }

        return controller;
    }

    private static MikroDbContext CreateMikroDbContext()
    {
        var options = new DbContextOptionsBuilder<MikroDbContext>()
            .UseInMemoryDatabase($"legacy-e-despatch-bridge-{Guid.NewGuid():N}")
            .Options;

        return new MikroDbContext(options);
    }

    private sealed class CapturingEDespatchService : IEDespatchService
    {
        public SendEDespatchRequest? LastRequest { get; private set; }
        public GetEDespatchStatusRequest? LastStatusRequest { get; private set; }
        public GetEDespatchPdfRequest? LastPdfRequest { get; private set; }

        public Task<SendEDespatchResponse> SendAsync(
            SendEDespatchRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;

            return Task.FromResult(new SendEDespatchResponse(
                request.DocumentType,
                request.DocumentSerie,
                request.DocumentOrderNo,
                "FRM2026600113344",
                Guid.NewGuid().ToString(),
                "service-id",
                "service-no",
                DateTime.UtcNow,
                "http://uyumsoft.local"));
        }

        public Task<GetEDespatchPdfResponse> GetPdfAsync(
            GetEDespatchPdfRequest request,
            CancellationToken cancellationToken = default)
        {
            LastPdfRequest = request;
            return Task.FromResult(new GetEDespatchPdfResponse(
                "FRM2026600113344.pdf", [1, 2, 3]));
        }

        public Task<GetEDespatchStatusResponse> GetStatusAsync(
            GetEDespatchStatusRequest request,
            CancellationToken cancellationToken = default)
        {
            LastStatusRequest = request;
            return Task.FromResult(new GetEDespatchStatusResponse(
                request.DocumentType, request.DocumentSerie, request.DocumentOrderNo,
                true, "PendingMetadata", "FRM2026600113344", Guid.NewGuid().ToString(),
                DateTime.UtcNow, false, true, "Mikro isaretlemesi bekliyor."));
        }
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
