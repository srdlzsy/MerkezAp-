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
    IAxataOutboundDeliveryImportService outboundDeliveryImportService,
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

    [HttpGet("live/audit/overview")]
    [Authorize(Policy = DetailPolicy)]
    [ProducesResponseType(typeof(AxataIntegrationAuditDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AxataIntegrationAuditDto>> GetLiveAuditOverview(
        [FromQuery] AxataIntegrationAuditHttpRequest request,
        CancellationToken cancellationToken) =>
        Ok(await integrationAuditService.GetOverviewAsync(
            new AxataIntegrationAuditRequest(
                request.StartDate,
                request.EndDate,
                request.WarehouseNo,
                request.Take,
                request.DocumentSerie,
                request.DocumentOrderNo,
                request.Statuses),
            cancellationToken));

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
        var warehouseNo = User.GetRequiredWarehouseNo();
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
        var warehouseNo = User.GetRequiredWarehouseNo();
        var requestedByUserId = User.GetRequiredUserId();
        var result = await ExecuteBatchAsync(
            request.Items,
            request.ContinueOnError,
            BuildCompanyReceivingReference,
            item => createCompanyReceivingUseCase.ExecuteAsync(
                BuildCreateCompanyReceivingRequest(warehouseNo, requestedByUserId, item),
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
        var warehouseNo = User.GetRequiredWarehouseNo();
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
        var warehouseNo = User.GetRequiredWarehouseNo();
        var requestedByUserId = User.GetRequiredUserId();
        var result = await ExecuteBatchAsync(
            request.Items,
            request.ContinueOnError,
            BuildInventoryCountReference,
            item => createInventoryCountUseCase.ExecuteAsync(
                BuildCreateInventoryCountRequest(warehouseNo, requestedByUserId, item),
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
        var warehouseNo = request.WarehouseNo ?? User.GetRequiredWarehouseNo();

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
        var resolvedWarehouseNo = warehouseNo ?? User.GetRequiredWarehouseNo();

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
        var warehouseNo = User.GetRequiredWarehouseNo();

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
        var warehouseNo = User.GetRequiredWarehouseNo();
        var result = await ExecuteBatchAsync(
            request.Items,
            request.ContinueOnError,
            item => $"{item.DocumentSerie}.{item.DocumentOrderNo}",
            item => acceptWarehouseReceivingUseCase.ExecuteAsync(
                BuildAcceptWarehouseReceivingRequest(warehouseNo, item.DocumentSerie, item.DocumentOrderNo, item),
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
    [RegularExpression("^(DryRun|Outbox)$")]
    public string ExecutionMode { get; init; } = "DryRun";

    [Range(1, int.MaxValue)]
    public int? WarehouseNo { get; init; }
}

public sealed class AxataSynchronizationExecuteTaskHttpRequest
{
    [Required(AllowEmptyStrings = false)]
    [RegularExpression("^(DryRun|Outbox)$")]
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
    [RegularExpression("^(DryRun|Outbox)$")]
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
    [RegularExpression("^(DryRun|Outbox)$")]
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
