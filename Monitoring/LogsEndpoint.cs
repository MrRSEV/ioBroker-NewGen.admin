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
using static ioBroker_NewGen.Admin.Monitoring.LogTail;

namespace ioBroker_NewGen.Admin.Monitoring;

internal sealed class LogsEndpoint : BaseRuntimeEndpoint
{
    private readonly IAdapterContext _context;

    public LogsEndpoint(IAdapterContext context) : base("/api/logs")
    {
        _context = context;
    }

    protected override Task<EndpointResponse> OnHandleAsync(EndpointRequest request)
    {
        if (!IsAuthorized(_context, request))
        {
            return Task.FromResult(Unauthorized());
        }

        var latestLog = Path.Combine(AppContext.BaseDirectory, "log", "latest.log");
        var logs = File.Exists(latestLog)
            ? new[]
            {
                new
                {
                    file = Path.GetFileName(latestLog),
                    lines = ReadTail(latestLog, 100)
                }
            }
            : Array.Empty<object>();
        return Task.FromResult(Response(200, JsonSerializer.Serialize(logs), "application/json; charset=utf-8"));
    }
}
