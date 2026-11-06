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
using static ioBroker_NewGen.Admin.Http.RequestJson;

namespace ioBroker_NewGen.Admin.Auth;

internal sealed class SecurityRolesEndpoint : AuthEndpointBase
{
    public SecurityRolesEndpoint(IAdapterContext context) : base("/api/security/roles", context)
    {
    }

    protected override async Task<EndpointResponse> OnHandleAsync(EndpointRequest request)
    {
        if (!IsAuthorized(Context, request))
        {
            return Unauthorized();
        }

        var security = SecurityOf(Context);
        using var body = ParseBody(request);
        if (security is null || body is null)
        {
            return Json(400, new { error = "Ungültige Anfrage" });
        }

        var root = body.RootElement;
        var name = Str(root, "name")?.Trim() ?? string.Empty;
        var builtin = SecurityRoles.Defaults.Any(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));

        try
        {
            switch (Str(root, "action"))
            {
                case "save":
                    if (builtin)
                    {
                        return Json(400, new { error = "Vordefinierte Rollen können nicht geändert werden." });
                    }

                    await security.Roles.SaveAsync(name, Str(root, "description"), StringList(root, "permissions"));
                    return Json(200, new { name, saved = true });
                case "delete":
                    if (builtin)
                    {
                        return Json(400, new { error = "Vordefinierte Rollen können nicht gelöscht werden." });
                    }

                    return await security.Roles.DeleteAsync(name)
                        ? Json(200, new { name, deleted = true })
                        : Json(404, new { error = $"Rolle '{name}' existiert nicht." });
                case "permissions":
                    return Json(200, SecurityPermissions.All);
                default:
                    return Json(400, new { error = "Unbekannte Aktion" });
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Json(400, new { error = ex.Message });
        }
    }
}
