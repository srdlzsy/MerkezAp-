using System.ComponentModel.DataAnnotations;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;
using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.DepoMalKabulleri.Detail;
using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.DepoMalKabulleri.List;
using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.MalKabuller.Accept;
using FurpaMerkezApi.Application.Modules.MalKabulIslemleri.MalKabuller.CompanyReceiving;
using FurpaMerkezApi.Application.Modules.SevkIslemleri.DepolarArasiSevkler.Create;
using FurpaMerkezApi.Application.Modules.SevkIslemleri.Common;
using FurpaMerkezApi.Application.Modules.StokIslemleri.SayimSonuclari;
using FurpaMerkezApi.Application.Modules.StokIslemleri.SayimSonuclari.Create;
using FurpaMerkezApi.WebApi.Controllers.Modules.Common;
using FurpaMerkezApi.WebApi.Controllers.Modules.MalKabulIslemleri.DepoMalKabulleri;
using FurpaMerkezApi.WebApi.Controllers.Modules.MalKabulIslemleri.FirmaMalKabulleri;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FurpaMerkezApi.WebApi.Controllers.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

[ApiController]
[Route("api/integrations/axata-sync")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class AxataSenkronizasyonuController(
    IAxataSynchronizationService synchronizationService,
    IAxataProductSynchronizationService productSynchronizationService,
    IAxataOutboundDeliveryImportService outboundDeliveryImportService,
    IAxataG01InboundAtfImportService g01InboundAtfImportService,
    IAxataDynamicCensusImportService dynamicCensusImportService,
    IAxataIntegrationAuditService integrationAuditService,
    ICreateCompanyReceivingUseCase createCompanyReceivingUseCase,
    ICreateInterWarehouseShipmentUseCase createInterWarehouseShipmentUseCase,
    ICreateInventoryCountUseCase createInventoryCountUseCase,
    IAcceptWarehouseReceivingUseCase acceptWarehouseReceivingUseCase,
    IListPendingWarehouseReceivingsUseCase listPendingWarehouseReceivingsUseCase,
    IGetPendingWarehouseReceivingDetailUseCase getPendingWarehouseReceivingDetailUseCase)
    : ModuleMenuControllerBase(ModuleCode, ModuleName, MenuCode, MenuName)
{
    private const string ModuleCode = "entegrasyon-islemleri";
    private const string ModuleName = "EntegrasyonIslemleri";
    private const string MenuCode = "axata-senkronizasyonu";
    private const string MenuName = "AxataSenkronizasyonu";
    private const string ListPolicy = "entegrasyon-islemleri.axata-senkronizasyonu.list";
    private const string DetailPolicy = "entegrasyon-islemleri.axata-senkronizasyonu.detail";
    private const string CreatePolicy = "entegrasyon-islemleri.axata-senkronizasyonu.create";
    private const string UpdatePolicy = "entegrasyon-islemleri.axata-senkronizasyonu.update";

    [HttpGet]
    [HttpGet("status")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(AxataSynchronizationOverviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AxataSynchronizationOverviewDto>> GetOverview(CancellationToken cancellationToken) =>
        Ok(await synchronizationService.GetOverviewAsync(cancellationToken));

    [HttpGet("health")]
    [HttpGet("connection-test")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(AxataSynchronizationConnectionTestDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AxataSynchronizationConnectionTestDto>> TestConnections(
        CancellationToken cancellationToken) =>
        Ok(await synchronizationService.TestConnectionsAsync(cancellationToken));

    [HttpGet("fetch-profiles")]
    [HttpGet("profiles")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(AxataSynchronizationFetchProfilesOverviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AxataSynchronizationFetchProfilesOverviewDto>> GetFetchProfiles(
        CancellationToken cancellationToken) =>
        Ok(await synchronizationService.GetFetchProfilesAsync(cancellationToken));

    [HttpGet("live/products/preview")]
    [HttpGet("operations/product-master/preview")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(AxataProductSynchronizationPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AxataProductSynchronizationPreviewDto>> PreviewProducts(
        [FromQuery] AxataProductSynchronizationPreviewHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await productSynchronizationService.PreviewAsync(
            request.ProductCode,
            request.Take,
            cancellationToken));

    [HttpPost("live/products/dispatch")]
    [HttpPost("operations/product-master/dispatch")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(AxataProductSynchronizationExecuteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AxataProductSynchronizationExecuteDto>> DispatchProducts(
        [FromBody] AxataProductSynchronizationDispatchHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await productSynchronizationService.DispatchAsync(
            new AxataProductSynchronizationDispatchRequest(
                request.ProductCodes,
                request.Take,
                request.ContinueOnError),
            cancellationToken));

    [HttpPost("live/products/{productCode}/dispatch")]
    [HttpPost("operations/product-master/products/{productCode}/dispatch")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(AxataProductSynchronizationExecuteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AxataProductSynchronizationExecuteDto>> DispatchSingleProduct(
        [StringLength(50), MinLength(1)] string productCode,
        CancellationToken cancellationToken) =>
        Ok(await productSynchronizationService.DispatchAsync(
            new AxataProductSynchronizationDispatchRequest(
                [productCode],
                1,
                false),
            cancellationToken));

    [HttpGet("live/axata/outbound-deliveries/by-date")]
    [HttpGet("operations/outbound-shipments/by-date")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(AxataOutboundDeliveriesByDateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AxataOutboundDeliveriesByDateDto>> GetOutboundDeliveriesByDate(
        [FromQuery] AxataOutboundDeliveriesByDateHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await synchronizationService.GetOutboundDeliveriesByDateAsync(
            request.Date!.Value,
            cancellationToken));

    [HttpGet("live/audit/overview")]
    [HttpGet("audit")]
    [HttpGet("advanced/audit")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(AxataIntegrationAuditDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AxataIntegrationAuditDto>> GetLiveAuditOverview(
        [FromQuery] AxataIntegrationAuditHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await integrationAuditService.GetOverviewAsync(MapAuditRequest(request), cancellationToken));

    [HttpGet("panel")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(AxataSynchronizationPanelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AxataSynchronizationPanelDto>> GetPanel(
        [FromQuery] AxataIntegrationAuditHttpRequest request,
        CancellationToken cancellationToken)
    {
        var audit = await integrationAuditService.GetOverviewAsync(MapAuditRequest(request), cancellationToken);
        return Ok(MapPanel(audit));
    }

    [HttpGet("workbench")]
    [HttpGet("is-merkezi")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(AxataSynchronizationWorkbenchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AxataSynchronizationWorkbenchDto>> GetWorkbench(
        [FromQuery] AxataIntegrationAuditHttpRequest request,
        CancellationToken cancellationToken)
    {
        var audit = await integrationAuditService.GetOverviewAsync(MapAuditRequest(request), cancellationToken);
        return Ok(MapWorkbench(audit));
    }

    [HttpGet("tasks/{taskCode}/preview")]
    [HttpGet("advanced/tasks/{taskCode}/preview")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(AxataSynchronizationPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AxataSynchronizationPreviewDto>> PreviewTask(
        string taskCode,
        [FromQuery] int? warehouseNo,
        [FromQuery] int? take,
        CancellationToken cancellationToken) =>
        Ok(await synchronizationService.PreviewAsync(
            new AxataSynchronizationPreviewRequest(taskCode, warehouseNo, take),
            User.GetRequiredWarehouseNo(),
            cancellationToken));

    [HttpPost("jobs")]
    [HttpPost("advanced/jobs")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(AxataSynchronizationJobDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AxataSynchronizationJobDto>> QueueJob(
        [FromBody] AxataSynchronizationExecuteHttpRequest request,
        CancellationToken cancellationToken)
    {
        var response = await synchronizationService.QueueAsync(
            new AxataSynchronizationExecuteRequest(
                request.TaskCode,
                request.ExecutionMode,
                request.WarehouseNo),
            User.GetRequiredUserId(),
            User.GetRequiredWarehouseNo(),
            cancellationToken);

        return AcceptedAtAction(nameof(GetJob), new { jobId = response.JobId }, response);
    }

    [HttpPost("tasks/{taskCode}/execute")]
    [HttpPost("advanced/tasks/{taskCode}/execute")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(AxataSynchronizationJobDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AxataSynchronizationJobDto>> ExecuteTask(
        string taskCode,
        [FromBody] AxataSynchronizationExecuteTaskHttpRequest request,
        CancellationToken cancellationToken)
    {
        var response = await synchronizationService.QueueAsync(
            new AxataSynchronizationExecuteRequest(
                taskCode,
                request.ExecutionMode,
                request.WarehouseNo),
            User.GetRequiredUserId(),
            User.GetRequiredWarehouseNo(),
            cancellationToken);

        return AcceptedAtAction(nameof(GetJob), new { jobId = response.JobId }, response);
    }

    [HttpGet("jobs/{jobId:guid}")]
    [HttpGet("advanced/jobs/{jobId:guid}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(AxataSynchronizationJobDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AxataSynchronizationJobDetailDto>> GetJob(
        Guid jobId,
        CancellationToken cancellationToken) =>
        Ok(await synchronizationService.GetJobAsync(jobId, cancellationToken));

    [HttpGet("manual/tasks/{taskCode}/documents/candidates")]
    [HttpGet("operations/{taskCode}/documents/candidates")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(AxataSynchronizationManualDocumentCandidatesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AxataSynchronizationManualDocumentCandidatesDto>> ListManualDocumentCandidates(
        string taskCode,
        [FromQuery] AxataSynchronizationManualDocumentCandidatesHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await synchronizationService.ListDocumentCandidatesAsync(
            new AxataSynchronizationManualDocumentCandidatesRequest(
                taskCode,
                request.WarehouseNo,
                request.StartDate,
                request.EndDate,
                request.Skip,
                request.Take),
            User.GetRequiredWarehouseNo(),
            cancellationToken));

    [HttpPost("manual/tasks/{taskCode}/documents/preview")]
    [HttpPost("operations/{taskCode}/documents/preview")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(AxataSynchronizationManualDocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AxataSynchronizationManualDocumentDto>> PreviewManualDocument(
        string taskCode,
        [FromBody] AxataSynchronizationManualDocumentHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await synchronizationService.PreviewDocumentAsync(
            new AxataSynchronizationManualDocumentRequest(
                taskCode,
                request.WarehouseNo,
                request.DocumentSerie,
                request.DocumentOrderNo,
                request.DocumentNo,
                request.DocumentDate),
            User.GetRequiredWarehouseNo(),
            cancellationToken));

    [HttpPost("manual/tasks/{taskCode}/documents/execute")]
    [HttpPost("advanced/{taskCode}/documents/outbox")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(AxataSynchronizationManualDocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AxataSynchronizationManualDocumentDto>> ExecuteManualDocument(
        string taskCode,
        [FromBody] AxataSynchronizationManualDocumentExecuteHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await synchronizationService.ExecuteDocumentAsync(
            new AxataSynchronizationManualDocumentExecuteRequest(
                taskCode,
                request.ExecutionMode,
                request.WarehouseNo,
                request.DocumentSerie,
                request.DocumentOrderNo,
                request.DocumentNo,
                request.DocumentDate),
            User.GetRequiredUserId(),
            User.GetRequiredWarehouseNo(),
            cancellationToken));

    [HttpPost("manual/tasks/{taskCode}/documents/preview-batch")]
    [HttpPost("operations/{taskCode}/documents/preview-batch")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(AxataSynchronizationManualDocumentBatchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AxataSynchronizationManualDocumentBatchDto>> PreviewManualDocumentsBatch(
        string taskCode,
        [FromBody] AxataSynchronizationManualDocumentBatchHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await synchronizationService.PreviewDocumentsAsync(
            new AxataSynchronizationManualDocumentBatchRequest(
                taskCode,
                request.WarehouseNo,
                MapManualDocumentItems(request.Documents),
                request.ContinueOnError),
            User.GetRequiredWarehouseNo(),
            cancellationToken));

    [HttpPost("manual/tasks/{taskCode}/documents/execute-batch")]
    [HttpPost("advanced/{taskCode}/documents/outbox-batch")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(AxataSynchronizationManualDocumentBatchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AxataSynchronizationManualDocumentBatchDto>> ExecuteManualDocumentsBatch(
        string taskCode,
        [FromBody] AxataSynchronizationManualDocumentBatchExecuteHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await synchronizationService.ExecuteDocumentsAsync(
            new AxataSynchronizationManualDocumentBatchExecuteRequest(
                taskCode,
                request.ExecutionMode,
                request.WarehouseNo,
                MapManualDocumentItems(request.Documents),
                request.ContinueOnError),
            User.GetRequiredUserId(),
            User.GetRequiredWarehouseNo(),
            cancellationToken));

    [HttpPost("manual/tasks/{taskCode}/documents/dispatch")]
    [HttpPost("operations/{taskCode}/documents/dispatch")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(AxataSynchronizationManualDispatchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AxataSynchronizationManualDispatchDto>> DispatchManualDocumentLive(
        string taskCode,
        [FromBody] AxataSynchronizationManualDocumentHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await synchronizationService.DispatchDocumentLiveAsync(
            new AxataSynchronizationManualDocumentRequest(
                taskCode,
                request.WarehouseNo,
                request.DocumentSerie,
                request.DocumentOrderNo,
                request.DocumentNo,
                request.DocumentDate),
            User.GetRequiredWarehouseNo(),
            cancellationToken));

    [HttpPost("manual/tasks/{taskCode}/documents/dispatch-batch")]
    [HttpPost("operations/{taskCode}/documents/dispatch-batch")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(AxataSynchronizationManualDispatchBatchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AxataSynchronizationManualDispatchBatchDto>> DispatchManualDocumentsLiveBatch(
        string taskCode,
        [FromBody] AxataSynchronizationManualDocumentBatchHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await synchronizationService.DispatchDocumentsLiveAsync(
            new AxataSynchronizationManualDocumentBatchRequest(
                taskCode,
                request.WarehouseNo,
                MapManualDocumentItems(request.Documents),
                request.ContinueOnError),
            User.GetRequiredWarehouseNo(),
            cancellationToken));

    [HttpGet("live/axata/outbound-deliveries/preview")]
    [HttpGet("queues/outbound-deliveries")]
    [HttpGet("operations/outbound-delivery-queue/preview")]
    [HttpGet("outbound-deliveries")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(AxataOutboundDeliveryQueuePreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AxataOutboundDeliveryQueuePreviewDto>> PreviewOutboundDeliveryQueue(
        [FromQuery] AxataOutboundDeliveryQueuePreviewHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await outboundDeliveryImportService.PreviewOutboundDeliveriesAsync(
            new AxataOutboundDeliveryQueuePreviewRequest(request.MovementType, request.Take),
            cancellationToken));

    [HttpGet("live/axata/outbound-deliveries/c01/preview")]
    [HttpGet("operations/c01-shipment/preview")]
    [HttpGet("c01/preview")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(AxataOutboundDeliveryImportPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AxataOutboundDeliveryImportPreviewDto>> PreviewC01OutboundDeliveryImport(
        [FromQuery] AxataOutboundDeliveryImportPreviewHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await outboundDeliveryImportService.PreviewC01Async(
            new AxataOutboundDeliveryImportPreviewRequest(request.Take),
            cancellationToken));

    [HttpPost("live/axata/outbound-deliveries/c01/import")]
    [HttpPost("operations/c01-shipment/import")]
    [HttpPost("c01/import")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(AxataOutboundDeliveryImportExecuteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AxataOutboundDeliveryImportExecuteDto>> ExecuteC01OutboundDeliveryImport(
        [FromBody] AxataOutboundDeliveryImportExecuteHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await outboundDeliveryImportService.ExecuteC01Async(
            new AxataOutboundDeliveryImportExecuteRequest(
                request.Take,
                request.ContinueOnError,
                request.Acknowledge,
                request.DateMode,
                request.MovementDate,
                request.DocumentDate),
            User.GetRequiredUserId(),
            cancellationToken));

    [HttpGet("live/axata/outbound-deliveries/c01/documents/{documentSerie}/{documentOrderNo:int}/preview")]
    [HttpGet("operations/c01-shipment/documents/{documentSerie}/{documentOrderNo:int}/preview")]
    [HttpGet("c01/documents/{documentSerie}/{documentOrderNo:int}/preview")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(AxataOutboundDeliveryImportPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AxataOutboundDeliveryImportPreviewDto>> PreviewC01OutboundDeliveryDocumentImport(
        string documentSerie,
        int documentOrderNo,
        [FromQuery] string? status,
        CancellationToken cancellationToken) =>
        Ok(await outboundDeliveryImportService.PreviewC01DocumentAsync(
            new AxataOutboundDeliveryDocumentImportPreviewRequest(
                documentSerie,
                documentOrderNo,
                status),
            cancellationToken));

    [HttpPost("live/axata/outbound-deliveries/c01/documents/{documentSerie}/{documentOrderNo:int}/import")]
    [HttpPost("operations/c01-shipment/documents/{documentSerie}/{documentOrderNo:int}/import")]
    [HttpPost("c01/documents/{documentSerie}/{documentOrderNo:int}/import")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(AxataOutboundDeliveryImportExecuteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AxataOutboundDeliveryImportExecuteDto>> ExecuteC01OutboundDeliveryDocumentImport(
        string documentSerie,
        int documentOrderNo,
        [FromBody] AxataOutboundDeliveryDocumentImportExecuteHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await outboundDeliveryImportService.ExecuteC01DocumentAsync(
            new AxataOutboundDeliveryDocumentImportExecuteRequest(
                documentSerie,
                documentOrderNo,
                request.Status,
                request.Acknowledge,
                request.DateMode,
                request.MovementDate,
                request.DocumentDate),
            User.GetRequiredUserId(),
            cancellationToken));

    [HttpGet("live/axata/outbound-deliveries/c02/preview")]
    [HttpGet("operations/c02-company-shipment/preview")]
    [HttpGet("c02/preview")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(AxataOutboundDeliveryImportPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AxataOutboundDeliveryImportPreviewDto>> PreviewC02OutboundDeliveryImport(
        [FromQuery] AxataOutboundDeliveryImportPreviewHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await outboundDeliveryImportService.PreviewC02Async(
            new AxataOutboundDeliveryImportPreviewRequest(request.Take),
            cancellationToken));

    [HttpPost("live/axata/outbound-deliveries/c02/import")]
    [HttpPost("operations/c02-company-shipment/import")]
    [HttpPost("c02/import")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(AxataOutboundDeliveryImportExecuteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AxataOutboundDeliveryImportExecuteDto>> ExecuteC02OutboundDeliveryImport(
        [FromBody] AxataOutboundDeliveryImportExecuteHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await outboundDeliveryImportService.ExecuteC02Async(
            new AxataOutboundDeliveryImportExecuteRequest(
                request.Take,
                request.ContinueOnError,
                request.Acknowledge),
            User.GetRequiredUserId(),
            cancellationToken));

    [HttpGet("live/axata/outbound-deliveries/c03/preview")]
    [HttpGet("operations/c03-legacy-movement/preview")]
    [HttpGet("c03/preview")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(AxataOutboundDeliveryImportPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AxataOutboundDeliveryImportPreviewDto>> PreviewC03OutboundDeliveryImport(
        [FromQuery] AxataOutboundDeliveryImportPreviewHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await outboundDeliveryImportService.PreviewC03Async(
            new AxataOutboundDeliveryImportPreviewRequest(request.Take),
            cancellationToken));

    [HttpPost("live/axata/outbound-deliveries/c03/import")]
    [HttpPost("operations/c03-legacy-movement/import")]
    [HttpPost("c03/import")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(AxataOutboundDeliveryImportExecuteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AxataOutboundDeliveryImportExecuteDto>> ExecuteC03OutboundDeliveryImport(
        [FromBody] AxataOutboundDeliveryImportExecuteHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await outboundDeliveryImportService.ExecuteC03Async(
            new AxataOutboundDeliveryImportExecuteRequest(
                request.Take,
                request.ContinueOnError,
                request.Acknowledge),
            User.GetRequiredUserId(),
            cancellationToken));

    [HttpGet("live/axata/outbound-deliveries/c04/preview")]
    [HttpGet("operations/c04-legacy-transfer/preview")]
    [HttpGet("c04/preview")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(AxataOutboundDeliveryImportPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AxataOutboundDeliveryImportPreviewDto>> PreviewC04OutboundDeliveryImport(
        [FromQuery] AxataOutboundDeliveryImportPreviewHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await outboundDeliveryImportService.PreviewC04Async(
            new AxataOutboundDeliveryImportPreviewRequest(request.Take),
            cancellationToken));

    [HttpPost("live/axata/outbound-deliveries/c04/import")]
    [HttpPost("operations/c04-legacy-transfer/import")]
    [HttpPost("c04/import")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(AxataOutboundDeliveryImportExecuteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AxataOutboundDeliveryImportExecuteDto>> ExecuteC04OutboundDeliveryImport(
        [FromBody] AxataOutboundDeliveryImportExecuteHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await outboundDeliveryImportService.ExecuteC04Async(
            new AxataOutboundDeliveryImportExecuteRequest(
                request.Take,
                request.ContinueOnError,
                request.Acknowledge),
            User.GetRequiredUserId(),
            cancellationToken));

    [HttpGet("live/axata/inbound-deliveries/g02/preview")]
    [HttpGet("operations/g02-warehouse-receiving/preview")]
    [HttpGet("g02/preview")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(AxataOutboundDeliveryImportPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AxataOutboundDeliveryImportPreviewDto>> PreviewG02InboundDeliveryImport(
        [FromQuery] AxataOutboundDeliveryImportPreviewHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await outboundDeliveryImportService.PreviewG02Async(
            new AxataOutboundDeliveryImportPreviewRequest(request.Take),
            cancellationToken));

    [HttpPost("live/axata/inbound-deliveries/g02/import")]
    [HttpPost("operations/g02-warehouse-receiving/import")]
    [HttpPost("g02/import")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(AxataOutboundDeliveryImportExecuteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AxataOutboundDeliveryImportExecuteDto>> ExecuteG02InboundDeliveryImport(
        [FromBody] AxataOutboundDeliveryImportExecuteHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await outboundDeliveryImportService.ExecuteG02Async(
            new AxataOutboundDeliveryImportExecuteRequest(
                request.Take,
                request.ContinueOnError,
                request.Acknowledge),
            User.GetRequiredUserId(),
            cancellationToken));

    [HttpGet("live/axata/inbound-deliveries/g02/documents/{documentSerie}/{documentOrderNo:int}/preview")]
    [HttpGet("operations/g02-warehouse-receiving/documents/{documentSerie}/{documentOrderNo:int}/preview")]
    [HttpGet("g02/documents/{documentSerie}/{documentOrderNo:int}/preview")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(AxataOutboundDeliveryImportPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AxataOutboundDeliveryImportPreviewDto>> PreviewG02InboundDeliveryDocumentImport(
        string documentSerie,
        int documentOrderNo,
        [FromQuery] string? status,
        CancellationToken cancellationToken) =>
        Ok(await outboundDeliveryImportService.PreviewG02DocumentAsync(
            new AxataOutboundDeliveryDocumentImportPreviewRequest(
                documentSerie,
                documentOrderNo,
                status),
            cancellationToken));

    [HttpPost("live/axata/inbound-deliveries/g02/documents/{documentSerie}/{documentOrderNo:int}/import")]
    [HttpPost("operations/g02-warehouse-receiving/documents/{documentSerie}/{documentOrderNo:int}/import")]
    [HttpPost("g02/documents/{documentSerie}/{documentOrderNo:int}/import")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(AxataOutboundDeliveryImportExecuteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AxataOutboundDeliveryImportExecuteDto>> ExecuteG02InboundDeliveryDocumentImport(
        string documentSerie,
        int documentOrderNo,
        [FromBody] AxataOutboundDeliveryDocumentImportExecuteHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await outboundDeliveryImportService.ExecuteG02DocumentAsync(
            new AxataOutboundDeliveryDocumentImportExecuteRequest(
                documentSerie,
                documentOrderNo,
                request.Status,
                request.Acknowledge),
            User.GetRequiredUserId(),
            cancellationToken));

    [HttpGet("live/axata/inbound-atf/g01/preview")]
    [HttpGet("operations/g01-company-receiving/preview")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(AxataG01InboundAtfPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AxataG01InboundAtfPreviewDto>> PreviewG01InboundAtfImport(
        [FromQuery] AxataOutboundDeliveryImportPreviewHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await g01InboundAtfImportService.PreviewAsync(
            new AxataG01InboundAtfPreviewRequest(request.Take),
            cancellationToken));

    [HttpPost("live/axata/inbound-atf/g01/import")]
    [HttpPost("operations/g01-company-receiving/import")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(AxataG01InboundAtfExecuteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AxataG01InboundAtfExecuteDto>> ExecuteG01InboundAtfImport(
        [FromBody] AxataOutboundDeliveryImportExecuteHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await g01InboundAtfImportService.ExecuteAsync(
            new AxataG01InboundAtfExecuteRequest(
                request.Take,
                request.ContinueOnError,
                request.Acknowledge),
            User.GetRequiredUserId(),
            cancellationToken));

    [HttpGet("live/axata/dynamic-census/preview")]
    [HttpGet("operations/dynamic-census/preview")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(AxataDynamicCensusPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AxataDynamicCensusPreviewDto>> PreviewDynamicCensusImport(
        [FromQuery] AxataOutboundDeliveryImportPreviewHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await dynamicCensusImportService.PreviewAsync(
            new AxataDynamicCensusPreviewRequest(request.Take),
            cancellationToken));

    [HttpPost("live/axata/dynamic-census/import")]
    [HttpPost("operations/dynamic-census/import")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(AxataDynamicCensusExecuteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AxataDynamicCensusExecuteDto>> ExecuteDynamicCensusImport(
        [FromBody] AxataOutboundDeliveryImportExecuteHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await dynamicCensusImportService.ExecuteAsync(
            new AxataDynamicCensusExecuteRequest(
                request.Take,
                request.ContinueOnError,
                request.Acknowledge),
            User.GetRequiredUserId(),
            cancellationToken));

    [HttpPost("manual/axata/outbound-deliveries/inter-warehouse-shipments")]
    [HttpPost("recovery/outbound-deliveries/from-body")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(CreateInterWarehouseShipmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CreateInterWarehouseShipmentResponse>> CreateManualAxataOutboundDeliveryAsInterWarehouseShipment(
        [FromBody] AxataOutboundDeliveryHttpRequest request,
        CancellationToken cancellationToken)
    {
        var response = await createInterWarehouseShipmentUseCase.ExecuteAsync(
            BuildCreateInterWarehouseShipmentRequest(request),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPost("manual/axata/outbound-deliveries/inter-warehouse-shipments/batch")]
    [HttpPost("recovery/outbound-deliveries/from-body-batch")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(AxataManualOutboundDeliveryBatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AxataManualOutboundDeliveryBatchResponse>> CreateManualAxataOutboundDeliveryAsInterWarehouseShipmentBatch(
        [FromBody] AxataOutboundDeliveryBatchHttpRequest request,
        CancellationToken cancellationToken)
    {
        var result = await ExecuteBatchAsync(
            request.Items,
            request.ContinueOnError,
            BuildAxataOutboundDeliveryReference,
            item => createInterWarehouseShipmentUseCase.ExecuteAsync(
                BuildCreateInterWarehouseShipmentRequest(item),
                cancellationToken));

        return Ok(new AxataManualOutboundDeliveryBatchResponse(
            result.RequestedCount,
            result.SucceededCount,
            result.FailedCount,
            result.Results,
            result.Failures));
    }

    [HttpPost("manual/axata/inbound-atf/company-receivings")]
    [HttpPost("recovery/company-receivings/from-atf-body")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(CreateCompanyReceivingResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CreateCompanyReceivingResponse>> CreateManualAxataInboundAtfAsCompanyReceiving(
        [FromBody] AxataInboundAtfCompanyReceivingHttpRequest request,
        CancellationToken cancellationToken)
    {
        var requestedByUserId = User.GetRequiredUserId();
        var response = await createCompanyReceivingUseCase.ExecuteAsync(
            BuildCreateCompanyReceivingRequest(requestedByUserId, request),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPost("manual/axata/inbound-atf/company-receivings/batch")]
    [HttpPost("recovery/company-receivings/from-atf-body-batch")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(AxataManualIncomingCompanyReceivingBatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AxataManualIncomingCompanyReceivingBatchResponse>> CreateManualAxataInboundAtfAsCompanyReceivingBatch(
        [FromBody] AxataInboundAtfCompanyReceivingBatchHttpRequest request,
        CancellationToken cancellationToken)
    {
        var requestedByUserId = User.GetRequiredUserId();
        var result = await ExecuteBatchAsync(
            request.Items,
            request.ContinueOnError,
            BuildAxataInboundAtfReference,
            item => createCompanyReceivingUseCase.ExecuteAsync(
                BuildCreateCompanyReceivingRequest(requestedByUserId, item),
                cancellationToken));

        return Ok(new AxataManualIncomingCompanyReceivingBatchResponse(
            result.RequestedCount,
            result.SucceededCount,
            result.FailedCount,
            result.Results,
            result.Failures));
    }

    [HttpPost("manual/incoming/company-receivings")]
    [HttpPost("recovery/company-receivings/manual")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(CreateCompanyReceivingResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateCompanyReceivingResponse>> CreateManualIncomingCompanyReceiving(
        [FromBody] CreateCompanyReceivingHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = User.ResolveWarehouseNoForPolicy(request.WarehouseNo, CreatePolicy);
        var requestedByUserId = User.GetRequiredUserId();
        var response = await createCompanyReceivingUseCase.ExecuteAsync(
            BuildCreateCompanyReceivingRequest(warehouseNo, requestedByUserId, request),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPost("manual/incoming/company-receivings/batch")]
    [HttpPost("recovery/company-receivings/manual-batch")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(AxataManualIncomingCompanyReceivingBatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AxataManualIncomingCompanyReceivingBatchResponse>> CreateManualIncomingCompanyReceivingBatch(
        [FromBody] AxataManualIncomingCompanyReceivingBatchHttpRequest request,
        CancellationToken cancellationToken)
    {
        var requestedByUserId = User.GetRequiredUserId();
        var result = await ExecuteBatchAsync(
            request.Items,
            request.ContinueOnError,
            BuildCompanyReceivingReference,
            item => createCompanyReceivingUseCase.ExecuteAsync(
                BuildCreateCompanyReceivingRequest(
                    User.ResolveWarehouseNoForPolicy(item.WarehouseNo, CreatePolicy),
                    requestedByUserId,
                    item),
                cancellationToken));

        return Ok(new AxataManualIncomingCompanyReceivingBatchResponse(
            result.RequestedCount,
            result.SucceededCount,
            result.FailedCount,
            result.Results,
            result.Failures));
    }

    [HttpPost("manual/incoming/inventory-counts")]
    [HttpPost("recovery/inventory-counts/manual")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(CreateInventoryCountResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateInventoryCountResponse>> CreateManualIncomingInventoryCount(
        [FromBody] CreateInventoryCountHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = User.ResolveWarehouseNoForPolicy(request.WarehouseNo, CreatePolicy);
        var requestedByUserId = User.GetRequiredUserId();
        var response = await createInventoryCountUseCase.ExecuteAsync(
            BuildCreateInventoryCountRequest(warehouseNo, requestedByUserId, request),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPost("manual/incoming/inventory-counts/batch")]
    [HttpPost("recovery/inventory-counts/manual-batch")]
    [Authorize(Policy = CreatePolicy)]
    [ProducesResponseType(typeof(AxataManualIncomingInventoryCountBatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AxataManualIncomingInventoryCountBatchResponse>> CreateManualIncomingInventoryCountBatch(
        [FromBody] AxataManualIncomingInventoryCountBatchHttpRequest request,
        CancellationToken cancellationToken)
    {
        var requestedByUserId = User.GetRequiredUserId();
        var result = await ExecuteBatchAsync(
            request.Items,
            request.ContinueOnError,
            BuildInventoryCountReference,
            item => createInventoryCountUseCase.ExecuteAsync(
                BuildCreateInventoryCountRequest(
                    User.ResolveWarehouseNoForPolicy(item.WarehouseNo, CreatePolicy),
                    requestedByUserId,
                    item),
                cancellationToken));

        return Ok(new AxataManualIncomingInventoryCountBatchResponse(
            result.RequestedCount,
            result.SucceededCount,
            result.FailedCount,
            result.Results,
            result.Failures));
    }

    [HttpGet("manual/incoming/warehouse-receivings")]
    [HttpGet("recovery/warehouse-receivings")]
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(IReadOnlyCollection<WarehouseShippingListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyCollection<WarehouseShippingListItemDto>>> ListManualIncomingWarehouseReceivings(
        [FromQuery] WarehouseOrderDateRangeHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = User.ResolveWarehouseScopeForPolicy(request.WarehouseNo, ListPolicy);

        return Ok(await listPendingWarehouseReceivingsUseCase.ExecuteAsync(
            new WarehouseShippingListRequest(
                warehouseNo,
                request.StartDate!.Value,
                request.EndDate!.Value),
            cancellationToken));
    }

    [HttpGet("manual/incoming/warehouse-receivings/{documentSerie}/{documentOrderNo:int}")]
    [HttpGet("recovery/warehouse-receivings/{documentSerie}/{documentOrderNo:int}")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(WarehouseShippingDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<WarehouseShippingDetailDto>> GetManualIncomingWarehouseReceivingDetail(
        string documentSerie,
        int documentOrderNo,
        [FromQuery, Range(1, int.MaxValue)] int? warehouseNo,
        CancellationToken cancellationToken)
    {
        var resolvedWarehouseNo = User.ResolveWarehouseNoForPolicy(warehouseNo, DetailPolicy);

        return Ok(await getPendingWarehouseReceivingDetailUseCase.ExecuteAsync(
            new WarehouseShippingDetailRequest(
                resolvedWarehouseNo,
                documentSerie,
                documentOrderNo),
            cancellationToken));
    }

    [HttpPost("manual/incoming/warehouse-receivings/{documentSerie}/{documentOrderNo:int}/accept")]
    [HttpPost("recovery/warehouse-receivings/{documentSerie}/{documentOrderNo:int}/accept")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(AcceptWarehouseReceivingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AcceptWarehouseReceivingResponse>> AcceptManualIncomingWarehouseReceiving(
        string documentSerie,
        int documentOrderNo,
        [FromBody] AcceptWarehouseReceivingHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = User.ResolveWarehouseNoForPolicy(request.WarehouseNo, UpdatePolicy);

        return Ok(await acceptWarehouseReceivingUseCase.ExecuteAsync(
            BuildAcceptWarehouseReceivingRequest(warehouseNo, documentSerie, documentOrderNo, request),
            cancellationToken));
    }

    [HttpPost("manual/incoming/warehouse-receivings/accept-batch")]
    [HttpPost("recovery/warehouse-receivings/accept-batch")]
    [Authorize(Policy = UpdatePolicy)]
    [ProducesResponseType(typeof(AxataManualIncomingWarehouseReceivingBatchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AxataManualIncomingWarehouseReceivingBatchResponse>> AcceptManualIncomingWarehouseReceivingBatch(
        [FromBody] AxataManualIncomingWarehouseReceivingBatchHttpRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseNo = User.ResolveWarehouseNoForPolicy(null, UpdatePolicy);
        var result = await ExecuteBatchAsync(
            request.Items,
            request.ContinueOnError,
            item => $"{item.DocumentSerie}.{item.DocumentOrderNo}",
            item => acceptWarehouseReceivingUseCase.ExecuteAsync(
                BuildAcceptWarehouseReceivingRequest(
                    warehouseNo,
                    item.DocumentSerie,
                    item.DocumentOrderNo,
                    item),
                cancellationToken));

        return Ok(new AxataManualIncomingWarehouseReceivingBatchResponse(
            result.RequestedCount,
            result.SucceededCount,
            result.FailedCount,
            result.Results,
            result.Failures));
    }

    private static CreateCompanyReceivingRequest BuildCreateCompanyReceivingRequest(
        int warehouseNo,
        Guid requestedByUserId,
        CreateCompanyReceivingHttpRequest request) =>
        new(
            warehouseNo,
            requestedByUserId,
            request.ClientRequestId,
            request.CustomerCode,
            request.MovementDate,
            request.DocumentDate,
            request.DocumentNo,
            request.Deliverer,
            request.Receiver,
            request.Description,
            request.AllowOrderOverReceiving,
            request.AutoCreateReturnForPartialAcceptance,
            request.Lines
                .Select(MapCompanyReceivingLine)
                .ToArray());

    private static CreateInterWarehouseShipmentRequest BuildCreateInterWarehouseShipmentRequest(
        AxataOutboundDeliveryHttpRequest request) =>
        new(
            request.SourceWarehouseNo,
            request.TargetWarehouseNo,
            request.TransitWarehouseNo ?? 60,
            request.MovementDate ?? request.DocumentDate,
            request.DocumentDate ?? request.MovementDate,
            request.DocumentNo ?? request.AxataDeliveryNo,
            request.Description
            ?? BuildAxataOutboundDeliveryReference(request),
            request.Lines
                .Select(line => new CreateInterWarehouseShipmentLineRequest(
                    line.StockCode,
                    line.Quantity,
                    null,
                    line.UnitPrice,
                    line.UnitPointer,
                    line.Description,
                    line.PartyCode,
                    line.LotNo,
                    line.ProjectCode,
                    line.CustomerResponsibilityCenter,
                    line.ProductResponsibilityCenter))
                .ToArray());

    private static CreateCompanyReceivingRequest BuildCreateCompanyReceivingRequest(
        Guid requestedByUserId,
        AxataInboundAtfCompanyReceivingHttpRequest request) =>
        new(
            request.WarehouseNo,
            requestedByUserId,
            null,
            request.CustomerCode,
            request.MovementDate ?? request.DocumentDate,
            request.DocumentDate ?? request.MovementDate,
            request.DocumentNo ?? request.InvoiceNo ?? request.AxataOrderNo,
            request.Deliverer,
            request.Receiver,
            request.Description
            ?? BuildAxataInboundAtfReference(request),
            request.AllowOrderOverReceiving,
            true,
            request.Lines
                .Select(line => new CreateCompanyReceivingLineRequest(
                    line.StockCode,
                    line.Quantity,
                    line.Quantity,
                    line.Quantity,
                    line.UnitPrice,
                    line.UnitPointer,
                    line.LastConsumingDate,
                    null,
                    line.Description,
                    line.PartyCode,
                    line.LotNo,
                    line.ProjectCode,
                    line.CustomerResponsibilityCenter,
                    line.ProductResponsibilityCenter))
                .ToArray());

    private static CreateCompanyReceivingLineRequest MapCompanyReceivingLine(CreateCompanyReceivingLineHttpRequest line)
    {
        var dispatchQuantity = line.DispatchQuantity ?? line.Quantity ?? line.AcceptedQuantity ?? 0d;
        var acceptedQuantity = line.AcceptedQuantity ?? line.Quantity ?? dispatchQuantity;

        return new CreateCompanyReceivingLineRequest(
            line.StockCode,
            dispatchQuantity,
            dispatchQuantity,
            acceptedQuantity,
            line.UnitPrice,
            line.UnitPointer,
            line.LastConsumingDate,
            line.OrderGuid,
            line.Description,
            line.PartyCode,
            line.LotNo,
            line.ProjectCode,
            line.CustomerResponsibilityCenter,
            line.ProductResponsibilityCenter);
    }

    private static CreateInventoryCountRequest BuildCreateInventoryCountRequest(
        int warehouseNo,
        Guid requestedByUserId,
        CreateInventoryCountHttpRequest request) =>
        new(
            warehouseNo,
            requestedByUserId,
            request.ClientRequestId,
            request.Name,
            request.DocumentDate,
            request.Lines
                .Select(line => new CreateInventoryCountLineRequest(
                    line.StockCode,
                    line.Quantity,
                    line.Barcode,
                    line.UnitPointer))
                .ToArray());

    private static AcceptWarehouseReceivingRequest BuildAcceptWarehouseReceivingRequest(
        int warehouseNo,
        string documentSerie,
        int documentOrderNo,
        AcceptWarehouseReceivingHttpRequest request) =>
        new(
            warehouseNo,
            documentSerie,
            documentOrderNo,
            request.AllowDiscrepancy,
            request.Lines
                .Select(line => new AcceptWarehouseReceivingLineRequest(
                    line.MovementGuid,
                    line.ReceivedQuantity))
                .ToArray());

    private static AcceptWarehouseReceivingRequest BuildAcceptWarehouseReceivingRequest(
        int warehouseNo,
        string documentSerie,
        int documentOrderNo,
        AxataManualIncomingWarehouseReceivingBatchItemHttpRequest request) =>
        new(
            warehouseNo,
            documentSerie,
            documentOrderNo,
            request.AllowDiscrepancy,
            request.Lines
                .Select(line => new AcceptWarehouseReceivingLineRequest(
                    line.MovementGuid,
                    line.ReceivedQuantity))
                .ToArray());

    private static string BuildCompanyReceivingReference(CreateCompanyReceivingHttpRequest request) =>
        string.IsNullOrWhiteSpace(request.DocumentNo)
            ? request.CustomerCode
            : $"{request.CustomerCode} / {request.DocumentNo}";

    private static string BuildAxataOutboundDeliveryReference(AxataOutboundDeliveryHttpRequest request) =>
        string.IsNullOrWhiteSpace(request.AxataDeliveryNo)
            ? $"AXATA {request.SourceWarehouseNo}->{request.TargetWarehouseNo}"
            : request.AxataDeliveryNo;

    private static string BuildAxataInboundAtfReference(AxataInboundAtfCompanyReceivingHttpRequest request) =>
        string.IsNullOrWhiteSpace(request.AxataOrderNo)
            ? $"{request.CustomerCode} / {request.DocumentNo ?? request.InvoiceNo ?? "ATF"}"
            : $"{request.CustomerCode} / {request.AxataOrderNo}";

    private static string BuildInventoryCountReference(CreateInventoryCountHttpRequest request) =>
        request.Name
        ?? request.DocumentDate?.ToString("yyyy-MM-dd")
        ?? "inventory-count";

    private static IReadOnlyCollection<AxataSynchronizationManualDocumentRequestItem> MapManualDocumentItems(
        IReadOnlyCollection<AxataSynchronizationManualDocumentItemHttpRequest> documents)
    {
        if (documents.Count == 0)
        {
            throw new ArgumentException("At least one document must be supplied for batch manual synchronization.");
        }

        return documents
            .Select(document => new AxataSynchronizationManualDocumentRequestItem(
                document.DocumentSerie,
                document.DocumentOrderNo,
                document.DocumentNo,
                document.DocumentDate))
            .ToArray();
    }

    private static AxataIntegrationAuditRequest MapAuditRequest(AxataIntegrationAuditHttpRequest request) =>
        new(
            request.StartDate,
            request.EndDate,
            request.WarehouseNo,
            request.Take,
            request.DocumentSerie,
            request.DocumentOrderNo,
            request.Statuses);

    private static AxataSynchronizationPanelDto MapPanel(AxataIntegrationAuditDto audit)
    {
        var flow = audit.FlowOverview;
        var summary = audit.Summary;
        var unsentDocumentCount = summary.UnsentWarehouseOrderDocumentCount +
                                  summary.PartiallySentWarehouseOrderDocumentCount;

        return new AxataSynchronizationPanelDto(
            flow.Title,
            flow.State,
            flow.Severity,
            flow.Narrative,
            audit.IsInSync,
            audit.GeneratedAtUtc,
            audit.StartDate,
            audit.EndDate,
            audit.WarehouseNo,
            BuildPanelMetrics(audit, unsentDocumentCount),
            flow.Steps
                .Select(step => new AxataSynchronizationPanelFlowStepDto(
                    step.Code,
                    step.Title,
                    step.State,
                    step.Severity,
                    step.CurrentDocumentCount,
                    step.ExpectedDocumentCount,
                    step.DifferenceDocumentCount,
                    step.Description,
                    step.ListRoute))
                .ToArray(),
            audit.Operations
                .OrderBy(operation => GetSeverityRank(operation.Severity))
                .ThenByDescending(operation => operation.DocumentCount)
                .Select(operation => new AxataSynchronizationPanelActionDto(
                    operation.Code,
                    operation.Title,
                    operation.State,
                    operation.Severity,
                    operation.DocumentCount,
                    operation.LineCount,
                    operation.Quantity,
                    operation.CanExecute,
                    operation.WritesData,
                    operation.ListRoute,
                    operation.PreviewRoute,
                    operation.ExecuteRoute,
                    operation.Description))
                .ToArray(),
            audit.OrderLifecycles
                .Where(document =>
                    document.RecommendedAction.RequiresManualAction ||
                    document.RecommendedAction.CanExecute)
                .OrderBy(document => GetSeverityRank(document.RecommendedAction.Severity))
                .ThenByDescending(document => document.RecommendedAction.CanExecute)
                .ThenBy(document => document.DocumentDate)
                .ThenBy(document => document.DocumentSerie)
                .ThenBy(document => document.DocumentOrderNo)
                .Take(50)
                .Select(document => new AxataSynchronizationPanelDocumentDto(
                    document.DocumentSerie,
                    document.DocumentOrderNo,
                    document.DocumentNo,
                    document.DocumentDate,
                    document.SourceWarehouseNo,
                    document.TargetWarehouseNo,
                    document.SynchronizationState,
                    GetSynchronizationStateLabel(document.SynchronizationState),
                    document.RecommendedAction.Severity,
                    document.RecommendedAction.Code,
                    document.RecommendedAction.Title,
                    document.RecommendedAction.CanExecute,
                    document.RecommendedAction.PreviewRoute,
                    document.RecommendedAction.ExecuteRoute,
                    document.MikroOrderQuantity,
                    document.MikroDeliveredQuantity,
                    document.AxataShipmentQuantity,
                    document.MikroLinkedShipmentQuantity,
                    document.ExistingMikroShipmentLineCount,
                    document.ExistingMikroShipmentQuantity,
                    document.ExistingMikroShipmentDocumentNo,
                    BuildQuantitySummary(
                        document.MikroOrderQuantity,
                        document.MikroDeliveredQuantity,
                        document.AxataShipmentQuantity,
                        document.MikroLinkedShipmentQuantity,
                        document.ExistingMikroShipmentQuantity),
                    document.RecommendedAction.Reason))
                .ToArray(),
            BuildPanelEndpoints(),
            audit.Notes);

        IReadOnlyCollection<AxataSynchronizationPanelMetricDto> BuildPanelMetrics(
            AxataIntegrationAuditDto source,
            int notSentDocumentCount) =>
        [
            new(
                "mikro-orders",
                "Mikro siparisi",
                source.WorkflowSummary.MikroOrderDocumentCount,
                source.WorkflowSummary.MikroOrderDocumentCount == 0 ? "Info" : "Success",
                "Secilen aralikta kontrol edilen Mikro depolar arasi siparis belge sayisi."),
            new(
                "not-sent-to-axata",
                "AXATA'ya gitmemis",
                notSentDocumentCount,
                notSentDocumentCount == 0 ? "Success" : "Warning",
                "Mikro'da olup AXATA gonderim bayragi eksik veya kismi olan siparisler."),
            new(
                "waiting-axata-shipment",
                "AXATA sevki bekleyen",
                source.WorkflowSummary.WaitingForAxataShipmentDocumentCount,
                source.WorkflowSummary.WaitingForAxataShipmentDocumentCount == 0 ? "Success" : "Info",
                "AXATA'da siparisi var ama henuz pozitif sevki olusmamis belgeler."),
            new(
                "ready-to-import-mikro",
                "Mikro'ya islenecek",
                source.FlowOverview.ReadyToImportToMikroDocumentCount,
                source.FlowOverview.ReadyToImportToMikroDocumentCount == 0 ? "Success" : "Critical",
                "AXATA sevki hazir olup Mikro sevk linki eksik olan ve import edilebilecek belgeler."),
            new(
                "ack-only",
                "Sadece AXATA onayi",
                source.FlowOverview.AckOnlyDocumentCount,
                source.FlowOverview.AckOnlyDocumentCount == 0 ? "Success" : "Warning",
                "Mikro'da sevk linki var ama AXATA status henuz kapanmamis belgeler."),
            new(
                "manual-review",
                "Manuel inceleme",
                source.FlowOverview.ManualReviewDocumentCount,
                source.FlowOverview.ManualReviewDocumentCount == 0 ? "Success" : "Critical",
                "Miktar, satir veya baglanti farki nedeniyle otomatik islem onerilmeyen belgeler."),
            new(
                "fully-synchronized",
                "Tamamlanan",
                source.WorkflowSummary.FullySynchronizedDocumentCount,
                "Success",
                "Mikro siparis, AXATA siparis, AXATA sevk ve Mikro linki uyumlu belgeler.")
        ];
    }

    private static AxataSynchronizationWorkbenchDto MapWorkbench(AxataIntegrationAuditDto audit)
    {
        var panel = MapPanel(audit);

        return new AxataSynchronizationWorkbenchDto(
            "AXATA is merkezi",
            "Tum AXATA islemlerini kaybolmadan izlemek, dogru sirayla onizlemek ve gerekirse kontrollu manuel mudahale yapmak.",
            panel.State,
            panel.Severity,
            panel.Message,
            panel,
            BuildWorkbenchScreenSections(),
            BuildWorkbenchOperationGroups(panel),
            BuildWorkbenchEndpointGroups(),
            BuildWorkbenchGlossary(),
            BuildWorkbenchRules());
    }

    private static IReadOnlyCollection<AxataSynchronizationWorkbenchScreenSectionDto> BuildWorkbenchScreenSections() =>
    [
        new(
            "summary",
            "Bugunku durum",
            10,
            "panel.summaryCards",
            "Kullanici ilk bakista bekleyen, hatali, aktarilabilir ve tamamlanan belge sayilarini gorur.",
            "Kartlar az metinli ve renk kodlu olmalidir; sayiya tiklaninca ilgili aksiyon veya liste acilmalidir."),
        new(
            "flow",
            "Is akisi",
            20,
            "panel.flowSteps",
            "Mikro siparis, AXATA siparis, AXATA sevk ve Mikro donus adimlarini sirali gosterir.",
            "Adimlar zaman cizelgesi gibi okunmali; kullanici hangi adimda takildigini hemen anlamalidir."),
        new(
            "actions",
            "Yapilacak islemler",
            30,
            "operationGroups + panel.actions",
            "Normal operasyon butonlari ve manuel mudahale butonlari tek yerde gruplanir.",
            "Veri yazan aksiyonlarda once preview, sonra onay modali, sonra execute/dispatch/import uygulanmalidir."),
        new(
            "priority-documents",
            "Oncelikli belgeler",
            40,
            "panel.priorityDocuments",
            "Kritik veya calistirilabilir aksiyonu olan belgeler ilk 50 kayit olarak gosterilir.",
            "Belge satirinda sebep, onerilen aksiyon, onizle ve calistir butonlari net gorunmelidir."),
        new(
            "advanced",
            "Teknik detay",
            90,
            "endpointGroups",
            "Ham audit, job, outbox, body import ve servis detaylari burada tutulur.",
            "Normal kullanicinin ilk ekranda teknik payload, WCF operasyon adi veya raw tablo alani gormemesi gerekir.")
    ];

    private static IReadOnlyCollection<AxataSynchronizationWorkbenchOperationGroupDto> BuildWorkbenchOperationGroups(
        AxataSynchronizationPanelDto panel)
    {
        var actions = panel.Actions.ToDictionary(action => action.Code, StringComparer.OrdinalIgnoreCase);

        return
        [
            new AxataSynchronizationWorkbenchOperationGroupDto(
                "control",
                "Kontrol ve izleme",
                "Okuma",
                "Sistemin sagligini, fark analizini ve AXATA sevk tarihlerini veri yazmadan gosterir.",
                [
                    Operation(
                        "control-panel",
                        "Sade kontrol paneli",
                        "Panel",
                        "Okuma",
                        "Mikro/Furpa/AXATA",
                        "UI",
                        null,
                        "Ana ekranin butun ozetini tek response'ta verir.",
                        "Sayfa acilisinda once is merkezi, sonra gerekirse teknik detay.",
                        "Her zaman ilk cagrilacak endpoint.",
                        panel.State,
                        panel.Severity,
                        0,
                        0,
                        0d,
                        false,
                        false,
                        "None",
                        "Paneli yenile",
                        "Veri yazmaz.",
                        "/api/integrations/axata-sync/panel",
                        null,
                        null,
                        ["workbench", "panel"]),
                    OperationFromAction(
                        actions,
                        null,
                        "technical-audit",
                        "Detayli fark analizi",
                        "Audit",
                        "Okuma",
                        "Mikro + AXATA",
                        "UI",
                        null,
                        "Ham listeler, farklar ve teknik route kanitlarini dondurur.",
                        "Paneldeki sayi veya belge sebebi yetmezse teknik inceleme icin acilir.",
                        "Gelismis detay ekraninda kullanilmalidir.",
                        false,
                        "None",
                        "Detayi ac",
                        "Veri yazmaz.",
                        "/api/integrations/axata-sync/audit",
                        null,
                        null,
                        ["audit-overview"]),
                    OperationFromAction(
                        actions,
                        null,
                        "connection-health",
                        "Baglanti testi",
                        "Baglanti",
                        "Okuma",
                        "API",
                        "Mikro/Furpa/AXATA",
                        null,
                        "Mikro SQL, Furpa SQL, AXATA Main ve EXT erisimlerini test eder.",
                        "Panel hata verirse veya senkronizasyon calismiyorsa ilk bakilacak teknik kontroldur.",
                        "Baglanti problemi ayiklamak icin kullanilir.",
                        false,
                        "None",
                        "Baglantiyi test et",
                        "Veri yazmaz.",
                        "/api/integrations/axata-sync/connection-test",
                        null,
                        null,
                        ["health"]),
                    OperationFromAction(
                        actions,
                        null,
                        "axata-shipment-by-date",
                        "AXATA sevk tarihine gore liste",
                        "Tarih listesi",
                        "Okuma",
                        "AXATA",
                        "UI",
                        "C01/C02/C03/C4",
                        "AXATA ENT006 sevk basliklarini secilen AXATA tarihine gore listeler.",
                        "Bir gunde AXATA'da hangi sevkler olusmus diye bakmak icin kullanilir.",
                        "Pending filtrelemez, status kapatmaz, Mikro'ya yazmaz.",
                        false,
                        "None",
                        "Sevkleri getir",
                        "Veri yazmaz.",
                        "/api/integrations/axata-sync/operations/outbound-shipments/by-date",
                        null,
                        null,
                        ["outbound-by-date"])
                ]),
            new AxataSynchronizationWorkbenchOperationGroupDto(
                "master-data",
                "Master veri gonderimleri",
                "Mikro/Furpa -> AXATA",
                "Urun, barkod, birim ve firma master verilerini AXATA'ya gonderir.",
                [
                    OperationFromAction(
                        actions,
                        null,
                        "product-master",
                        "Urun master gonderimi",
                        "Urun master",
                        "Mikro -> AXATA",
                        "Mikro",
                        "AXATA",
                        null,
                        "Stok, barkod ve birim bilgilerini AXATA addSKUMaster paketine cevirir.",
                        "Yeni/degisen urun AXATA'da gorunmuyorsa veya toplu master tazeleme gerekiyorsa kullanilir.",
                        "Once preview, sonra secili urun/toplu dispatch.",
                        true,
                        "AXATA",
                        "AXATA'ya gonder",
                        "AXATA'ya master veri yazar.",
                        "/api/integrations/axata-sync/operations/product-master/preview",
                        "/api/integrations/axata-sync/operations/product-master/preview",
                        "/api/integrations/axata-sync/operations/product-master/dispatch",
                        ["product-preview", "product-dispatch", "product-single-dispatch"]),
                    OperationFromAction(
                        actions,
                        null,
                        "firm-master",
                        "Firma master gonderimi",
                        "Firma master",
                        "Mikro/Furpa -> AXATA",
                        "Furpa/Mikro",
                        "AXATA",
                        null,
                        "Cari hesap ve adres bilgilerini AXATA firma master formatina cevirir.",
                        "Firma/adres AXATA'da eksikse veya master tazeleme gerekiyorsa kullanilir.",
                        "Generic task preview/execute ile calisir.",
                        true,
                        "AXATA",
                        "Firma master gonder",
                        "AXATA'ya firma master yazar.",
                        "/api/integrations/axata-sync/advanced/tasks/firm-master-sync/preview",
                        "/api/integrations/axata-sync/advanced/tasks/firm-master-sync/preview",
                        "/api/integrations/axata-sync/advanced/tasks/firm-master-sync/execute",
                        ["task-preview", "task-execute"])
                ]),
            new AxataSynchronizationWorkbenchOperationGroupDto(
                "mikro-to-axata",
                "Mikro'dan AXATA'ya gonderimler",
                "Mikro -> AXATA",
                "Mikro'da olusan siparis veya mal kabul belgelerini AXATA'ya canli gonderir.",
                [
                    OperationFromAction(
                        actions,
                        "warehouse-orders-not-sent-to-axata",
                        "c01-order-dispatch",
                        "C01 depo siparisini AXATA'ya gonder",
                        "C01 siparis",
                        "Mikro -> AXATA",
                        "Mikro",
                        "AXATA",
                        "C01",
                        "Merkezden cikan depolar arasi siparisi AXATA outbound order olarak gonderir.",
                        "Mikro'da siparis var ama AXATA siparis kaydi yoksa kullanilir.",
                        "Once candidates/preview, sonra dispatch.",
                        true,
                        "AXATA",
                        "Siparisi gonder",
                        "AXATA'ya siparis yazar ve basarili olursa Mikro gonderim bayragini isaretler.",
                        "/api/integrations/axata-sync/operations/issued-warehouse-order-sync/documents/candidates",
                        "/api/integrations/axata-sync/operations/issued-warehouse-order-sync/documents/preview",
                        "/api/integrations/axata-sync/operations/issued-warehouse-order-sync/documents/dispatch",
                        ["manual-candidates", "manual-document-preview", "manual-document-dispatch", "manual-document-dispatch-batch"]),
                    OperationFromAction(
                        actions,
                        null,
                        "c02-order-dispatch",
                        "C02 firma siparisini AXATA'ya gonder",
                        "C02 siparis",
                        "Mikro -> AXATA",
                        "Mikro",
                        "AXATA",
                        "C02",
                        "Alinan firma/musteri siparisini AXATA outbound order olarak gonderir.",
                        "C02 siparis AXATA'da yoksa veya tekrar gonderim gerekiyorsa kullanilir.",
                        "Once candidates/preview, sonra dispatch.",
                        true,
                        "AXATA",
                        "C02 gonder",
                        "AXATA'ya siparis yazar.",
                        "/api/integrations/axata-sync/operations/received-company-order-sync/documents/candidates",
                        "/api/integrations/axata-sync/operations/received-company-order-sync/documents/preview",
                        "/api/integrations/axata-sync/operations/received-company-order-sync/documents/dispatch",
                        ["manual-candidates", "manual-document-preview", "manual-document-dispatch"]),
                    OperationFromAction(
                        actions,
                        null,
                        "g02-inbound-order-dispatch",
                        "G02 giris siparisini AXATA'ya gonder",
                        "G02 siparis",
                        "Mikro -> AXATA",
                        "Mikro",
                        "AXATA",
                        "G02",
                        "Merkez depoya gelen depolar arasi siparisi AXATA inbound order olarak gonderir.",
                        "G02 kabul akisi AXATA tarafinda baslamadiysa kullanilir.",
                        "Once candidates/preview, sonra dispatch.",
                        true,
                        "AXATA",
                        "G02 gonder",
                        "AXATA'ya inbound order yazar.",
                        "/api/integrations/axata-sync/operations/warehouse-inbound-order-sync/documents/candidates",
                        "/api/integrations/axata-sync/operations/warehouse-inbound-order-sync/documents/preview",
                        "/api/integrations/axata-sync/operations/warehouse-inbound-order-sync/documents/dispatch",
                        ["manual-candidates", "manual-document-preview", "manual-document-dispatch"]),
                    OperationFromAction(
                        actions,
                        null,
                        "g01-company-receiving-dispatch",
                        "G01 firma mal kabulunu AXATA'ya gonder",
                        "G01 mal kabul",
                        "Mikro -> AXATA",
                        "Mikro",
                        "AXATA",
                        "G01",
                        "Mikro firma mal kabul belgesini AXATA inbound order olarak gonderir.",
                        "Firma mal kabul AXATA tarafinda eksikse kullanilir.",
                        "Once candidates/preview, sonra dispatch.",
                        true,
                        "AXATA",
                        "G01 gonder",
                        "AXATA'ya inbound order yazar.",
                        "/api/integrations/axata-sync/operations/company-receiving-sync/documents/candidates",
                        "/api/integrations/axata-sync/operations/company-receiving-sync/documents/preview",
                        "/api/integrations/axata-sync/operations/company-receiving-sync/documents/dispatch",
                        ["manual-candidates", "manual-document-preview", "manual-document-dispatch"])
                ]),
            new AxataSynchronizationWorkbenchOperationGroupDto(
                "axata-to-mikro",
                "AXATA'dan Mikro'ya islemler",
                "AXATA -> Mikro",
                "AXATA'da olusan sevk, mal kabul ve stok duzeltmelerini Mikro'ya isler.",
                [
                    OperationFromAction(
                        actions,
                        "axata-pending-outbound-deliveries",
                        "c01-shipment-import",
                        "C01 AXATA sevkini Mikro'ya isle",
                        "C01 sevk",
                        "AXATA -> Mikro",
                        "AXATA",
                        "Mikro",
                        "C01",
                        "AXATA depo sevkini Mikro depolar arasi sevk fisine cevirir.",
                        "AXATA Status=0 C01 teslimatlar Mikro'ya dusmediyse kullanilir.",
                        "Once C01 preview, sonra import. Basarili olursa ack atilabilir.",
                        true,
                        "MikroAndAxataAck",
                        "C01 isle",
                        "Mikro'ya sevk yazar; acknowledge=true ise AXATA status kapatir.",
                        "/api/integrations/axata-sync/operations/c01-shipment/preview",
                        "/api/integrations/axata-sync/operations/c01-shipment/preview",
                        "/api/integrations/axata-sync/operations/c01-shipment/import",
                        ["c01-preview", "c01-import"]),
                    OperationFromAction(
                        actions,
                        null,
                        "c02-company-shipment-import",
                        "C02 firma sevkini Mikro'ya isle",
                        "C02 sevk",
                        "AXATA -> Mikro",
                        "AXATA",
                        "Mikro",
                        "C02",
                        "AXATA firma sevk teslimatini Mikro firma sevk hareketine cevirir.",
                        "C02 Status=0 teslimatlar Mikro'ya dusmediyse kullanilir.",
                        "Once C02 preview, sonra import.",
                        true,
                        "MikroAndAxataAck",
                        "C02 isle",
                        "Mikro'ya firma sevki yazar; acknowledge=true ise AXATA status kapatir.",
                        "/api/integrations/axata-sync/operations/c02-company-shipment/preview",
                        "/api/integrations/axata-sync/operations/c02-company-shipment/preview",
                        "/api/integrations/axata-sync/operations/c02-company-shipment/import",
                        ["c02-preview", "c02-import"]),
                    OperationFromAction(
                        actions,
                        null,
                        "c03-legacy-import",
                        "C03 legacy hareketi Mikro'ya isle",
                        "C03 legacy",
                        "AXATA -> Mikro",
                        "AXATA",
                        "Mikro",
                        "C03",
                        "AXATA legacy ozel cikis/firma iade hareketini Mikro'ya yazar.",
                        "Eski worker C03 kuyrugu birikirse kullanilir.",
                        "Once C03 preview, sonra import.",
                        true,
                        "MikroAndAxataAck",
                        "C03 isle",
                        "Mikro'ya legacy hareket yazar; acknowledge=true ise AXATA status kapatir.",
                        "/api/integrations/axata-sync/operations/c03-legacy-movement/preview",
                        "/api/integrations/axata-sync/operations/c03-legacy-movement/preview",
                        "/api/integrations/axata-sync/operations/c03-legacy-movement/import",
                        ["c03-preview", "c03-import"]),
                    OperationFromAction(
                        actions,
                        null,
                        "c04-legacy-import",
                        "C04 legacy hareketi Mikro'ya isle",
                        "C04 legacy",
                        "AXATA -> Mikro",
                        "AXATA",
                        "Mikro",
                        "C4",
                        "AXATA C4 legacy 50 -> 51 teslimatini Mikro hareketine cevirir.",
                        "Eski worker C4 kuyrugu birikirse kullanilir.",
                        "Once C04 preview, sonra import.",
                        true,
                        "MikroAndAxataAck",
                        "C04 isle",
                        "Mikro'ya legacy hareket yazar; acknowledge=true ise AXATA status kapatir.",
                        "/api/integrations/axata-sync/operations/c04-legacy-transfer/preview",
                        "/api/integrations/axata-sync/operations/c04-legacy-transfer/preview",
                        "/api/integrations/axata-sync/operations/c04-legacy-transfer/import",
                        ["c04-preview", "c04-import"]),
                    OperationFromAction(
                        actions,
                        null,
                        "g01-atf-import",
                        "G01 ATF mal kabulunu Mikro'ya isle",
                        "G01 ATF",
                        "AXATA -> Mikro",
                        "AXATA",
                        "Mikro",
                        "G01",
                        "AXATA inbound ATF satirlarini Mikro firma mal kabul hareketine cevirir.",
                        "G01 Status=0 ATF kayitlari Mikro'ya dusmediyse kullanilir.",
                        "Once G01 preview, sonra import.",
                        true,
                        "MikroAndAxataAck",
                        "G01 isle",
                        "Mikro'ya firma mal kabul yazar; acknowledge=true ise AXATA status kapatir.",
                        "/api/integrations/axata-sync/operations/g01-company-receiving/preview",
                        "/api/integrations/axata-sync/operations/g01-company-receiving/preview",
                        "/api/integrations/axata-sync/operations/g01-company-receiving/import",
                        ["g01-preview", "g01-import"]),
                    OperationFromAction(
                        actions,
                        null,
                        "g02-warehouse-receiving-import",
                        "G02 depo kabulunu Mikro'ya isle",
                        "G02 kabul",
                        "AXATA -> Mikro",
                        "AXATA",
                        "Mikro",
                        "G02",
                        "AXATA G02 kabulunu mevcut Mikro bekleyen sevk fisine uygular.",
                        "Merkez depo kabul AXATA'da var ama Mikro kabul kapanmadiysa kullanilir.",
                        "Once G02 preview, sonra import.",
                        true,
                        "MikroAndAxataAck",
                        "G02 kabul et",
                        "Mikro'daki bekleyen sevki kabul eder; acknowledge=true ise AXATA status kapatir.",
                        "/api/integrations/axata-sync/operations/g02-warehouse-receiving/preview",
                        "/api/integrations/axata-sync/operations/g02-warehouse-receiving/preview",
                        "/api/integrations/axata-sync/operations/g02-warehouse-receiving/import",
                        ["g02-preview", "g02-import"]),
                    OperationFromAction(
                        actions,
                        null,
                        "dynamic-census-import",
                        "Stok duzeltmeleri Mikro'ya isle",
                        "Stok duzeltme",
                        "AXATA -> Mikro",
                        "AXATA EXT",
                        "Mikro",
                        "DynamicCensus",
                        "AXATA vw_stok_duzeltme satirlarini Mikro stok duzeltme hareketine cevirir.",
                        "AXATA tarafinda stok duzeltme kuyrugu olustugunda kullanilir.",
                        "Once preview, sonra import.",
                        true,
                        "MikroAndAxataAck",
                        "Duzeltmeleri isle",
                        "Mikro'ya stok duzeltme yazar; acknowledge=true ise AXATA status kapatir.",
                        "/api/integrations/axata-sync/operations/dynamic-census/preview",
                        "/api/integrations/axata-sync/operations/dynamic-census/preview",
                        "/api/integrations/axata-sync/operations/dynamic-census/import",
                        ["dynamic-census-preview", "dynamic-census-import"])
                ]),
            new AxataSynchronizationWorkbenchOperationGroupDto(
                "manual-recovery",
                "Manuel kurtarma araclari",
                "Operasyonel mudahale",
                "Normal akis bozuldugunda veya elde AXATA body bilgisi varsa kullanilacak kontrollu araclar.",
                [
                    OperationFromAction(
                        actions,
                        "sent-to-axata-missing-mikro-shipment",
                        "c01-document-rescue",
                        "Eksik C01 sevki kurtar",
                        "C01 rescue",
                        "AXATA -> Mikro",
                        "AXATA",
                        "Mikro",
                        "C01",
                        "AXATA'da sevki kesilmis ama Mikro linki eksik tek belgeyi tekrar inceler ve uygunsa Mikro'ya dusurur.",
                        "Belge bazinda missing Mikro shipment gorulurse kullanilir.",
                        "Once document preview, sonra document import.",
                        true,
                        "MikroAndOptionalAxataAck",
                        "Rescue yap",
                        "Mikro'ya eksik sevk yazar; acknowledge=true ise AXATA status kapatir.",
                        "/api/integrations/axata-sync/audit#sentWarehouseOrdersMissingMikroShipments",
                        "/api/integrations/axata-sync/operations/c01-shipment/documents/{documentSerie}/{documentOrderNo}/preview",
                        "/api/integrations/axata-sync/operations/c01-shipment/documents/{documentSerie}/{documentOrderNo}/import",
                        ["c01-document-preview", "c01-document-import"]),
                    OperationFromAction(
                        actions,
                        null,
                        "g02-document-rescue",
                        "Tek G02 belge kabulunu kurtar",
                        "G02 rescue",
                        "AXATA -> Mikro",
                        "AXATA",
                        "Mikro",
                        "G02",
                        "Tek G02 belgeyi AXATA'dan belge bazinda arar ve mevcut Mikro bekleyen sevk fisine uygular.",
                        "Toplu G02 import yerine tek belge kontrol etmek gerekiyorsa kullanilir.",
                        "Once document preview, sonra document import.",
                        true,
                        "MikroAndOptionalAxataAck",
                        "G02 belgeyi kabul et",
                        "Mikro kabul yazar; acknowledge=true ise AXATA status kapatir.",
                        "/api/integrations/axata-sync/audit",
                        "/api/integrations/axata-sync/operations/g02-warehouse-receiving/documents/{documentSerie}/{documentOrderNo}/preview",
                        "/api/integrations/axata-sync/operations/g02-warehouse-receiving/documents/{documentSerie}/{documentOrderNo}/import",
                        ["g02-document-preview", "g02-document-import"]),
                    OperationFromAction(
                        actions,
                        null,
                        "manual-body-outbound",
                        "AXATA body ile depo sevki olustur",
                        "Body sevk",
                        "Manuel",
                        "UI/Operasyon",
                        "Mikro",
                        "C01/C02/C03/C4",
                        "Elde hazir AXATA outbound delivery body varsa Mikro depolar arasi sevke cevirir.",
                        "Canli AXATA fetch ile bulunamayan ama body elde olan kurtarma durumunda kullanilir.",
                        "Normal akis degil, ileri operasyon aracidir.",
                        true,
                        "Mikro",
                        "Body'den sevk olustur",
                        "Mikro'ya yeni sevk yazar.",
                        null,
                        null,
                        "/api/integrations/axata-sync/recovery/outbound-deliveries/from-body",
                        ["manual-outbound-body", "manual-outbound-body-batch"]),
                    OperationFromAction(
                        actions,
                        null,
                        "manual-incoming-company-receiving",
                        "Manuel firma mal kabul olustur",
                        "Manuel mal kabul",
                        "Manuel",
                        "UI/Operasyon",
                        "Mikro",
                        null,
                        "Serbest body ile Mikro firma mal kabul olusturur.",
                        "AXATA'dan gelen kabul verisi elle toparlandiysa kullanilir.",
                        "Normal akis degil, kontrollu kurtarma aracidir.",
                        true,
                        "Mikro",
                        "Mal kabul olustur",
                        "Mikro'ya firma mal kabul yazar.",
                        null,
                        null,
                        "/api/integrations/axata-sync/recovery/company-receivings/manual",
                        ["manual-company-receiving", "manual-company-receiving-batch"]),
                    OperationFromAction(
                        actions,
                        null,
                        "manual-warehouse-receiving-accept",
                        "Bekleyen depo kabulunu manuel tamamla",
                        "Bekleyen kabul",
                        "Manuel",
                        "Mikro",
                        "Mikro",
                        "G02",
                        "Mikro'ya dusmus ama kabulde bekleyen depo sevkini manuel kabul eder.",
                        "AXATA import disinda, mevcut bekleyen kabul fisini operasyonel olarak tamamlamak icin kullanilir.",
                        "Once bekleyen liste/detay, sonra accept.",
                        true,
                        "Mikro",
                        "Kabul et",
                        "Mikro bekleyen kabulunu kapatir.",
                        "/api/integrations/axata-sync/recovery/warehouse-receivings",
                        "/api/integrations/axata-sync/recovery/warehouse-receivings/{documentSerie}/{documentOrderNo}",
                        "/api/integrations/axata-sync/recovery/warehouse-receivings/{documentSerie}/{documentOrderNo}/accept",
                        ["manual-warehouse-receivings", "manual-warehouse-receiving-detail", "manual-warehouse-receiving-accept", "manual-warehouse-receiving-accept-batch"])
                ])
        ];

        AxataSynchronizationWorkbenchOperationDto OperationFromAction(
            IReadOnlyDictionary<string, AxataSynchronizationPanelActionDto> actionMap,
            string? actionCode,
            string code,
            string title,
            string shortTitle,
            string direction,
            string sourceSystem,
            string targetSystem,
            string? movementType,
            string purpose,
            string normalFlow,
            string whenToUse,
            bool writesData,
            string writeScope,
            string primaryButtonLabel,
            string confirmationMessage,
            string? listRoute,
            string? previewRoute,
            string? executeRoute,
            IReadOnlyCollection<string> endpointCodes)
        {
            var action = actionCode is null ? null : actionMap.GetValueOrDefault(actionCode);

            return Operation(
                code,
                title,
                shortTitle,
                direction,
                sourceSystem,
                targetSystem,
                movementType,
                purpose,
                normalFlow,
                whenToUse,
                action?.State ?? "Ready",
                action?.Severity ?? "Info",
                action?.DocumentCount ?? 0,
                action?.LineCount ?? 0,
                action?.Quantity ?? 0d,
                action?.CanExecute ?? writesData,
                writesData,
                writeScope,
                primaryButtonLabel,
                confirmationMessage,
                action?.ListRoute ?? listRoute,
                action?.PreviewRoute ?? previewRoute,
                action?.ExecuteRoute ?? executeRoute,
                endpointCodes);
        }
    }

    private static AxataSynchronizationWorkbenchOperationDto Operation(
        string code,
        string title,
        string shortTitle,
        string direction,
        string sourceSystem,
        string targetSystem,
        string? movementType,
        string purpose,
        string normalFlow,
        string whenToUse,
        string state,
        string severity,
        int documentCount,
        int lineCount,
        double quantity,
        bool canExecute,
        bool writesData,
        string writeScope,
        string primaryButtonLabel,
        string confirmationMessage,
        string? listRoute,
        string? previewRoute,
        string? executeRoute,
        IReadOnlyCollection<string> endpointCodes) =>
        new(
            code,
            title,
            shortTitle,
            direction,
            sourceSystem,
            targetSystem,
            movementType,
            purpose,
            normalFlow,
            whenToUse,
            state,
            severity,
            documentCount,
            lineCount,
            quantity,
            canExecute,
            writesData,
            writeScope,
            primaryButtonLabel,
            confirmationMessage,
            listRoute,
            previewRoute,
            executeRoute,
            endpointCodes);

    private static IReadOnlyCollection<AxataSynchronizationWorkbenchEndpointGroupDto> BuildWorkbenchEndpointGroups() =>
    [
        new(
            "main",
            "Ana ekran ve kontrol endpointleri",
            "Sayfa acilisi, baglanti testi, sade panel ve teknik audit endpointleri.",
            [
                Endpoint("overview", "Modul genel durumu", "GET", "/api/integrations/axata-sync", "Main", false, "None", "Genel durum", "Task, worker, scheduler ve son job ozetini getirir.", null, "AxataSynchronizationOverviewDto"),
                Endpoint("workbench", "Is merkezi", "GET", "/api/integrations/axata-sync/workbench", "Main", false, "None", "Is merkezini ac", "Sade panel, operasyon gruplari, endpoint katalogu ve ekran kurallarini tek response'ta verir.", "AxataIntegrationAuditHttpRequest(query)", "AxataSynchronizationWorkbenchDto"),
                Endpoint("workbench-tr", "Is merkezi Turkce alias", "GET", "/api/integrations/axata-sync/is-merkezi", "Main", false, "None", "Is merkezini ac", "Workbench endpointinin Turkce route alias'idir.", "AxataIntegrationAuditHttpRequest(query)", "AxataSynchronizationWorkbenchDto"),
                Endpoint("panel", "Sade panel", "GET", "/api/integrations/axata-sync/panel", "Main", false, "None", "Paneli yenile", "Ozet kart, akis adimi, aksiyon ve oncelikli belge listesi doner.", "AxataIntegrationAuditHttpRequest(query)", "AxataSynchronizationPanelDto"),
                Endpoint("audit-overview", "Detayli fark analizi", "GET", "/api/integrations/axata-sync/audit", "Advanced", false, "None", "Teknik detayi ac", "Ham fark listeleri, operasyon kartlari ve teknik kanitlari doner.", "AxataIntegrationAuditHttpRequest(query)", "AxataIntegrationAuditDto"),
                Endpoint("health", "Baglanti testi", "GET", "/api/integrations/axata-sync/connection-test", "Main", false, "None", "Baglantiyi test et", "Mikro SQL, Furpa SQL, AXATA Main ve AXATA EXT erisimlerini test eder.", null, "AxataSynchronizationConnectionTestDto"),
                Endpoint("fetch-profiles", "Fetch profil katalogu", "GET", "/api/integrations/axata-sync/profiles", "Advanced", false, "None", "Profilleri gor", "AXATA'dan okunabilen profil ve hareket tiplerini listeler.", null, "AxataSynchronizationFetchProfilesOverviewDto"),
                Endpoint("outbound-by-date", "AXATA sevklerini tarihe gore getir", "GET", "/api/integrations/axata-sync/operations/outbound-shipments/by-date", "Advanced", false, "None", "Sevkleri getir", "AXATA ENT006 sevk basliklarini secilen S06ITAR tarihine gore listeler.", "AxataOutboundDeliveriesByDateHttpRequest(query)", "AxataOutboundDeliveriesByDateDto")
            ]),
        new(
            "master-data",
            "Master veri endpointleri",
            "Urun/firma master preview ve canli AXATA gonderimleri.",
            [
                Endpoint("product-preview", "Urun master onizle", "GET", "/api/integrations/axata-sync/operations/product-master/preview", "Main", false, "None", "Urunleri onizle", "Mikro urun, barkod ve birim bilgisinden AXATA SKU payload onizlemesi uretir.", "AxataProductSynchronizationPreviewHttpRequest(query)", "AxataProductSynchronizationPreviewDto"),
                Endpoint("product-dispatch", "Urun master toplu gonder", "POST", "/api/integrations/axata-sync/operations/product-master/dispatch", "Main", true, "AXATA", "Urunleri gonder", "Secili veya take kadar urunu AXATA addSKUMaster operasyonuna gonderir.", "AxataProductSynchronizationDispatchHttpRequest", "AxataProductSynchronizationExecuteDto"),
                Endpoint("product-single-dispatch", "Tek urun master gonder", "POST", "/api/integrations/axata-sync/operations/product-master/products/{productCode}/dispatch", "Main", true, "AXATA", "Urunu gonder", "Tek urunu AXATA addSKUMaster operasyonuna gonderir.", null, "AxataProductSynchronizationExecuteDto")
            ]),
        new(
            "task-engine",
            "Generic task ve job endpointleri",
            "Worker/outbox/live task calistirma altyapisidir; normal UI'da gelismis bolumde tutulmalidir.",
            [
                Endpoint("task-preview", "Task preview", "GET", "/api/integrations/axata-sync/advanced/tasks/{taskCode}/preview", "Advanced", false, "None", "Task onizle", "Secili task icin payload onizleme yapar.", "taskCode + warehouseNo/take query", "AxataSynchronizationPreviewDto"),
                Endpoint("queue-job", "Job kuyruga al", "POST", "/api/integrations/axata-sync/advanced/jobs", "Advanced", true, "Queue", "Job baslat", "Secili task icin in-memory job kuyruga alir.", "AxataSynchronizationExecuteHttpRequest", "AxataSynchronizationJobDto"),
                Endpoint("task-execute", "Task execute", "POST", "/api/integrations/axata-sync/advanced/tasks/{taskCode}/execute", "Advanced", true, "QueueOrConfigured", "Task calistir", "DryRun/Outbox/Live moduna gore task'i kuyruga alir.", "AxataSynchronizationExecuteTaskHttpRequest", "AxataSynchronizationJobDto"),
                Endpoint("job-detail", "Job detayi", "GET", "/api/integrations/axata-sync/advanced/jobs/{jobId}", "Advanced", false, "None", "Job detayini gor", "Kuyruktaki veya tamamlanmis job detayini dondurur.", null, "AxataSynchronizationJobDetailDto")
            ]),
        new(
            "mikro-to-axata",
            "Mikro'dan AXATA'ya evrak gonderimi",
            "Mikro evrak adaylari, payload onizleme ve canli dispatch endpointleri.",
            [
                Endpoint("manual-candidates", "Evrak adaylarini getir", "GET", "/api/integrations/axata-sync/operations/{taskCode}/documents/candidates", "Main", false, "None", "Adaylari getir", "Manuel gonderim/kurtarma icin Mikro evrak adaylarini listeler.", "AxataSynchronizationManualDocumentCandidatesHttpRequest(query)", "AxataSynchronizationManualDocumentCandidatesDto"),
                Endpoint("manual-document-preview", "Tek evrak onizle", "POST", "/api/integrations/axata-sync/operations/{taskCode}/documents/preview", "Main", false, "None", "Onizle", "Tek Mikro evrakindan AXATA payload onizlemesi uretir.", "AxataSynchronizationManualDocumentHttpRequest", "AxataSynchronizationManualDocumentDto"),
                Endpoint("manual-document-execute", "Tek evrak dryrun/outbox", "POST", "/api/integrations/axata-sync/advanced/{taskCode}/documents/outbox", "Advanced", true, "File", "Outbox hazirla", "Tek evrak icin DryRun veya Outbox calistirir; AXATA'ya canli gonderim degildir.", "AxataSynchronizationManualDocumentExecuteHttpRequest", "AxataSynchronizationManualDocumentDto"),
                Endpoint("manual-document-preview-batch", "Toplu evrak onizle", "POST", "/api/integrations/axata-sync/operations/{taskCode}/documents/preview-batch", "Main", false, "None", "Toplu onizle", "Birden fazla Mikro evraki icin AXATA payload onizlemesi uretir.", "AxataSynchronizationManualDocumentBatchHttpRequest", "AxataSynchronizationManualDocumentBatchDto"),
                Endpoint("manual-document-execute-batch", "Toplu evrak dryrun/outbox", "POST", "/api/integrations/axata-sync/advanced/{taskCode}/documents/outbox-batch", "Advanced", true, "File", "Toplu outbox", "Birden fazla evrak icin DryRun veya Outbox calistirir.", "AxataSynchronizationManualDocumentBatchExecuteHttpRequest", "AxataSynchronizationManualDocumentBatchDto"),
                Endpoint("manual-document-dispatch", "Tek evrak AXATA'ya gonder", "POST", "/api/integrations/axata-sync/operations/{taskCode}/documents/dispatch", "Main", true, "AXATA", "AXATA'ya gonder", "Tek Mikro evrakini AXATA Main servisine canli gonderir.", "AxataSynchronizationManualDocumentHttpRequest", "AxataSynchronizationManualDispatchDto"),
                Endpoint("manual-document-dispatch-batch", "Toplu evrak AXATA'ya gonder", "POST", "/api/integrations/axata-sync/operations/{taskCode}/documents/dispatch-batch", "Main", true, "AXATA", "Toplu gonder", "Birden fazla Mikro evrakini AXATA Main servisine canli gonderir.", "AxataSynchronizationManualDocumentBatchHttpRequest", "AxataSynchronizationManualDispatchBatchDto")
            ]),
        new(
            "axata-to-mikro",
            "AXATA'dan Mikro'ya canli import",
            "AXATA kuyruklarini okur; uygun kayitlari Mikro'ya isler ve opsiyonel AXATA ack atar.",
            [
                Endpoint("outbound-preview", "Outbound kuyruk preview", "GET", "/api/integrations/axata-sync/operations/outbound-delivery-queue/preview", "Advanced", false, "None", "Kuyrugu gor", "C01/C02/C03/C4 pending outbound delivery kuyrugunu hareket tipine gore okur.", "AxataOutboundDeliveryQueuePreviewHttpRequest(query)", "AxataOutboundDeliveryQueuePreviewDto"),
                Endpoint("c01-preview", "C01 sevk preview", "GET", "/api/integrations/axata-sync/operations/c01-shipment/preview", "Main", false, "None", "C01 onizle", "C01 pending teslimatlari Mikro depolar arasi siparisle eslestirir.", "AxataOutboundDeliveryImportPreviewHttpRequest(query)", "AxataOutboundDeliveryImportPreviewDto"),
                Endpoint("c01-import", "C01 sevk import", "POST", "/api/integrations/axata-sync/operations/c01-shipment/import", "Main", true, "MikroAndAxataAck", "C01 isle", "C01 teslimatini Mikro depolar arasi sevk fisine cevirir.", "AxataOutboundDeliveryImportExecuteHttpRequest", "AxataOutboundDeliveryImportExecuteDto"),
                Endpoint("c02-preview", "C02 firma sevk preview", "GET", "/api/integrations/axata-sync/operations/c02-company-shipment/preview", "Main", false, "None", "C02 onizle", "C02 pending teslimatlari Mikro firma siparisiyle eslestirir.", "AxataOutboundDeliveryImportPreviewHttpRequest(query)", "AxataOutboundDeliveryImportPreviewDto"),
                Endpoint("c02-import", "C02 firma sevk import", "POST", "/api/integrations/axata-sync/operations/c02-company-shipment/import", "Main", true, "MikroAndAxataAck", "C02 isle", "C02 teslimatini Mikro firma sevk hareketine cevirir.", "AxataOutboundDeliveryImportExecuteHttpRequest", "AxataOutboundDeliveryImportExecuteDto"),
                Endpoint("c03-preview", "C03 legacy preview", "GET", "/api/integrations/axata-sync/operations/c03-legacy-movement/preview", "Advanced", false, "None", "C03 onizle", "C03 pending legacy teslimatlari kontrol eder.", "AxataOutboundDeliveryImportPreviewHttpRequest(query)", "AxataOutboundDeliveryImportPreviewDto"),
                Endpoint("c03-import", "C03 legacy import", "POST", "/api/integrations/axata-sync/operations/c03-legacy-movement/import", "Advanced", true, "MikroAndAxataAck", "C03 isle", "C03 teslimatini Mikro legacy hareketine cevirir.", "AxataOutboundDeliveryImportExecuteHttpRequest", "AxataOutboundDeliveryImportExecuteDto"),
                Endpoint("c04-preview", "C04 legacy preview", "GET", "/api/integrations/axata-sync/operations/c04-legacy-transfer/preview", "Advanced", false, "None", "C04 onizle", "AXATA C4 pending legacy teslimatlari kontrol eder.", "AxataOutboundDeliveryImportPreviewHttpRequest(query)", "AxataOutboundDeliveryImportPreviewDto"),
                Endpoint("c04-import", "C04 legacy import", "POST", "/api/integrations/axata-sync/operations/c04-legacy-transfer/import", "Advanced", true, "MikroAndAxataAck", "C04 isle", "C4 teslimatini Mikro legacy hareketine cevirir.", "AxataOutboundDeliveryImportExecuteHttpRequest", "AxataOutboundDeliveryImportExecuteDto"),
                Endpoint("g02-preview", "G02 kabul preview", "GET", "/api/integrations/axata-sync/operations/g02-warehouse-receiving/preview", "Main", false, "None", "G02 onizle", "G02 pending giris teslimatlarini Mikro siparis ve bekleyen sevk fisleriyle eslestirir.", "AxataOutboundDeliveryImportPreviewHttpRequest(query)", "AxataOutboundDeliveryImportPreviewDto"),
                Endpoint("g02-import", "G02 kabul import", "POST", "/api/integrations/axata-sync/operations/g02-warehouse-receiving/import", "Main", true, "MikroAndAxataAck", "G02 kabul et", "G02 teslimatini mevcut Mikro bekleyen sevk fisine kabul olarak uygular.", "AxataOutboundDeliveryImportExecuteHttpRequest", "AxataOutboundDeliveryImportExecuteDto"),
                Endpoint("g01-preview", "G01 ATF preview", "GET", "/api/integrations/axata-sync/operations/g01-company-receiving/preview", "Main", false, "None", "G01 onizle", "G01 ATF satirlarini Mikro firma siparisiyle eslestirir.", "AxataG01InboundAtfPreviewRequest(query)", "AxataG01InboundAtfPreviewDto"),
                Endpoint("g01-import", "G01 ATF import", "POST", "/api/integrations/axata-sync/operations/g01-company-receiving/import", "Main", true, "MikroAndAxataAck", "G01 isle", "G01 ATF satirlarini Mikro firma mal kabul hareketine cevirir.", "AxataOutboundDeliveryImportExecuteHttpRequest", "AxataG01InboundAtfExecuteDto"),
                Endpoint("dynamic-census-preview", "Stok duzeltme preview", "GET", "/api/integrations/axata-sync/operations/dynamic-census/preview", "Main", false, "None", "Duzeltmeleri onizle", "AXATA EXT vw_stok_duzeltme satirlarini onizler.", "AxataDynamicCensusPreviewRequest(query)", "AxataDynamicCensusPreviewDto"),
                Endpoint("dynamic-census-import", "Stok duzeltme import", "POST", "/api/integrations/axata-sync/operations/dynamic-census/import", "Main", true, "MikroAndAxataAck", "Duzeltmeleri isle", "AXATA stok duzeltme satirlarini Mikro hareketine cevirir.", "AxataOutboundDeliveryImportExecuteHttpRequest", "AxataDynamicCensusExecuteDto")
            ]),
        new(
            "document-rescue",
            "Belge bazli rescue ve manuel body araclari",
            "Toplu akis yerine tek belge veya elle hazirlanan body ile kontrollu mudahale endpointleri.",
            [
                Endpoint("c01-document-preview", "C01 belge preview", "GET", "/api/integrations/axata-sync/operations/c01-shipment/documents/{documentSerie}/{documentOrderNo}/preview", "Main", false, "None", "Belgeyi onizle", "Tek C01 belgeyi AXATA'dan arar ve Mikro link durumunu kontrol eder.", "status query", "AxataOutboundDeliveryImportPreviewDto"),
                Endpoint("c01-document-import", "C01 belge import", "POST", "/api/integrations/axata-sync/operations/c01-shipment/documents/{documentSerie}/{documentOrderNo}/import", "Main", true, "MikroAndOptionalAxataAck", "Rescue yap", "Tek C01 belgeyi Mikro depolar arasi sevke cevirir.", "AxataOutboundDeliveryDocumentImportExecuteHttpRequest", "AxataOutboundDeliveryImportExecuteDto"),
                Endpoint("g02-document-preview", "G02 belge preview", "GET", "/api/integrations/axata-sync/operations/g02-warehouse-receiving/documents/{documentSerie}/{documentOrderNo}/preview", "Main", false, "None", "G02 belge onizle", "Tek G02 belgeyi AXATA'dan arar ve Mikro kabul/link durumunu kontrol eder.", "status query", "AxataOutboundDeliveryImportPreviewDto"),
                Endpoint("g02-document-import", "G02 belge import", "POST", "/api/integrations/axata-sync/operations/g02-warehouse-receiving/documents/{documentSerie}/{documentOrderNo}/import", "Main", true, "MikroAndOptionalAxataAck", "G02 belge kabul et", "Tek G02 belgeyi mevcut Mikro bekleyen sevk fisine uygular.", "AxataOutboundDeliveryDocumentImportExecuteHttpRequest", "AxataOutboundDeliveryImportExecuteDto"),
                Endpoint("manual-outbound-body", "AXATA outbound body ile sevk", "POST", "/api/integrations/axata-sync/recovery/outbound-deliveries/from-body", "Advanced", true, "Mikro", "Body'den sevk", "Hazir AXATA outbound delivery body bilgisinden Mikro sevk fisi olusturur.", "AxataOutboundDeliveryHttpRequest", "CreateInterWarehouseShipmentResponse"),
                Endpoint("manual-outbound-body-batch", "AXATA outbound body toplu sevk", "POST", "/api/integrations/axata-sync/recovery/outbound-deliveries/from-body-batch", "Advanced", true, "Mikro", "Toplu body sevk", "Birden fazla AXATA outbound delivery body bilgisinden Mikro sevkleri olusturur.", "AxataOutboundDeliveryBatchHttpRequest", "AxataManualOutboundDeliveryBatchResponse"),
                Endpoint("manual-atf-body", "AXATA ATF body ile mal kabul", "POST", "/api/integrations/axata-sync/recovery/company-receivings/from-atf-body", "Advanced", true, "Mikro", "ATF'den kabul", "Hazir AXATA inbound ATF body bilgisinden Mikro firma mal kabul olusturur.", "AxataInboundAtfCompanyReceivingHttpRequest", "CreateCompanyReceivingResponse"),
                Endpoint("manual-atf-body-batch", "AXATA ATF body toplu mal kabul", "POST", "/api/integrations/axata-sync/recovery/company-receivings/from-atf-body-batch", "Advanced", true, "Mikro", "Toplu ATF kabul", "Birden fazla AXATA inbound ATF body bilgisinden Mikro firma mal kabul olusturur.", "AxataInboundAtfCompanyReceivingBatchHttpRequest", "AxataManualIncomingCompanyReceivingBatchResponse"),
                Endpoint("manual-company-receiving", "Serbest firma mal kabul", "POST", "/api/integrations/axata-sync/recovery/company-receivings/manual", "Advanced", true, "Mikro", "Mal kabul olustur", "Serbest body ile Mikro firma mal kabul olusturur.", "CreateCompanyReceivingHttpRequest", "CreateCompanyReceivingResponse"),
                Endpoint("manual-company-receiving-batch", "Serbest firma mal kabul toplu", "POST", "/api/integrations/axata-sync/recovery/company-receivings/manual-batch", "Advanced", true, "Mikro", "Toplu kabul olustur", "Birden fazla serbest firma mal kabul payload'ini Mikro'ya yazar.", "AxataManualIncomingCompanyReceivingBatchHttpRequest", "AxataManualIncomingCompanyReceivingBatchResponse"),
                Endpoint("manual-inventory-count", "Serbest sayim olustur", "POST", "/api/integrations/axata-sync/recovery/inventory-counts/manual", "Advanced", true, "Mikro", "Sayim olustur", "Serbest body ile Mikro sayim sonucu olusturur.", "CreateInventoryCountHttpRequest", "CreateInventoryCountResponse"),
                Endpoint("manual-inventory-count-batch", "Serbest sayim toplu", "POST", "/api/integrations/axata-sync/recovery/inventory-counts/manual-batch", "Advanced", true, "Mikro", "Toplu sayim", "Birden fazla sayim payload'ini Mikro'ya yazar.", "AxataManualIncomingInventoryCountBatchHttpRequest", "AxataManualIncomingInventoryCountBatchResponse"),
                Endpoint("manual-warehouse-receivings", "Bekleyen depo kabulleri", "GET", "/api/integrations/axata-sync/recovery/warehouse-receivings", "Main", false, "None", "Bekleyenleri getir", "Mikro'da kabul bekleyen depo sevklerini listeler.", "warehouseNo/startDate/endDate query", "IReadOnlyCollection<WarehouseShippingListItemDto>"),
                Endpoint("manual-warehouse-receiving-detail", "Bekleyen depo kabul detayi", "GET", "/api/integrations/axata-sync/recovery/warehouse-receivings/{documentSerie}/{documentOrderNo}", "Main", false, "None", "Detayi ac", "Tek bekleyen depo kabul detayini getirir.", "warehouseNo query", "WarehouseShippingDetailDto"),
                Endpoint("manual-warehouse-receiving-accept", "Bekleyen depo kabul et", "POST", "/api/integrations/axata-sync/recovery/warehouse-receivings/{documentSerie}/{documentOrderNo}/accept", "Main", true, "Mikro", "Kabul et", "Tek bekleyen depo kabulunu Mikro'da kabul eder.", "AcceptWarehouseReceivingHttpRequest", "AcceptWarehouseReceivingResponse"),
                Endpoint("manual-warehouse-receiving-accept-batch", "Bekleyen depo kabul toplu", "POST", "/api/integrations/axata-sync/recovery/warehouse-receivings/accept-batch", "Main", true, "Mikro", "Toplu kabul et", "Birden fazla bekleyen depo kabulunu Mikro'da kabul eder.", "AxataManualIncomingWarehouseReceivingBatchHttpRequest", "AxataManualIncomingWarehouseReceivingBatchResponse")
            ])
    ];

    private static AxataSynchronizationWorkbenchEndpointDto Endpoint(
        string code,
        string title,
        string method,
        string route,
        string level,
        bool writesData,
        string writeScope,
        string buttonLabel,
        string description,
        string? requestModel,
        string? responseModel) =>
        new(
            code,
            title,
            method,
            route,
            level,
            writesData,
            writeScope,
            buttonLabel,
            description,
            requestModel,
            responseModel);

    private static IReadOnlyCollection<AxataSynchronizationWorkbenchGlossaryItemDto> BuildWorkbenchGlossary() =>
    [
        new("preview", "Onizle", "Canli veriyi okur ve eslesme/uygunluk sonucu dondurur.", "Veri yazmaz; onaydan once mutlaka kullanilmalidir."),
        new("dispatch", "AXATA'ya gonder", "Mikro/Furpa kaynakli belgeyi AXATA Main servisine canli yazar.", "Basarili olursa AXATA tarafinda kayit olusur; bazi akislarda Mikro gonderim bayragi da isaretlenir."),
        new("import", "Mikro'ya isle", "AXATA kaynakli belgeyi Mikro hareketine veya kabule cevirir.", "Mikro'ya veri yazar; acknowledge=true ise AXATA status de kapanabilir."),
        new("acknowledge", "AXATA onayi", "Mikro yazimi basarili olduktan sonra AXATA status alanini 1 yapar.", "Mikro yazimi basarisizsa ack atilmamalidir."),
        new("rescue", "Belge kurtarma", "Toplu akista eksik kalan tek belgeyi belge numarasiyla tekrar kontrol edip isler.", "Duplicate riskine karsi once preview sonucu okunmalidir."),
        new("outbox", "Outbox", "AXATA'ya gondermeden payload dosyasi hazirlar.", "AXATA kabul etti anlamina gelmez."),
        new("C01", "Depo siparisi/sevki", "Merkezden depolara giden depolar arasi siparis ve sevk akisi.", "C01 importta Mikro fis tarihi import gunudur."),
        new("C02", "Firma sevki", "AXATA outbound delivery bilgisinden Mikro firma sevk hareketi olusturan akis.", "Siparis satiri linki ve teslim miktari dogrulanmalidir."),
        new("G01", "Firma mal kabul", "AXATA inbound ATF veya Mikro firma mal kabul gonderim akisi.", "MikroApi yolunda siparis linkleri geri okunmadan ack atilmaz."),
        new("G02", "Depo kabul", "AXATA giris teslimatini mevcut Mikro bekleyen depo sevk fisine kabul olarak uygulayan akis.", "Yeni sevk fisi yaratmaz; bekleyen fis kabul edilir."),
        new("DynamicCensus", "Stok duzeltme", "AXATA EXT vw_stok_duzeltme satirlarini Mikro stok duzeltme hareketine cevirir.", "Duplicate icin AXATA satir no grup kodunda izlenir.")
    ];

    private static IReadOnlyCollection<string> BuildWorkbenchRules() =>
    [
        "Normal UI acilisi icin once /workbench veya /is-merkezi cagrilir; kullanici teknik endpoint listesine dusurulmez.",
        "Veri yazan her islemde once preview cagrilir, sonra kullanici onayi alinir, sonra dispatch/import/accept calistirilir.",
        "writeScope=None veri yazmaz; AXATA sadece AXATA'ya yazar; Mikro sadece Mikro'ya yazar; MikroAndAxataAck Mikro yazimindan sonra AXATA status kapatabilir.",
        "execute ve outbox teknik arac olarak kalmalidir; normal kullanici icin dispatch/import butonlari daha anlasilirdir.",
        "Toplu import veya dispatch oncesi documentCount, lineCount ve quantity kullaniciya ozet olarak gosterilmelidir.",
        "Belge bazli rescue sadece ilgili preview basarili ve canExecute=true ise calistirilmelidir.",
        "Ham payload, WCF operasyon adi, tablo alanlari ve job detaylari gelismis/teknik detay bolumunde tutulmalidir."
    ];

    private static IReadOnlyCollection<AxataSynchronizationPanelEndpointDto> BuildPanelEndpoints() =>
    [
        new(
            "panel",
            "Sade panel",
            "GET",
            "/api/integrations/axata-sync/panel",
            false,
            "UI ana ekrani icin ozet kartlari, akis adimlari, aksiyonlar ve oncelikli belgeleri dondurur."),
        new(
            "audit-overview",
            "Detayli fark analizi",
            "GET",
            "/api/integrations/axata-sync/audit",
            false,
            "Teknik detay, ham listeler ve derin inceleme icin kullanilir."),
        new(
            "connection-test",
            "Baglanti testi",
            "GET",
            "/api/integrations/axata-sync/connection-test",
            false,
            "Mikro, Furpa ve AXATA erisimlerini kontrol eder."),
        new(
            "dispatch-product-master",
            "Urunleri AXATA'ya gonder",
            "POST",
            "/api/integrations/axata-sync/operations/product-master/dispatch",
            true,
            "Mikro stok master, barkod ve birim bilgilerini AXATA'ya canli gonderir."),
        new(
            "send-order-to-axata",
            "C01 depo siparisini AXATA'ya gonder",
            "POST",
            "/api/integrations/axata-sync/operations/issued-warehouse-order-sync/documents/dispatch",
            true,
            "Mikro'da olup AXATA'ya gitmeyen tek depolar arasi siparisi canli gonderir."),
        new(
            "send-g02-order-to-axata",
            "G02 giris siparisini AXATA'ya gonder",
            "POST",
            "/api/integrations/axata-sync/operations/warehouse-inbound-order-sync/documents/dispatch",
            true,
            "Merkez depoya gelen depolar arasi siparisi AXATA G02 inbound order olarak gonderir."),
        new(
            "send-g01-company-receiving-to-axata",
            "G01 firma mal kabulunu AXATA'ya gonder",
            "POST",
            "/api/integrations/axata-sync/operations/company-receiving-sync/documents/dispatch",
            true,
            "Mikro firma mal kabul belgesini AXATA G01 inbound order olarak gonderir."),
        new(
            "import-c01-to-mikro",
            "AXATA sevkini Mikro'ya isle",
            "POST",
            "/api/integrations/axata-sync/operations/c01-shipment/import",
            true,
            "AXATA C01 bekleyen sevklerini Mikro depolar arasi sevk fisine cevirir."),
        new(
            "import-c02-to-mikro",
            "C02 firma sevkini Mikro'ya isle",
            "POST",
            "/api/integrations/axata-sync/operations/c02-company-shipment/import",
            true,
            "AXATA C02 bekleyen teslimatini Mikro firma sevk hareketine cevirir."),
        new(
            "import-c03-to-mikro",
            "C03 legacy hareketi Mikro'ya isle",
            "POST",
            "/api/integrations/axata-sync/operations/c03-legacy-movement/import",
            true,
            "AXATA C03 bekleyen teslimatini Mikro legacy firma iade/ozel cikis hareketine cevirir."),
        new(
            "import-c04-to-mikro",
            "C04 legacy hareketi Mikro'ya isle",
            "POST",
            "/api/integrations/axata-sync/operations/c04-legacy-transfer/import",
            true,
            "AXATA C4 bekleyen teslimatini Mikro 50 -> 51 legacy hareketine cevirir."),
        new(
            "rescue-c01-document",
            "Eksik C01 sevki kurtar",
            "POST",
            "/api/integrations/axata-sync/operations/c01-shipment/documents/{documentSerie}/{documentOrderNo}/import",
            true,
            "AXATA'da sevki kesilmis ama Mikro linki eksik tek C01 belgeyi Mikro'ya dusurur."),
        new(
            "import-g02-to-mikro",
            "G02 kabulunu Mikro'ya isle",
            "POST",
            "/api/integrations/axata-sync/operations/g02-warehouse-receiving/import",
            true,
            "AXATA G02 bekleyen kabulunu mevcut Mikro bekleyen sevk fisine uygular."),
        new(
            "import-g01-atf-to-mikro",
            "G01 ATF mal kabulunu Mikro'ya isle",
            "POST",
            "/api/integrations/axata-sync/operations/g01-company-receiving/import",
            true,
            "AXATA G01 ATF satirlarini Mikro firma mal kabul hareketine cevirir."),
        new(
            "import-dynamic-census-to-mikro",
            "Stok duzeltmeleri Mikro'ya isle",
            "POST",
            "/api/integrations/axata-sync/operations/dynamic-census/import",
            true,
            "AXATA EXT vw_stok_duzeltme satirlarini Mikro stok duzeltme hareketine cevirir.")
    ];

    private static string GetSynchronizationStateLabel(string state) =>
        state switch
        {
            "WaitingForAxataOrder" => "AXATA siparisi bekleniyor",
            "WaitingForAxataShipment" => "AXATA sevki bekleniyor",
            "WaitingForMikroTransfer" => "AXATA sevki var, Mikro sevk linki yok",
            "MikroShipmentLinkMissing" => "Mikro sevki var, siparis linki yok",
            "ExistingMikroShipmentMissingLink" => "Mikro sevki var, siparis linki yok",
            "MikroOrderDeliveredMissingLink" => "Siparis teslim kapanmis, sevk linki yok",
            "MikroOrderMarkedDeliveredMissingLink" => "Siparis teslim kapanmis, sevk linki yok",
            "PartiallyLinked" => "Mikro sevk linki kismi",
            "PartiallyLinkedInMikro" => "Mikro sevk linki kismi",
            "FullyLinked" => "Mikro sevk linki tamam",
            "FullySynchronized" => "Tum adimlar tamam",
            "WaitingForAxataAck" => "AXATA onayi bekleniyor",
            "MissingAxataOrder" => "AXATA siparisi yok",
            "QuantityMismatch" => "Miktar farki var",
            "ManualReviewRequired" => "Manuel inceleme gerekli",
            "Ignored" => "Islem beklenmiyor",
            _ => state
        };

    private static string BuildQuantitySummary(
        double mikroOrderQuantity,
        double mikroDeliveredQuantity,
        double axataShipmentQuantity,
        double mikroLinkedShipmentQuantity,
        double existingMikroShipmentQuantity)
    {
        var summary =
            $"Siparis {mikroOrderQuantity:0.###} / Teslim {mikroDeliveredQuantity:0.###} / AXATA sevk {axataShipmentQuantity:0.###} / Mikro link {mikroLinkedShipmentQuantity:0.###}";

        return existingMikroShipmentQuantity > 0
            ? $"{summary} / Mikro sevk {existingMikroShipmentQuantity:0.###}"
            : summary;
    }

    private static int GetSeverityRank(string severity) =>
        severity switch
        {
            "Critical" => 0,
            "Warning" => 1,
            "Info" => 2,
            "Success" => 3,
            _ => 4
        };

    private static async Task<AxataBatchExecutionResult<TResult>> ExecuteBatchAsync<TItem, TResult>(
        IReadOnlyCollection<TItem> items,
        bool continueOnError,
        Func<TItem, string> getReference,
        Func<TItem, Task<TResult>> executeAsync)
    {
        if (items.Count == 0)
        {
            throw new ArgumentException("At least one item must be supplied for batch processing.");
        }

        var results = new List<TResult>(items.Count);
        var failures = new List<AxataManualIncomingBatchFailureResponse>();

        foreach (var item in items)
        {
            try
            {
                results.Add(await executeAsync(item));
            }
            catch (Exception exception)
            {
                if (!continueOnError)
                {
                    throw;
                }

                failures.Add(new AxataManualIncomingBatchFailureResponse(
                    getReference(item),
                    exception.Message));
            }
        }

        return new AxataBatchExecutionResult<TResult>(
            items.Count,
            results.Count,
            failures.Count,
            results,
            failures);
    }
}

public sealed class AxataSynchronizationExecuteHttpRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(100)]
    public string TaskCode { get; init; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [RegularExpression("^(DryRun|Outbox|Live)$")]
    public string ExecutionMode { get; init; } = "DryRun";

    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }
}

public sealed class AxataSynchronizationExecuteTaskHttpRequest
{
    [Required(AllowEmptyStrings = false)]
    [RegularExpression("^(DryRun|Outbox|Live)$")]
    public string ExecutionMode { get; init; } = "DryRun";

    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }
}

public sealed class AxataSynchronizationManualDocumentCandidatesHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    public DateTime? StartDate { get; init; }

    public DateTime? EndDate { get; init; }

    [Range(0, int.MaxValue)]
    public int? Skip { get; init; }

    [Range(1, 100)]
    public int? Take { get; init; }
}

public sealed class AxataIntegrationAuditHttpRequest
{
    public DateTime? StartDate { get; init; }

    public DateTime? EndDate { get; init; }

    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    [Range(1, 200)]
    public int? Take { get; init; }

    [StringLength(20)]
    public string? DocumentSerie { get; init; }

    [Range(1, int.MaxValue)]
    public int? DocumentOrderNo { get; init; }

    [StringLength(20)]
    [RegularExpression(@"^\s*[01]\s*(,\s*[01]\s*)*$")]
    public string? Statuses { get; init; }
}

public sealed class AxataOutboundDeliveriesByDateHttpRequest
{
    [Required]
    public DateTime? Date { get; init; }
}

public sealed class AxataProductSynchronizationPreviewHttpRequest
{
    [StringLength(50)]
    public string? ProductCode { get; init; }

    [Range(1, 100000)]
    public int? Take { get; init; }
}

public sealed class AxataProductSynchronizationDispatchHttpRequest
{
    [MaxLength(100000)]
    public IReadOnlyCollection<string> ProductCodes { get; init; } = Array.Empty<string>();

    [Range(1, 100000)]
    public int? Take { get; init; }

    public bool ContinueOnError { get; init; } = true;
}

public sealed class AxataOutboundDeliveryQueuePreviewHttpRequest
{
    [StringLength(10)]
    public string? MovementType { get; init; }

    [Range(1, 200)]
    public int? Take { get; init; }
}

public sealed class AxataOutboundDeliveryImportPreviewHttpRequest
{
    [Range(1, 200)]
    public int? Take { get; init; }
}

public sealed class AxataOutboundDeliveryImportExecuteHttpRequest
{
    [Range(1, 200)]
    public int? Take { get; init; }

    public bool ContinueOnError { get; init; } = true;

    public bool Acknowledge { get; init; } = true;

    [StringLength(20)]
    public string? DateMode { get; init; }

    public DateTime? MovementDate { get; init; }

    public DateTime? DocumentDate { get; init; }
}

public sealed class AxataOutboundDeliveryDocumentImportExecuteHttpRequest
{
    [RegularExpression("^[01]$")]
    public string? Status { get; init; }

    public bool Acknowledge { get; init; }

    [StringLength(20)]
    public string? DateMode { get; init; }

    public DateTime? MovementDate { get; init; }

    public DateTime? DocumentDate { get; init; }
}

public class AxataSynchronizationManualDocumentHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    [StringLength(25)]
    public string? DocumentSerie { get; init; }

    [Range(0, int.MaxValue)]
    public int? DocumentOrderNo { get; init; }

    [Range(0, int.MaxValue)]
    public int? DocumentNo { get; init; }

    public DateTime? DocumentDate { get; init; }
}

public sealed class AxataSynchronizationManualDocumentExecuteHttpRequest
    : AxataSynchronizationManualDocumentHttpRequest
{
    [Required(AllowEmptyStrings = false)]
    [RegularExpression("^(DryRun|Outbox|Live)$")]
    public string ExecutionMode { get; init; } = "DryRun";
}

public sealed class AxataSynchronizationManualDocumentItemHttpRequest
{
    [StringLength(25)]
    public string? DocumentSerie { get; init; }

    [Range(0, int.MaxValue)]
    public int? DocumentOrderNo { get; init; }

    [Range(0, int.MaxValue)]
    public int? DocumentNo { get; init; }

    public DateTime? DocumentDate { get; init; }
}

public class AxataSynchronizationManualDocumentBatchHttpRequest
{
    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }

    public bool ContinueOnError { get; init; } = true;

    [Required]
    [MinLength(1)]
    public IReadOnlyCollection<AxataSynchronizationManualDocumentItemHttpRequest> Documents { get; init; } =
        Array.Empty<AxataSynchronizationManualDocumentItemHttpRequest>();
}

public sealed class AxataSynchronizationManualDocumentBatchExecuteHttpRequest
    : AxataSynchronizationManualDocumentBatchHttpRequest
{
    [Required(AllowEmptyStrings = false)]
    [RegularExpression("^(DryRun|Outbox|Live)$")]
    public string ExecutionMode { get; init; } = "DryRun";
}

public sealed class AxataOutboundDeliveryHttpRequest
{
    [Range(1, int.MaxValue)]
    public int SourceWarehouseNo { get; init; }

    [Range(1, int.MaxValue)]
    public int TargetWarehouseNo { get; init; }

    [Range(1, int.MaxValue)]
    public int? TransitWarehouseNo { get; init; }

    public DateTime? MovementDate { get; init; }

    public DateTime? DocumentDate { get; init; }

    [StringLength(50)]
    public string? DocumentNo { get; init; }

    [StringLength(50)]
    public string? AxataDeliveryNo { get; init; }

    [StringLength(10)]
    public string? MovementCode { get; init; }

    [StringLength(250)]
    public string? Description { get; init; }

    [Required]
    [MinLength(1)]
    public IReadOnlyCollection<AxataOutboundDeliveryLineHttpRequest> Lines { get; init; } =
        Array.Empty<AxataOutboundDeliveryLineHttpRequest>();
}

public sealed class AxataOutboundDeliveryLineHttpRequest
{
    [Range(1, int.MaxValue)]
    public int LineNo { get; init; }

    [Required]
    [StringLength(25)]
    public string StockCode { get; init; } = string.Empty;

    [Range(0.000001, double.MaxValue)]
    public double Quantity { get; init; }

    [Range(0, double.MaxValue)]
    public double UnitPrice { get; init; }

    [Range(1, byte.MaxValue)]
    public int UnitPointer { get; init; } = 1;

    [StringLength(50)]
    public string? Description { get; init; }

    [StringLength(25)]
    public string? PartyCode { get; init; }

    [Range(0, int.MaxValue)]
    public int LotNo { get; init; }

    [StringLength(25)]
    public string? ProjectCode { get; init; }

    [StringLength(25)]
    public string? CustomerResponsibilityCenter { get; init; }

    [StringLength(25)]
    public string? ProductResponsibilityCenter { get; init; }
}

public sealed class AxataOutboundDeliveryBatchHttpRequest
{
    public bool ContinueOnError { get; init; } = true;

    [Required]
    [MinLength(1)]
    public IReadOnlyCollection<AxataOutboundDeliveryHttpRequest> Items { get; init; } =
        Array.Empty<AxataOutboundDeliveryHttpRequest>();
}

public sealed class AxataInboundAtfCompanyReceivingHttpRequest
{
    [Range(1, int.MaxValue)]
    public int WarehouseNo { get; init; }

    [Required]
    [StringLength(25)]
    public string CustomerCode { get; init; } = string.Empty;

    public DateTime? MovementDate { get; init; }

    public DateTime? DocumentDate { get; init; }

    [StringLength(50)]
    public string? DocumentNo { get; init; }

    [StringLength(50)]
    public string? AxataOrderNo { get; init; }

    [StringLength(50)]
    public string? InvoiceNo { get; init; }

    [StringLength(100)]
    public string? Deliverer { get; init; }

    [StringLength(100)]
    public string? Receiver { get; init; }

    [StringLength(250)]
    public string? Description { get; init; }

    public bool AllowOrderOverReceiving { get; init; }

    [Required]
    [MinLength(1)]
    public IReadOnlyCollection<AxataInboundAtfCompanyReceivingLineHttpRequest> Lines { get; init; } =
        Array.Empty<AxataInboundAtfCompanyReceivingLineHttpRequest>();
}

public sealed class AxataInboundAtfCompanyReceivingLineHttpRequest
{
    [Range(1, int.MaxValue)]
    public int LineNo { get; init; }

    [Required]
    [StringLength(25)]
    public string StockCode { get; init; } = string.Empty;

    [Range(0.000001, double.MaxValue)]
    public double Quantity { get; init; }

    [Range(0, double.MaxValue)]
    public double UnitPrice { get; init; }

    [Range(1, byte.MaxValue)]
    public int UnitPointer { get; init; } = 1;

    public DateTime? LastConsumingDate { get; init; }

    [StringLength(50)]
    public string? Description { get; init; }

    [StringLength(25)]
    public string? PartyCode { get; init; }

    [Range(0, int.MaxValue)]
    public int LotNo { get; init; }

    [StringLength(25)]
    public string? ProjectCode { get; init; }

    [StringLength(25)]
    public string? CustomerResponsibilityCenter { get; init; }

    [StringLength(25)]
    public string? ProductResponsibilityCenter { get; init; }
}

public sealed class AxataInboundAtfCompanyReceivingBatchHttpRequest
{
    public bool ContinueOnError { get; init; } = true;

    [Required]
    [MinLength(1)]
    public IReadOnlyCollection<AxataInboundAtfCompanyReceivingHttpRequest> Items { get; init; } =
        Array.Empty<AxataInboundAtfCompanyReceivingHttpRequest>();
}

public sealed class AxataManualIncomingCompanyReceivingBatchHttpRequest
{
    public bool ContinueOnError { get; init; } = true;

    [Required]
    [MinLength(1)]
    public IReadOnlyCollection<CreateCompanyReceivingHttpRequest> Items { get; init; } =
        Array.Empty<CreateCompanyReceivingHttpRequest>();
}

public sealed class AxataManualIncomingInventoryCountBatchHttpRequest
{
    public bool ContinueOnError { get; init; } = true;

    [Required]
    [MinLength(1)]
    public IReadOnlyCollection<CreateInventoryCountHttpRequest> Items { get; init; } =
        Array.Empty<CreateInventoryCountHttpRequest>();
}

public sealed class AxataManualIncomingWarehouseReceivingBatchHttpRequest
{
    public bool ContinueOnError { get; init; } = true;

    [Required]
    [MinLength(1)]
    public IReadOnlyCollection<AxataManualIncomingWarehouseReceivingBatchItemHttpRequest> Items { get; init; } =
        Array.Empty<AxataManualIncomingWarehouseReceivingBatchItemHttpRequest>();
}

public sealed class AxataManualIncomingWarehouseReceivingBatchItemHttpRequest
{
    [Required]
    [StringLength(25)]
    public string DocumentSerie { get; init; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int DocumentOrderNo { get; init; }

    public bool AllowDiscrepancy { get; init; }

    [Required]
    [MinLength(1)]
    public IReadOnlyCollection<AcceptWarehouseReceivingLineHttpRequest> Lines { get; init; } =
        Array.Empty<AcceptWarehouseReceivingLineHttpRequest>();
}

public sealed record AxataManualIncomingCompanyReceivingBatchResponse(
    int RequestedCount,
    int SucceededCount,
    int FailedCount,
    IReadOnlyCollection<CreateCompanyReceivingResponse> Results,
    IReadOnlyCollection<AxataManualIncomingBatchFailureResponse> Failures);

public sealed record AxataManualOutboundDeliveryBatchResponse(
    int RequestedCount,
    int SucceededCount,
    int FailedCount,
    IReadOnlyCollection<CreateInterWarehouseShipmentResponse> Results,
    IReadOnlyCollection<AxataManualIncomingBatchFailureResponse> Failures);

public sealed record AxataManualIncomingInventoryCountBatchResponse(
    int RequestedCount,
    int SucceededCount,
    int FailedCount,
    IReadOnlyCollection<CreateInventoryCountResponse> Results,
    IReadOnlyCollection<AxataManualIncomingBatchFailureResponse> Failures);

public sealed record AxataManualIncomingWarehouseReceivingBatchResponse(
    int RequestedCount,
    int SucceededCount,
    int FailedCount,
    IReadOnlyCollection<AcceptWarehouseReceivingResponse> Results,
    IReadOnlyCollection<AxataManualIncomingBatchFailureResponse> Failures);

public sealed record AxataManualIncomingBatchFailureResponse(
    string Reference,
    string ErrorMessage);

internal sealed record AxataBatchExecutionResult<TResult>(
    int RequestedCount,
    int SucceededCount,
    int FailedCount,
    IReadOnlyCollection<TResult> Results,
    IReadOnlyCollection<AxataManualIncomingBatchFailureResponse> Failures);
