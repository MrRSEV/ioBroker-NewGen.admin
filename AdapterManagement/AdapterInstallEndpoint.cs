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

internal sealed class AdapterInstallEndpoint : BaseRuntimeEndpoint
{
    private readonly IAdapterContext _context;

    public AdapterInstallEndpoint(IAdapterContext context) : base("/api/adapters/install")
    {
        _context = context;
    }

    protected override async Task<EndpointResponse> OnHandleAsync(EndpointRequest request)
    {
        if (!IsAuthorized(_context, request))
        {
            return Unauthorized();
        }

        if (_context.AdapterPackageInstaller is null)
        {
            return Response(503, "{\"error\":\"Adapter-Deployment ist nicht initialisiert\"}", "application/json; charset=utf-8");
        }

        var packageDirectory = request.Body is { Length: > 0 }
            ? Encoding.UTF8.GetString(request.Body).Trim()
            : string.Empty;
        if (string.IsNullOrWhiteSpace(packageDirectory))
        {
            return Response(400, "{\"error\":\"Paketverzeichnis fehlt\"}", "application/json; charset=utf-8");
        }

        try
        {
            var manifest = await _context.AdapterPackageInstaller.InstallAsync(packageDirectory);
            return Response(
                200,
                JsonSerializer.Serialize(new
                {
                    manifest.Name,
                    manifest.Version,
                    manifest.EntryAssembly,
                    restartRequired = true
                }),
                "application/json; charset=utf-8");
        }
        catch (Exception ex)
        {
            return Response(400, JsonSerializer.Serialize(new { error = ex.Message }), "application/json; charset=utf-8");
        }
    }
}
