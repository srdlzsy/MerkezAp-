namespace FurpaMerkezApi.Infrastructure.Persistence.Mikro.Models;

public sealed class ZReportTotalEntity
{
    public int TotalId { get; set; }

    public int ZNo { get; set; }

    public int BillNo { get; set; }

    public string CashRegisterNo { get; set; } = string.Empty;

    public DateTime Date { get; set; }

    public bool IsSent { get; set; }

    public double CashPaymentTotal { get; set; }

    public double CreditCardPaymentTotal { get; set; }

    public double GreatTotal { get; set; }
}

public sealed class ZReportDetailEntity
{
    public int DetailId { get; set; }

    public int TotalId { get; set; }

    public byte TaxRate { get; set; }

    public double BillTotal { get; set; }

    public double BillTaxTotal { get; set; }
}

public sealed class ZReportBankDetailEntity
{
    public int BankDetailId { get; set; }

    public int TotalId { get; set; }

    public string Bank { get; set; } = string.Empty;

    public double BankAmount { get; set; }

    public int BankingNumber { get; set; }
}

public sealed class CashRegisterBranchEntity
{
    public int Id { get; set; }

    public string CashRegisterNo { get; set; } = string.Empty;

    public int BranchNo { get; set; }
}

public sealed class BranchInvoiceEntity
{
    public int InvoiceId { get; set; }

    public Guid InvoiceGuid { get; set; }

    public int BranchNo { get; set; }

    public int DocumentNo { get; set; }

    public string CustomerTaxNo { get; set; } = string.Empty;

    public DateTime InvoiceDate { get; set; }

    public string PaymentType { get; set; } = string.Empty;

    public decimal InvoiceTotal { get; set; }

    public bool IsSent { get; set; }
}

public sealed class BranchInvoiceLineEntity
{
    public int LineId { get; set; }

    public int InvoiceId { get; set; }

    public short TaxRate { get; set; }

    public decimal Amount { get; set; }

    public decimal TaxAmount { get; set; }
}

public sealed class ExpenseNoteEntity
{
    public int ExpenseId { get; set; }

    public Guid ExpenseGuid { get; set; }

    public string DocumentNo { get; set; } = string.Empty;

    public int BranchNo { get; set; }

    public DateTime ExpenseDate { get; set; }

    public string PaymentType { get; set; } = string.Empty;

    public decimal ExpenseTotal { get; set; }

    public bool IsSent { get; set; }
}

public sealed class ExpenseNoteLineEntity
{
    public int LineId { get; set; }

    public int ExpenseNoteId { get; set; }

    public short TaxRate { get; set; }

    public decimal Amount { get; set; }

    public decimal TaxAmount { get; set; }
}
