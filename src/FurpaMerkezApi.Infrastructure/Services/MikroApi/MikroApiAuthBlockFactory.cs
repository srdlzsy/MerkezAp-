using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using FurpaMerkezApi.Application.Abstractions.Time;
using Microsoft.Extensions.Options;

namespace FurpaMerkezApi.Infrastructure.Services.MikroApi;

public sealed class MikroApiAuthBlockFactory(
    IOptionsMonitor<MikroApiOptions> options,
    IClock clock)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = null
    };

    public MikroApiAuthBlock CreateAuthBlock()
    {
        var currentOptions = GetRequiredAuthOptions();
        var hashDate = DateOnly.FromDateTime(clock.UtcNow.AddHours(currentOptions.HashDateUtcOffsetHours));

        return new MikroApiAuthBlock(
            currentOptions.FirmaKodu.Trim(),
            currentOptions.CalismaYili,
            currentOptions.KullaniciKodu.Trim(),
            CreateDailyPasswordHash(hashDate, currentOptions.SifreAnahtari),
            currentOptions.FirmaNo,
            currentOptions.SubeNo,
            currentOptions.ApiKey.Trim());
    }

    public JsonObject CreateLoginPayload()
    {
        var authBlock = CreateAuthBlock();

        return new JsonObject
        {
            [nameof(MikroApiAuthBlock.FirmaKodu)] = authBlock.FirmaKodu,
            [nameof(MikroApiAuthBlock.CalismaYili)] = authBlock.CalismaYili,
            [nameof(MikroApiAuthBlock.ApiKey)] = authBlock.ApiKey,
            [nameof(MikroApiAuthBlock.KullaniciKodu)] = authBlock.KullaniciKodu,
            [nameof(MikroApiAuthBlock.Sifre)] = authBlock.Sifre,
            [nameof(MikroApiAuthBlock.FirmaNo)] = authBlock.FirmaNo,
            [nameof(MikroApiAuthBlock.SubeNo)] = authBlock.SubeNo
        };
    }

    public JsonObject CreateEnvelopePayload(object? payload = null)
    {
        var root = new JsonObject
        {
            ["Mikro"] = JsonSerializer.SerializeToNode(CreateAuthBlock(), JsonOptions)
        };

        MergeObject(root, payload, "top-level payload");
        return root;
    }

    public JsonObject CreateMikroPayload(object? mikroPayload = null)
    {
        var root = new JsonObject
        {
            ["Mikro"] = JsonSerializer.SerializeToNode(CreateAuthBlock(), JsonOptions)
        };

        if (root["Mikro"] is not JsonObject mikroBlock)
        {
            throw new MikroApiException("Mikro auth block could not be created.");
        }

        MergeObject(mikroBlock, mikroPayload, "Mikro payload");
        return root;
    }

    public static string CreateDailyPasswordHash(DateOnly date, string passwordSeed)
    {
        var rawValue = $"{date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} {passwordSeed}";
        var bytes = Encoding.UTF8.GetBytes(rawValue);
        var hash = MD5.HashData(bytes);

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private MikroApiOptions GetRequiredAuthOptions()
    {
        var currentOptions = options.CurrentValue;

        if (string.IsNullOrWhiteSpace(currentOptions.FirmaKodu))
        {
            throw new MikroApiException("MikroApi:FirmaKodu is not configured.");
        }

        if (currentOptions.CalismaYili <= 0)
        {
            throw new MikroApiException("MikroApi:CalismaYili must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(currentOptions.KullaniciKodu))
        {
            throw new MikroApiException("MikroApi:KullaniciKodu is not configured.");
        }

        if (string.IsNullOrWhiteSpace(currentOptions.SifreAnahtari))
        {
            throw new MikroApiException("MikroApi:SifreAnahtari is not configured.");
        }

        if (string.IsNullOrWhiteSpace(currentOptions.ApiKey))
        {
            throw new MikroApiException("MikroApi:ApiKey is not configured.");
        }

        return currentOptions;
    }

    private static void MergeObject(JsonObject target, object? payload, string context)
    {
        if (payload is null)
        {
            return;
        }

        var payloadNode = JsonSerializer.SerializeToNode(payload, JsonOptions);

        if (payloadNode is null)
        {
            return;
        }

        if (payloadNode is not JsonObject payloadObject)
        {
            target["Data"] = payloadNode.DeepClone();
            return;
        }

        foreach (var property in payloadObject)
        {
            if (target.ContainsKey(property.Key))
            {
                throw new MikroApiException(
                    $"Mikro API {context} contains reserved field '{property.Key}'.");
            }

            target[property.Key] = property.Value?.DeepClone();
        }
    }
}

public sealed record MikroApiAuthBlock(
    string FirmaKodu,
    int CalismaYili,
    string KullaniciKodu,
    string Sifre,
    int FirmaNo,
    int SubeNo,
    string ApiKey);
