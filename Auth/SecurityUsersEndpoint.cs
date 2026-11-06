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

internal sealed class SecurityUsersEndpoint : AuthEndpointBase
{
    public SecurityUsersEndpoint(IAdapterContext context) : base("/api/security/users", context)
    {
    }

    protected override async Task<EndpointResponse> OnHandleAsync(EndpointRequest request)
    {
        if (!IsAuthorized(Context, request))
        {
            return Unauthorized();
        }

        var current = await ResolveSessionAsync(Context, request);
        var security = SecurityOf(Context);
        using var body = ParseBody(request);
        if (security is null || body is null)
        {
            return Json(400, new { error = "Ungültige Anfrage" });
        }

        var root = body.RootElement;
        var action = Str(root, "action");
        var username = Str(root, "username")?.Trim() ?? string.Empty;
        var self = current is { } c && string.Equals(c.User.Username, username, StringComparison.OrdinalIgnoreCase);

        try
        {
            switch (action)
            {
                case "create":
                {
                    var roles = StringList(root, "roles");
                    await ValidateRolesAsync(security, roles);
                    await security.Users.CreateAsync(username, Str(root, "password") ?? string.Empty, Str(root, "displayName"), roles);
                    if (root.TryGetProperty("mustChangePassword", out var must) && must.ValueKind == JsonValueKind.True)
                    {
                        await security.Users.SetPasswordAsync(username, Str(root, "password") ?? string.Empty, true);
                    }

                    return Json(200, new { username, created = true });
                }
                case "update":
                {
                    if (await security.Users.FindAsync(username) is null)
                    {
                        return Json(404, new { error = $"Benutzer '{username}' existiert nicht." });
                    }

                    var enabled = !root.TryGetProperty("enabled", out var en) || en.ValueKind != JsonValueKind.False;
                    if (self && !enabled)
                    {
                        return Json(400, new { error = "Das eigene Konto kann nicht deaktiviert werden." });
                    }

                    var wanted = StringList(root, "roles");
                    await ValidateRolesAsync(security, wanted);
                    if (self && !wanted.Contains(SecurityRoles.Administrator, StringComparer.OrdinalIgnoreCase))
                    {
                        var currentRoles = await security.Users.GetRolesAsync(username);
                        if (currentRoles.Contains(SecurityRoles.Administrator, StringComparer.OrdinalIgnoreCase))
                        {
                            return Json(400, new { error = "Dem eigenen Konto kann die Rolle Administrator nicht entzogen werden." });
                        }
                    }

                    await security.Users.SetDisplayNameAsync(username, Str(root, "displayName"));
                    await security.Users.SetEnabledAsync(username, enabled);
                    var existing = await security.Users.GetRolesAsync(username);
                    foreach (var role in existing.Where(r => !wanted.Contains(r, StringComparer.OrdinalIgnoreCase)))
                    {
                        await security.Roles.RemoveAsync(username, role);
                    }

                    foreach (var role in wanted.Where(r => !existing.Contains(r, StringComparer.OrdinalIgnoreCase)))
                    {
                        await security.Roles.AssignAsync(username, role);
                    }

                    var password = Str(root, "password");
                    if (!string.IsNullOrEmpty(password))
                    {
                        var must = root.TryGetProperty("mustChangePassword", out var m) && m.ValueKind == JsonValueKind.True;
                        await security.Users.SetPasswordAsync(username, password, must);
                    }

                    return Json(200, new { username, updated = true });
                }
                case "delete":
                    if (self)
                    {
                        return Json(400, new { error = "Das eigene Konto kann nicht gelöscht werden." });
                    }

                    return await security.Users.DeleteAsync(username)
                        ? Json(200, new { username, deleted = true })
                        : Json(404, new { error = $"Benutzer '{username}' existiert nicht." });
                default:
                    return Json(400, new { error = "Unbekannte Aktion" });
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Json(400, new { error = ex.Message });
        }
    }

    private static async Task ValidateRolesAsync(SecurityService security, IReadOnlyList<string> roles)
    {
        foreach (var role in roles)
        {
            if (!await security.Roles.ExistsAsync(role))
            {
                throw new InvalidOperationException($"Rolle '{role}' existiert nicht.");
            }
        }
    }
}
