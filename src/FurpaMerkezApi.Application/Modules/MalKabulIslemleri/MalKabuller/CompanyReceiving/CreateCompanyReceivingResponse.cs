namespace FurpaMerkezApi.Application.Modules.MalKabulIslemleri.MalKabuller.CompanyReceiving;

public sealed record CreateCompanyReceivingResponse(
    string DocumentSerie,
    int DocumentOrderNo,
    DateTime MovementDate,
    DateTime DocumentDate,
    string DocumentNo,
    int WarehouseNo,
    string CustomerCode,
    int LineCount,
    double TotalReceivedQuantity,
    double TotalOrderLinkedQuantity,
    double TotalOrderlessQuantity,
    double TotalOrderOverReceivedQuantity,
    double TotalAmount,
    string WriteConnectionName,
    IReadOnlyCollection<CreateCompanyReceivingLineResultDto> Lines);

public sealed record CreateCompanyReceivingLineResultDto(
    Guid MovementGuid,
    int SourceLineNo,
    int MovementLineNo,
    string StockCode,
    Guid? OrderGuid,
    bool IsOrderLinked,
    string ReceivingMode,
    double RequestedQuantity,
    double AcceptedQuantity,
    double OrderLinkedQuantity,
    double OrderlessQuantity,
    double OrderRemainingBefore,
    double OrderRemainingAfter);
