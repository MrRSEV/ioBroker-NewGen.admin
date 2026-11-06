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
using ioBroker_NewGen.Admin.Jobs;
using static ioBroker_NewGen.Admin.Auth.AuthGuard;
using static ioBroker_NewGen.Admin.Http.EndpointResults;

namespace ioBroker_NewGen.Admin.AdapterManagement;

internal sealed class LegacyAdapterEndpoint : BaseRuntimeEndpoint
{
    private readonly IAdapterContext _context;

    public LegacyAdapterEndpoint(IAdapterContext context) : base("/api/adapters/legacy")
    {
        _context = context;
    }

    protected override async Task<EndpointResponse> OnHandleAsync(EndpointRequest request)
    {
        if (!IsAuthorized(_context, request))
        {
            return Unauthorized();
        }

        var legacy = _context.Legacy;
        if (legacy is null)
        {
            return Response(503, "{\"error\":\"Legacy-Adapter-Runtime ist nicht initialisiert\"}", "application/json; charset=utf-8");
        }

        var body = request.Body ?? Array.Empty<byte>();
        if (body.Length == 0)
        {
            return Response(200, JsonSerializer.Serialize(legacy.GetInstalled()), "application/json; charset=utf-8");
        }

        // ZIP-Upload (Magic "PK") oder JSON-Aktion.
        if (body.Length > 4 && body[0] == (byte)'P' && body[1] == (byte)'K')
        {
            var upload = body;
            var task = AdminTasks.Start("Legacy-Adapter (ZIP) installieren", async t =>
            {
                t.Log($"ZIP empfangen ({upload.Length / 1024} KB).");
                using var stream = new MemoryStream(upload, writable: false);
                var info = await legacy.InstallAsync(stream, default, t.Log);
                return $"Adapter installiert: {info.Name} {info.Version}" + (info.Warnings?.Count > 0 ? " (Warnungen: " + string.Join(" ", info.Warnings) + ")" : string.Empty);
            });
            return Response(202, JsonSerializer.Serialize(new { taskId = task.Id }), "application/json; charset=utf-8");
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var action = document.RootElement.GetProperty("action").GetString();
            var name = document.RootElement.GetProperty("name").GetString() ?? string.Empty;
            if (action == "uninstall")
            {
                return await legacy.UninstallAsync(name)
                    ? Response(200, JsonSerializer.Serialize(new { name }), "application/json; charset=utf-8")
                    : Response(404, "{\"error\":\"Adapter nicht installiert\"}", "application/json; charset=utf-8");
            }
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return Response(400, "{\"error\":\"Erwartet ZIP-Datei oder {action, name}\"}", "application/json; charset=utf-8");
        }
        catch (IOException ex)
        {
            return Response(400, JsonSerializer.Serialize(new { error = ex.Message }), "application/json; charset=utf-8");
        }

        return Response(400, "{\"error\":\"Unbekannte Aktion\"}", "application/json; charset=utf-8");
    }
}
