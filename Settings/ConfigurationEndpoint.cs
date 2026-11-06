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
using static ioBroker_NewGen.Admin.Auth.AuthGuard;
using static ioBroker_NewGen.Admin.Http.EndpointResults;
using static ioBroker_NewGen.Admin.Settings.AdminConfig;

namespace ioBroker_NewGen.Admin.Settings;

internal sealed class ConfigurationEndpoint : BaseRuntimeEndpoint
{
    private readonly IAdapterContext _context;

    public ConfigurationEndpoint(IAdapterContext context) : base("/api/config")
    {
        _context = context;
    }

    protected override async Task<EndpointResponse> OnHandleAsync(EndpointRequest request)
    {
        if (!IsAuthorized(_context, request))
        {
            return Unauthorized();
        }

        var values = new Dictionary<string, object?>
        {
            ["node.nodeId"] = ReadConfig("node.nodeId", string.Empty),
            ["node.instanceName"] = ReadConfig("node.instanceName", string.Empty),
            ["ipc.ipcHost"] = ReadConfig("ipc.ipcHost", "127.0.0.1"),
            ["ipc.ipcPort"] = ReadConfig("ipc.ipcPort", 5001),
            ["adapter.customAdapterDirectory"] = ReadConfig("adapter.customAdapterDirectory", "Adapters/custom"),
            ["web.port"] = ReadConfig("web.port", 8080)
        };
        foreach (var (key, fallback) in SystemSettings.Defaults)
        {
            values[key] = ReadConfig(key, fallback);
        }

        values["system.setupDone"] = ReadConfig("system.setupDone", false);

        if (request.Body is not { Length: > 0 })
        {
            return Response(200, JsonSerializer.Serialize(values), "application/json; charset=utf-8");
        }

        try
        {
            using var document = JsonDocument.Parse(request.Body);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return Response(400, "{\"error\":\"Konfiguration muss ein JSON-Objekt sein\"}", "application/json; charset=utf-8");
            }

            var changed = new List<string>();
            var toPersist = new Dictionary<string, object?>();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!values.ContainsKey(property.Name) ||
                    string.Equals(property.Name, "admin.adminToken", StringComparison.OrdinalIgnoreCase))
                {
                    return Response(400, JsonSerializer.Serialize(new { error = $"Konfigurationsschlüssel nicht editierbar: {property.Name}" }), "application/json; charset=utf-8");
                }

                var converted = SystemSettings.IsSystemKey(property.Name)
                    ? SystemSettings.Validate(property.Name, property.Value)
                    : ToConfigValue(property.Value);
                _context.Config.Set(property.Name, converted);
                toPersist[property.Name] = converted;
                changed.Add(property.Name);
            }

            var configPath = Path.Combine(AppContext.BaseDirectory, "config.toml");
            ioBroker_NewGen.Startup.ConfigFileWriter.Update(configPath, toPersist);
            return Response(200, JsonSerializer.Serialize(new { updated = changed }), "application/json; charset=utf-8");
        }
        catch (Exception ex)
        {
            return Response(400, JsonSerializer.Serialize(new { error = ex.Message }), "application/json; charset=utf-8");
        }
    }

    private T ReadConfig<T>(string key, T fallback)
    {
        try
        {
            return _context.Config.Get<T>(key);
        }
        catch
        {
            return fallback;
        }
    }

    private static object ToConfigValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when value.TryGetInt32(out var integer) => integer,
            JsonValueKind.Number when value.TryGetInt64(out var longInteger) => longInteger,
            JsonValueKind.Number when value.TryGetDouble(out var number) => number,
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Array => value.EnumerateArray()
                .Select(element => element.GetString() ?? string.Empty)
                .ToArray(),
            _ => throw new InvalidDataException("Nur boolesche, numerische, String- und String-Array-Werte sind zulässig.")
        };
    }
}
