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

internal sealed class AdapterOAuthEndpoint : BaseRuntimeEndpoint
{
    private readonly IAdapterContext _context;

    public AdapterOAuthEndpoint(IAdapterContext context) : base("/api/adapters/oauth")
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

        var action = body?["action"]?.ToString();
        var oauth = _context.AdapterRegistry.OAuth;
        try
        {
            switch (action)
            {
                case "start":
                {
                    var registration = _context.AdapterRegistry.Get(body?["adapterId"]?.ToString() ?? string.Empty);
                    if (registration is null)
                    {
                        return Error(404, "Adapter nicht gefunden");
                    }

                    var session = oauth.Start(registration.Id, registration.Name, body?["provider"]?.ToString() ?? string.Empty);
                    return Ok(Describe(session));
                }
                case "status":
                {
                    var session = oauth.Get(body?["sessionId"]?.ToString() ?? string.Empty);
                    if (session is null)
                    {
                        return Error(404, "Sitzung nicht gefunden");
                    }

                    var restarted = false;
                    if (session.State == "success" && !session.Handled)
                    {
                        session.Handled = true;
                        var registration = _context.AdapterRegistry.Get(session.InstanceId);
                        if (registration is { Type: AdapterType.Custom, Status: AdapterStatus.Connected })
                        {
                            restarted = await _context.RestartAdapterAsync(registration.Id);
                        }
                    }

                    var result = Describe(session);
                    result["restarted"] = restarted;
                    return Ok(result);
                }
                case "cancel":
                    return Ok(new JsonObject { ["cancelled"] = oauth.Cancel(body?["sessionId"]?.ToString() ?? string.Empty) });
                default:
                    return Error(400, "Unbekannte Aktion");
            }
        }
        catch (AdapterConfigException ex)
        {
            return Error(400, ex.Message);
        }
    }

    private static JsonObject Describe(OAuthSession session) => new()
    {
        ["sessionId"] = session.Id,
        ["state"] = session.State,
        ["message"] = session.Message,
        ["browserLaunched"] = session.BrowserLaunched,
        ["authorizeUrl"] = session.State == "pending" ? session.AuthorizeUrl : null
    };

    private static EndpointResponse Ok(JsonObject payload)
        => Response(200, payload.ToJsonString(), "application/json; charset=utf-8");

    private static EndpointResponse Error(int status, string message)
        => Response(status, new JsonObject { ["error"] = message }.ToJsonString(), "application/json; charset=utf-8");
}
