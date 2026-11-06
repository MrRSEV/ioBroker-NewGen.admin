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

internal sealed class UserRoleEndpoint : BaseRuntimeEndpoint
{
    private readonly IAdapterContext _context;

    public UserRoleEndpoint(IAdapterContext context) : base("/api/users")
    {
        _context = context;
    }

    protected override Task<EndpointResponse> OnHandleAsync(EndpointRequest request)
    {
        if (!IsAuthorized(_context, request))
        {
            return Task.FromResult(Unauthorized());
        }

        if (request.Body is not { Length: > 0 } && SecurityOf(_context) is { } securityService)
        {
            return SecurityListings.UsersAsync(securityService);
        }

        if (_context.RoleService is not IRoleManagementService roleManagementService)
        {
            return Task.FromResult(Response(503, "{\"error\":\"Rollenverwaltung ist nicht verfügbar\"}", "application/json; charset=utf-8"));
        }

        if (request.Body is not { Length: > 0 })
        {
            return Task.FromResult(Response(
                200,
                JsonSerializer.Serialize(roleManagementService.GetUserRoleAssignments()),
                "application/json; charset=utf-8"));
        }

        try
        {
            using var document = JsonDocument.Parse(request.Body);
            var root = document.RootElement;
            var user = root.GetProperty("user").GetString() ?? string.Empty;
            var role = root.GetProperty("role").GetString() ?? string.Empty;
            var operation = root.TryGetProperty("operation", out var operationElement)
                ? operationElement.GetString()
                : "assign";
            var success = string.Equals(operation, "revoke", StringComparison.OrdinalIgnoreCase)
                ? roleManagementService.RevokeRole(user, role)
                : roleManagementService.AssignRole(user, role);

            return Task.FromResult(Response(
                success ? 200 : 400,
                JsonSerializer.Serialize(new { user, role, operation, success }),
                "application/json; charset=utf-8"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Response(400, JsonSerializer.Serialize(new { error = ex.Message }), "application/json; charset=utf-8"));
        }
    }
}
