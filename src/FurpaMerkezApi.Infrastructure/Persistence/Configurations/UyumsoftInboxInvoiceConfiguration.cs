using FurpaMerkezApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurpaMerkezApi.Infrastructure.Persistence.Configurations;

public sealed class UyumsoftInboxInvoiceConfiguration : IEntityTypeConfiguration<UyumsoftInboxInvoice>
{
    private readonly bool useSqlServerTypes;

    public UyumsoftInboxInvoiceConfiguration()
        : this(false)
    {
    }

    public UyumsoftInboxInvoiceConfiguration(bool useSqlServerTypes)
    {
        this.useSqlServerTypes = useSqlServerTypes;
    }

    public void Configure(EntityTypeBuilder<UyumsoftInboxInvoice> builder)
    {
        builder.ToTable("uyumsoft_inbox_invoices");

        builder.HasKey(item => item.Id);

        builder.Property(item => item.Id)
            .HasColumnName("id");

        builder.Property(item => item.DocumentId)
            .HasColumnName("document_id")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(item => item.InvoiceId)
            .HasColumnName("invoice_id")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(item => item.ServiceDocumentId)
            .HasColumnName("service_document_id")
            .HasMaxLength(150);

        builder.Property(item => item.LocalDocumentId)
            .HasColumnName("local_document_id")
            .HasMaxLength(250);

        builder.Property(item => item.CustomerTitle)
            .HasColumnName("customer_title")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(item => item.CustomerTcknVkn)
            .HasColumnName("customer_tckn_vkn")
            .HasMaxLength(50)
            .IsRequired();

        var createDate = builder.Property(item => item.CreateDate)
            .HasColumnName("create_date");

        if (!useSqlServerTypes)
        {
            createDate.HasColumnType("timestamp without time zone");
        }

        var invoiceDate = builder.Property(item => item.InvoiceDate)
            .HasColumnName("invoice_date");

        if (!useSqlServerTypes)
        {
            invoiceDate.HasColumnType("timestamp without time zone");
        }

        builder.Property(item => item.InvoiceType)
            .HasColumnName("invoice_type")
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(item => item.InvoiceTotal)
            .HasColumnName("invoice_total")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(item => item.DespatchId)
            .HasColumnName("despatch_id")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(item => item.IsProcessed)
            .HasColumnName("is_processed")
            .IsRequired();

        builder.Property(item => item.IsPrinted)
            .HasColumnName("is_printed")
            .IsRequired();

        builder.Property(item => item.IsStandard)
            .HasColumnName("is_standard")
            .IsRequired();

        builder.Property(item => item.StatusCode)
            .HasColumnName("status_code")
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(item => item.Status)
            .HasColumnName("status")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(item => item.EnvelopeStatusCode)
            .HasColumnName("envelope_status_code")
            .HasMaxLength(80);

        builder.Property(item => item.EnvelopeIdentifier)
            .HasColumnName("envelope_identifier")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(item => item.Message)
            .HasColumnName("message")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(item => item.TaxTotal)
            .HasColumnName("tax_total")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(item => item.TaxExclusiveAmount)
            .HasColumnName("tax_exclusive_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(item => item.DocumentCurrencyCode)
            .HasColumnName("document_currency_code")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(item => item.ExchangeRate)
            .HasColumnName("exchange_rate")
            .HasPrecision(18, 6)
            .IsRequired();

        builder.Property(item => item.OrderDocumentId)
            .HasColumnName("order_document_id")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(item => item.IsArchived)
            .HasColumnName("is_archived")
            .IsRequired();

        builder.Property(item => item.InvoiceTipType)
            .HasColumnName("invoice_tip_type")
            .HasMaxLength(80)
            .IsRequired();

        builder.Property(item => item.InvoiceTipTypeCode)
            .HasColumnName("invoice_tip_type_code")
            .IsRequired();

        builder.Property(item => item.IsSeen)
            .HasColumnName("is_seen");

        var createdAtUtc = builder.Property(item => item.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .IsRequired();

        var updatedAtUtc = builder.Property(item => item.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .IsRequired();

        var lastSynchronizedAtUtc = builder.Property(item => item.LastSynchronizedAtUtc)
            .HasColumnName("last_synchronized_at_utc")
            .IsRequired();

        if (!useSqlServerTypes)
        {
            createdAtUtc.HasColumnType("timestamp with time zone");
            updatedAtUtc.HasColumnType("timestamp with time zone");
            lastSynchronizedAtUtc.HasColumnType("timestamp with time zone");
        }

        builder.HasIndex(item => item.DocumentId)
            .IsUnique()
            .HasDatabaseName("ux_uyumsoft_inbox_invoices_document_id");

        builder.HasIndex(item => item.InvoiceDate)
            .HasDatabaseName("ix_uyumsoft_inbox_invoices_invoice_date");

        builder.HasIndex(item => item.CreateDate)
            .HasDatabaseName("ix_uyumsoft_inbox_invoices_create_date");

        builder.HasIndex(item => new { item.IsProcessed, item.IsPrinted })
            .HasDatabaseName("ix_uyumsoft_inbox_invoices_processed_printed");

        builder.HasIndex(item => item.OrderDocumentId)
            .HasDatabaseName("ix_uyumsoft_inbox_invoices_order_document_id");
    }
}
