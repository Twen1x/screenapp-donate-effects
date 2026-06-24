using System.Globalization;
using System.Text.Json;

namespace ScreenApp.Donations;

/// <summary>
/// Разбор push-сообщения Centrifugo в <see cref="Donation"/>. Чистая логика без WS,
/// чтобы покрыть unit-тестами. Порт parse_donation из python-воркера: поддерживает
/// новый протокол Centrifugo v4+ (push.pub.data) и старый (result.data[.data]).
/// </summary>
public static class DonationParser
{
    /// <summary>
    /// Извлечь донат из распарсенного JSON-сообщения. Возвращает null, если это
    /// служебное сообщение (ack/ping) или не донат.
    /// </summary>
    public static Donation? Parse(JsonElement msg)
    {
        if (!TryGetData(msg, out JsonElement data))
        {
            return null;
        }

        decimal amount = ReadAmount(data);
        if (amount <= 0)
        {
            // Не донат по сумме — пропускаем, если это не явный alert_type=1.
            if (ReadString(data, "alert_type") != "1")
            {
                return null;
            }
        }

        return new Donation
        {
            DonationId = ReadString(data, "id"),
            Amount = amount,
            Username = Trim(ReadString(data, "username"), 255),
            Message = Trim(ReadString(data, "message"), 1000),
            IsTest = ReadBool(data, "_is_test_alert"),
        };
    }

    /// <summary>Удобная перегрузка: распарсить сырую JSON-строку.</summary>
    public static Donation? Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return Parse(doc.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryGetData(JsonElement msg, out JsonElement data)
    {
        data = default;

        // Новый протокол: { "push": { "pub": { "data": {...} } } }
        if (msg.TryGetProperty("push", out var push) &&
            push.TryGetProperty("pub", out var pub) &&
            pub.TryGetProperty("data", out var d1) &&
            d1.ValueKind == JsonValueKind.Object)
        {
            data = d1;
            return true;
        }

        // Старый протокол: { "result": { "data": {...} | { "data": {...} } } }
        if (msg.TryGetProperty("result", out var result) &&
            result.TryGetProperty("data", out var inner))
        {
            if (inner.ValueKind == JsonValueKind.Object &&
                inner.TryGetProperty("data", out var d2) &&
                d2.ValueKind == JsonValueKind.Object)
            {
                data = d2;
                return true;
            }

            if (inner.ValueKind == JsonValueKind.Object)
            {
                data = inner;
                return true;
            }
        }

        return false;
    }

    private static decimal ReadAmount(JsonElement data)
    {
        foreach (var key in new[] { "amount_main", "amount" })
        {
            if (!data.TryGetProperty(key, out var el))
            {
                continue;
            }

            switch (el.ValueKind)
            {
                case JsonValueKind.Number when el.TryGetDecimal(out var num):
                    return num;
                case JsonValueKind.String when decimal.TryParse(
                    el.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed):
                    return parsed;
            }
        }

        return 0m;
    }

    private static string? ReadString(JsonElement data, string key)
    {
        if (!data.TryGetProperty(key, out var el))
        {
            return null;
        }

        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.ToString(),
            _ => null,
        };
    }

    private static bool ReadBool(JsonElement data, string key) =>
        data.TryGetProperty(key, out var el) &&
        el.ValueKind == JsonValueKind.True;

    private static string? Trim(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Trim();
        return value.Length > max ? value[..max] : value;
    }
}
