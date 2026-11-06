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

namespace ioBroker_NewGen.Admin.Auth;

internal sealed class PermissionEndpoint : BaseRuntimeEndpoint
{
    private readonly IAdapterContext _context;

    public PermissionEndpoint(IAdapterContext context) : base("/api/permissions")
    {
        _context = context;
    }

    protected override Task<EndpointResponse> OnHandleAsync(EndpointRequest request)
    {
        if (!IsAuthorized(_context, request))
        {
            return Task.FromResult(Unauthorized());
        }

        if (request.Body is not { Length: > 0 })
        {
            if (_context.RoleService is not IRoleManagementService roleManagementService)
            {
                return Task.FromResult(Response(503, "{\"error\":\"Rollenverwaltung ist nicht verfügbar\"}", "application/json; charset=utf-8"));
            }

            return Task.FromResult(Response(
                200,
                JsonSerializer.Serialize(new
                {
                    userPermissions = roleManagementService.GetUserPermissions(),
                    roles = roleManagementService.GetRoles()
                }),
                "application/json; charset=utf-8"));
        }

        try
        {
            using var document = JsonDocument.Parse(request.Body);
            var root = document.RootElement;
            var user = root.GetProperty("user").GetString() ?? string.Empty;
            var stateId = root.GetProperty("stateId").GetString() ?? string.Empty;
            var canRead = root.GetProperty("canRead").GetBoolean();
            var canWrite = root.GetProperty("canWrite").GetBoolean();
            var success = _context.Matrix.SetPermissions(user, stateId, canRead, canWrite);
            return Task.FromResult(Response(success ? 200 : 400, JsonSerializer.Serialize(new { success }), "application/json; charset=utf-8"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Response(400, JsonSerializer.Serialize(new { error = ex.Message }), "application/json; charset=utf-8"));
        }
    }
}
