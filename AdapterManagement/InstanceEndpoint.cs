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
using static ioBroker_NewGen.Admin.AdapterManagement.AutoStartSettings;
using static ioBroker_NewGen.Admin.Auth.AuthGuard;
using static ioBroker_NewGen.Admin.Http.EndpointResults;

namespace ioBroker_NewGen.Admin.AdapterManagement;

internal sealed class InstanceEndpoint : BaseRuntimeEndpoint
{
    private readonly IAdapterContext _context;

    public InstanceEndpoint(IAdapterContext context) : base("/api/adapters/instances")
    {
        _context = context;
    }

    protected override async Task<EndpointResponse> OnHandleAsync(EndpointRequest request)
    {
        if (!IsAuthorized(_context, request))
        {
            return Unauthorized();
        }

        string action;
        string adapterId;
        try
        {
            using var document = JsonDocument.Parse(request.Body is { Length: > 0 } ? request.Body : Array.Empty<byte>());
            action = document.RootElement.GetProperty("action").GetString() ?? string.Empty;
            adapterId = document.RootElement.GetProperty("adapterId").GetString() ?? string.Empty;
        }
        catch (Exception)
        {
            return Response(400, "{\"error\":\"Erwartet {action, adapterId}\"}", "application/json; charset=utf-8");
        }

        var registration = _context.AdapterRegistry.Get(adapterId);
        var legacy = _context.Legacy;
        if (legacy is not null && (legacy.IsInstalled(adapterId) || legacy.IsInstance(adapterId) ||
                                   registration?.Type == AdapterType.IoBroker))
        {
            if (action == "add")
            {
                var legacyId = await _context.AddInstanceAsync(adapterId);
                if (legacyId is null)
                {
                    return Response(400, "{\"error\":\"Instanz konnte nicht angelegt werden\"}", "application/json; charset=utf-8");
                }

                await _context.StartAdapterAsync(legacyId);
                return Response(200, JsonSerializer.Serialize(new { adapterId = legacyId }), "application/json; charset=utf-8");
            }

            if (action == "remove")
            {
                return await _context.RemoveInstanceAsync(adapterId)
                    ? Response(200, JsonSerializer.Serialize(new { adapterId }), "application/json; charset=utf-8")
                    : Response(400, "{\"error\":\"Instanz nicht gefunden\"}", "application/json; charset=utf-8");
            }

            return Response(400, "{\"error\":\"Unbekannte Aktion\"}", "application/json; charset=utf-8");
        }

        if (registration is null || registration.Type != AdapterType.Custom ||
            string.Equals(registration.Name, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return Response(400, "{\"error\":\"Für diesen Adapter sind keine weiteren Instanzen möglich\"}", "application/json; charset=utf-8");
        }

        if (action == "add")
        {
            var id = await _context.AddInstanceAsync(adapterId);
            if (id is null)
            {
                return Response(400, "{\"error\":\"Instanz konnte nicht angelegt werden\"}", "application/json; charset=utf-8");
            }

            SetAutoStart(_context, _context.AdapterRegistry.Get(id)!, true);
            await _context.StartAdapterAsync(id);
            return Response(200, JsonSerializer.Serialize(new { adapterId = id }), "application/json; charset=utf-8");
        }

        if (action == "remove")
        {
            SetAutoStart(_context, registration, false);
            if (!await _context.RemoveInstanceAsync(adapterId))
            {
                return Response(400, "{\"error\":\"Die erste Instanz (.0) kann nicht gelöscht werden\"}", "application/json; charset=utf-8");
            }

            return Response(200, JsonSerializer.Serialize(new { adapterId }), "application/json; charset=utf-8");
        }

        return Response(400, "{\"error\":\"Unbekannte Aktion\"}", "application/json; charset=utf-8");
    }
}
