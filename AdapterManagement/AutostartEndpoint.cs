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

internal sealed class AutostartEndpoint : BaseRuntimeEndpoint
{
    private readonly IAdapterContext _context;

    public AutostartEndpoint(IAdapterContext context) : base("/api/adapters/autostart")
    {
        _context = context;
    }

    protected override Task<EndpointResponse> OnHandleAsync(EndpointRequest request)
    {
        if (!IsAuthorized(_context, request))
        {
            return Task.FromResult(Unauthorized());
        }

        string adapterId;
        bool enabled;
        try
        {
            using var document = JsonDocument.Parse(request.Body is { Length: > 0 } ? request.Body : Array.Empty<byte>());
            adapterId = document.RootElement.GetProperty("adapterId").GetString() ?? string.Empty;
            enabled = document.RootElement.GetProperty("enabled").GetBoolean();
        }
        catch (Exception)
        {
            return Task.FromResult(Response(400, "{\"error\":\"Erwartet {adapterId, enabled}\"}", "application/json; charset=utf-8"));
        }

        var adapter = _context.AdapterRegistry.Get(adapterId);
        if (adapter is null)
        {
            return Task.FromResult(Response(404, "{\"error\":\"Adapter nicht gefunden\"}", "application/json; charset=utf-8"));
        }

        if (adapter.Type != AdapterType.Custom || CoreAdapters.IsCore(adapter.Name))
        {
            return Task.FromResult(Response(400, "{\"error\":\"Autostart ist für diesen Adapter nicht änderbar\"}", "application/json; charset=utf-8"));
        }

        SetAutoStart(_context, adapter, enabled);
        return Task.FromResult(Response(200, JsonSerializer.Serialize(new { adapterId = adapter.Id, autoStart = enabled }), "application/json; charset=utf-8"));
    }
}
