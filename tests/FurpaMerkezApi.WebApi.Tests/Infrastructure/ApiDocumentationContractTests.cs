using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Writers;
using Swashbuckle.AspNetCore.Swagger;
using Xunit;
using Xunit.Abstractions;

namespace FurpaMerkezApi.WebApi.Tests.Infrastructure;

public sealed class ApiDocumentationContractTests(ITestOutputHelper output)
{
    [Fact]
    public void Documentation_MatchesRegisteredApiContract()
    {
        using var factory = new FurpaWebApplicationFactory();
        using var client = factory.CreateClient();
        var descriptions = factory.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>()
            .ApiDescriptionGroups.Items.SelectMany(x => x.Items)
            .Where(x => x.ActionDescriptor is not ControllerActionDescriptor c || c.ControllerTypeInfo.Assembly == typeof(Program).Assembly)
            .OrderBy(x => x.RelativePath, StringComparer.Ordinal).ThenBy(x => x.HttpMethod, StringComparer.Ordinal).ToArray();
        var swagger = factory.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
        swagger.Paths.Remove("/api/test/fallback");
        // Global Swagger bearer metadata also marks anonymous actions; export effective endpoint access instead.
        swagger.SecurityRequirements.Clear();
        var extraRoutes = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<ControllerActionDescriptor>() is not { } c || c.ControllerTypeInfo.Assembly == typeof(Program).Assembly)
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["*"])
                .Select(method => new { Path = "/" + e.RoutePattern.RawText!.TrimStart('/'), Method = method }))
            .Where(route => !descriptions.Any(a => a.HttpMethod == route.Method && string.Equals(Normalize("/" + a.RelativePath!.Split('?')[0]), Normalize(route.Path), StringComparison.OrdinalIgnoreCase)))
            .Select(route => route.Path)
            .Distinct().Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "/health/live", "/health/ready" }, extraRoutes);
        var endpoints = new StringBuilder("# API Sozlesme Referansi\n\n");
        endpoints.AppendLine("Bu dosya test hostundaki ApiExplorer ve Swagger metadata'sindan uretilir. Canli DB veya dis servise baglanilmaz.");
        endpoints.AppendLine("Ana rehber: [UI_API_DOKUMANI.md](UI_API_DOKUMANI.md). Makine formati: [API_SOZLESMESI.json](API_SOZLESMESI.json).\n");
        endpoints.AppendLine("Kapsam: kayitli HTTP method/route ve alias'lar, binding kaynagi, request alanlari, tanimli response tipleri, controller/action yetki metadata'si ve erisilen JSON modelleri. Servis icindeki kosullu depo/permission kontrolleri, hata kodlari, feature flag'ler ve is kurallari ana rehberde ayrica okunmalidir. Modelde zorunluluk, yalniz OpenAPI/validasyon metadata'sini ifade eder; kosullu zorunluluklar ana rehberdedir.\n");
        endpoints.AppendLine("`bildirilmemis` response tipi, runtime cevabinin bos oldugu anlamina gelmez. Anonim endpointler konfigurasyon/ag/servis kontrollerine tabi olabilir. JWT fallback policy, acik Authorize olmasa da uygulanir.\n");
        endpoints.AppendLine($"Endpoint sayisi (method + route): {descriptions.Length}. Model sayisi: {swagger.Components.Schemas.Count}.\n");
        endpoints.AppendLine("Bunlara ek olarak EndpointDataSource uzerinden dogrulanan iki health route'u vardir: `/health/live`, `/health/ready`. Bu middleware route'larinda method kisiti yoktur; istemci GET kullanmalidir. Anonimdir, JSON dondurur; Healthy/Degraded=200, Unhealthy=503. `live` yalniz prosesi, `ready` core_dependencies ve operations_export_path kontrollerini olcer. Alanlar: status (string), durationMilliseconds (number), checks (ad -> status/durationMilliseconds/description/data). Tum runtime route'lari envanterde yer alir; yeni ve ApiExplorer disinda kalan route eklenirse test basarisiz olur.\n");
        endpoints.AppendLine("Ozel response'lar: `/` Hosting:ExposeDiagnosticsOnRoot=false iken yalniz service/status; true iken architecture/authDatabase/businessDatabase/swagger ekler. E-irsaliye PDF endpointleri application/pdf binary/inline doner. Operations authorization-files/saveauthorizationfile POST 201 body dondurmez. Ana rehberdeki ozel response notlari bu metadata bosluklarini tamamlar.\n");
        endpoints.AppendLine("## Endpointler\n");
        endpoints.AppendLine("| Method | Route | Yetki | Parametreler (kaynak: ad / tip) | Tanimli response |\n|---|---|---|---|---|");
        foreach (var api in descriptions)
        {
            var anonymous = api.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any();
            var auth = api.ActionDescriptor.EndpointMetadata.OfType<IAuthorizeData>().ToArray();
            var policies = auth.Where(x => !string.IsNullOrEmpty(x.Policy)).Select(x => x.Policy!).Distinct().Order().ToArray();
            var roles = auth.Where(x => !string.IsNullOrEmpty(x.Roles)).Select(x => "roles=" + x.Roles).ToArray();
            var access = anonymous ? "Anonim" : string.Join(" + ", new[] { "JWT" }.Concat(policies).Concat(roles));
            var path = "/" + api.RelativePath!.Split('?')[0];
            var operation = swagger.Paths[path].Operations[Enum.Parse<OperationType>(api.HttpMethod!, true)];
            operation.Extensions["x-permissions"] = new Microsoft.OpenApi.Any.OpenApiArray();
            foreach (var policy in policies)
                ((Microsoft.OpenApi.Any.OpenApiArray)operation.Extensions["x-permissions"]).Add(new Microsoft.OpenApi.Any.OpenApiString(policy));
            operation.Security = anonymous ? [] : [new OpenApiSecurityRequirement
            {
                [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = []
            }];
            var routeParameters = operation.Parameters.Where(p => p is not null).Select(p => FormatRouteParameter(p!));
            var bodyParameters = api.ParameterDescriptions.Where(p => p.Source?.Id == "Body")
                .Select(p => $"body: {p.Name} / {TypeName(p.Type)}{(p.IsRequired ? " (zorunlu)" : "")}");
            var parameters = string.Join("<br>", routeParameters.Concat(bodyParameters));
            var responses = string.Join("<br>", api.SupportedResponseTypes.Select(r => $"{r.StatusCode}: {TypeName(r.Type)}"));
            endpoints.AppendLine($"| {api.HttpMethod} | `{path}` | {access} | {parameters} | {responses} |");
        }
        endpoints.AppendLine("\n## JSON Modelleri\n");
        foreach (var (name, schema) in swagger.Components.Schemas.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            endpoints.AppendLine($"### {name}\n");
            endpoints.AppendLine("| JSON alani | Tip | Zorunlu | Sinirlar |\n|---|---|---|---|");
            foreach (var (field, value) in schema.Properties.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                var limits = new List<string>();
                if (value.MinLength.HasValue) limits.Add("minLength=" + value.MinLength);
                if (value.MaxLength.HasValue) limits.Add("maxLength=" + value.MaxLength);
                if (value.Minimum.HasValue) limits.Add("min=" + value.Minimum.Value.ToString(CultureInfo.InvariantCulture));
                if (value.Maximum.HasValue) limits.Add("max=" + value.Maximum.Value.ToString(CultureInfo.InvariantCulture));
                if (value.Pattern != null) limits.Add("pattern=" + value.Pattern.Replace("|", "\\|"));
                if (value.Nullable) limits.Add("nullable");
                endpoints.AppendLine($"| `{field}` | {SchemaType(value)} | {(schema.Required.Contains(field) ? "Evet" : "Hayir")} | {string.Join("; ", limits)} |");
            }
            if (schema.Enum.Count > 0)
                endpoints.AppendLine("\nEnum degerleri JSON sozlesmesinde bulunur.");
            endpoints.AppendLine();
        }
        using var jsonText = new StringWriter(CultureInfo.InvariantCulture);
        swagger.SerializeAsV3(new OpenApiJsonWriter(jsonText));
        var root = FindRoot();
        VerifyOrUpdate(Path.Combine(root, "docs/API_SOZLESME_REFERANSI.md"), endpoints.ToString());
        VerifyOrUpdate(Path.Combine(root, "docs/API_SOZLESMESI.json"), jsonText.ToString() + "\n");
        var uiDocumentationPath = Path.Combine(root, "docs/UI_API_DOKUMANI.md");
        var endpointContractStart = endpoints.ToString().IndexOf("## Endpointler", StringComparison.Ordinal);
        Assert.True(endpointContractStart >= 0, "Generated endpoint contract section was not found.");
        var uiResponseContract =
            "<!-- BEGIN AUTO-GENERATED API RESPONSE CONTRACT -->\n" +
            "## Tum Endpoint ve Response Sozlesmeleri\n\n" +
            "Bu bolum test hostundaki ApiExplorer ve Swagger metadata'sindan otomatik uretilir. " +
            "Her endpointin tanimli HTTP response tiplerini ve response modellerindeki tum JSON alanlarini icerir. " +
            "Elle duzenlenmemelidir; `FURPA_UPDATE_API_DOCS=true` ile sozlesme testi tarafindan yenilenir.\n\n" +
            endpoints.ToString()[endpointContractStart..].TrimEnd() + "\n" +
            "<!-- END AUTO-GENERATED API RESPONSE CONTRACT -->\n";
        VerifyOrUpdateGeneratedSection(uiDocumentationPath, uiResponseContract);
        var narrative = File.ReadAllText(uiDocumentationPath).Replace("\r\n", "\n");
        Assert.Contains("API_SOZLESME_REFERANSI.md", narrative);
        var missing = descriptions.Where(x => !Normalize(narrative).Contains(Normalize("/" + x.RelativePath!.Split('?')[0]), StringComparison.OrdinalIgnoreCase))
            .Select(x => $"{x.HttpMethod} /{x.RelativePath}").ToArray();
        output.WriteLine($"Contract: {descriptions.Length} endpoints, {swagger.Components.Schemas.Count} schemas. Narrative exact-template gaps: {missing.Length} (examples/aliases may cover these).");
        output.WriteLine(string.Join("\n", missing));
        var undocumentedActions = descriptions.Where(a => a.ActionDescriptor is ControllerActionDescriptor)
            .GroupBy(a => ((ControllerActionDescriptor)a.ActionDescriptor).MethodInfo)
            .Where(group => !group.Any(a => Normalize(narrative).Contains(Normalize("/" + a.RelativePath!.Split('?')[0]), StringComparison.OrdinalIgnoreCase)))
            .Select(g => g.Key.DeclaringType!.Name + "." + g.Key.Name).ToArray();
        Assert.Empty(undocumentedActions);
        Assert.Contains("Tam ve surumlenmis alan", narrative);
        Assert.DoesNotContain(descriptions, x => x.RelativePath!.StartsWith("api/test/", StringComparison.Ordinal));
    }

    private static string TypeName(Type? type) => type == null || type == typeof(void) ? "bildirilmemis" :
        type.IsGenericType ? type.Name.Split('`')[0] + "&lt;" + string.Join(",", type.GenericTypeArguments.Select(TypeName)) + "&gt;" : type.Name;
    private static string FormatRouteParameter(OpenApiParameter parameter) =>
        $"{parameter.In.ToString().ToLowerInvariant()}: {parameter.Name} / {SchemaType(parameter.Schema ?? new OpenApiSchema { Type = "unknown" })}{(parameter.Required ? " (zorunlu)" : "")}";
    private static string SchemaType(OpenApiSchema schema) => schema.Reference?.Id ??
        (schema.Type == "array" ? SchemaType(schema.Items) + "[]" : schema.Type + (schema.Format == null ? "" : " (" + schema.Format + ")"));
    private static string Normalize(string text) => Regex.Replace(text, @"\{[^}\r\n]+\}", "{param}");
    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "FurpaMerkezApi.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
    private static void VerifyOrUpdate(string path, string actual)
    {
        actual = actual.Replace("\r\n", "\n");
        if (Environment.GetEnvironmentVariable("FURPA_UPDATE_API_DOCS") == "true") File.WriteAllText(path, actual, new UTF8Encoding(false));
        Assert.True(File.Exists(path), $"Missing {Path.GetFileName(path)}. Regenerate with FURPA_UPDATE_API_DOCS=true.");
        Assert.Equal(File.ReadAllText(path).Replace("\r\n", "\n"), actual);
    }

    private static void VerifyOrUpdateGeneratedSection(string path, string generatedSection)
    {
        const string beginMarker = "<!-- BEGIN AUTO-GENERATED API RESPONSE CONTRACT -->";
        const string endMarker = "<!-- END AUTO-GENERATED API RESPONSE CONTRACT -->";

        generatedSection = generatedSection.Replace("\r\n", "\n");
        Assert.True(File.Exists(path), $"Missing {Path.GetFileName(path)}.");
        var current = File.ReadAllText(path).Replace("\r\n", "\n");
        var beginIndex = current.IndexOf(beginMarker, StringComparison.Ordinal);
        string expected;

        if (beginIndex < 0)
        {
            expected = current.TrimEnd() + "\n\n" + generatedSection;
        }
        else
        {
            var endIndex = current.IndexOf(endMarker, beginIndex, StringComparison.Ordinal);
            Assert.True(endIndex >= 0, $"Missing generated section end marker in {Path.GetFileName(path)}.");
            endIndex += endMarker.Length;
            expected = current[..beginIndex] + generatedSection.TrimEnd() + current[endIndex..];
        }

        if (Environment.GetEnvironmentVariable("FURPA_UPDATE_API_DOCS") == "true")
            File.WriteAllText(path, expected, new UTF8Encoding(false));

        Assert.Equal(File.ReadAllText(path).Replace("\r\n", "\n"), expected);
    }
}
