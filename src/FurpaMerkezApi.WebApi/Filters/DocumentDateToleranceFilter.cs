using System.Collections;
using System.Reflection;
using System.Security.Claims;
using FurpaMerkezApi.WebApi.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FurpaMerkezApi.WebApi.Filters;

public sealed class DocumentDateToleranceFilter(Func<DateTime>? nowProvider = null) : IActionFilter
{
    public const int AllowedPastDays = 10;
    public const int AllowedFutureDays = 1;

    private static readonly ISet<string> DocumentDatePropertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "DocumentDate",
        "MovementDate",
        "OfficialDocumentDate",
        "SourceDocumentDate",
        "OrderDate",
        "CountDate",
        "GoodsAcceptanceDate",
        "SummaryDate",
        "BanknoteTrackDate",
        "Date"
    };

    private static readonly ISet<string> DeliveryDatePropertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "DeliveryDate"
    };

    private readonly Func<DateTime> _nowProvider = nowProvider ?? (() => DateTime.Today);

    public void OnActionExecuting(ActionExecutingContext context)
    {
        var httpMethod = context.HttpContext.Request.Method;
        if (!HttpMethods.IsPost(httpMethod) && !HttpMethods.IsPut(httpMethod))
        {
            return;
        }

        if (context.ActionDescriptor.EndpointMetadata.OfType<IgnoreDocumentDateToleranceAttribute>().Any())
        {
            return;
        }

        var validationEnabled =
            context.ActionDescriptor.EndpointMetadata.OfType<ValidateDocumentDateToleranceAttribute>().Any() ||
            context.ActionArguments.Values.Any(value =>
                value?.GetType().GetCustomAttribute<ValidateDocumentDateToleranceAttribute>() is not null);

        if (!validationEnabled)
        {
            return;
        }

        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated == true && user.CanAccessAllWarehouses())
        {
            return;
        }

        var today = _nowProvider().Date;
        var minDocumentDate = today.AddDays(-AllowedPastDays);
        var maxDocumentDate = today.AddDays(AllowedFutureDays);

        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);

        foreach (var argument in context.ActionArguments.Values)
        {
            ValidateValue(argument, minDocumentDate, maxDocumentDate, visited);
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }

    private static void ValidateValue(
        object? value,
        DateTime minDocumentDate,
        DateTime maxDocumentDate,
        ISet<object> visited)
    {
        if (value is null)
        {
            return;
        }

        if (value is string || IsSimpleType(value.GetType()))
        {
            return;
        }

        if (!visited.Add(value))
        {
            return;
        }

        if (value is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                ValidateValue(item, minDocumentDate, maxDocumentDate, visited);
            }

            return;
        }

        foreach (var property in value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (property.GetIndexParameters().Length > 0 || !property.CanRead)
            {
                continue;
            }

            if (property.GetCustomAttribute<IgnoreDocumentDateToleranceAttribute>() is not null)
            {
                continue;
            }

            var propertyValue = property.GetValue(value);
            if (propertyValue is null)
            {
                continue;
            }

            if (DocumentDatePropertyNames.Contains(property.Name))
            {
                if (TryGetDate(propertyValue, out var date))
                {
                    if (date < minDocumentDate || date > maxDocumentDate)
                    {
                        throw new ArgumentException(
                            $"Belge tarihi kabul edilebilir aralıkta değil. Tarih son {AllowedPastDays} gün ile yarın arasında olmalıdır. " +
                            $"(Alan: {property.Name}, Girilen Tarih: {date:yyyy-MM-dd}, İzin Verilen Aralık: {minDocumentDate:yyyy-MM-dd} - {maxDocumentDate:yyyy-MM-dd})");
                    }
                }

                continue;
            }

            if (DeliveryDatePropertyNames.Contains(property.Name))
            {
                if (TryGetDate(propertyValue, out var date))
                {
                    if (date < minDocumentDate)
                    {
                        throw new ArgumentException(
                            $"Teslim tarihi geçmiş bir tarih olamaz. Tarih en erken son {AllowedPastDays} gün içinde olmalıdır. " +
                            $"(Alan: {property.Name}, Girilen Tarih: {date:yyyy-MM-dd}, İzin Verilen En Erken: {minDocumentDate:yyyy-MM-dd})");
                    }
                }

                continue;
            }

            ValidateValue(propertyValue, minDocumentDate, maxDocumentDate, visited);
        }
    }

    private static bool TryGetDate(object value, out DateTime date)
    {
        if (value is DateTime dateTime)
        {
            date = dateTime.Date;
            return true;
        }

        if (value is DateOnly dateOnly)
        {
            date = dateOnly.ToDateTime(TimeOnly.MinValue);
            return true;
        }

        date = default;
        return false;
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

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property)]
public sealed class IgnoreDocumentDateToleranceAttribute : Attribute;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ValidateDocumentDateToleranceAttribute : Attribute;
