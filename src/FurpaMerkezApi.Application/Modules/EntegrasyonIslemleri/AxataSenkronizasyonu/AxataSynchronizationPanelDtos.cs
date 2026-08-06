namespace FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.AxataSenkronizasyonu;

public sealed record AxataSynchronizationPanelDto(
    string Title,
    string State,
    string Severity,
    string Message,
    bool IsInSync,
    DateTime GeneratedAtUtc,
    DateTime StartDate,
    DateTime EndDate,
    int? WarehouseNo,
    IReadOnlyCollection<AxataSynchronizationPanelMetricDto> SummaryCards,
    IReadOnlyCollection<AxataSynchronizationPanelFlowStepDto> FlowSteps,
    IReadOnlyCollection<AxataSynchronizationPanelActionDto> Actions,
    IReadOnlyCollection<AxataSynchronizationPanelDocumentDto> PriorityDocuments,
    IReadOnlyCollection<AxataSynchronizationPanelEndpointDto> PrimaryEndpoints,
    IReadOnlyCollection<string> Notes);

public sealed record AxataSynchronizationPanelMetricDto(
    string Code,
    string Label,
    int Value,
    string Severity,
    string Description);

public sealed record AxataSynchronizationPanelFlowStepDto(
    string Code,
    string Label,
    string State,
    string Severity,
    int CurrentDocumentCount,
    int ExpectedDocumentCount,
    int DifferenceDocumentCount,
    string Description,
    string? ListRoute);

public sealed record AxataSynchronizationPanelActionDto(
    string Code,
    string Label,
    string State,
    string Severity,
    int DocumentCount,
    int LineCount,
    double Quantity,
    bool CanExecute,
    bool WritesData,
    string? ListRoute,
    string? PreviewRoute,
    string? ExecuteRoute,
    string Description);

public sealed record AxataSynchronizationPanelDocumentDto(
    string DocumentSerie,
    int DocumentOrderNo,
    string DocumentNo,
    DateTime DocumentDate,
    int SourceWarehouseNo,
    int TargetWarehouseNo,
    string SynchronizationState,
    string SynchronizationStateLabel,
    string Severity,
    string RecommendedActionCode,
    string RecommendedActionTitle,
    bool CanExecute,
    string? PreviewRoute,
    string? ExecuteRoute,
    double MikroOrderQuantity,
    double MikroDeliveredQuantity,
    double AxataShipmentQuantity,
    double MikroLinkedShipmentQuantity,
    int ExistingMikroShipmentLineCount,
    double ExistingMikroShipmentQuantity,
    string? ExistingMikroShipmentDocumentNo,
    string QuantitySummary,
    string Reason);

public sealed record AxataSynchronizationPanelEndpointDto(
    string Code,
    string Label,
    string Method,
    string Route,
    bool WritesData,
    string Description);

public sealed record AxataSynchronizationWorkbenchDto(
    string Title,
    string Purpose,
    string State,
    string Severity,
    string Message,
    AxataSynchronizationPanelDto Panel,
    IReadOnlyCollection<AxataSynchronizationWorkbenchScreenSectionDto> ScreenSections,
    IReadOnlyCollection<AxataSynchronizationWorkbenchOperationGroupDto> OperationGroups,
    IReadOnlyCollection<AxataSynchronizationWorkbenchEndpointGroupDto> EndpointGroups,
    IReadOnlyCollection<AxataSynchronizationWorkbenchGlossaryItemDto> Glossary,
    IReadOnlyCollection<string> Rules);

public sealed record AxataSynchronizationWorkbenchScreenSectionDto(
    string Code,
    string Title,
    int SortOrder,
    string DataSource,
    string Purpose,
    string UiBehavior);

public sealed record AxataSynchronizationWorkbenchOperationGroupDto(
    string Code,
    string Title,
    string Direction,
    string Description,
    IReadOnlyCollection<AxataSynchronizationWorkbenchOperationDto> Operations);

public sealed record AxataSynchronizationWorkbenchOperationDto(
    string Code,
    string Title,
    string ShortTitle,
    string Direction,
    string SourceSystem,
    string TargetSystem,
    string? MovementType,
    string Purpose,
    string NormalFlow,
    string WhenToUse,
    string State,
    string Severity,
    int DocumentCount,
    int LineCount,
    double Quantity,
    bool CanExecute,
    bool WritesData,
    string WriteScope,
    string PrimaryButtonLabel,
    string ConfirmationMessage,
    string? ListRoute,
    string? PreviewRoute,
    string? ExecuteRoute,
    IReadOnlyCollection<string> EndpointCodes);

public sealed record AxataSynchronizationWorkbenchEndpointGroupDto(
    string Code,
    string Title,
    string Description,
    IReadOnlyCollection<AxataSynchronizationWorkbenchEndpointDto> Endpoints);

public sealed record AxataSynchronizationWorkbenchEndpointDto(
    string Code,
    string Title,
    string Method,
    string Route,
    string Level,
    bool WritesData,
    string WriteScope,
    string ButtonLabel,
    string Description,
    string? RequestModel,
    string? ResponseModel);

public sealed record AxataSynchronizationWorkbenchGlossaryItemDto(
    string Term,
    string UiLabel,
    string Meaning,
    string UserWarning);
