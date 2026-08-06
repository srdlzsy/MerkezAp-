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
    string Severity,
    string RecommendedActionCode,
    string RecommendedActionTitle,
    bool CanExecute,
    string? PreviewRoute,
    string? ExecuteRoute,
    double MikroOrderQuantity,
    double AxataShipmentQuantity,
    double MikroLinkedShipmentQuantity,
    string Reason);

public sealed record AxataSynchronizationPanelEndpointDto(
    string Code,
    string Label,
    string Method,
    string Route,
    bool WritesData,
    string Description);
