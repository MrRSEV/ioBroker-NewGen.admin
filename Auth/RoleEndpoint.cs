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

internal sealed class RoleEndpoint : BaseRuntimeEndpoint
{
    private readonly IAdapterContext _context;

    public RoleEndpoint(IAdapterContext context) : base("/api/roles")
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
            return SecurityListings.RolesAsync(securityService);
        }

        if (_context.RoleService is not IRoleManagementService roleManagementService)
        {
            return Task.FromResult(Response(503, "{\"error\":\"Rollenverwaltung ist nicht verfügbar\"}", "application/json; charset=utf-8"));
        }

        if (request.Body is not { Length: > 0 })
        {
            return Task.FromResult(Response(
                200,
                JsonSerializer.Serialize(roleManagementService.GetRoles()),
                "application/json; charset=utf-8"));
        }

        try
        {
            using var document = JsonDocument.Parse(request.Body);
            var root = document.RootElement;
            var name = root.GetProperty("name").GetString() ?? string.Empty;
            var operation = root.TryGetProperty("operation", out var operationElement)
                ? operationElement.GetString()
                : "upsert";

            if (string.Equals(operation, "remove", StringComparison.OrdinalIgnoreCase))
            {
                var removed = roleManagementService.RemoveRole(name);
                return Task.FromResult(Response(
                    removed ? 200 : 404,
                    JsonSerializer.Serialize(new { name, removed }),
                    "application/json; charset=utf-8"));
            }

            var description = root.TryGetProperty("description", out var descriptionElement)
                ? descriptionElement.GetString() ?? string.Empty
                : string.Empty;
            roleManagementService.UpsertRole(name, description);

            if (root.TryGetProperty("stateId", out var stateElement) &&
                !string.IsNullOrWhiteSpace(stateElement.GetString()))
            {
                var stateId = stateElement.GetString()!;
                var canRead = root.TryGetProperty("canRead", out var readElement) && readElement.GetBoolean();
                var canWrite = root.TryGetProperty("canWrite", out var writeElement) && writeElement.GetBoolean();
                if (!roleManagementService.SetRolePermissions(name, stateId, canRead, canWrite))
                {
                    return Task.FromResult(Response(400, "{\"error\":\"Rolle konnte nicht berechtigt werden\"}", "application/json; charset=utf-8"));
                }
            }

            return Task.FromResult(Response(
                200,
                JsonSerializer.Serialize(new { name, updated = true }),
                "application/json; charset=utf-8"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Response(400, JsonSerializer.Serialize(new { error = ex.Message }), "application/json; charset=utf-8"));
        }
    }
}
