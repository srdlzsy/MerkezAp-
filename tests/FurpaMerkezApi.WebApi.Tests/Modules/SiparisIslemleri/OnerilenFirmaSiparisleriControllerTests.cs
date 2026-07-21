using System.Security.Claims;
using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.BelgeAkisTakibi;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.OnerilenFirmaSiparisleri.List;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.VerilenFirmaSiparisleri.Create;
using FurpaMerkezApi.WebApi.Controllers.Modules.SiparisIslemleri.OnerilenFirmaSiparisleri;
using FurpaMerkezApi.WebApi.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Modules.SiparisIslemleri;

public sealed class OnerilenFirmaSiparisleriControllerTests
{
    [Fact]
    public async Task List_RejectsRequestedWarehouseForNonAdminUser()
    {
        var listUseCase = new CapturingListSuggestedCompanyOrdersUseCase();
        var controller = CreateController(listUseCase, warehouseNo: 101);
        var request = new SuggestedCompanyOrderListHttpRequest
        {
            WarehouseNo = 202,
            SupplierCode = "32000999"
        };

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            controller.List(request, CancellationToken.None));

        Assert.Null(listUseCase.LastRequest);
    }

    [Fact]
    public async Task List_UsesCurrentWarehouseWhenWarehouseIsNotRequested()
    {
        var listUseCase = new CapturingListSuggestedCompanyOrdersUseCase();
        var controller = CreateController(listUseCase, warehouseNo: 101);
        var request = new SuggestedCompanyOrderListHttpRequest
        {
            SupplierCode = "32000999"
        };

        await controller.List(request, CancellationToken.None);

        Assert.NotNull(listUseCase.LastRequest);
        Assert.Equal(101, listUseCase.LastRequest.WarehouseNo);
        Assert.Equal("32000999", listUseCase.LastRequest.SupplierCode);
    }

    private static OnerilenFirmaSiparisleriController CreateController(
        CapturingListSuggestedCompanyOrdersUseCase listUseCase,
        int warehouseNo)
    {
        var controller = new OnerilenFirmaSiparisleriController(
            listUseCase,
            new ThrowingDocumentFlowService(),
            new ThrowingCreateIssuedCompanyOrderUseCase());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = CreateUser(warehouseNo)
            }
        };

        return controller;
    }

    private static ClaimsPrincipal CreateUser(int warehouseNo) =>
        new(new ClaimsIdentity(
            [
                new Claim("warehouse_no", warehouseNo.ToString()),
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
            ],
            authenticationType: "Test"));

    private sealed class CapturingListSuggestedCompanyOrdersUseCase : IListSuggestedCompanyOrdersUseCase
    {
        public SuggestedCompanyOrderListRequest? LastRequest { get; private set; }

        public Task<IReadOnlyCollection<SuggestedCompanyOrderListItemDto>> ExecuteAsync(
            SuggestedCompanyOrderListRequest request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult<IReadOnlyCollection<SuggestedCompanyOrderListItemDto>>([]);
        }
    }

    private sealed class ThrowingDocumentFlowService : IDocumentFlowService
    {
        public Task<DocumentFlowListResponse> ListAsync(
            DocumentFlowListRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<DocumentFlowDetailDto> GetAsync(
            Guid id,
            int? allowedWarehouseNo,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task RecordAsync(
            RecordDocumentFlowRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class ThrowingCreateIssuedCompanyOrderUseCase : ICreateIssuedCompanyOrderUseCase
    {
        public Task<CreateIssuedCompanyOrderResponse> ExecuteAsync(
            CreateIssuedCompanyOrderRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
