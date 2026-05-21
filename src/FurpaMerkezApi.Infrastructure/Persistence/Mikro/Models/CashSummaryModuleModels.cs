namespace FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;

public sealed class SummaryEntity
{
    public int Id { get; set; }

    public string DocumentSerie { get; set; } = string.Empty;

    public int DocumentOrderNo { get; set; }

    public int CashNo { get; set; }

    public int ZReportNo { get; set; }

    public int CashierNo { get; set; }

    public int ManagerNo { get; set; }

    public DateTime SummaryDate { get; set; }

    public double Total { get; set; }

    public int PaymentTypeId { get; set; }

    public double Amount { get; set; }

    public int WarehouseNo { get; set; }

    public string TypeName { get; set; } = string.Empty;

    public string AccountCode { get; set; } = string.Empty;

    public int SlipNumber { get; set; }

    public string TerminalId { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int? StoreExpenseType { get; set; }

    public DateTime CreateDate { get; set; }
}

public sealed class BanknoteMovementEntity
{
    public int Id { get; set; }

    public string DocumentSerie { get; set; } = string.Empty;

    public int DocumentOrderNo { get; set; }

    public DateTime SummaryDate { get; set; }

    public int WarehouseNo { get; set; }

    public double Value { get; set; }

    public int BanknoteType { get; set; }

    public int Quantity { get; set; }

    public double Total { get; set; }

    public DateTime CreateDate { get; set; }
}

public sealed class GiftCheckMovementEntity
{
    public int Id { get; set; }

    public string DocumentSerie { get; set; } = string.Empty;

    public int DocumentOrderNo { get; set; }

    public DateTime SummaryDate { get; set; }

    public int WarehouseNo { get; set; }

    public double Value { get; set; }

    public int GiftCheckType { get; set; }

    public int Quantity { get; set; }

    public double Total { get; set; }

    public DateTime CreateDate { get; set; }
}

public sealed class BanknoteTrackEntity
{
    public Guid Id { get; set; }

    public int WarehouseNo { get; set; }

    public DateTime BanknoteTrackDate { get; set; }

    public double TotalAmount { get; set; }

    public double DeliveryTotalAmount { get; set; }

    public string Deliverer { get; set; } = string.Empty;

    public string Receiver { get; set; } = string.Empty;

    public DateTime CreateDate { get; set; }
}

public sealed class PaymentTypeEntity
{
    public int PaymentTypeNo { get; set; }

    public string PaymentName { get; set; } = string.Empty;
}

public sealed class BanknoteTypeEntity
{
    public int BanknoteType { get; set; }

    public double Value { get; set; }
}

public sealed class GiftCheckTypeEntity
{
    public int GiftCheckType { get; set; }

    public double Value { get; set; }
}

public sealed class CashRegisterDetailEntity
{
    public int Id { get; set; }

    public string CashRegisterNo { get; set; } = string.Empty;

    public string Bank { get; set; } = string.Empty;

    public string TerminalId { get; set; } = string.Empty;

    public string MerchantNo { get; set; } = string.Empty;

    public int? CashNo { get; set; }
}
