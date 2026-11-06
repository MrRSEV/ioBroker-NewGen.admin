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

internal sealed class AdapterCommandEndpoint : BaseRuntimeEndpoint
{
    private readonly IAdapterContext _context;
    private readonly string _action;

    public AdapterCommandEndpoint(string route, IAdapterContext context, string action) : base(route)
    {
        _context = context;
        _action = action;
    }

    protected override async Task<EndpointResponse> OnHandleAsync(EndpointRequest request)
    {
        if (!IsAuthorized(_context, request))
        {
            return Unauthorized();
        }

        var body = request.Body is { Length: > 0 } ? Encoding.UTF8.GetString(request.Body) : string.Empty;
        var adapterId = body.Trim();
        if (body.TrimStart().StartsWith("{", StringComparison.Ordinal))
        {
            try
            {
                using var document = JsonDocument.Parse(body);
                adapterId = document.RootElement.GetProperty("adapterId").GetString() ?? string.Empty;
            }
            catch (JsonException)
            {
                return Response(400, "{\"error\":\"Ungültiger JSON-Body\"}", "application/json; charset=utf-8");
            }
        }

        if (string.IsNullOrWhiteSpace(adapterId))
        {
            return Response(400, "{\"error\":\"adapterId fehlt\"}", "application/json; charset=utf-8");
        }

        var success = _action switch
        {
            "start" => await _context.StartAdapterAsync(adapterId),
            "restart" => await _context.RestartAdapterAsync(adapterId),
            _ => await _context.StopAdapterAsync(adapterId)
        };
        return Response(success ? 200 : 404, JsonSerializer.Serialize(new { adapterId, success }), "application/json; charset=utf-8");
    }
}
