using System.Collections;
using System.Reflection;
using FurpaMerkezApi.WebApi.Extensions;
using FurpaMerkezApi.WebApi.Security;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FurpaMerkezApi.WebApi.Filters;

public sealed class WarehouseAccessFilter : IActionFilter
{
    private static readonly ISet<string> ScopedWarehousePropertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "WarehouseNo",
        "BranchNo",
        "InWarehouseNo"
    };

    public void OnActionExecuting(ActionExecutingContext context)
    {
        var user = context.HttpContext.User;

        if (user.Identity?.IsAuthenticated != true || user.IsAdministrator())
        {
            return;
        }

        var currentWarehouseNo = user.GetRequiredWarehouseNo();
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);

        foreach (var argument in context.ActionArguments.ToArray())
        {
            context.ActionArguments[argument.Key] = NormalizeValue(
                argument.Key,
                argument.Value,
                currentWarehouseNo,
                visited);
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }

    private static object? NormalizeValue(
        string? name,
        object? value,
        int currentWarehouseNo,
        ISet<object> visited)
    {
        if (value is null)
        {
            return IsScopedWarehouseName(name) ? currentWarehouseNo : null;
        }

        if (IsScopedWarehouseName(name) && TryGetWarehouseNo(value, out var requestedWarehouseNo))
        {
            EnsureWarehouseAccess(currentWarehouseNo, requestedWarehouseNo);
            return currentWarehouseNo;
        }

        if (value is string || IsSimpleType(value.GetType()))
        {
            return value;
        }

        if (!visited.Add(value))
        {
            return value;
        }

        if (value is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                NormalizeValue(null, item, currentWarehouseNo, visited);
            }

            return value;
        }

        foreach (var property in value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.GetIndexParameters().Length > 0 || !property.CanRead)
            {
                continue;
            }

            var propertyValue = property.GetValue(value);

            if (IsScopedWarehouseName(property.Name))
            {
                if (property.PropertyType == typeof(int))
                {
                    EnsureWarehouseAccess(currentWarehouseNo, propertyValue);
                    SetPropertyValue(property, value, currentWarehouseNo);
                }
                else if (property.PropertyType == typeof(int?))
                {
                    EnsureWarehouseAccess(currentWarehouseNo, propertyValue);
                    SetPropertyValue(property, value, currentWarehouseNo);
                }

                continue;
            }

            NormalizeValue(property.Name, propertyValue, currentWarehouseNo, visited);
        }

        return value;
    }

    private static bool IsScopedWarehouseName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && ScopedWarehousePropertyNames.Contains(name);

    private static bool TryGetWarehouseNo(object value, out int? warehouseNo)
    {
        warehouseNo = value is int number ? number : null;

        return value is int;
    }

    private static void EnsureWarehouseAccess(int currentWarehouseNo, object? requestedWarehouseNo)
    {
        if (requestedWarehouseNo is null)
        {
            return;
        }

        if (requestedWarehouseNo is int warehouseNo && warehouseNo != currentWarehouseNo)
        {
            throw new ForbiddenAccessException("Current user is not allowed to access the requested warehouse.");
        }
    }

    private static void SetPropertyValue(PropertyInfo property, object target, int currentWarehouseNo)
    {
        if (property.SetMethod is null)
        {
            return;
        }

        property.SetValue(target, currentWarehouseNo);
    }

    private static bool IsSimpleType(Type type) =>
        type.IsPrimitive ||
        type.IsEnum ||
        type == typeof(decimal) ||
        type == typeof(DateTime) ||
        type == typeof(DateOnly) ||
        type == typeof(TimeOnly) ||
        type == typeof(Guid);
}
