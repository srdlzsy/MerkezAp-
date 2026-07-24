using System.Security.Claims;
using FurpaMerkezApi.Application.Modules.RaporIslemleri.TedarikciPerformansKarnesi;
using FurpaMerkezApi.WebApi.Controllers.Modules.RaporIslemleri.TedarikciPerformansKarnesi;
using FurpaMerkezApi.WebApi.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Modules.RaporIslemleri.TedarikciPerformansKarnesi;

public sealed class TedarikciPerformansKarnesiControllerTests
{
    [Fact]
    public async Task List_UsesCurrentWarehouseForRegularUser()
    {
        var useCase = new CapturingTedarikciPerformansKarnesiUseCase();
        var controller = CreateController(useCase, warehouseNo: 101);

        await controller.List(
            new SupplierPerformanceHttpRequest
            {
                StartDate = new DateTime(2026, 7, 1),
                EndDate = new DateTime(2026, 7, 3),
                CustomerCode = "120.01.03106"
            },
            CancellationToken.None);

        Assert.NotNull(useCase.LastReportRequest);
        Assert.Equal(101, useCase.LastReportRequest.WarehouseNo);
        Assert.Equal("120.01.03106", useCase.LastReportRequest.CustomerCode);
    }

    [Fact]
    public async Task List_RejectsDifferentWarehouseForRegularUser()
    {
        var useCase = new CapturingTedarikciPerformansKarnesiUseCase();
        var controller = CreateController(useCase, warehouseNo: 101);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            controller.List(
                new SupplierPerformanceHttpRequest
                {
                    WarehouseNo = 202,
                    StartDate = new DateTime(2026, 7, 1),
                    EndDate = new DateTime(2026, 7, 3)
                },
                CancellationToken.None));

        Assert.Null(useCase.LastReportRequest);
    }

    [Fact]
    public async Task Detail_AllowsAdministratorToQueryAllWarehouses()
    {
        var useCase = new CapturingTedarikciPerformansKarnesiUseCase();
        var controller = CreateController(useCase, warehouseNo: 0, roles: ["Administrator"]);

        await controller.Detail(
            "120.01.03106",
            new SupplierPerformanceDetailHttpRequest
            {
                StartDate = new DateTime(2026, 7, 1),
                EndDate = new DateTime(2026, 7, 3),
                EventTake = 25
            },
            CancellationToken.None);

        Assert.NotNull(useCase.LastDetailRequest);
        Assert.Null(useCase.LastDetailRequest.WarehouseNo);
        Assert.Equal("120.01.03106", useCase.LastDetailRequest.CustomerCode);
        Assert.Equal(25, useCase.LastDetailRequest.EventTake);
    }

    private static TedarikciPerformansKarnesiController CreateController(
        ITedarikciPerformansKarnesiUseCase useCase,
        int warehouseNo,
        IReadOnlyCollection<string>? roles = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "11111111-1111-1111-1111-111111111111"),
            new("warehouse_no", warehouseNo.ToString())
        };

        claims.AddRange((roles ?? []).Select(role => new Claim(ClaimTypes.Role, role)));

        return new TedarikciPerformansKarnesiController(useCase)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"))
                }
            }
        };
    }

    private sealed class CapturingTedarikciPerformansKarnesiUseCase : ITedarikciPerformansKarnesiUseCase
    {
        public SupplierPerformanceRequest? LastReportRequest { get; private set; }
        public SupplierPerformanceDetailRequest? LastDetailRequest { get; private set; }

        public Task<SupplierPerformanceReportDto> GetReportAsync(
            SupplierPerformanceRequest request,
            CancellationToken cancellationToken)
        {
            LastReportRequest = request;

            return Task.FromResult(new SupplierPerformanceReportDto(
                request.WarehouseNo,
                request.StartDate,
                request.EndDate,
                new DateTime(2026, 7, 24, 9, 0, 0, DateTimeKind.Utc),
                CreateEmptySummary(),
                [],
                []));
        }

        public Task<SupplierPerformanceDetailDto> GetDetailAsync(
            SupplierPerformanceDetailRequest request,
            CancellationToken cancellationToken)
        {
            LastDetailRequest = request;

            return Task.FromResult(new SupplierPerformanceDetailDto(
                CreateHealthyCard(request.CustomerCode),
                []));
        }

        private static SupplierPerformanceSummaryDto CreateEmptySummary() =>
            new(
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                "summary-only",
                0,
                "NoData",
                "Secili donem icin tedarikci hareketi bulunamadi.");

        private static SupplierPerformanceCardDto CreateHealthyCard(string customerCode) =>
            new(
                customerCode,
                "ORNEK TEDARIKCI A.S.",
                "1234567890",
                100,
                "A",
                "Healthy",
                new SupplierOrderPerformanceDto(0, 0, 0, 0, 0, 0, 0, 0, 0),
                new SupplierReceivingPerformanceDto(0, 0, 0, 0, 0, 0, 0, 0),
                new SupplierReturnPerformanceDto(0, 0, 0, 0, 0),
                new SupplierOutageImpactDto(0, 0, 0, 0, 0, "stok-karti-varsayilan-tedarikci"),
                new SupplierInvoicePerformanceDto(0, 0, 0, 0, "summary-only", "Test response."),
                new SupplierPerformanceScoreBreakdownDto(0, 0, 0, 0, 0, 0),
                [new SupplierPerformanceSignalDto("strong-performance", "Healthy", "Guclu performans", "Test response.")]);
    }
}
