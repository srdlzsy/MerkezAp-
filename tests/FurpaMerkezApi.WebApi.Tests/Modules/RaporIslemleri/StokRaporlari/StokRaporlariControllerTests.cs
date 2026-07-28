using System.Security.Claims;
using FurpaMerkezApi.Application.Modules.RaporIslemleri.StokRaporlari;
using FurpaMerkezApi.WebApi.Controllers.Modules.RaporIslemleri.StokRaporlari;
using FurpaMerkezApi.WebApi.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Modules.RaporIslemleri.StokRaporlari;

public sealed class StokRaporlariControllerTests
{
    [Fact]
    public async Task StockOnHand_UsesCurrentWarehouseForRegularUser()
    {
        var useCase = new CapturingStockReportsUseCase();
        var controller = CreateController(useCase, warehouseNo: 110);

        await controller.StockOnHand(
            new StockOnHandReportHttpRequest
            {
                ReportDate = new DateTime(2026, 7, 27),
                Search = "ELMA"
            },
            CancellationToken.None);

        Assert.NotNull(useCase.LastStockOnHandRequest);
        Assert.Equal(110, useCase.LastStockOnHandRequest.WarehouseNo);
        Assert.Equal("ELMA", useCase.LastStockOnHandRequest.Search);
    }

    [Fact]
    public async Task ProductWarehouseStock_RejectsDifferentWarehouseForRegularUser()
    {
        var useCase = new CapturingStockReportsUseCase();
        var controller = CreateController(useCase, warehouseNo: 110);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            controller.ProductWarehouseStock(
                new ProductWarehouseStockHttpRequest
                {
                    WarehouseNo = 120,
                    StockCodeOrBarcode = "153.01.0001"
                },
                CancellationToken.None));

        Assert.Null(useCase.LastProductWarehouseStockRequest);
    }

    [Fact]
    public async Task ProductWarehouseStock_AllowsAllWarehousesPermissionToQueryAllWarehouses()
    {
        var useCase = new CapturingStockReportsUseCase();
        var controller = CreateController(
            useCase,
            warehouseNo: 0,
            permissions: ["rapor-islemleri.stok-raporlari.all-warehouses"]);

        await controller.ProductWarehouseStock(
            new ProductWarehouseStockHttpRequest
            {
                StockCodeOrBarcode = "153.01.0001",
                OnlyWithStock = false
            },
            CancellationToken.None);

        Assert.NotNull(useCase.LastProductWarehouseStockRequest);
        Assert.Null(useCase.LastProductWarehouseStockRequest.WarehouseNo);
        Assert.Equal("153.01.0001", useCase.LastProductWarehouseStockRequest.StockCodeOrBarcode);
        Assert.False(useCase.LastProductWarehouseStockRequest.OnlyWithStock);
    }

    [Fact]
    public async Task ProductWarehouseStockByPath_UsesPathStockCode()
    {
        var useCase = new CapturingStockReportsUseCase();
        var controller = CreateController(useCase, warehouseNo: 110);

        await controller.ProductWarehouseStockByPath(
            "8690000000000",
            new ProductWarehouseStockByPathHttpRequest(),
            CancellationToken.None);

        Assert.NotNull(useCase.LastProductWarehouseStockRequest);
        Assert.Equal(110, useCase.LastProductWarehouseStockRequest.WarehouseNo);
        Assert.Equal("8690000000000", useCase.LastProductWarehouseStockRequest.StockCodeOrBarcode);
    }

    private static StokRaporlariController CreateController(
        IStockReportsUseCase useCase,
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

        return new StokRaporlariController(useCase)
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

    private sealed class CapturingStockReportsUseCase : IStockReportsUseCase
    {
        public StockOnHandReportRequest? LastStockOnHandRequest { get; private set; }
        public ProductWarehouseStockRequest? LastProductWarehouseStockRequest { get; private set; }

        public Task<StockOnHandReportDto> GetStockOnHandAsync(
            StockOnHandReportRequest request,
            CancellationToken cancellationToken)
        {
            LastStockOnHandRequest = request;

            return Task.FromResult(new StockOnHandReportDto(
                request.WarehouseNo,
                $"Depo {request.WarehouseNo}",
                request.ReportDate,
                0,
                0,
                0,
                []));
        }

        public Task<IReadOnlyCollection<ProductWarehouseStockDto>> GetProductWarehouseStockAsync(
            ProductWarehouseStockRequest request,
            CancellationToken cancellationToken)
        {
            LastProductWarehouseStockRequest = request;
            return Task.FromResult<IReadOnlyCollection<ProductWarehouseStockDto>>([]);
        }

        public Task<IReadOnlyCollection<StockCardDetailDto>> GetStockCardDetailsAsync(
            StockCardDetailRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<StockCardDetailDto>>([]);

        public Task<IReadOnlyCollection<StockCategoryOptionDto>> GetCategoryOptionsAsync(
            StockCategoryOptionRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<StockCategoryOptionDto>>([]);

        public Task<IReadOnlyCollection<WarehouseMissingStockDto>> GetWarehouseHasBranchMissingAsync(
            WarehouseMissingStockRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<WarehouseMissingStockDto>>([]);

        public Task<IReadOnlyCollection<WarehouseZeroStockDto>> GetWarehouseZeroStocksAsync(
            WarehouseZeroStockRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<WarehouseZeroStockDto>>([]);

        public Task<IReadOnlyCollection<StockMovementReportItemDto>> GetStockMovementsAsync(
            StockMovementReportRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<StockMovementReportItemDto>>([]);

        public Task<IReadOnlyCollection<MovementInOutComparisonDto>> GetInOutComparisonAsync(
            MovementInOutComparisonRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<MovementInOutComparisonDto>>([]);

        public Task<IReadOnlyCollection<BranchSalesReportItemDto>> GetBranchSalesAsync(
            BranchSalesReportRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<BranchSalesReportItemDto>>([]);

        public Task<IReadOnlyCollection<YearSalesComparisonItemDto>> GetYearSalesComparisonAsync(
            YearSalesComparisonRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<YearSalesComparisonItemDto>>([]);

        public Task<IReadOnlyCollection<ReturnBranchReportItemDto>> GetReturnBranchesAsync(
            ReturnBranchReportRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<ReturnBranchReportItemDto>>([]);

        public Task<IReadOnlyCollection<NotSoldProductReportItemDto>> GetNotSoldProductsAsync(
            NotSoldProductReportRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<NotSoldProductReportItemDto>>([]);

        public Task<IReadOnlyCollection<ProfitabilityReportItemDto>> GetProfitabilityAsync(
            ProfitabilityReportRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<ProfitabilityReportItemDto>>([]);

        public Task<IReadOnlyCollection<CountingComparisonReportItemDto>> GetCountingComparisonAsync(
            CountingComparisonReportRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<CountingComparisonReportItemDto>>([]);
    }
}
