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

internal sealed class RemoteAdapterInstallEndpoint : BaseRuntimeEndpoint
{
    private readonly IAdapterContext _context;

    public RemoteAdapterInstallEndpoint(IAdapterContext context) : base("/api/adapters/install-remote")
    {
        _context = context;
    }

    protected override async Task<EndpointResponse> OnHandleAsync(EndpointRequest request)
    {
        if (!IsAuthorized(_context, request))
        {
            return Unauthorized();
        }

        if (_context.AdapterRepositoryClient is null)
        {
            return Response(503, "{\"error\":\"Repository-Client ist nicht initialisiert\"}", "application/json; charset=utf-8");
        }

        try
        {
            using var document = JsonDocument.Parse(request.Body ?? Array.Empty<byte>());
            var root = document.RootElement;
            var indexUri = new Uri(root.GetProperty("indexUri").GetString() ?? string.Empty, UriKind.Absolute);
            var name = root.GetProperty("name").GetString() ?? string.Empty;
            var version = root.GetProperty("version").GetString() ?? string.Empty;
            var staging = Path.Combine(AppContext.BaseDirectory, "adapter-staging");
            var manifest = await _context.AdapterRepositoryClient.DownloadAndInstallAsync(indexUri, name, version, staging);
            return Response(200, JsonSerializer.Serialize(new { manifest.Name, manifest.Version, restartRequired = true }), "application/json; charset=utf-8");
        }
        catch (Exception ex)
        {
            return Response(400, JsonSerializer.Serialize(new { error = ex.Message }), "application/json; charset=utf-8");
        }
    }
}
