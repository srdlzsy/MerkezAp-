using System.Security.Claims;
using FurpaMerkezApi.WebApi.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Security;

public sealed class DocumentDateToleranceFilterTests
{
    private static readonly DateTime FixedToday = new(2026, 11, 10);

    [Theory]
    [InlineData("2026-11-10")] // Bugün
    [InlineData("2026-11-05")] // 5 gün önce
    [InlineData("2026-10-31")] // Tam 10 gün önce (alt sınır)
    [InlineData("2026-11-11")] // Yarın (üst sınır)
    public void OnActionExecuting_AllowsDate_WithinToleranceWindow(string dateStr)
    {
        var targetDate = DateTime.Parse(dateStr);
        var request = new SampleCreateDocumentRequest
        {
            DocumentDate = targetDate,
            MovementDate = targetDate
        };
        var context = CreateContext(request, httpMethod: "POST", warehouseNo: 50);
        var filter = new DocumentDateToleranceFilter(() => FixedToday);

        filter.OnActionExecuting(context);

        Assert.Equal(targetDate, request.DocumentDate);
    }

    [Theory]
    [InlineData("2019-01-01")] // Bozuk el terminali tarihi (2019)
    [InlineData("2026-10-30")] // 11 gün önce (tolerans dışı)
    [InlineData("2020-05-15")] // Eski tarih
    public void OnActionExecuting_RejectsPastDate_Beyond10Days_ForRegularUser(string dateStr)
    {
        var targetDate = DateTime.Parse(dateStr);
        var request = new SampleCreateDocumentRequest
        {
            DocumentDate = targetDate
        };
        var context = CreateContext(request, httpMethod: "POST", warehouseNo: 50);
        var filter = new DocumentDateToleranceFilter(() => FixedToday);

        var exception = Assert.Throws<ArgumentException>(() => filter.OnActionExecuting(context));

        Assert.Contains("Belge tarihi kabul edilebilir aralıkta değil", exception.Message);
        Assert.Contains("son 10 gün ile yarın arasında olmalıdır", exception.Message);
        Assert.Contains("DocumentDate", exception.Message);
        Assert.Contains(dateStr, exception.Message);
    }

    [Theory]
    [InlineData("2026-11-12")] // 2 gün sonra
    [InlineData("2027-01-01")] // Gelecek yıl
    public void OnActionExecuting_RejectsFutureDate_BeyondTomorrow_ForRegularUser(string dateStr)
    {
        var targetDate = DateTime.Parse(dateStr);
        var request = new SampleCreateDocumentRequest
        {
            MovementDate = targetDate
        };
        var context = CreateContext(request, httpMethod: "POST", warehouseNo: 50);
        var filter = new DocumentDateToleranceFilter(() => FixedToday);

        var exception = Assert.Throws<ArgumentException>(() => filter.OnActionExecuting(context));

        Assert.Contains("Belge tarihi kabul edilebilir aralıkta değil", exception.Message);
        Assert.Contains("son 10 gün ile yarın arasında olmalıdır", exception.Message);
        Assert.Contains("MovementDate", exception.Message);
    }

    [Fact]
    public void OnActionExecuting_AllowsAnyDate_ForAdministrator()
    {
        var request = new SampleCreateDocumentRequest
        {
            DocumentDate = new DateTime(2019, 1, 1) // 2019 tarihi
        };
        var context = CreateContext(
            request,
            httpMethod: "POST",
            warehouseNo: 1,
            roles: ["Administrator"]);
        var filter = new DocumentDateToleranceFilter(() => FixedToday);

        // Admin için istisna fırlatmamalı
        filter.OnActionExecuting(context);

        Assert.Equal(new DateTime(2019, 1, 1), request.DocumentDate);
    }

    [Fact]
    public void OnActionExecuting_AllowsAnyDate_ForUserWithAllWarehousesPermission()
    {
        var request = new SampleCreateDocumentRequest
        {
            DocumentDate = new DateTime(2020, 5, 20)
        };
        var context = CreateContext(
            request,
            httpMethod: "POST",
            warehouseNo: 1,
            permissions: ["siparis-islemleri.verilen-depo-siparisleri.all-warehouses"]);
        var filter = new DocumentDateToleranceFilter(() => FixedToday);

        // All-warehouses yetkisi olan kullanıcı için istisna fırlatmamalı
        filter.OnActionExecuting(context);

        Assert.Equal(new DateTime(2020, 5, 20), request.DocumentDate);
    }

    [Fact]
    public void OnActionExecuting_Ignores_GetRequests()
    {
        var request = new SampleCreateDocumentRequest
        {
            DocumentDate = new DateTime(2019, 1, 1)
        };
        var context = CreateContext(request, httpMethod: "GET", warehouseNo: 50);
        var filter = new DocumentDateToleranceFilter(() => FixedToday);

        filter.OnActionExecuting(context);

        Assert.Equal(new DateTime(2019, 1, 1), request.DocumentDate);
    }

    [Fact]
    public void OnActionExecuting_Ignores_EndpointsWithIgnoreAttribute()
    {
        var request = new SampleCreateDocumentRequest
        {
            DocumentDate = new DateTime(2019, 1, 1)
        };
        var context = CreateContext(
            request,
            httpMethod: "POST",
            warehouseNo: 50,
            endpointMetadata: [new IgnoreDocumentDateToleranceAttribute()]);
        var filter = new DocumentDateToleranceFilter(() => FixedToday);

        filter.OnActionExecuting(context);

        Assert.Equal(new DateTime(2019, 1, 1), request.DocumentDate);
    }

    [Fact]
    public void OnActionExecuting_DeliveryDate_AllowsFutureDate_ButRejectsOldPastDate()
    {
        var requestFuture = new SampleOrderRequest
        {
            OrderDate = FixedToday,
            DeliveryDate = FixedToday.AddDays(15) // Gelecek teslimat tarihi geçerli
        };
        var contextFuture = CreateContext(requestFuture, httpMethod: "POST", warehouseNo: 50);
        var filter = new DocumentDateToleranceFilter(() => FixedToday);

        filter.OnActionExecuting(contextFuture);

        Assert.Equal(FixedToday.AddDays(15), requestFuture.DeliveryDate);

        var requestPast = new SampleOrderRequest
        {
            OrderDate = FixedToday,
            DeliveryDate = new DateTime(2019, 1, 1) // 2019 teslimat tarihi geçersiz
        };
        var contextPast = CreateContext(requestPast, httpMethod: "POST", warehouseNo: 50);

        var exception = Assert.Throws<ArgumentException>(() => filter.OnActionExecuting(contextPast));
        Assert.Contains("Teslim tarihi geçmiş bir tarih olamaz", exception.Message);
    }

    [Fact]
    public void OnActionExecuting_AllowsNullDates()
    {
        var request = new SampleCreateDocumentRequest
        {
            DocumentDate = null,
            MovementDate = null
        };
        var context = CreateContext(request, httpMethod: "POST", warehouseNo: 50);
        var filter = new DocumentDateToleranceFilter(() => FixedToday);

        filter.OnActionExecuting(context);

        Assert.Null(request.DocumentDate);
    }

    private static ActionExecutingContext CreateContext(
        object request,
        string httpMethod,
        int warehouseNo,
        IReadOnlyCollection<string>? roles = null,
        IReadOnlyCollection<string>? permissions = null,
        object[]? endpointMetadata = null)
    {
        var httpContext = new DefaultHttpContext
        {
            User = CreateUser(warehouseNo, roles, permissions)
        };
        httpContext.Request.Method = httpMethod;

        var actionDescriptor = new ActionDescriptor
        {
            EndpointMetadata = endpointMetadata ?? []
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

    private sealed class SampleCreateDocumentRequest
    {
        public DateTime? DocumentDate { get; init; }
        public DateTime? MovementDate { get; init; }
    }

    private sealed class SampleOrderRequest
    {
        public DateTime? OrderDate { get; init; }
        public DateTime? DeliveryDate { get; init; }
    }
}
