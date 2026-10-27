using FurpaMerkezApi.Application.Modules.OperasyonIslemleri.BelgeAkisTakibi;
using FurpaMerkezApi.Application.Modules.SiparisIslemleri.Common;
using FurpaMerkezApi.Domain.Entities;
using FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;
using Xunit;

namespace FurpaMerkezApi.Infrastructure.Tests.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

public sealed class AxataOrderDocumentFlowRecorderTests
{
    [Theory]
    [InlineData("C01", DocumentFlowType.IssuedWarehouseOrder, 50, 131)]
    [InlineData("G02", DocumentFlowType.ReceivedWarehouseOrder, 131, 50)]
    public async Task RecordWarehouseOrderSentAsync_UsesPhysicalDirectionAndExpectedType(
        string movementType,
        DocumentFlowType expectedType,
        int outWarehouseNo,
        int inWarehouseNo)
    {
        var service = new RecordingDocumentFlowService();
        var recorder = new AxataOrderDocumentFlowRecorder(service);
        var detail = CreateWarehouseOrder(outWarehouseNo, inWarehouseNo);

        await recorder.RecordWarehouseOrderSentAsync(
            detail,
            movementType,
            Guid.Empty,
            CancellationToken.None);

        var request = Assert.Single(service.Requests);
        Assert.Equal(expectedType, request.DocumentType);
        Assert.Equal(outWarehouseNo, request.SourceWarehouseNo);
        Assert.Equal(inWarehouseNo, request.TargetWarehouseNo);
        Assert.Equal(DocumentFlowStep.AxataOrderSent, request.Step);
        Assert.Equal(DocumentFlowStatus.Succeeded, request.Status);
        Assert.Equal(
            DocumentFlowKeys.Create(expectedType, outWarehouseNo, "F131", 5506),
            request.FlowKey);
    }

    [Theory]
    [InlineData("C02", DocumentFlowType.ReceivedCompanyOrder)]
    [InlineData("G01", DocumentFlowType.IssuedCompanyOrder)]
    public async Task RecordCompanyOrderSentAsync_UsesExpectedType(
        string movementType,
        DocumentFlowType expectedType)
    {
        var service = new RecordingDocumentFlowService();
        var recorder = new AxataOrderDocumentFlowRecorder(service);
        var detail = CreateCompanyOrder();

        await recorder.RecordCompanyOrderSentAsync(
            detail,
            movementType,
            Guid.Empty,
            CancellationToken.None);

        var request = Assert.Single(service.Requests);
        Assert.Equal(expectedType, request.DocumentType);
        Assert.Equal(50, request.SourceWarehouseNo);
        Assert.Null(request.TargetWarehouseNo);
        Assert.Equal(DocumentFlowStep.AxataOrderSent, request.Step);
        Assert.Equal(DocumentFlowStatus.Succeeded, request.Status);
        Assert.Equal(
            DocumentFlowKeys.Create(expectedType, 50, "A", 1939),
            request.FlowKey);
    }

    private static WarehouseOrderDetailDto CreateWarehouseOrder(
        int outWarehouseNo,
        int inWarehouseNo) =>
        new(
            new WarehouseOrderHeaderDto(
                null,
                new DateTime(2026, 10, 27),
                null,
                "F131",
                5506,
                "F131.5506",
                inWarehouseNo,
                "Hedef Depo",
                outWarehouseNo,
                "Kaynak Depo",
                inWarehouseNo,
                "Hedef Depo",
                outWarehouseNo,
                "Kaynak Depo",
                2,
                10,
                0,
                10,
                0,
                false),
            Array.Empty<WarehouseOrderLineItemDto>());

    private static CompanyOrderDetailDto CreateCompanyOrder() =>
        new(
            new CompanyOrderHeaderDto(
                null,
                new DateTime(2026, 10, 27),
                null,
                "A",
                1939,
                "A.1939",
                50,
                "MERKEZ DEPO",
                "7740669327",
                "SORELLA GIDA",
                string.Empty,
                "SORELLA GIDA",
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                true,
                5,
                19740,
                0,
                19740,
                2027550,
                false),
            Array.Empty<CompanyOrderLineItemDto>());

    private sealed class RecordingDocumentFlowService : IDocumentFlowService
    {
        public List<RecordDocumentFlowRequest> Requests { get; } = [];

        public Task RecordAsync(
            RecordDocumentFlowRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.CompletedTask;
        }

        public Task<DocumentFlowListResponse> ListAsync(
            DocumentFlowListRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<DocumentFlowDetailDto> GetAsync(
            Guid id,
            int? allowedWarehouseNo,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
