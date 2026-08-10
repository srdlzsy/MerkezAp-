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
            BuildOperationDefinitions(serviceKind, catalog.Operations)));
    }

    public Task<IReadOnlyCollection<UyumsoftOperationDefinitionDto>> GetOperationsAsync(
        UyumsoftConnectedServiceKind serviceKind,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var catalog = UyumsoftConnectedServiceCatalog.GetService(serviceKind);

        IReadOnlyCollection<UyumsoftOperationDefinitionDto> operations =
            BuildOperationDefinitions(serviceKind, catalog.Operations);

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

    private static IReadOnlyCollection<UyumsoftOperationDefinitionDto> BuildOperationDefinitions(
        UyumsoftConnectedServiceKind serviceKind,
        IReadOnlyCollection<UyumsoftOperationDefinitionDto> operations)
    {
        var contractType = serviceKind switch
        {
            UyumsoftConnectedServiceKind.EInvoice => typeof(UyumsoftInvoice.IBasicIntegration),
            UyumsoftConnectedServiceKind.EDespatch => typeof(UyumsoftDespatch.IBasicDespatchIntegration),
            _ => throw new ArgumentOutOfRangeException(nameof(serviceKind), serviceKind, "Unsupported Uyumsoft service.")
        };

        return operations
            .Select(operation => operation with
            {
                SoapAction = operation.OperationName,
                Parameters = BuildOperationParameterDefinitions(contractType, operation.OperationName)
            })
            .ToArray();
    }

    private static IReadOnlyCollection<UyumsoftOperationParameterDefinitionDto> BuildOperationParameterDefinitions(
        Type contractType,
        string operationName)
    {
        var method = FindOperationMethod(contractType, operationName);
        if (method is null)
        {
            return Array.Empty<UyumsoftOperationParameterDefinitionDto>();
        }

        var parameters = new List<UyumsoftOperationParameterDefinitionDto>();

        foreach (var parameter in method.GetParameters())
        {
            if (IsUyumsoftUserInformation(parameter.ParameterType))
            {
                continue;
            }

            var parameterName = parameter.Name ?? string.Empty;
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                continue;
            }

            if (IsSupportedScalarParameter(parameter.ParameterType))
            {
                parameters.Add(CreateParameterDefinition(parameterName, parameter.ParameterType, isRequired: true));
                continue;
            }

            foreach (var property in GetSupportedProperties(parameter.ParameterType))
            {
                parameters.Add(CreateParameterDefinition(property.Name, property.PropertyType, isRequired: false));
            }
        }

        return parameters
            .GroupBy(parameter => NormalizeParameterName(parameter.Name), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    private static MethodInfo? FindOperationMethod(Type contractType, string operationName)
    {
        var methodName = $"{operationName}Async";

        return contractType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(item =>
                string.Equals(item.Name, methodName, StringComparison.OrdinalIgnoreCase) &&
                typeof(Task).IsAssignableFrom(item.ReturnType));
    }

    private static bool IsUyumsoftUserInformation(Type targetType) =>
        targetType == typeof(UyumsoftInvoice.UserInformation) ||
        targetType == typeof(UyumsoftDespatch.UserInformation);

    private static IReadOnlyCollection<PropertyInfo> GetSupportedProperties(Type modelType)
    {
        var typeHierarchy = new Stack<Type>();
        for (var currentType = modelType; currentType is not null && currentType != typeof(object); currentType = currentType.BaseType)
        {
            typeHierarchy.Push(currentType);
        }

        var properties = new List<PropertyInfo>();
        while (typeHierarchy.Count > 0)
        {
            var currentType = typeHierarchy.Pop();
            properties.AddRange(currentType
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(property =>
                    property.CanWrite &&
                    property.GetIndexParameters().Length == 0 &&
                    IsSupportedScalarParameter(property.PropertyType)));
        }

        return properties;
    }

    private static bool IsSupportedScalarParameter(Type targetType)
    {
        if (targetType.IsArray)
        {
            return IsSimpleType(targetType.GetElementType()!);
        }

        return IsSimpleType(targetType);
    }

    private static UyumsoftOperationParameterDefinitionDto CreateParameterDefinition(
        string name,
        Type parameterType,
        bool isRequired)
    {
        var isArray = parameterType.IsArray;
        var valueType = isArray ? parameterType.GetElementType()! : parameterType;
        var actualType = Nullable.GetUnderlyingType(valueType) ?? valueType;

        return new UyumsoftOperationParameterDefinitionDto(
            name,
            $"{GetParameterTypeName(actualType)}{(isArray ? "[]" : string.Empty)}",
            isArray,
            isRequired,
            GetParameterDescription(name, isArray),
            actualType.IsEnum ? Enum.GetNames(actualType) : Array.Empty<string>());
    }

    private static string GetParameterTypeName(Type targetType)
    {
        targetType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (targetType == typeof(string))
        {
            return "string";
        }

        if (targetType == typeof(bool))
        {
            return "bool";
        }

        if (targetType == typeof(int))
        {
            return "int";
        }

        if (targetType == typeof(long))
        {
            return "long";
        }

        if (targetType == typeof(decimal))
        {
            return "decimal";
        }

        if (targetType == typeof(DateTime))
        {
            return "dateTime";
        }

        if (targetType == typeof(Guid))
        {
            return "guid";
        }

        return targetType.IsEnum ? targetType.Name : targetType.Name;
    }

    private static string? GetParameterDescription(string name, bool isArray) =>
        name switch
        {
            "PageIndex" => "0 tabanli sayfa indexi. Bos veya eksi gelirse 0 kabul edilir.",
            "PageSize" => "Sayfa boyutu. Bos veya 0 gelirse 50 kabul edilir.",
            "ExecutionStartDate" => "Belge/duzenleme tarihi baslangic filtresi.",
            "ExecutionEndDate" => "Belge/duzenleme tarihi bitis filtresi.",
            "ActualDespatchStartDate" => "Fiili sevk tarihi baslangic filtresi.",
            "ActualDespatchEndDate" => "Fiili sevk tarihi bitis filtresi.",
            "CreateStartDate" => "Uyumsoft kayit/olusturma tarihi baslangic filtresi.",
            "CreateEndDate" => "Uyumsoft kayit/olusturma tarihi bitis filtresi.",
            "SystemCreateDateBegin" => "Sistem kullanicisi olusturma tarihi baslangic filtresi.",
            "SystemCreateDateEnd" => "Sistem kullanicisi olusturma tarihi bitis filtresi.",
            "FirstCreateDateBegin" => "Ilk olusturma tarihi baslangic filtresi.",
            "FirstCreateDateEnd" => "Ilk olusturma tarihi bitis filtresi.",
            "UpdateDateBegin" => "Guncelleme tarihi baslangic filtresi.",
            "UpdateDateEnd" => "Guncelleme tarihi bitis filtresi.",
            "Filter" => "Metin arama filtresi.",
            "InvoiceIds" => "Fatura UUID filtreleri.",
            "InvoiceNumbers" => "Fatura numarasi filtreleri.",
            "DespatchIds" => "Irsaliye UUID filtreleri.",
            "DespatchNumbers" => "Irsaliye numarasi filtreleri.",
            "ReceiptAdviceIds" => "Makbuz UUID filtreleri.",
            "ReceiptAdviceNumbers" => "Makbuz numarasi filtreleri.",
            "Status" => "Tek durum filtresi.",
            "Statuses" => "Durum filtreleri.",
            "StatusInList" => "Dahil edilecek durum listesi.",
            "StatusNotInList" => "Haric tutulacak durum listesi.",
            "SortColumn" => "Siralama kolonu.",
            "SortMode" => "Siralama yonu.",
            "IsArchived" => "Arsiv durum filtresi.",
            "OnlyNewestInvoices" => "Ayni belge icin sadece en guncel fatura kaydini getirir.",
            "OnlyNewestDespatches" => "Ayni belge icin sadece en guncel irsaliye kaydini getirir.",
            "SetTaken" => "Gelen belgeyi Uyumsoft tarafinda alindi/isleniyor olarak isaretler.",
            "TargetTitle" => "Alici/unvan filtresi.",
            "TargetTcknVkn" => "Alici VKN/TCKN filtresi.",
            "Scenario" => "Fatura senaryo filtresi.",
            "startDate" => "Rapor baslangic tarihi.",
            "endDate" => "Rapor bitis tarihi.",
            "periodFormat" => "Rapor periyot formati.",
            "invoiceId" => "Fatura UUID bilgisi.",
            "despatchId" => "Irsaliye UUID bilgisi.",
            "isInbox" => "true ise gelen kutusu, false ise giden kutusu.",
            "format" => "Tarih format degeri.",
            "vknTckn" => "VKN/TCKN bilgisi.",
            _ when isArray => "Ayni parameter adi tekrar edilerek coklu deger gonderilebilir; tekil ad da kabul edilir.",
            _ => null
        };

    private static string NormalizeParameterName(string name) =>
        name.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToLowerInvariant();

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

        values = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

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

                values = values
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .ToArray();

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
