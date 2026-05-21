namespace FurpaMerkezApi.Application.Modules.KasaIslemleri.EtiketBelgeleri;

public sealed record LabelDocumentProductDto
{
    public string Package { get; init; } = string.Empty;

    public string PackageFactor { get; init; } = string.Empty;

    public DateTime LastUpdateDate { get; init; }

    public string BarcodeContent { get; init; } = string.Empty;

    public byte BulkSaleTaxRate { get; init; }

    public byte RetailSaleTaxRate { get; init; }

    public string ProductCode { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    public string Barcode { get; init; } = string.Empty;

    public double OldPrice { get; init; }

    public double Price { get; init; }

    public string PriceChangeDate { get; init; } = string.Empty;

    public string SupplierCode { get; init; } = string.Empty;

    public byte IsClosedToSale { get; init; }

    public byte IsClosedToOrder { get; init; }

    public byte IsClosedToReceiving { get; init; }

    public bool IsPassive { get; init; }

    public string UnitName { get; init; } = string.Empty;

    public string UnitName2 { get; init; } = string.Empty;

    public string TypeCode { get; init; } = string.Empty;

    public byte IsDomestic { get; init; }

    public string Origin { get; init; } = string.Empty;

    public double UnitPriceFactor { get; init; }

    public string AlternativeUnitName { get; init; } = string.Empty;

    public int PluNo { get; init; }

    public string SectorCode { get; init; } = string.Empty;

    public short ShelfLife { get; init; }

    public string Type { get; init; } = string.Empty;

    public Guid? OrderGuid { get; init; }

    public bool CanBeCalled { get; init; }

    public double Quantity { get; init; }

    public double DeliveredQuantity { get; init; }

    public int DocumentOrderNo { get; init; }

    public string CategoryCode { get; init; } = string.Empty;
}
