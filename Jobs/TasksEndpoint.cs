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

namespace ioBroker_NewGen.Admin.Jobs;

internal sealed class TasksEndpoint : BaseRuntimeEndpoint
{
    private readonly IAdapterContext _context;

    public TasksEndpoint(IAdapterContext context) : base("/api/tasks")
    {
        _context = context;
    }

    protected override Task<EndpointResponse> OnHandleAsync(EndpointRequest request)
    {
        if (!IsAuthorized(_context, request))
        {
            return Task.FromResult(Unauthorized());
        }

        const string json = "application/json; charset=utf-8";
        var body = request.Body ?? Array.Empty<byte>();
        if (body.Length == 0)
        {
            return Task.FromResult(Response(200, JsonSerializer.Serialize(AdminTasks.All().Select(t => t.Snapshot(int.MaxValue))), json));
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var id = root.TryGetProperty("id", out var idElement) ? idElement.GetString() ?? string.Empty : string.Empty;
            var since = root.TryGetProperty("since", out var sinceElement) && sinceElement.TryGetInt32(out var s) ? s : 0;
            var action = root.TryGetProperty("action", out var actionElement) ? actionElement.GetString() : "get";
            if (action == "dismiss")
            {
                return Task.FromResult(Response(200, JsonSerializer.Serialize(new { dismissed = AdminTasks.Dismiss(id) }), json));
            }

            var task = AdminTasks.Find(id);
            return Task.FromResult(task is null
                ? Response(404, "{\"error\":\"Task nicht gefunden\"}", json)
                : Response(200, JsonSerializer.Serialize(task.Snapshot(since)), json));
        }
        catch (JsonException)
        {
            return Task.FromResult(Response(400, "{\"error\":\"Ungültige Anfrage\"}", json));
        }
    }
}
