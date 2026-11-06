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

namespace ioBroker_NewGen.Admin.AdapterManagement;

internal sealed class AdapterConfigEndpoint : BaseRuntimeEndpoint
{
    private readonly IAdapterContext _context;

    public AdapterConfigEndpoint(IAdapterContext context) : base("/api/adapters/config")
    {
        _context = context;
    }

    protected override async Task<EndpointResponse> OnHandleAsync(EndpointRequest request)
    {
        if (!IsAuthorized(_context, request))
        {
            return Unauthorized();
        }

        JsonObject? body;
        try
        {
            body = request.Body is { Length: > 0 } ? JsonNode.Parse(request.Body) as JsonObject : null;
        }
        catch (JsonException)
        {
            body = null;
        }

        var adapterId = body?["adapterId"]?.GetValue<string>();
        var action = body?["action"]?.GetValue<string>() ?? "get";
        if (string.IsNullOrWhiteSpace(adapterId))
        {
            return Error(400, "adapterId fehlt");
        }

        var registration = _context.AdapterRegistry.Get(adapterId);
        var configs = _context.AdapterRegistry.Configs;
        if (registration is null || !configs.HasConfig(registration.Name))
        {
            return Error(404, "Adapter hat keine Einstellungen");
        }

        try
        {
            var schema = configs.GetSchema(registration.Name)!;
            var running = registration.Status == AdapterStatus.Connected;
            var managed = registration.Type == AdapterType.Custom || _context.Legacy?.IsInstance(registration.Id) == true;
            if (action == "save")
            {
                if (body!["values"] is not JsonObject values)
                {
                    return Error(400, "values fehlt");
                }

                configs.Save(registration.Id, registration.Name, values);
                var restarted = false;
                var allowRestart = body["restart"] is not JsonValue flag || !flag.TryGetValue<bool>(out var wanted) || wanted;
                if (managed && running && allowRestart)
                {
                    restarted = await _context.RestartAdapterAsync(registration.Id);
                }
                else if (!managed)
                {
                    configs.EnsureFile(registration.Id, registration.Name);
                }

                return Ok(new JsonObject
                {
                    ["saved"] = true,
                    ["restarted"] = restarted,
                    ["restartRequired"] = !managed && running
                });
            }

            var current = configs.GetValues(registration.Id, registration.Name);
            var oauth = new JsonArray();
            foreach (var provider in schema.OAuthProviders)
            {
                oauth.Add(new JsonObject
                {
                    ["id"] = provider.Id,
                    ["label"] = provider.Label,
                    ["redirectUri"] = provider.RedirectUri,
                    ["connected"] = !string.IsNullOrEmpty(current[provider.TokenKey]?.ToString())
                });
            }

            return Ok(new JsonObject
            {
                ["adapterId"] = registration.Id,
                ["name"] = registration.Name,
                ["type"] = registration.Type.ToString(),
                ["autoRestart"] = managed,
                ["running"] = running,
                ["oauth"] = oauth,
                ["jsonConfig"] = schema.JsonConfig is null ? null : JsonNode.Parse(schema.JsonConfig),
                ["values"] = configs.Mask(registration.Name, current),
                ["secrets"] = new JsonArray(configs.SecretKeys(registration.Name).Select(k => (JsonNode?)JsonValue.Create(k)).ToArray())
            });
        }
        catch (AdapterConfigException ex)
        {
            return Error(400, ex.Message);
        }
    }

    private static EndpointResponse Ok(JsonObject payload)
        => Response(200, payload.ToJsonString(), "application/json; charset=utf-8");

    private static EndpointResponse Error(int status, string message)
        => Response(status, new JsonObject { ["error"] = message }.ToJsonString(), "application/json; charset=utf-8");
}
