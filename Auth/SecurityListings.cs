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
using static ioBroker_NewGen.Admin.Http.EndpointResults;

namespace ioBroker_NewGen.Admin.Auth;

internal static class SecurityListings
{
    public static async Task<EndpointResponse> RolesAsync(SecurityService security)
    {
        var roles = await security.Roles.ListAsync();
        var payload = new List<object>();
        foreach (var role in roles)
        {
            payload.Add(new
            {
                name = role.Name,
                description = role.Description,
                builtin = SecurityRoles.Defaults.Any(d => string.Equals(d.Name, role.Name, StringComparison.OrdinalIgnoreCase)),
                permissions = await security.Roles.GetPermissionsAsync(role.Name)
            });
        }

        return Response(200, JsonSerializer.Serialize(payload), "application/json; charset=utf-8");
    }

    public static async Task<EndpointResponse> UsersAsync(SecurityService security)
    {
        var users = await security.Users.ListAsync();
        var payload = new List<object>();
        foreach (var user in users)
        {
            payload.Add(new
            {
                username = user.Username,
                displayName = user.DisplayName,
                isEnabled = user.IsEnabled,
                mustChangePassword = user.MustChangePassword,
                roles = await security.Users.GetRolesAsync(user.Username),
                createdUtc = user.CreatedUtc,
                lastLoginUtc = user.LastLoginUtc
            });
        }

        return Response(200, JsonSerializer.Serialize(payload), "application/json; charset=utf-8");
    }
}
