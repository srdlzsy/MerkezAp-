using System.Text.Json;

namespace FurpaMerkezApi.Infrastructure.Services.MikroApi;

internal static class MikroApiCreatedDocumentResultReader
{
    public static IReadOnlyList<MikroApiCreatedDocumentRow> ReadRows(string? rawResponse)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(rawResponse);
            var rows = new List<MikroApiCreatedDocumentRow>();
            AddRows(document.RootElement, rows);

            return rows;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static void AddRows(JsonElement element, List<MikroApiCreatedDocumentRow> rows)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (TryGetProperty(element, "result", out var resultElement))
        {
            if (resultElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var resultItem in resultElement.EnumerateArray())
                {
                    AddRows(resultItem, rows);
                }

                return;
            }

            AddRows(resultElement, rows);
            return;
        }

        if (TryGetProperty(element, "Data", out var dataElement))
        {
            AddRowsFromData(dataElement, rows);
            return;
        }

        AddRowsFromData(element, rows);
    }

    private static void AddRowsFromData(JsonElement dataElement, List<MikroApiCreatedDocumentRow> rows)
    {
        if (dataElement.ValueKind == JsonValueKind.Object &&
            TryGetProperty(dataElement, "list", out var listElement))
        {
            AddRowsFromList(listElement, rows);
            return;
        }

        AddRowsFromList(dataElement, rows);
    }

    private static void AddRowsFromList(JsonElement listElement, List<MikroApiCreatedDocumentRow> rows)
    {
        if (listElement.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in listElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var guid = ReadGuidProperty(item, "cariHarGuid") ??
                       ReadGuidProperty(item, "guid") ??
                       ReadGuidProperty(item, "Guid") ??
                       ReadGuidProperty(item, "ssip_Guid") ??
                       ReadGuidProperty(item, "sip_Guid") ??
                       ReadGuidProperty(item, "sth_Guid");

            if (!guid.HasValue)
            {
                continue;
            }

            rows.Add(new MikroApiCreatedDocumentRow(
                guid.Value,
                TryGetStringProperty(item, "evrakno_seri"),
                ReadInt32Property(item, "evrakno_sira")));
        }
    }

    private static Guid? ReadGuidProperty(JsonElement element, string propertyName)
    {
        var value = TryGetStringProperty(element, propertyName);

        return Guid.TryParse(value, out var guid) ? guid : null;
    }

    private static string? TryGetStringProperty(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => NormalizeOptional(value.GetString()),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    private static int? ReadInt32Property(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(value.GetString(), out var number) => number,
            _ => null
        };
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.NameEquals(propertyName) ||
                property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

internal sealed record MikroApiCreatedDocumentRow(
    Guid Guid,
    string? DocumentSerie,
    int? DocumentOrderNo);
