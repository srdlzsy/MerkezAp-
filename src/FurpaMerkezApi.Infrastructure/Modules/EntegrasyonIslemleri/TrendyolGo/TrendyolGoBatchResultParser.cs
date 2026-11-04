using System.Text.Json;

namespace FurpaMerkezApi.Infrastructure.Modules.EntegrasyonIslemleri.TrendyolGo;

internal static class TrendyolGoBatchResultParser
{
    public static TrendyolGoParsedBatchResult Parse(JsonElement response)
    {
        var status = ReadString(response, "status")?.ToUpperInvariant() ?? string.Empty;
        var itemCount = ReadInt(response, "itemCount") ?? 0;
        var failedItemCount = ReadInt(response, "failedItemCount") ?? 0;
        var successfulBarcodes = new HashSet<string>(StringComparer.Ordinal);
        var failedBarcodes = new HashSet<string>(StringComparer.Ordinal);
        var failureReasons = new List<string>();
        AppendFailureReasons(response, failureReasons);

        if (response.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            if (itemCount == 0)
            {
                itemCount = items.GetArrayLength();
            }

            foreach (var item in items.EnumerateArray())
            {
                var itemStatus = ReadString(item, "status")?.ToUpperInvariant() ?? string.Empty;
                var barcode = TryReadBarcode(item);
                if (itemStatus == "SUCCESS" && barcode is not null)
                {
                    successfulBarcodes.Add(barcode);
                }
                else if (itemStatus == "FAILED")
                {
                    if (barcode is not null)
                    {
                        failedBarcodes.Add(barcode);
                    }

                    AppendFailureReasons(item, failureReasons);
                }
            }

            if (failedItemCount == 0)
            {
                failedItemCount = failedBarcodes.Count;
            }
        }

        return new TrendyolGoParsedBatchResult(
            status,
            status is "COMPLETED" or "SUCCESS",
            status == "FAILED",
            itemCount,
            failedItemCount,
            successfulBarcodes,
            failedBarcodes,
            failureReasons.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static string? TryReadBarcode(JsonElement item)
    {
        if (!item.TryGetProperty("requestItem", out var requestItem) || requestItem.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return ReadString(requestItem, "barcode")
            ?? ReadNestedString(requestItem, "product", "barcode")
            ?? ReadNestedString(requestItem, "request", "barcode");
    }

    private static string? ReadNestedString(JsonElement value, string parentName, string name) =>
        value.TryGetProperty(parentName, out var parent) && parent.ValueKind == JsonValueKind.Object
            ? ReadString(parent, name)
            : null;

    private static string? ReadFailureReason(JsonElement reason)
    {
        if (reason.ValueKind == JsonValueKind.String)
        {
            return reason.GetString();
        }

        if (reason.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return ReadString(reason, "message")
            ?? ReadString(reason, "reason")
            ?? ReadString(reason, "description")
            ?? ReadString(reason, "code");
    }

    private static void AppendFailureReasons(JsonElement value, ICollection<string> target)
    {
        if (!value.TryGetProperty("failureReasons", out var reasons) || reasons.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var reason in reasons.EnumerateArray().Select(ReadFailureReason))
        {
            if (!string.IsNullOrWhiteSpace(reason))
            {
                target.Add(reason);
            }
        }
    }

    private static string? ReadString(JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(name, out var property) &&
        property.ValueKind == JsonValueKind.String
            ? property.GetString()?.Trim()
            : null;

    private static int? ReadInt(JsonElement value, string name) =>
        value.ValueKind == JsonValueKind.Object &&
        value.TryGetProperty(name, out var property) &&
        property.ValueKind == JsonValueKind.Number &&
        property.TryGetInt32(out var result)
            ? result
            : null;
}

internal sealed record TrendyolGoParsedBatchResult(
    string Status,
    bool IsCompleted,
    bool IsFailed,
    int ItemCount,
    int FailedItemCount,
    IReadOnlySet<string> SuccessfulBarcodes,
    IReadOnlySet<string> FailedBarcodes,
    IReadOnlyCollection<string> FailureReasons);
