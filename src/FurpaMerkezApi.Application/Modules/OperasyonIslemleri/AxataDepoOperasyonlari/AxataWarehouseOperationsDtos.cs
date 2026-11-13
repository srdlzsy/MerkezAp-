namespace FurpaMerkezApi.Application.Modules.OperasyonIslemleri.AxataDepoOperasyonlari;

public sealed record AxataOperationsMetadata(
    DateTimeOffset GeneratedAtUtc,
    string DataSource,
    string Freshness,
    int? WarehouseNo,
    DateOnly? Date);

public sealed record AxataWarehouseOverviewDto(
    AxataOperationsMetadata Metadata,
    AxataOutboundOverview Outbound,
    AxataInboundOverview Inbound,
    AxataQueueOverview Queues,
    IReadOnlyList<AxataActiveLoadingItem> ActiveLoadings,
    IReadOnlyList<AxataPendingOrderItem> PendingOrders);

public sealed record AxataOutboundOverview(
    int PendingOrderCount,
    int PickingOrderCount,
    int ShippedTodayCount,
    decimal ShippedTodayQty);

public sealed record AxataInboundOverview(
    int PendingReceivingCount,
    int ProcessingReceivingCount,
    int CompletedTodayCount);

public sealed record AxataQueueOverview(
    int OutboundWaitingIntegrationCount,
    int InboundWaitingIntegrationCount);

public sealed record AxataActiveLoadingItem(
    int ShipmentYear,
    int ShipmentNo,
    string? Plate,
    string? DestinationCode,
    string? DestinationName,
    string? Ramp,
    string? Picker,
    string? StartTime,
    int LineCount);

public sealed record AxataPendingOrderItem(
    string OrderNo,
    string? MovementType,
    string? TargetCode,
    string? TargetName,
    DateOnly OrderDate,
    int LineCount,
    decimal TotalOrderQty);

public sealed record AxataDocumentTraceDto(
    AxataOperationsMetadata Metadata,
    string SearchTerm,
    string DocumentType,
    string? OrderNo,
    string? MovementType,
    string? TargetCode,
    string? TargetName,
    DateOnly? OrderDate,
    int? OrderStatus,
    string? OrderStatusDescription,
    IReadOnlyList<AxataTraceShipmentItem> Shipments,
    IReadOnlyList<AxataTracePickingLogItem> PickingLogs,
    IReadOnlyList<AxataTraceInboundItem> Inbounds,
    IReadOnlyList<AxataTraceQueueItem> IntegrationQueues);

public sealed record AxataTraceShipmentItem(
    int ShipmentYear,
    int ShipmentNo,
    string? MovementType,
    int? Status,
    string? StatusDescription,
    string? Plate,
    string? Ramp,
    string? ShipmentDate,
    IReadOnlyList<AxataTraceShipmentDetailItem> Details);

public sealed record AxataTraceShipmentDetailItem(
    int LineNo,
    int ItemOrderNo,
    string Sku,
    string? SkuName,
    decimal OrderedQty,
    decimal ShippedQty,
    decimal DifferenceQty);

public sealed record AxataTracePickingLogItem(
    int Sequence,
    string? Warehouse,
    string? Zone,
    string? Location,
    string? Sku,
    string? SkuName,
    decimal Quantity,
    string? Picker,
    string? LogDate,
    string? LogTime,
    string? PalletNo);

public sealed record AxataTraceInboundItem(
    long OrderId,
    string DocumentNo,
    string? MovementType,
    string? SupplierCode,
    string? SupplierName,
    string? ReceiptNo,
    DateOnly? ReceiptDate,
    int? Status,
    string? StatusDescription,
    IReadOnlyList<AxataTraceInboundDetailItem> Details);

public sealed record AxataTraceInboundDetailItem(
    int LineNo,
    string Sku,
    string? SkuName,
    decimal ExpectedQty,
    decimal ReceivedQty);

public sealed record AxataTraceQueueItem(
    string QueueType,
    string DocumentNo,
    int Status,
    string StatusDescription,
    string? TargetCode,
    string? CreationDate);

public sealed record AxataPickingPerformanceDto(
    AxataOperationsMetadata Metadata,
    IReadOnlyList<AxataPickerPerformanceItem> Pickers,
    IReadOnlyList<AxataZonePerformanceItem> Zones);

public sealed record AxataPickerPerformanceItem(
    string UserId,
    string? UserName,
    int TotalShipments,
    int TotalPickTasks,
    int TotalLines,
    decimal TotalQuantity,
    int UnpickedLines,
    double CompletionRate,
    double AverageDurationMinutes);

public sealed record AxataZonePerformanceItem(
    string ZoneCode,
    int TotalShipments,
    int TotalLines,
    decimal TotalQuantity,
    int UnpickedLines);

public sealed record AxataFillRateDto(
    AxataOperationsMetadata Metadata,
    decimal OverallFillRate,
    int TotalOrderedLines,
    int FullySuppliedLines,
    int PartiallySuppliedLines,
    int UnsuppliedLines,
    IReadOnlyList<AxataStoreFillRateItem> Stores,
    IReadOnlyList<AxataSkuFillRateItem> TopMissingSkus);

public sealed record AxataStoreFillRateItem(
    string StoreCode,
    string? StoreName,
    int OrderCount,
    int TotalLines,
    decimal TotalOrderedQty,
    decimal TotalShippedQty,
    decimal TotalMissingQty,
    double FillRatePercentage);

public sealed record AxataSkuFillRateItem(
    string Sku,
    string? SkuName,
    int OrderCount,
    decimal TotalOrderedQty,
    decimal TotalShippedQty,
    decimal TotalMissingQty,
    double FillRatePercentage);

public sealed record AxataCapacityDto(
    AxataOperationsMetadata Metadata,
    int TotalLocations,
    int OccupiedLocations,
    int EmptyLocations,
    int BlockedLocations,
    double OccupancyRatePercentage,
    IReadOnlyList<AxataZoneCapacityItem> Zones,
    IReadOnlyList<AxataStockTypeCapacityItem> StockTypes);

public sealed record AxataZoneCapacityItem(
    string Zone,
    int TotalLocations,
    int OccupiedLocations,
    int EmptyLocations,
    double OccupancyRatePercentage);

public sealed record AxataStockTypeCapacityItem(
    string TypeCode,
    string? TypeDescription,
    int LocationCount);
