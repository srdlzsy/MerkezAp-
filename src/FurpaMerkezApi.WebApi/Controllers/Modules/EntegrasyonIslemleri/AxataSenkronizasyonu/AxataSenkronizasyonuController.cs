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
    [Authorize(Policy = ListPolicy)]
    [ProducesResponseType(typeof(AxataSynchronizationOverviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AxataSynchronizationOverviewDto>> GetOverview(CancellationToken cancellationToken) =>
        Ok(await synchronizationService.GetOverviewAsync(cancellationToken));

    [HttpGet("health")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(AxataSynchronizationConnectionTestDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AxataSynchronizationConnectionTestDto>> TestConnections(
        CancellationToken cancellationToken) =>
        Ok(await synchronizationService.TestConnectionsAsync(cancellationToken));

    [HttpGet("fetch-profiles")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(AxataSynchronizationFetchProfilesOverviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AxataSynchronizationFetchProfilesOverviewDto>> GetFetchProfiles(
        CancellationToken cancellationToken) =>
        Ok(await synchronizationService.GetFetchProfilesAsync(cancellationToken));

    [HttpGet("live/products/preview")]
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

    [HttpGet("tasks/{taskCode}/preview")]
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
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(AxataSynchronizationJobDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AxataSynchronizationJobDetailDto>> GetJob(
        Guid jobId,
        CancellationToken cancellationToken) =>
        Ok(await synchronizationService.GetJobAsync(jobId, cancellationToken));

    [HttpGet("manual/tasks/{taskCode}/documents/candidates")]
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
                request.Acknowledge),
            User.GetRequiredUserId(),
            cancellationToken));

    [HttpGet("live/axata/outbound-deliveries/c01/documents/{documentSerie}/{documentOrderNo:int}/preview")]
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
                request.Acknowledge),
            User.GetRequiredUserId(),
            cancellationToken));

    [HttpGet("live/axata/outbound-deliveries/c02/preview")]
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
                    document.RecommendedAction.Severity,
                    document.RecommendedAction.Code,
                    document.RecommendedAction.Title,
                    document.RecommendedAction.CanExecute,
                    document.RecommendedAction.PreviewRoute,
                    document.RecommendedAction.ExecuteRoute,
                    document.MikroOrderQuantity,
                    document.AxataShipmentQuantity,
                    document.MikroLinkedShipmentQuantity,
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
            "/api/integrations/axata-sync/live/audit/overview",
            false,
            "Teknik detay, ham listeler ve derin inceleme icin kullanilir."),
        new(
            "connection-test",
            "Baglanti testi",
            "GET",
            "/api/integrations/axata-sync/health",
            false,
            "Mikro, Furpa ve AXATA erisimlerini kontrol eder."),
        new(
            "dispatch-product-master",
            "Urunleri AXATA'ya gonder",
            "POST",
            "/api/integrations/axata-sync/live/products/dispatch",
            true,
            "Mikro stok master, barkod ve birim bilgilerini AXATA'ya canli gonderir."),
        new(
            "send-order-to-axata",
            "C01 depo siparisini AXATA'ya gonder",
            "POST",
            "/api/integrations/axata-sync/manual/tasks/issued-warehouse-order-sync/documents/dispatch",
            true,
            "Mikro'da olup AXATA'ya gitmeyen tek depolar arasi siparisi canli gonderir."),
        new(
            "send-g02-order-to-axata",
            "G02 giris siparisini AXATA'ya gonder",
            "POST",
            "/api/integrations/axata-sync/manual/tasks/warehouse-inbound-order-sync/documents/dispatch",
            true,
            "Merkez depoya gelen depolar arasi siparisi AXATA G02 inbound order olarak gonderir."),
        new(
            "send-g01-company-receiving-to-axata",
            "G01 firma mal kabulunu AXATA'ya gonder",
            "POST",
            "/api/integrations/axata-sync/manual/tasks/company-receiving-sync/documents/dispatch",
            true,
            "Mikro firma mal kabul belgesini AXATA G01 inbound order olarak gonderir."),
        new(
            "import-c01-to-mikro",
            "AXATA sevkini Mikro'ya isle",
            "POST",
            "/api/integrations/axata-sync/live/axata/outbound-deliveries/c01/import",
            true,
            "AXATA C01 bekleyen sevklerini Mikro depolar arasi sevk fisine cevirir."),
        new(
            "import-c02-to-mikro",
            "C02 firma sevkini Mikro'ya isle",
            "POST",
            "/api/integrations/axata-sync/live/axata/outbound-deliveries/c02/import",
            true,
            "AXATA C02 bekleyen teslimatini Mikro firma sevk hareketine cevirir."),
        new(
            "import-c03-to-mikro",
            "C03 legacy hareketi Mikro'ya isle",
            "POST",
            "/api/integrations/axata-sync/live/axata/outbound-deliveries/c03/import",
            true,
            "AXATA C03 bekleyen teslimatini Mikro legacy firma iade/ozel cikis hareketine cevirir."),
        new(
            "import-c04-to-mikro",
            "C04 legacy hareketi Mikro'ya isle",
            "POST",
            "/api/integrations/axata-sync/live/axata/outbound-deliveries/c04/import",
            true,
            "AXATA C4 bekleyen teslimatini Mikro 50 -> 51 legacy hareketine cevirir."),
        new(
            "rescue-c01-document",
            "Eksik C01 sevki kurtar",
            "POST",
            "/api/integrations/axata-sync/live/axata/outbound-deliveries/c01/documents/{documentSerie}/{documentOrderNo}/import",
            true,
            "AXATA'da sevki kesilmis ama Mikro linki eksik tek C01 belgeyi Mikro'ya dusurur."),
        new(
            "import-g02-to-mikro",
            "G02 kabulunu Mikro'ya isle",
            "POST",
            "/api/integrations/axata-sync/live/axata/inbound-deliveries/g02/import",
            true,
            "AXATA G02 bekleyen kabulunu mevcut Mikro bekleyen sevk fisine uygular."),
        new(
            "import-g01-atf-to-mikro",
            "G01 ATF mal kabulunu Mikro'ya isle",
            "POST",
            "/api/integrations/axata-sync/live/axata/inbound-atf/g01/import",
            true,
            "AXATA G01 ATF satirlarini Mikro firma mal kabul hareketine cevirir."),
        new(
            "import-dynamic-census-to-mikro",
            "Stok duzeltmeleri Mikro'ya isle",
            "POST",
            "/api/integrations/axata-sync/live/axata/dynamic-census/import",
            true,
            "AXATA EXT vw_stok_duzeltme satirlarini Mikro stok duzeltme hareketine cevirir.")
    ];

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
}

public sealed class AxataOutboundDeliveryDocumentImportExecuteHttpRequest
{
    [RegularExpression("^[01]$")]
    public string? Status { get; init; }

    public bool Acknowledge { get; init; }
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
