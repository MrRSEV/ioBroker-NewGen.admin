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

internal sealed class AdaptersEndpoint : BaseRuntimeEndpoint
{
    private readonly IAdapterContext _context;

    public AdaptersEndpoint(IAdapterContext context) : base("/api/adapters")
    {
        _context = context;
    }

    protected override Task<EndpointResponse> OnHandleAsync(EndpointRequest request)
    {
        if (!IsAuthorized(_context, request))
        {
            return Task.FromResult(Unauthorized());
        }

        var enabledAdapters = ReadEnabledAdapters(_context);
        var adapters = _context.AdapterRegistry.GetAll()
            .OrderBy(adapter => adapter.Id, StringComparer.Ordinal)
            .Select(adapter => new
            {
                adapter.Name,
                adapter.Id,
                Type = adapter.Type.ToString(),
                adapter.Version,
                Status = adapter.Status.ToString(),
                AutoStart = adapter.Type == AdapterType.IoBroker
                    ? _context.Legacy?.IsInstance(adapter.Id) == true
                    : IsAutoStart(enabledAdapters, adapter),
                LegacyInstance = _context.Legacy?.IsInstance(adapter.Id) == true,
                HasConfig = _context.AdapterRegistry.Configs.HasConfig(adapter.Name),
                AutoStartEditable = adapter.Type == AdapterType.Custom && !CoreAdapters.IsCore(adapter.Name),
                adapter.Metadata,
                adapter.TcpEndpoint,
                adapter.RegisteredAt,
                adapter.LastSeenAt,
                Metrics = new
                {
                    adapter.StartedAt,
                    adapter.StoppedAt,
                    adapter.LastStatusChangeAt,
                    UptimeSeconds = adapter.Status == AdapterStatus.Connected && adapter.StartedAt is { } startedAt
                        ? (long)(DateTime.UtcNow - startedAt).TotalSeconds
                        : 0L,
                    adapter.StartCount,
                    adapter.RestartCount,
                    adapter.FaultCount,
                    adapter.LastError,
                    adapter.LastErrorAt
                }
            });
        return Task.FromResult(Response(200, JsonSerializer.Serialize(adapters), "application/json; charset=utf-8"));
    }
}
