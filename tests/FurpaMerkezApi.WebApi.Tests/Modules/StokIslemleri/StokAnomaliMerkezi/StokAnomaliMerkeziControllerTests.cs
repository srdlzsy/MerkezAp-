using System.Security.Claims;
using FurpaMerkezApi.Application.Modules.StokIslemleri.StokAnomaliMerkezi;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.WebApi.Controllers.Modules.StokIslemleri.StokAnomaliMerkezi;
using FurpaMerkezApi.WebApi.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Modules.StokIslemleri.StokAnomaliMerkezi;

public sealed class StokAnomaliMerkeziControllerTests
{
    [Fact]
    public async Task List_UsesCurrentWarehouseForRegularUser()
    {
        var service = new CapturingStockAnomalyCenterService();
        var controller = CreateController(service, warehouseNo: 110);

        await controller.List(
            new StockAnomalyListHttpRequest
            {
                Status = StockAnomalyStatus.Open,
                Take = 50
            },
            CancellationToken.None);

        Assert.NotNull(service.LastListRequest);
        Assert.Equal(110, service.LastListRequest.WarehouseNo);
        Assert.Equal(StockAnomalyStatus.Open, service.LastListRequest.Status);
        Assert.Equal(50, service.LastListRequest.Take);
    }

    [Fact]
    public async Task List_RejectsDifferentWarehouseForRegularUser()
    {
        var service = new CapturingStockAnomalyCenterService();
        var controller = CreateController(service, warehouseNo: 110);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            controller.List(
                new StockAnomalyListHttpRequest
                {
                    WarehouseNo = 120
                },
                CancellationToken.None));

        Assert.Null(service.LastListRequest);
    }

    [Fact]
    public async Task List_AllowsAllWarehousesPermissionToQueryAllWarehouses()
    {
        var service = new CapturingStockAnomalyCenterService();
        var controller = CreateController(
            service,
            warehouseNo: 110,
            permissions: ["stok-islemleri.stok-anomali-merkezi.all-warehouses"]);

        await controller.List(new StockAnomalyListHttpRequest(), CancellationToken.None);

        Assert.NotNull(service.LastListRequest);
        Assert.Null(service.LastListRequest.WarehouseNo);
    }

    [Fact]
    public async Task ProductManagers_UsesCurrentWarehouseForRegularUser()
    {
        var service = new CapturingStockAnomalyCenterService();
        var controller = CreateController(service, warehouseNo: 110);

        await controller.ProductManagers(new StockAnomalyProductManagerHttpRequest(), CancellationToken.None);

        Assert.NotNull(service.LastProductManagerRequest);
        Assert.Equal(110, service.LastProductManagerRequest.WarehouseNo);
        Assert.Equal(StockAnomalyStatus.Open, service.LastProductManagerRequest.Status);
    }

    [Fact]
    public async Task Scan_UsesCurrentWarehouseForRegularUser()
    {
        var service = new CapturingStockAnomalyCenterService();
        var controller = CreateController(service, warehouseNo: 110);

        await controller.Scan(
            new StockAnomalyScanHttpRequest
            {
                StartDate = new DateTime(2026, 7, 27),
                EndDate = new DateTime(2026, 7, 28)
            },
            CancellationToken.None);

        Assert.NotNull(service.LastScanRequest);
        Assert.Equal(110, service.LastScanRequest.WarehouseNo);
        Assert.Equal(new DateTime(2026, 7, 27), service.LastScanRequest.StartDate);
        Assert.Equal(new DateTime(2026, 7, 28), service.LastScanRequest.EndDate);
    }

    private static StokAnomaliMerkeziController CreateController(
        IStockAnomalyCenterService service,
        int warehouseNo,
        IReadOnlyCollection<string>? roles = null,
        IReadOnlyCollection<string>? permissions = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "11111111-1111-1111-1111-111111111111"),
            new("warehouse_no", warehouseNo.ToString())
        };

        claims.AddRange((roles ?? []).Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange((permissions ?? []).Select(permission => new Claim("permission", permission)));

        return new StokAnomaliMerkeziController(service)
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

    private sealed class CapturingStockAnomalyCenterService : IStockAnomalyCenterService
    {
        public StockAnomalyListRequest? LastListRequest { get; private set; }
        public StockAnomalyScanRequest? LastScanRequest { get; private set; }
        public StockAnomalyProductManagerListRequest? LastProductManagerRequest { get; private set; }

        public Task<StockAnomalyListResponse> ListAsync(
            StockAnomalyListRequest request,
            CancellationToken cancellationToken)
        {
            LastListRequest = request;

            return Task.FromResult(new StockAnomalyListResponse(
                0,
                new StockAnomalySummaryDto(0, 0, 0, 0, 0, 0),
                []));
        }

        public Task<StockAnomalyDetailDto> GetAsync(
            Guid id,
            int? allowedWarehouseNo,
            CancellationToken cancellationToken) =>
            Task.FromResult(CreateDetail(id));

        public Task<StockAnomalyScanResponse> ScanAsync(
            StockAnomalyScanRequest request,
            CancellationToken cancellationToken)
        {
            LastScanRequest = request;

            var now = new DateTime(2026, 7, 28, 9, 0, 0, DateTimeKind.Utc);
            return Task.FromResult(new StockAnomalyScanResponse(now, now, 0, []));
        }

        public Task<StockAnomalyDetailDto> ChangeStatusAsync(
            ChangeStockAnomalyStatusRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(CreateDetail(request.Id));

        public Task<IReadOnlyCollection<StockAnomalyProductManagerDto>> ListProductManagersAsync(
            StockAnomalyProductManagerListRequest request,
            CancellationToken cancellationToken)
        {
            LastProductManagerRequest = request;
            return Task.FromResult<IReadOnlyCollection<StockAnomalyProductManagerDto>>([]);
        }

        private static StockAnomalyDetailDto CreateDetail(Guid id) =>
            new(
                id,
                "test",
                StockAnomalyType.NegativeStock.ToString(),
                StockAnomalySeverity.High.ToString(),
                StockAnomalyStatus.Open.ToString(),
                110,
                null,
                "TEST BRANCH",
                null,
                "STK-001",
                "Test Stock",
                null,
                null,
                null,
                null,
                null,
                null,
                -1,
                0,
                -1,
                null,
                null,
                "Test anomaly",
                null,
                null,
                new DateTime(2026, 7, 28, 9, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 7, 28, 9, 0, 0, DateTimeKind.Utc),
                null,
                []);
    }
}
