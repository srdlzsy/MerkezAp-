using System.Globalization;
using System.Reflection;
using System.Text;
using FurpaMerkezApi.Application.Modules.EntegrasyonIslemleri.UyumsoftServisleri;
using Microsoft.Extensions.Options;
using UyumsoftDespatch = FurpaMerkezApi.Infrastructure.Services.ServiceReferences.Uyumsoft.Despatch;
using UyumsoftInvoice = FurpaMerkezApi.Infrastructure.Services.ServiceReferences.Uyumsoft.Invoice;

namespace FurpaMerkezApi.Infrastructure.Services;

public sealed class UyumsoftConnectedQueryService(IOptions<UyumsoftConnectedServicesOptions> options)
    : IUyumsoftConnectedQueryService
{
    public Task<UyumsoftConnectedServiceOverviewDto> GetOverviewAsync(
        UyumsoftConnectedServiceKind serviceKind,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var catalog = UyumsoftConnectedServiceCatalog.GetService(serviceKind);
        var config = ResolveServiceOptions(serviceKind, catalog);

        return Task.FromResult(new UyumsoftConnectedServiceOverviewDto(
            catalog.ServiceKey,
            catalog.ServiceName,
            config.EndpointUrl,
            config.WsdlUrl,
            config.ContractName,
            catalog.Operations
                .Select(operation => operation with
                {
                    SoapAction = operation.OperationName
                })
                .ToArray()));
    }

    public Task<IReadOnlyCollection<UyumsoftOperationDefinitionDto>> GetOperationsAsync(
        UyumsoftConnectedServiceKind serviceKind,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var catalog = UyumsoftConnectedServiceCatalog.GetService(serviceKind);

        IReadOnlyCollection<UyumsoftOperationDefinitionDto> operations = catalog.Operations
            .Select(operation => operation with
            {
                SoapAction = operation.OperationName
            })
            .ToArray();

        return Task.FromResult(operations);
    }

    public async Task<UyumsoftOperationResponseDto> InvokeGetOperationAsync(
        UyumsoftConnectedServiceKind serviceKind,
        UyumsoftOperationInvocationRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.OperationName))
        {
            throw new ArgumentException("Operation name is required.", nameof(request));
        }

        var catalog = UyumsoftConnectedServiceCatalog.GetService(serviceKind);
        var config = ResolveServiceOptions(serviceKind, catalog);
        var operation = UyumsoftConnectedServiceCatalog.GetGetOperation(serviceKind, request.OperationName);

        return serviceKind switch
        {
            UyumsoftConnectedServiceKind.EInvoice => await InvokeInvoiceOperationAsync(
                catalog,
                config,
                operation.OperationName,
                request.Parameters,
                cancellationToken),

            UyumsoftConnectedServiceKind.EDespatch => await InvokeDespatchOperationAsync(
                catalog,
                config,
                operation.OperationName,
                request.Parameters,
                cancellationToken),

            _ => throw new ArgumentOutOfRangeException(nameof(serviceKind), serviceKind, "Unsupported Uyumsoft service.")
        };
    }

    public Task<byte[]> GetInboxInvoicePdfFileAsync(
        string invoiceId,
        CancellationToken cancellationToken) =>
        GetInvoicePdfFileAsync(invoiceId, isInbox: true, cancellationToken);

    public Task<byte[]> GetOutboxInvoicePdfFileAsync(
        string invoiceId,
        CancellationToken cancellationToken) =>
        GetInvoicePdfFileAsync(invoiceId, isInbox: false, cancellationToken);

    private UyumsoftServiceEndpointOptions ResolveServiceOptions(
        UyumsoftConnectedServiceKind serviceKind,
        UyumsoftServiceCatalogEntry catalog)
    {
        var configured = serviceKind switch
        {
            UyumsoftConnectedServiceKind.EInvoice => options.Value.EInvoice,
            UyumsoftConnectedServiceKind.EDespatch => options.Value.EDespatch,
            _ => throw new ArgumentOutOfRangeException(nameof(serviceKind), serviceKind, "Unsupported Uyumsoft service.")
        };

        var resolved = configured with
        {
            EndpointUrl = string.IsNullOrWhiteSpace(configured.EndpointUrl)
                ? catalog.DefaultEndpointUrl
                : configured.EndpointUrl,
            WsdlUrl = string.IsNullOrWhiteSpace(configured.WsdlUrl)
                ? catalog.DefaultWsdlUrl
                : configured.WsdlUrl,
            ContractName = string.IsNullOrWhiteSpace(configured.ContractName)
                ? catalog.ContractName
                : configured.ContractName
        };

        if (string.IsNullOrWhiteSpace(resolved.EndpointUrl))
        {
            throw new InvalidOperationException($"{catalog.ServiceName} endpoint configuration is required.");
        }

        if (string.IsNullOrWhiteSpace(resolved.Username))
        {
            throw new InvalidOperationException($"{catalog.ServiceName} username configuration is required.");
        }

        if (string.IsNullOrWhiteSpace(resolved.Password))
        {
            throw new InvalidOperationException($"{catalog.ServiceName} password configuration is required.");
        }

        return resolved;
    }

    private static async Task<UyumsoftOperationResponseDto> InvokeInvoiceOperationAsync(
        UyumsoftServiceCatalogEntry catalog,
        UyumsoftServiceEndpointOptions config,
        string operationName,
        IReadOnlyCollection<UyumsoftOperationParameterRequest> parameters,
        CancellationToken cancellationToken)
    {
        var client = UyumsoftWcfClientHelper.CreateInvoiceClient(config);

        try
        {
            var response = await InvokeClientOperationAsync(
                client,
                operationName,
                parameters,
                type => type == typeof(UyumsoftInvoice.UserInformation)
                    ? UyumsoftWcfClientHelper.CreateInvoiceUserInfo(config)
                    : null,
                cancellationToken);

            return UyumsoftWcfClientHelper.ToOperationResponse(catalog, operationName, response);
        }
        catch
        {
            UyumsoftWcfClientHelper.Abort(client);
            throw;
        }
        finally
        {
            await UyumsoftWcfClientHelper.CloseAsync(client);
        }
    }

    private async Task<byte[]> GetInvoicePdfFileAsync(
        string invoiceId,
        bool isInbox,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(invoiceId))
        {
            throw new ArgumentException("Invoice ID is required.", nameof(invoiceId));
        }

        var catalog = UyumsoftConnectedServiceCatalog.GetService(UyumsoftConnectedServiceKind.EInvoice);
        var config = ResolveServiceOptions(UyumsoftConnectedServiceKind.EInvoice, catalog);
        var client = UyumsoftWcfClientHelper.CreateInvoiceClient(config);

        try
        {
            var userInfo = UyumsoftWcfClientHelper.CreateInvoiceUserInfo(config);
            var normalizedInvoiceId = invoiceId.Trim();
            var response = isInbox
                ? await client.GetInboxInvoicePdfAsync(userInfo, normalizedInvoiceId)
                    .WaitAsync(cancellationToken)
                : await client.GetOutboxInvoicePdfAsync(userInfo, normalizedInvoiceId)
                    .WaitAsync(cancellationToken);

            if (!response.IsSucceded)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(response.Message)
                        ? "Uyumsoft PDF istegini reddetti."
                        : response.Message);
            }

            var pdfData = response.Value?.Data;
            if (pdfData is null || pdfData.Length == 0)
            {
                throw new InvalidOperationException("Uyumsoft PDF cevabinda dosya verisi bulunamadi.");
            }

            return NormalizePdfData(pdfData);
        }
        catch
        {
            UyumsoftWcfClientHelper.Abort(client);
            throw;
        }
        finally
        {
            await UyumsoftWcfClientHelper.CloseAsync(client);
        }
    }

    private static async Task<UyumsoftOperationResponseDto> InvokeDespatchOperationAsync(
        UyumsoftServiceCatalogEntry catalog,
        UyumsoftServiceEndpointOptions config,
        string operationName,
        IReadOnlyCollection<UyumsoftOperationParameterRequest> parameters,
        CancellationToken cancellationToken)
    {
        var client = UyumsoftWcfClientHelper.CreateDespatchClient(config);

        try
        {
            var response = await InvokeClientOperationAsync(
                client,
                operationName,
                parameters,
                type => type == typeof(UyumsoftDespatch.UserInformation)
                    ? UyumsoftWcfClientHelper.CreateDespatchUserInfo(config)
                    : null,
                cancellationToken);

            return UyumsoftWcfClientHelper.ToOperationResponse(catalog, operationName, response);
        }
        catch
        {
            UyumsoftWcfClientHelper.Abort(client);
            throw;
        }
        finally
        {
            await UyumsoftWcfClientHelper.CloseAsync(client);
        }
    }

    private static async Task<object?> InvokeClientOperationAsync(
        object client,
        string operationName,
        IReadOnlyCollection<UyumsoftOperationParameterRequest> parameters,
        Func<Type, object?> specialArgumentFactory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var methodName = $"{operationName}Async";
        var method = client.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(item =>
                string.Equals(item.Name, methodName, StringComparison.OrdinalIgnoreCase) &&
                typeof(Task).IsAssignableFrom(item.ReturnType));

        if (method is null)
        {
            throw new ArgumentException($"{operationName} WCF operation was not found.");
        }

        var bag = new ParameterBag(parameters);
        var arguments = method.GetParameters()
            .Select(parameter => BuildArgument(parameter.ParameterType, parameter.Name ?? string.Empty, bag, specialArgumentFactory))
            .ToArray();
        var task = (Task?)method.Invoke(client, arguments)
                   ?? throw new InvalidOperationException($"{operationName} WCF operation did not return a task.");

        await task.WaitAsync(cancellationToken);

        return task.GetType().GetProperty("Result", BindingFlags.Instance | BindingFlags.Public)
            ?.GetValue(task);
    }

    private static object? BuildArgument(
        Type targetType,
        string parameterName,
        ParameterBag bag,
        Func<Type, object?> specialArgumentFactory)
    {
        var specialArgument = specialArgumentFactory(targetType);
        if (specialArgument is not null)
        {
            return specialArgument;
        }

        if (targetType.IsArray)
        {
            return BuildArrayArgument(targetType.GetElementType()!, parameterName, bag);
        }

        if (IsSimpleType(targetType) &&
            TryConvertSimple(targetType, bag.GetSingle(parameterName, required: true), out var simpleValue))
        {
            return simpleValue;
        }

        var instance = Activator.CreateInstance(targetType)
                       ?? throw new InvalidOperationException($"{targetType.Name} could not be created.");

        PopulateObject(instance, bag);

        return instance;
    }

    private static Array BuildArrayArgument(Type elementType, string parameterName, ParameterBag bag)
    {
        var values = bag.GetMany(parameterName);
        if (values.Count == 0)
        {
            values = bag.GetMany(ToSingular(parameterName));
        }

        var array = Array.CreateInstance(elementType, values.Count);

        for (var i = 0; i < values.Count; i++)
        {
            if (!TryConvertSimple(elementType, values[i], out var converted))
            {
                throw new ArgumentException($"{parameterName} parameter could not be converted to {elementType.Name}.");
            }

            array.SetValue(converted, i);
        }

        return array;
    }

    private static void PopulateObject(object instance, ParameterBag bag)
    {
        var properties = instance.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property => property.CanWrite && property.GetIndexParameters().Length == 0);

        foreach (var property in properties)
        {
            if (property.PropertyType.IsArray)
            {
                var values = bag.GetMany(property.Name);
                if (values.Count == 0)
                {
                    values = bag.GetMany(ToSingular(property.Name));
                }

                if (values.Count == 0)
                {
                    continue;
                }

                property.SetValue(instance, BuildArrayValue(property.PropertyType.GetElementType()!, values));
                continue;
            }

            var value = bag.GetSingle(property.Name, required: false);
            if (value is null)
            {
                continue;
            }

            if (TryConvertSimple(property.PropertyType, value, out var converted))
            {
                property.SetValue(instance, converted);
            }
        }

        ApplyPagedDefaults(instance);
    }

    private static Array BuildArrayValue(Type elementType, IReadOnlyList<string?> values)
    {
        var array = Array.CreateInstance(elementType, values.Count);

        for (var i = 0; i < values.Count; i++)
        {
            if (!TryConvertSimple(elementType, values[i], out var converted))
            {
                throw new ArgumentException($"Parameter value could not be converted to {elementType.Name}.");
            }

            array.SetValue(converted, i);
        }

        return array;
    }

    private static void ApplyPagedDefaults(object instance)
    {
        var pageIndexProperty = instance.GetType().GetProperty("PageIndex", BindingFlags.Instance | BindingFlags.Public);
        var pageSizeProperty = instance.GetType().GetProperty("PageSize", BindingFlags.Instance | BindingFlags.Public);

        if (pageIndexProperty is not null && pageIndexProperty.PropertyType == typeof(int) &&
            (int)(pageIndexProperty.GetValue(instance) ?? 0) < 0)
        {
            pageIndexProperty.SetValue(instance, 0);
        }

        if (pageSizeProperty is not null && pageSizeProperty.PropertyType == typeof(int) &&
            (int)(pageSizeProperty.GetValue(instance) ?? 0) <= 0)
        {
            pageSizeProperty.SetValue(instance, 50);
        }
    }

    private static bool TryConvertSimple(Type targetType, string? rawValue, out object? converted)
    {
        targetType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        converted = null;

        if (targetType == typeof(string))
        {
            converted = rawValue ?? string.Empty;
            return true;
        }

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            converted = targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            return true;
        }

        if (targetType == typeof(bool))
        {
            converted = bool.Parse(rawValue);
            return true;
        }

        if (targetType == typeof(int))
        {
            converted = int.Parse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture);
            return true;
        }

        if (targetType == typeof(long))
        {
            converted = long.Parse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture);
            return true;
        }

        if (targetType == typeof(decimal))
        {
            converted = decimal.Parse(rawValue, NumberStyles.Number, CultureInfo.InvariantCulture);
            return true;
        }

        if (targetType == typeof(DateTime))
        {
            converted = DateTime.Parse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces);
            return true;
        }

        if (targetType == typeof(Guid))
        {
            converted = Guid.Parse(rawValue);
            return true;
        }

        if (targetType.IsEnum)
        {
            converted = Enum.Parse(targetType, rawValue, ignoreCase: true);
            return true;
        }

        return false;
    }

    private static bool IsSimpleType(Type targetType)
    {
        targetType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        return targetType == typeof(string) ||
               targetType == typeof(bool) ||
               targetType == typeof(int) ||
               targetType == typeof(long) ||
               targetType == typeof(decimal) ||
               targetType == typeof(DateTime) ||
               targetType == typeof(Guid) ||
               targetType.IsEnum;
    }

    private static byte[] NormalizePdfData(byte[] data)
    {
        if (HasPdfSignature(data))
        {
            return data;
        }

        try
        {
            var decoded = Convert.FromBase64String(Encoding.ASCII.GetString(data).Trim());
            if (HasPdfSignature(decoded))
            {
                return decoded;
            }
        }
        catch (FormatException)
        {
            // The WCF response should normally already contain decoded PDF bytes.
        }

        throw new InvalidOperationException("Uyumsoft cevabi gecerli bir PDF dosyasi degil.");
    }

    private static bool HasPdfSignature(ReadOnlySpan<byte> data) =>
        data.Length >= 5 &&
        data[0] == (byte)'%' &&
        data[1] == (byte)'P' &&
        data[2] == (byte)'D' &&
        data[3] == (byte)'F' &&
        data[4] == (byte)'-';

    private static string ToSingular(string name) =>
        name.EndsWith("ies", StringComparison.OrdinalIgnoreCase)
            ? $"{name[..^3]}y"
            : name.EndsWith("s", StringComparison.OrdinalIgnoreCase)
                ? name[..^1]
                : name;

    private sealed class ParameterBag(IReadOnlyCollection<UyumsoftOperationParameterRequest> parameters)
    {
        private readonly Dictionary<string, List<string?>> values = parameters
            .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Name))
            .GroupBy(parameter => NormalizeName(parameter.Name), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(parameter => parameter.Value).ToList(),
                StringComparer.OrdinalIgnoreCase);

        public string? GetSingle(string name, bool required)
        {
            var normalizedName = NormalizeName(name);
            if (values.TryGetValue(normalizedName, out var exactValues) && exactValues.Count > 0)
            {
                return exactValues[^1];
            }

            var singular = NormalizeName(ToSingular(name));
            if (values.TryGetValue(singular, out var singularValues) && singularValues.Count > 0)
            {
                return singularValues[^1];
            }

            if (required)
            {
                throw new ArgumentException($"{name} parameter is required.");
            }

            return null;
        }

        public IReadOnlyList<string?> GetMany(string name)
        {
            var normalizedName = NormalizeName(name);
            return values.TryGetValue(normalizedName, out var exactValues)
                ? exactValues
                : Array.Empty<string?>();
        }

        private static string NormalizeName(string name) =>
            name.Replace("_", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Trim()
                .ToLowerInvariant();
    }
}
