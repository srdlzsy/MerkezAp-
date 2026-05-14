namespace FurpaMerkezApi.Domain.Entities;

public sealed class UyumsoftInboxInvoice
{
    private UyumsoftInboxInvoice()
    {
        DocumentId = string.Empty;
        InvoiceId = string.Empty;
        CustomerTitle = string.Empty;
        CustomerTcknVkn = string.Empty;
        InvoiceType = string.Empty;
        DespatchId = string.Empty;
        StatusCode = string.Empty;
        Status = string.Empty;
    }

    public Guid Id { get; private set; }

    public string DocumentId { get; private set; }

    public string InvoiceId { get; private set; }

    public string? ServiceDocumentId { get; private set; }

    public string? LocalDocumentId { get; private set; }

    public string CustomerTitle { get; private set; }

    public string CustomerTcknVkn { get; private set; }

    public DateTime? CreateDate { get; private set; }

    public DateTime? InvoiceDate { get; private set; }

    public string InvoiceType { get; private set; }

    public decimal InvoiceTotal { get; private set; }

    public string DespatchId { get; private set; }

    public bool IsProcessed { get; private set; }

    public bool IsPrinted { get; private set; }

    public bool IsStandard { get; private set; }

    public string StatusCode { get; private set; }

    public string Status { get; private set; }

    public string? EnvelopeStatusCode { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public DateTime LastSynchronizedAtUtc { get; private set; }

    public UyumsoftInboxInvoice(
        Guid id,
        string documentId,
        string invoiceId,
        string? serviceDocumentId,
        string? localDocumentId,
        string? customerTitle,
        string? customerTcknVkn,
        DateTime? createDate,
        DateTime? invoiceDate,
        string? invoiceType,
        decimal invoiceTotal,
        string? despatchId,
        bool isProcessed,
        bool isPrinted,
        bool isStandard,
        string? statusCode,
        string? status,
        string? envelopeStatusCode,
        DateTime synchronizedAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Inbox invoice id can not be empty.", nameof(id));
        }

        Id = id;
        DocumentId = NormalizeRequired(documentId, nameof(documentId), 150);
        InvoiceId = NormalizeInvoiceId(invoiceId, documentId);
        ServiceDocumentId = NormalizeOptional(serviceDocumentId, 150);
        LocalDocumentId = NormalizeOptional(localDocumentId, 250);
        CustomerTitle = NormalizeOptional(customerTitle, 255) ?? string.Empty;
        CustomerTcknVkn = NormalizeOptional(customerTcknVkn, 50) ?? string.Empty;
        CreateDate = createDate;
        InvoiceDate = invoiceDate;
        InvoiceType = NormalizeOptional(invoiceType, 80) ?? string.Empty;
        InvoiceTotal = invoiceTotal;
        DespatchId = NormalizeOptional(despatchId, 150) ?? string.Empty;
        IsProcessed = isProcessed;
        IsPrinted = isPrinted;
        IsStandard = isStandard;
        StatusCode = NormalizeOptional(statusCode, 80) ?? string.Empty;
        Status = NormalizeOptional(status, 120) ?? string.Empty;
        EnvelopeStatusCode = NormalizeOptional(envelopeStatusCode, 80);
        CreatedAtUtc = NormalizeUtc(synchronizedAtUtc);
        UpdatedAtUtc = CreatedAtUtc;
        LastSynchronizedAtUtc = CreatedAtUtc;
    }

    public void ApplySynchronization(
        string documentId,
        string invoiceId,
        string? serviceDocumentId,
        string? localDocumentId,
        string? customerTitle,
        string? customerTcknVkn,
        DateTime? createDate,
        DateTime? invoiceDate,
        string? invoiceType,
        decimal invoiceTotal,
        string? despatchId,
        bool isProcessed,
        bool isStandard,
        string? statusCode,
        string? status,
        string? envelopeStatusCode,
        DateTime synchronizedAtUtc)
    {
        DocumentId = NormalizeRequired(documentId, nameof(documentId), 150);
        InvoiceId = NormalizeInvoiceId(invoiceId, documentId);
        ServiceDocumentId = NormalizeOptional(serviceDocumentId, 150);
        LocalDocumentId = NormalizeOptional(localDocumentId, 250);
        CustomerTitle = NormalizeOptional(customerTitle, 255) ?? string.Empty;
        CustomerTcknVkn = NormalizeOptional(customerTcknVkn, 50) ?? string.Empty;
        CreateDate = createDate;
        InvoiceDate = invoiceDate;
        InvoiceType = NormalizeOptional(invoiceType, 80) ?? string.Empty;
        InvoiceTotal = invoiceTotal;
        DespatchId = NormalizeOptional(despatchId, 150) ?? string.Empty;
        IsProcessed = isProcessed;
        IsStandard = isStandard;
        StatusCode = NormalizeOptional(statusCode, 80) ?? string.Empty;
        Status = NormalizeOptional(status, 120) ?? string.Empty;
        EnvelopeStatusCode = NormalizeOptional(envelopeStatusCode, 80);
        LastSynchronizedAtUtc = NormalizeUtc(synchronizedAtUtc);
        UpdatedAtUtc = LastSynchronizedAtUtc;
    }

    public void SetPrintedState(bool isPrinted, DateTime updatedAtUtc)
    {
        IsPrinted = isPrinted;
        UpdatedAtUtc = NormalizeUtc(updatedAtUtc);
    }

    private static DateTime NormalizeUtc(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static string NormalizeRequired(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        }

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"{parameterName} can not exceed {maxLength} characters.", parameterName);
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
        {
            return normalized[..maxLength];
        }

        return normalized;
    }

    private static string NormalizeInvoiceId(string invoiceId, string documentId) =>
        NormalizeOptional(invoiceId, 150) ?? NormalizeRequired(documentId, nameof(documentId), 150);
}
