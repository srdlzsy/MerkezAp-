using System.Globalization;
using System.Reflection;

namespace FurpaMerkezApi.Infrastructure.Services.MikroApi;

internal static class MikroApiPartialUpdateRowMapper
{
    internal static IReadOnlyDictionary<string, object?> Snapshot(object row) =>
        row.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .ToDictionary(property => property.Name, property => property.GetValue(row), StringComparer.Ordinal);

    internal static Dictionary<string, object?> Build(
        object row,
        IReadOnlyDictionary<string, object?> original,
        string guidPropertyName)
    {
        var result = BuildCore(row, original, guidPropertyName);
        if (result.Count == 1)
        {
            throw new ArgumentException("At least one changed Mikro field is required.", nameof(row));
        }

        return result;
    }

    internal static Dictionary<string, object?>? TryBuild(
        object row,
        IReadOnlyDictionary<string, object?> original,
        string guidPropertyName)
    {
        var result = BuildCore(row, original, guidPropertyName);
        return result.Count == 1 ? null : result;
    }

    private static Dictionary<string, object?> BuildCore(
        object row,
        IReadOnlyDictionary<string, object?> original,
        string guidPropertyName)
    {
        var properties = row.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
        var guidProperty = properties.Single(property => property.Name == guidPropertyName);
        var result = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [guidPropertyName] = guidProperty.GetValue(row)
        };

        foreach (var property in properties)
        {
            if (property.Name == guidPropertyName || IsExcluded(property.Name))
            {
                continue;
            }

            var currentValue = property.GetValue(row);
            if (currentValue is not null &&
                original.TryGetValue(property.Name, out var originalValue) &&
                !Equals(currentValue, originalValue))
            {
                result[property.Name] = NormalizeValue(currentValue);
            }
        }

        return result;
    }

    private static bool IsExcluded(string name) =>
        name.EndsWith("_DBCno", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith("_SpecRECno", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith("_fileid", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith("_checksum", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith("_create_user", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith("_create_date", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith("_lastup_user", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith("_lastup_date", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith("_degisti", StringComparison.OrdinalIgnoreCase);

    private static object NormalizeValue(object value) => value switch
    {
        DateTime date => date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
        DateTimeOffset date => date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
        _ => value
    };
}
