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

namespace ioBroker_NewGen.Admin.Auth;

internal sealed class MeEndpoint : AuthEndpointBase
{
    public MeEndpoint(IAdapterContext context) : base("/api/auth/me", context)
    {
    }

    protected override async Task<EndpointResponse> OnHandleAsync(EndpointRequest request)
    {
        var resolved = await ResolveSessionAsync(Context, request);
        if (resolved is null)
        {
            return Json(401, new { error = "Authentifizierung erforderlich" });
        }

        var (security, _, user) = resolved.Value;
        var roles = await security.Users.GetRolesAsync(user.Username);
        var permissions = new List<string>();
        foreach (var permission in SecurityPermissions.All)
        {
            if (await security.Authorization.HasPermissionAsync(user.Id, permission))
            {
                permissions.Add(permission);
            }
        }

        return Json(200, new { username = user.Username, displayName = user.DisplayName, mustChangePassword = user.MustChangePassword, roles, permissions });
    }
}
