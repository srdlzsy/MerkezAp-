using System.Security.Claims;
using FurpaMerkezApi.Application.Modules.Home.DepoOncelikleri;
using FurpaMerkezApi.WebApi.Controllers;
using FurpaMerkezApi.WebApi.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Modules.Home;

public sealed class HomeWarehousePrioritiesControllerTests
{
    [Fact]
    public async Task Get_UsesCurrentWarehouseForRegularUser()
    {
        var service = new CapturingHomeWarehousePrioritiesService();
        var controller = CreateController(service, warehouseNo: 101, warehouseName: "TEST BRANCH");

        var result = await controller.Get(new HomeWarehousePrioritiesHttpRequest(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<HomeWarehousePrioritiesDto>(ok.Value);
        Assert.NotNull(service.LastRequest);
        Assert.Equal(101, service.LastRequest.WarehouseNo);
        Assert.Equal("TEST BRANCH", service.LastRequest.WarehouseName);
    }

    [Fact]
    public async Task Get_AllowsAdminToSelectWarehouse()
    {
        var service = new CapturingHomeWarehousePrioritiesService();
        var controller = CreateController(
            service,
            warehouseNo: 0,
            warehouseName: "MERKEZ",
            roles: ["Administrator"]);

        await controller.Get(
            new HomeWarehousePrioritiesHttpRequest
            {
                WarehouseNo = 12,
                Date = new DateOnly(2026, 7, 24)
            },
            CancellationToken.None);

        Assert.NotNull(service.LastRequest);
        Assert.Equal(12, service.LastRequest.WarehouseNo);
        Assert.Null(service.LastRequest.WarehouseName);
        Assert.Equal(new DateOnly(2026, 7, 24), service.LastRequest.Date);
    }

    [Fact]
    public async Task Get_RejectsDifferentWarehouseForRegularUser()
    {
        var service = new CapturingHomeWarehousePrioritiesService();
        var controller = CreateController(service, warehouseNo: 101, warehouseName: "TEST BRANCH");

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            controller.Get(
                new HomeWarehousePrioritiesHttpRequest
                {
                    WarehouseNo = 202
                },
                CancellationToken.None));
    }

    private static HomeWarehousePrioritiesController CreateController(
        IHomeWarehousePrioritiesService service,
        int warehouseNo,
        string warehouseName,
        IReadOnlyCollection<string>? roles = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "11111111-1111-1111-1111-111111111111"),
            new(ClaimTypes.Name, "test.user"),
            new("warehouse_no", warehouseNo.ToString()),
            new("warehouse_name", warehouseName)
        };

        claims.AddRange((roles ?? []).Select(role => new Claim(ClaimTypes.Role, role)));

        return new HomeWarehousePrioritiesController(service)
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

    private sealed class CapturingHomeWarehousePrioritiesService : IHomeWarehousePrioritiesService
    {
        public HomeWarehousePrioritiesRequest? LastRequest { get; private set; }

        public Task<HomeWarehousePrioritiesDto> GetAsync(
            HomeWarehousePrioritiesRequest request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;

            return Task.FromResult(new HomeWarehousePrioritiesDto(
                request.Date,
                new DateTime(2026, 7, 24, 9, 0, 0, DateTimeKind.Utc),
                request.WarehouseNo,
                request.WarehouseName ?? "Depo",
                "healthy",
                "Bugun acil oncelik yok",
                [],
                [],
                []));
        }
    }
}
