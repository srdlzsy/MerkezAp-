using System.Text.Json;

namespace FurpaMerkezApi.Infrastructure.Services.MikroApi;

internal sealed record MikroApiWriteAuditContext(
    string? DocumentSerie,
    int? DocumentOrderNo,
    int? WarehouseNo,
    int? LineCount);

internal static class MikroApiWriteAuditContextExtractor
{
    private static readonly string[] DocumentSerieNames =
    [
        "documentSerie",
        "sth_evrakno_seri",
        "ssip_evrakno_seri",
        "sip_evrakno_seri",
        "cha_evrakno_seri",
        "say_evrakno_seri",
        "evrakno_seri"
    ];

    private static readonly string[] DocumentOrderNoNames =
    [
        "documentOrderNo",
        "sth_evrakno_sira",
        "ssip_evrakno_sira",
        "sip_evrakno_sira",
        "cha_evrakno_sira",
        "say_evrakno_sira",
        "evrakno_sira"
    ];

    private static readonly string[] WarehouseNoNames =
    [
        "warehouseNo",
        "sourceWarehouseNo",
        "sth_cikis_depo_no",
        "sth_giris_depo_no",
        "ssip_girdepo",
        "ssip_cikdepo",
        "sip_depono",
        "say_depo_no",
        "depo_no"
    ];

    private static readonly string[] LineArrayNames = ["satirlar", "lines", "Kayit"];

    public static MikroApiWriteAuditContext Extract(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return new(null, null, null, null);
        }

        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            var root = document.RootElement;

            return new MikroApiWriteAuditContext(
                FindString(root, DocumentSerieNames),
                FindInt32(root, DocumentOrderNoNames),
                FindInt32(root, WarehouseNoNames),
                FindArrayLength(root, LineArrayNames));
        }
        catch (JsonException)
        {
            return new(null, null, null, null);
        }
    }

    private static string? FindString(JsonElement root, IEnumerable<string> propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (TryFindProperty(root, propertyName, out var value) &&
                value.ValueKind == JsonValueKind.String)
            {
                var result = value.GetString();
                if (!string.IsNullOrWhiteSpace(result))
                {
                    return result;
                }
            }
        }

        return null;
    }

    private static int? FindInt32(JsonElement root, IEnumerable<string> propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!TryFindProperty(root, propertyName, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            {
                return number;
            }

            if (value.ValueKind == JsonValueKind.String &&
                int.TryParse(value.GetString(), out number))
            {
                return number;
            }
        }

        return null;
    }

    private static int? FindArrayLength(JsonElement root, IEnumerable<string> propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (TryFindProperty(root, propertyName, out var value) &&
                value.ValueKind == JsonValueKind.Array)
            {
                return value.GetArrayLength();
            }
        }

        return null;
    }

    private static bool TryFindProperty(
        JsonElement element,
        string propertyName,
        out JsonElement value)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        value = property.Value;
                        return true;
                    }

                    if (TryFindProperty(property.Value, propertyName, out value))
                    {
                        return true;
                    }
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (TryFindProperty(item, propertyName, out value))
                    {
                        return true;
                    }
                }

                break;
        }

        value = default;
        return false;
    }
}
