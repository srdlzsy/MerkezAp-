using System.Security.Claims;
using FurpaMerkezApi.WebApi.Controllers.Modules.AramaIslemleri;
using FurpaMerkezApi.WebApi.Controllers.Modules.OperasyonIslemleri;
using FurpaMerkezApi.WebApi.Filters;
using FurpaMerkezApi.WebApi.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Security;

public sealed class WarehouseAccessFilterTests
{
    [Fact]
    public void OnActionExecuting_DoesNotScopeWarehouseLookupFilter()
    {
        var request = new WarehouseSearchHttpRequest
        {
            SearchText = "depo",
            LookupWarehouseNo = 1,
            Take = 20
        };
        var context = CreateContext(request, currentWarehouseNo: 50);
        var filter = new WarehouseAccessFilter();

        filter.OnActionExecuting(context);

        Assert.Equal(1, request.LookupWarehouseNo);
    }

    [Fact]
    public void OnActionExecuting_DoesNotDefaultWarehouseLookupFilterToCurrentWarehouse()
    {
        var request = new WarehouseSearchHttpRequest
        {
            SearchText = "depo",
            Take = 20
        };
        var context = CreateContext(request, currentWarehouseNo: 50);
        var filter = new WarehouseAccessFilter();

        filter.OnActionExecuting(context);

        Assert.Null(request.LookupWarehouseNo);
    }

    [Fact]
    public void OnActionExecuting_StillRejectsScopedWarehouseMismatch()
    {
        var request = new ScopedWarehouseRequest
        {
            WarehouseNo = 1
        };
        var context = CreateContext(request, currentWarehouseNo: 50);
        var filter = new WarehouseAccessFilter();

        Assert.Throws<ForbiddenAccessException>(() => filter.OnActionExecuting(context));
    }

    [Fact]
    public void OnActionExecuting_AllowsScopedWarehouseMismatchWithAllWarehousesPermission()
    {
        var request = new ScopedWarehouseRequest
        {
            WarehouseNo = 1
        };
        var context = CreateContext(
            request,
            currentWarehouseNo: 50,
            policy: "stok-islemleri.zayiat-fisleri.list",
            permissions: ["stok-islemleri.zayiat-fisleri.all-warehouses"]);
        var filter = new WarehouseAccessFilter();

        filter.OnActionExecuting(context);

        Assert.Equal(1, request.WarehouseNo);
    }

    [Fact]
    public void OnActionExecuting_RejectsAdministratorRoleWithoutAllWarehousesPermission()
    {
        var request = new ScopedWarehouseRequest
        {
            WarehouseNo = 1
        };
        var context = CreateContext(
            request,
            currentWarehouseNo: 50,
            policy: "stok-islemleri.zayiat-fisleri.list",
            roles: ["Administrator"]);
        var filter = new WarehouseAccessFilter();

        Assert.Throws<ForbiddenAccessException>(() => filter.OnActionExecuting(context));
    }

    [Fact]
    public void OnActionExecuting_DoesNotScopeProductDistributionTargetWarehouses()
    {
        var request = new ProductDistributionSaveHttpRequest
        {
            StockCode = "STK001",
            DistributionCenterWarehouseNo = 50,
            TotalCaseQuantity = 10,
            Lines =
            [
                new ProductDistributionSaveLineHttpRequest
                {
                    WarehouseNo = 1,
                    CaseQuantity = 4
                },
                new ProductDistributionSaveLineHttpRequest
                {
                    WarehouseNo = 114,
                    CaseQuantity = 6
                }
            ]
        };
        var context = CreateContext(request, currentWarehouseNo: 50);
        var filter = new WarehouseAccessFilter();

        filter.OnActionExecuting(context);

        Assert.Equal([1, 114], request.Lines.Select(line => line.WarehouseNo));
    }

    private static ActionExecutingContext CreateContext(
        object request,
        int currentWarehouseNo,
        string? policy = null,
        IReadOnlyCollection<string>? roles = null,
        IReadOnlyCollection<string>? permissions = null)
    {
        var httpContext = new DefaultHttpContext
        {
            User = CreateUser(currentWarehouseNo, roles, permissions)
        };
        var actionDescriptor = new ActionDescriptor
        {
            EndpointMetadata = string.IsNullOrWhiteSpace(policy)
                ? []
                :
                [
                    new AuthorizeAttribute
                    {
                        Policy = policy
                    }
                ]
        };

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            actionDescriptor,
            new ModelStateDictionary());

        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>
            {
                ["request"] = request
            },
            controller: null!);
    }

    private static ClaimsPrincipal CreateUser(
        int warehouseNo,
        IReadOnlyCollection<string>? roles,
        IReadOnlyCollection<string>? permissions)
    {
        var claims = new List<Claim>
        {
            new("warehouse_no", warehouseNo.ToString())
        };

        claims.AddRange((roles ?? []).Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange((permissions ?? []).Select(permission => new Claim("permission", permission)));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
    }

    private sealed class ScopedWarehouseRequest
    {
        public int? WarehouseNo { get; init; }
    }
}
