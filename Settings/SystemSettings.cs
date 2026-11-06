using RSEV.Utilities.ApiBuilder;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ioBroker.Core.Security;
using ioBroker_NewGen.Controllers;
using ioBroker_NewGen.Core.Adapters;
using ioBroker_NewGen.Core.Adapters.Configuration;
using ioBroker_NewGen.Core.Bus;
using ioBroker_NewGen.Core.Matrix;
using ioBroker_NewGen.Core.Registry;
using ioBroker_NewGen.Core.Routing;
using ioBroker_NewGen.Core.Web;

namespace ioBroker_NewGen.Admin.Settings;

internal static class SystemSettings
{
    private static readonly string[] TemperatureUnits = { "°C", "°F" };
    private static readonly string[] DateFormats = { "DD.MM.YYYY", "DD/MM/YYYY", "MM/DD/YYYY", "YYYY-MM-DD" };
    private static readonly string[] Weekdays = { "monday", "sunday", "saturday" };

    public static readonly IReadOnlyDictionary<string, object> Defaults = new Dictionary<string, object>
    {
        ["system.temperatureUnit"] = "°C",
        ["system.currency"] = "€",
        ["system.dateFormat"] = "DD.MM.YYYY",
        ["system.isFloatComma"] = true,
        ["system.firstDayOfWeek"] = "monday",
        ["system.country"] = string.Empty,
        ["system.city"] = string.Empty,
        ["system.address"] = string.Empty,
        ["system.latitude"] = string.Empty,
        ["system.longitude"] = string.Empty
    };

    public static bool IsSystemKey(string key)
        => key == "system.setupDone" || Defaults.ContainsKey(key);

    public static object Validate(string key, JsonElement value)
    {
        switch (key)
        {
            case "system.setupDone":
            case "system.isFloatComma":
                return value.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => throw new InvalidDataException($"{key} muss ein boolescher Wert sein.")
                };
            case "system.temperatureUnit":
                return OneOf(key, value, TemperatureUnits);
            case "system.dateFormat":
                return OneOf(key, value, DateFormats);
            case "system.firstDayOfWeek":
                return OneOf(key, value, Weekdays);
            case "system.latitude":
                return Coordinate(key, value, 90);
            case "system.longitude":
                return Coordinate(key, value, 180);
            default:
            {
                var text = (value.ValueKind == JsonValueKind.String ? value.GetString() : null)
                    ?? throw new InvalidDataException($"{key} muss ein Text sein.");
                text = text.Trim();
                if (text.Length > 200 || text.Any(char.IsControl))
                {
                    throw new InvalidDataException($"{key} ist ungültig.");
                }

                return text;
            }
        }
    }

    private static string OneOf(string key, JsonElement value, string[] allowed)
    {
        var text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        return text is not null && allowed.Contains(text)
            ? text
            : throw new InvalidDataException($"{key} hat einen ungültigen Wert.");
    }

    private static string Coordinate(string key, JsonElement value, double limit)
    {
        var text = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString()?.Trim().Replace(',', '.') ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            _ => throw new InvalidDataException($"{key} muss eine Zahl sein.")
        };
        if (text.Length == 0)
        {
            return string.Empty;
        }

        if (!double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number)
            || double.IsNaN(number) || Math.Abs(number) > limit)
        {
            throw new InvalidDataException($"{key} liegt außerhalb des gültigen Bereichs (±{limit}).");
        }

        return number.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);
    }
}
