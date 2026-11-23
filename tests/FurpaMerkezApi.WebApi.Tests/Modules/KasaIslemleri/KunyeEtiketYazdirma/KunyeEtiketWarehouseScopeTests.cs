using System.Security.Claims;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri.Tags;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KunyeEtiketYazdirma;
using FurpaMerkezApi.Application.Security;
using FurpaMerkezApi.WebApi.Controllers.Modules.KasaIslemleri.KunyeEtiketYazdirma;
using FurpaMerkezApi.WebApi.Controllers.Modules.KasaIslemleri.ManavKunyeEtiketYazdirma;
using FurpaMerkezApi.WebApi.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Modules.KasaIslemleri.KunyeEtiketYazdirma;

public sealed class KunyeEtiketWarehouseScopeTests
{
    [Fact]
    public async Task KunyeList_UsesRequestedWarehouse_ForAllWarehousesUser()
    {
        var useCase = new RecordingLabelTagsUseCase();
        var controller = ConfigureController(
            new KunyeEtiketYazdirmaController(useCase),
            warehouseNo: 101,
            "kasa-islemleri.kunye-etiket-yazdirma.all-warehouses");

        await controller.List(
            new KunyeLabelTagListHttpRequest
            {
                DateToGet = new DateTime(2026, 11, 23),
                WarehouseNo = 120
            },
            CancellationToken.None);

        Assert.Equal(120, useCase.LastRequest?.WarehouseNo);
    }

    [Fact]
    public async Task KunyeList_RejectsDifferentWarehouse_WithoutAllWarehousesPermission()
    {
        var controller = ConfigureController(
            new KunyeEtiketYazdirmaController(new RecordingLabelTagsUseCase()),
            warehouseNo: 101);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => controller.List(
            new KunyeLabelTagListHttpRequest
            {
                DateToGet = new DateTime(2026, 11, 23),
                WarehouseNo = 120
            },
            CancellationToken.None));
    }

    [Fact]
    public async Task ManavList_UsesRequestedWarehouse_ForAllWarehousesUser()
    {
        var useCase = new RecordingKunyeLabelTagsUseCase();
        var controller = ConfigureController(
            new ManavKunyeEtiketYazdirmaController(useCase, new EmptyManavCodeListUseCase()),
            warehouseNo: 101,
            "kasa-islemleri.manav-kunye-etiket-yazdirma.all-warehouses");

        await controller.List(
            new ManavKunyeDetailedLabelTagListHttpRequest
            {
                WarehouseNo = 120,
                DateToGet = new DateTime(2026, 11, 23)
            },
            CancellationToken.None);

        Assert.Equal(120, useCase.LastRequest?.WarehouseNo);
    }

    private static TController ConfigureController<TController>(
        TController controller,
        int warehouseNo,
        params string[] permissions)
        where TController : ControllerBase
    {
        var claims = new List<Claim>
        {
            new("warehouse_no", warehouseNo.ToString())
        };

        claims.AddRange(permissions.Select(permission =>
            new Claim(AuthorizationConstants.PermissionClaimType, permission)));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
            }
        };

        return controller;
    }

    private sealed class RecordingLabelTagsUseCase : IListLabelTagsUseCase
    {
        public LabelTagListRequest? LastRequest { get; private set; }

        public Task<IReadOnlyCollection<LabelTagDto>> ExecuteAsync(
            LabelTagListRequest request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult<IReadOnlyCollection<LabelTagDto>>([]);
        }
    }

    private sealed class RecordingKunyeLabelTagsUseCase : IListKunyeLabelTagsUseCase
    {
        public KunyeLabelTagListRequest? LastRequest { get; private set; }

        public Task<IReadOnlyCollection<KunyeLabelTagDto>> ExecuteAsync(
            KunyeLabelTagListRequest request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult<IReadOnlyCollection<KunyeLabelTagDto>>([]);
        }
    }

    private sealed class EmptyManavCodeListUseCase : IListManavCodeListUseCase
    {
        public Task<IReadOnlyCollection<ManavCodeListItemDto>> ExecuteAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<ManavCodeListItemDto>>([]);
    }
}
