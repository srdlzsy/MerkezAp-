using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.UrunDagilimlari;
using FurpaMerkezApi.WebApi.Controllers.Modules.OperasyonIslemleri;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Modules.OperasyonIslemleri.UrunDagilimlari;

public sealed class UrunDagilimlariControllerRequestTests
{
    [Fact]
    public async Task Proposal_UsesTargetCaseQuantity_WhenItIsSent()
    {
        var service = new CapturingProductDistributionService();
        var controller = new UrunDagilimlariController(service);

        await controller.Proposal(
            new ProductDistributionProposalHttpRequest
            {
                StockCode = "153.01.0001",
                DistributionCenterWarehouseNo = 50,
                TotalCaseQuantity = 120,
                TargetCaseQuantity = 2100,
                AllocatedCaseQuantity = 2000
            },
            CancellationToken.None);

        Assert.NotNull(service.LastProposalRequest);
        Assert.Equal(2100, service.LastProposalRequest.TotalCaseQuantity);
    }

    [Fact]
    public async Task Balance_ForwardsLockedLinesAndTargetCaseQuantity()
    {
        var service = new CapturingProductDistributionService();
        var controller = new UrunDagilimlariController(service);

        await controller.Balance(
            new ProductDistributionBalanceHttpRequest
            {
                StockCode = "153.01.0001",
                TargetCaseQuantity = 2000,
                Lines =
                [
                    new ProductDistributionBalanceLineHttpRequest
                    {
                        WarehouseNo = 110,
                        WarehouseName = "Sube 110",
                        RegionCode = "1",
                        LastSalesQuantity = 84,
                        CurrentStockQuantity = 12,
                        CompanyAverageDailySales = 1.45,
                        BranchAverageDailySales = 2,
                        CaseQuantity = 120,
                        IsLocked = true
                    }
                ]
            },
            CancellationToken.None);

        Assert.NotNull(service.LastBalanceRequest);
        Assert.Equal(2000, service.LastBalanceRequest.TargetCaseQuantity);
        var line = Assert.Single(service.LastBalanceRequest.Lines);
        Assert.Equal(110, line.WarehouseNo);
        Assert.True(line.IsLocked);
    }

    [Fact]
    public async Task Save_ForwardsTargetAndAllocatedCaseQuantity_ForTotalValidation()
    {
        var service = new CapturingProductDistributionService();
        var controller = new UrunDagilimlariController(service);

        await controller.Save(
            new ProductDistributionSaveHttpRequest
            {
                StockCode = "153.01.0001",
                DistributionCenterWarehouseNo = 50,
                TotalCaseQuantity = 120,
                TargetCaseQuantity = 2100,
                AllocatedCaseQuantity = 2000,
                DistributedBy = "MERKEZ",
                Lines =
                [
                    new ProductDistributionSaveLineHttpRequest
                    {
                        WarehouseNo = 110,
                        CaseQuantity = 2100,
                        UnitQuantity = 25200
                    }
                ]
            },
            CancellationToken.None);

        Assert.NotNull(service.LastSaveRequest);
        Assert.Equal(120, service.LastSaveRequest.TotalCaseQuantity);
        Assert.Equal(2100, service.LastSaveRequest.TargetCaseQuantity);
        Assert.Equal(2000, service.LastSaveRequest.AllocatedCaseQuantity);
    }

    private sealed class CapturingProductDistributionService : IProductDistributionService
    {
        public ProductDistributionProposalRequest? LastProposalRequest { get; private set; }

        public ProductDistributionBalanceRequest? LastBalanceRequest { get; private set; }

        public ProductDistributionSaveRequest? LastSaveRequest { get; private set; }

        public Task<IReadOnlyCollection<ProductDistributionCenterDto>> GetDistributionCentersAsync(
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<ProductDistributionProposalDto> CreateProposalAsync(
            ProductDistributionProposalRequest request,
            CancellationToken cancellationToken)
        {
            LastProposalRequest = request;
            return Task.FromResult(CreateProposal());
        }

        public Task<ProductDistributionBalanceDto> BalanceAsync(
            ProductDistributionBalanceRequest request,
            CancellationToken cancellationToken)
        {
            LastBalanceRequest = request;
            return Task.FromResult(CreateBalance());
        }

        public Task<IReadOnlyCollection<ProductDistributionListItemDto>> ListAsync(
            ProductDistributionListRequest request,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<ProductDistributionDetailDto> GetAsync(string documentNo, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<ProductDistributionDetailDto> SaveAsync(
            ProductDistributionSaveRequest request,
            CancellationToken cancellationToken)
        {
            LastSaveRequest = request;
            return Task.FromResult(CreateDetail());
        }

        public Task<ProductDistributionDetailDto> UpdateAsync(
            string documentNo,
            ProductDistributionSaveRequest request,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<ProductDistributionNotificationDto> NotifyAsync(
            string documentNo,
            ProductDistributionNotifyRequest request,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<ProductDistributionFinalizeDto> FinalizeAsync(
            string documentNo,
            ProductDistributionFinalizeRequest request,
            CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<ProductDistributionDeleteDto> DeleteAsync(string documentNo, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        private static ProductDistributionProposalDto CreateProposal() =>
            new(
                CreateStock(),
                CreateWarehouse(),
                CreateSummary(),
                Array.Empty<ProductDistributionLineDto>(),
                Array.Empty<string>());

        private static ProductDistributionBalanceDto CreateBalance() =>
            new(
                CreateStock(),
                CreateSummary(),
                Array.Empty<ProductDistributionBalanceLineDto>(),
                Array.Empty<string>());

        private static ProductDistributionDetailDto CreateDetail() =>
            new(
                new ProductDistributionHeaderDto(
                    "1",
                    new ProductDistributionStatusDto(0, "Kaydedildi", "info"),
                    DateTime.Today,
                    null,
                    CreateStock(),
                    CreateWarehouse(),
                    null),
                CreateSummary(),
                Array.Empty<ProductDistributionLineDto>(),
                Array.Empty<ProductDistributionActionDto>());

        private static ProductDistributionSummaryDto CreateSummary() =>
            new(42, DateTime.Today, 0, 2000, 2000, 0, 0, true, "Dengeli.");

        private static ProductDistributionStockDto CreateStock() =>
            new("153.01.0001", "Test stok", null, 12, "ADET");

        private static ProductDistributionWarehouseDto CreateWarehouse() =>
            new(50, "Merkez", null);
    }
}