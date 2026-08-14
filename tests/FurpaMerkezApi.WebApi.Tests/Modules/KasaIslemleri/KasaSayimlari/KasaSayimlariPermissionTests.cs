using System.Security.Claims;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari.Commands;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari.Files;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari.Lookups;
using FurpaMerkezApi.Application.Modules.KasaIslemleri.KasaSayimlari.Queries;
using FurpaMerkezApi.Application.Security;
using FurpaMerkezApi.WebApi.Controllers.Modules.KasaIslemleri.KasaSayimlari;
using FurpaMerkezApi.WebApi.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FurpaMerkezApi.WebApi.Tests.Modules.KasaIslemleri.KasaSayimlari;

public sealed class KasaSayimlariPermissionTests
{
    [Fact]
    public void PermissionCatalog_SplitsCashSummaryViewingAndEntryMenus()
    {
        var kasaSayimlariActions = PermissionCatalog.Definitions
            .Where(definition =>
                definition.ModuleCode == "kasa-islemleri" &&
                definition.MenuCode == "kasa-sayimlari")
            .Select(definition => definition.ActionCode)
            .Order()
            .ToArray();

        var icmalKaydiGirisiActions = PermissionCatalog.Definitions
            .Where(definition =>
                definition.ModuleCode == "kasa-islemleri" &&
                definition.MenuCode == "icmal-kaydi-girisi")
            .Select(definition => definition.ActionCode)
            .Order()
            .ToArray();

        Assert.Equal(["all-warehouses", "delete", "detail", "list", "page", "update"], kasaSayimlariActions);
        Assert.Equal(["all-warehouses", "create", "list", "page"], icmalKaydiGirisiActions);
    }

    [Theory]
    [InlineData(nameof(KasaSayimlariController.Create), "kasa-islemleri.icmal-kaydi-girisi.create")]
    [InlineData(nameof(KasaSayimlariController.UpdateDetails), "kasa-islemleri.kasa-sayimlari.update")]
    [InlineData(nameof(KasaSayimlariController.UpdateBanknotes), "kasa-islemleri.kasa-sayimlari.update")]
    [InlineData(nameof(KasaSayimlariController.UpdateGiftChecks), "kasa-islemleri.kasa-sayimlari.update")]
    [InlineData(nameof(KasaSayimlariController.Delete), "kasa-islemleri.kasa-sayimlari.delete")]
    [InlineData(nameof(KasaSayimlariController.UpdateSummaryDetailsLegacy), "kasa-islemleri.kasa-sayimlari.update")]
    [InlineData(nameof(KasaSayimlariController.UpdateBanknoteMovementsLegacy), "kasa-islemleri.kasa-sayimlari.update")]
    [InlineData(nameof(KasaSayimlariController.UpdateGiftCheckMovementsLegacy), "kasa-islemleri.kasa-sayimlari.update")]
    [InlineData(nameof(KasaSayimlariController.DeleteSummaryLegacy), "kasa-islemleri.kasa-sayimlari.delete")]
    public void WriteActions_SplitCreateFromListEditDeletePolicies(string methodName, string expectedPolicy)
    {
        var authorizeAttribute = typeof(KasaSayimlariController)
            .GetMethods()
            .Single(method => method.Name == methodName)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal(expectedPolicy, authorizeAttribute.Policy);
    }

    [Fact]
    public async Task Create_UsesCurrentWarehouse_WhenUserCannotAccessAllWarehousesAndWarehouseNoIsMissing()
    {
        var commandsUseCase = new CapturingCashSummaryCommandsUseCase();
        var controller = CreateController(
            new CapturingCashSummaryQueriesUseCase(),
            commandsUseCase,
            currentWarehouseNo: 50);

        await controller.Create(CreateValidCreateRequest(), CancellationToken.None);

        Assert.Equal(50, commandsUseCase.LastCreateRequest?.WarehouseNo);
    }

    [Fact]
    public async Task Create_UsesSelectedWarehouse_WhenUserCanAccessAllWarehouses()
    {
        var commandsUseCase = new CapturingCashSummaryCommandsUseCase();
        var controller = CreateController(
            new CapturingCashSummaryQueriesUseCase(),
            commandsUseCase,
            currentWarehouseNo: 1,
            permissions:
            [
                "kasa-islemleri.icmal-kaydi-girisi.all-warehouses"
            ]);

        await controller.Create(CreateValidCreateRequest(warehouseNo: 116), CancellationToken.None);

        Assert.Equal(116, commandsUseCase.LastCreateRequest?.WarehouseNo);
    }

    [Fact]
    public async Task Create_RequiresWarehouseNo_WhenUserCanAccessAllWarehouses()
    {
        var controller = CreateController(
            new CapturingCashSummaryQueriesUseCase(),
            new CapturingCashSummaryCommandsUseCase(),
            currentWarehouseNo: 1,
            permissions:
            [
                "kasa-islemleri.icmal-kaydi-girisi.all-warehouses"
            ]);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            controller.Create(CreateValidCreateRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task Create_RejectsDifferentWarehouse_WhenUserCannotAccessAllWarehouses()
    {
        var controller = CreateController(
            new CapturingCashSummaryQueriesUseCase(),
            new CapturingCashSummaryCommandsUseCase(),
            currentWarehouseNo: 50);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            controller.Create(CreateValidCreateRequest(warehouseNo: 116), CancellationToken.None));
    }

    [Theory]
    [InlineData(nameof(KasaSayimlariController.List), "kasa-islemleri.kasa-sayimlari.list")]
    [InlineData(nameof(KasaSayimlariController.Report), "kasa-islemleri.kasa-sayimlari.list")]
    [InlineData(nameof(KasaSayimlariController.Detail), "kasa-islemleri.kasa-sayimlari.detail")]
    [InlineData(nameof(KasaSayimlariController.DetailLines), "kasa-islemleri.kasa-sayimlari.detail")]
    public void ViewingActions_KeepCashSummaryViewingPolicies(string methodName, string expectedPolicy)
    {
        var authorizeAttribute = typeof(KasaSayimlariController)
            .GetMethods()
            .Single(method => method.Name == methodName)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal(expectedPolicy, authorizeAttribute.Policy);
    }

    [Fact]
    public async Task DocumentReadActions_UseWarehouseFromDocumentSerie_WhenWarehouseNoIsMissingAndUserCanAccessAllWarehouses()
    {
        var queriesUseCase = new CapturingCashSummaryQueriesUseCase();
        var controller = CreateController(queriesUseCase, currentWarehouseNo: 50, permissions:
        [
            "kasa-islemleri.kasa-sayimlari.all-warehouses"
        ]);

        await controller.DetailLines("F116.54", 2490, warehouseNo: null, CancellationToken.None);
        await controller.BanknoteMovements("F116.54", 2490, warehouseNo: null, CancellationToken.None);
        await controller.GiftCheckMovements("F116.54", 2490, warehouseNo: null, CancellationToken.None);

        Assert.Equal(116, queriesUseCase.LastDetailsRequest?.WarehouseNo);
        Assert.Equal(116, queriesUseCase.LastBanknoteMovementsRequest?.WarehouseNo);
        Assert.Equal(116, queriesUseCase.LastGiftCheckMovementsRequest?.WarehouseNo);
    }

    [Fact]
    public async Task DocumentReadActions_KeepCurrentWarehouse_WhenWarehouseNoIsMissingAndUserCannotAccessAllWarehouses()
    {
        var queriesUseCase = new CapturingCashSummaryQueriesUseCase();
        var controller = CreateController(queriesUseCase, currentWarehouseNo: 50);

        await controller.DetailLines("F116.54", 2490, warehouseNo: null, CancellationToken.None);

        Assert.Equal(50, queriesUseCase.LastDetailsRequest?.WarehouseNo);
    }

    [Fact]
    public async Task DocumentReadActions_UseWarehouseFromDocumentSerie_WhenWarehouseNoDiffersAndUserCanAccessAllWarehouses()
    {
        var queriesUseCase = new CapturingCashSummaryQueriesUseCase();
        var controller = CreateController(
            queriesUseCase,
            currentWarehouseNo: 1,
            permissions:
            [
                "kasa-islemleri.kasa-sayimlari.all-warehouses"
            ]);

        await controller.DetailLines("F116.57", 1456, warehouseNo: 1, CancellationToken.None);

        Assert.Equal(116, queriesUseCase.LastDetailsRequest?.WarehouseNo);
    }

    [Fact]
    public async Task DocumentWriteActions_UseWarehouseFromDocumentSerie_WhenWarehouseNoIsMissingAndUserCanAccessAllWarehouses()
    {
        var commandsUseCase = new CapturingCashSummaryCommandsUseCase();
        var controller = CreateController(
            new CapturingCashSummaryQueriesUseCase(),
            commandsUseCase,
            currentWarehouseNo: 50,
            permissions:
            [
                "kasa-islemleri.kasa-sayimlari.all-warehouses"
            ]);

        await controller.UpdateDetails(
            "F116.54",
            2490,
            new UpdateCashSummaryDetailsHttpRequest
            {
                Details =
                [
                    new UpdateCashSummaryDetailLineHttpRequest
                    {
                        PaymentTypeId = 1,
                        Amount = 125
                    }
                ]
            },
            CancellationToken.None);

        await controller.UpdateBanknotes(
            "F116.54",
            2490,
            new UpdateCashSummaryBanknotesHttpRequest
            {
                BanknoteMovements =
                [
                    new UpdateCashSummaryBanknoteLineHttpRequest
                    {
                        BanknoteType = 1,
                        Quantity = 1,
                        Total = 200,
                        Value = 200
                    }
                ]
            },
            CancellationToken.None);

        await controller.UpdateGiftChecks(
            "F116.54",
            2490,
            new UpdateCashSummaryGiftChecksHttpRequest
            {
                GiftCheckMovements =
                [
                    new UpdateCashSummaryGiftCheckLineHttpRequest
                    {
                        GiftCheckType = 1,
                        Quantity = 1,
                        Total = 100,
                        Value = 100
                    }
                ]
            },
            CancellationToken.None);

        await controller.Delete("F116.54", 2490, warehouseNo: null, CancellationToken.None);
        await controller.Delete("F116.57", 1456, warehouseNo: 1, CancellationToken.None);

        Assert.Equal(116, commandsUseCase.LastUpdateDetailsRequest?.WarehouseNo);
        Assert.Equal(116, commandsUseCase.LastUpdateBanknotesRequest?.WarehouseNo);
        Assert.Equal(116, commandsUseCase.LastUpdateGiftChecksRequest?.WarehouseNo);
        Assert.Equal(116, commandsUseCase.LastDeleteRequest?.WarehouseNo);
    }

    private static CreateCashSummaryHttpRequest CreateValidCreateRequest(int? warehouseNo = null) =>
        new()
        {
            WarehouseNo = warehouseNo,
            CashNo = 1,
            ZReportNo = 125,
            CashierNo = 1001,
            ManagerNo = 1002,
            ZTotalValue = 6500,
            Total = 6500,
            SummaryDate = new DateTime(2026, 4, 24),
            PaymentTypes =
            [
                new CreatePaymentTypeHttpRequest
                {
                    PaymentName = "Akbank POS",
                    PaymentTypeNo = 1,
                    AccountCode = "POS-AKBANK",
                    TerminalId = "TERM-01",
                    SlipNumber = 12,
                    AmountValue = 2500
                }
            ]
        };

    private static KasaSayimlariController CreateController(
        CapturingCashSummaryQueriesUseCase queriesUseCase,
        int currentWarehouseNo,
        IReadOnlyCollection<string>? permissions = null) =>
        CreateController(
            queriesUseCase,
            new CapturingCashSummaryCommandsUseCase(),
            currentWarehouseNo,
            permissions);

    private static KasaSayimlariController CreateController(
        CapturingCashSummaryQueriesUseCase queriesUseCase,
        CapturingCashSummaryCommandsUseCase commandsUseCase,
        int currentWarehouseNo,
        IReadOnlyCollection<string>? permissions = null)
    {
        var controller = new KasaSayimlariController(
            queriesUseCase,
            new ThrowingCashSummaryLookupsUseCase(),
            commandsUseCase,
            new ThrowingGetCashSummaryZReportTotalUseCase())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = CreateUser(currentWarehouseNo, permissions)
                }
            }
        };

        return controller;
    }

    private static ClaimsPrincipal CreateUser(
        int warehouseNo,
        IReadOnlyCollection<string>? permissions = null)
    {
        var claims = new List<Claim>
        {
            new("warehouse_no", warehouseNo.ToString())
        };

        claims.AddRange((permissions ?? []).Select(permission => new Claim("permission", permission)));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
    }

    private sealed class CapturingCashSummaryQueriesUseCase : ICashSummaryQueriesUseCase
    {
        public CashSummaryDocumentRequest? LastDetailsRequest { get; private set; }

        public CashSummaryDocumentRequest? LastBanknoteMovementsRequest { get; private set; }

        public CashSummaryDocumentRequest? LastGiftCheckMovementsRequest { get; private set; }

        public Task<IReadOnlyCollection<CashSummaryReportItemDto>> GetReportAsync(
            CashSummaryDateRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyCollection<CashSummaryListItemDto>> ListAsync(
            CashSummaryDateRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyCollection<CashSummaryDetailItemDto>> GetDetailsAsync(
            CashSummaryDocumentRequest request,
            CancellationToken cancellationToken)
        {
            LastDetailsRequest = request;

            return Task.FromResult<IReadOnlyCollection<CashSummaryDetailItemDto>>(
            [
                new CashSummaryDetailItemDto(
                    "Akbank POS",
                    1,
                    "POS-AKBANK",
                    1,
                    125,
                    "TERM-01",
                    string.Empty)
            ]);
        }

        public Task<IReadOnlyCollection<BanknoteMovementItemDto>> GetBanknoteMovementsAsync(
            CashSummaryDocumentRequest request,
            CancellationToken cancellationToken)
        {
            LastBanknoteMovementsRequest = request;

            return Task.FromResult<IReadOnlyCollection<BanknoteMovementItemDto>>([]);
        }

        public Task<IReadOnlyCollection<GiftCheckMovementItemDto>> GetGiftCheckMovementsAsync(
            CashSummaryDocumentRequest request,
            CancellationToken cancellationToken)
        {
            LastGiftCheckMovementsRequest = request;

            return Task.FromResult<IReadOnlyCollection<GiftCheckMovementItemDto>>([]);
        }
    }

    private sealed class CapturingCashSummaryCommandsUseCase : ICashSummaryCommandsUseCase
    {
        public CreateCashSummaryRequest? LastCreateRequest { get; private set; }

        public UpdateCashSummaryDetailsRequest? LastUpdateDetailsRequest { get; private set; }

        public UpdateCashSummaryBanknotesRequest? LastUpdateBanknotesRequest { get; private set; }

        public UpdateCashSummaryGiftChecksRequest? LastUpdateGiftChecksRequest { get; private set; }

        public DeleteCashSummaryRequest? LastDeleteRequest { get; private set; }

        public Task<CreateCashSummaryResponse> CreateAsync(
            CreateCashSummaryRequest request,
            CancellationToken cancellationToken)
        {
            LastCreateRequest = request;

            return Task.FromResult(new CreateCashSummaryResponse(
                $"F{request.WarehouseNo}.{request.CashNo}",
                1,
                request.SummaryDate,
                request.WarehouseNo,
                request.PaymentTypes.Count,
                request.Total,
                "MikroConnection"));
        }

        public Task<UpdateCashSummaryDetailsResponse> UpdateDetailsAsync(
            UpdateCashSummaryDetailsRequest request,
            CancellationToken cancellationToken)
        {
            LastUpdateDetailsRequest = request;

            return Task.FromResult(new UpdateCashSummaryDetailsResponse(
                request.DocumentSerie,
                request.DocumentOrderNo,
                request.Details.Count,
                request.Details.Sum(line => line.Amount)));
        }

        public Task<UpdateCashSummaryBanknotesResponse> UpdateBanknotesAsync(
            UpdateCashSummaryBanknotesRequest request,
            CancellationToken cancellationToken)
        {
            LastUpdateBanknotesRequest = request;

            return Task.FromResult(new UpdateCashSummaryBanknotesResponse(
                request.DocumentSerie,
                request.DocumentOrderNo,
                request.BanknoteMovements.Count,
                request.BanknoteMovements.Sum(line => line.Total)));
        }

        public Task<UpdateCashSummaryGiftChecksResponse> UpdateGiftChecksAsync(
            UpdateCashSummaryGiftChecksRequest request,
            CancellationToken cancellationToken)
        {
            LastUpdateGiftChecksRequest = request;

            return Task.FromResult(new UpdateCashSummaryGiftChecksResponse(
                request.DocumentSerie,
                request.DocumentOrderNo,
                request.GiftCheckMovements.Count,
                request.GiftCheckMovements.Sum(line => line.Total)));
        }

        public Task<DeleteCashSummaryResponse> DeleteAsync(
            DeleteCashSummaryRequest request,
            CancellationToken cancellationToken)
        {
            LastDeleteRequest = request;

            return Task.FromResult(new DeleteCashSummaryResponse(
                request.DocumentSerie,
                request.DocumentOrderNo,
                1,
                1,
                0,
                1));
        }
    }

    private sealed class ThrowingCashSummaryLookupsUseCase : ICashSummaryLookupsUseCase
    {
        public Task<IReadOnlyCollection<CashierItemDto>> GetCashierAndManagerAsync(
            CashierPairRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyCollection<CashRegistryItemDto>> GetCashRegistriesAsync(
            CashRegistryRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CashRegisterDetailDto?> GetCashRegisterDetailAsync(
            CashRegisterLookupRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyCollection<CashierSearchItemDto>> SearchCashiersAsync(
            CashierSearchRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyCollection<BanknoteTypeItemDto>> ListBanknoteTypesAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyCollection<GiftCheckTypeItemDto>> ListGiftCheckTypesAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyCollection<PaymentTypeItemDto>> ListBankPaymentTypesAsync(
            BankPaymentTypeRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyCollection<PaymentTypeItemDto>> ListFoodCheckPaymentTypesAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyCollection<PaymentTypeItemDto>> ListOnlineSalesPaymentTypesAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyCollection<PaymentTypeItemDto>> ListExpenseCompassPaymentTypesAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyCollection<PaymentTypeItemDto>> ListStoreExpensePaymentTypesAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyCollection<CashRegisterDetailDto>> ListOnlineCashRegistersAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class ThrowingGetCashSummaryZReportTotalUseCase : IGetCashSummaryZReportTotalUseCase
    {
        public Task<double> ExecuteAsync(
            ZReportValueRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
