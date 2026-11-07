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
using static ioBroker_NewGen.Admin.Settings.AdminConfig;

namespace ioBroker_NewGen.Admin.Auth;

/// <summary>Sitzungs-, Token- und Berechtigungsprüfung für alle Admin-Endpunkte.</summary>
internal static class AuthGuard
{
    internal const string SessionCookie = "ngsession";

    internal static readonly AsyncLocal<bool> Forbidden = new();

    internal static SecurityService? SecurityOf(IAdapterContext context)
    {
        // Adapter erhalten einen Proxy statt des MainRuntimeContext; daher auf die Prozess-Instanz zurückfallen.
        return (context.RuntimeContext as MainRuntimeContext ?? MainRuntimeContext.Instance).Security;
    }

    internal static string? GetHeader(EndpointRequest request, string name)
    {
        return request.Headers
            .FirstOrDefault(pair => string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
            .Value;
    }

    internal static string? ReadSessionToken(EndpointRequest request)
    {
        var cookies = GetHeader(request, "Cookie");
        if (string.IsNullOrWhiteSpace(cookies))
        {
            return null;
        }

        foreach (var part in cookies.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = part.IndexOf('=');
            if (separator > 0 && string.Equals(part[..separator], SessionCookie, StringComparison.Ordinal))
            {
                var value = part[(separator + 1)..];
                return value.Length > 0 ? value : null;
            }
        }

        return null;
    }

    internal static string BuildSessionCookie(string value, int maxAgeSeconds)
    {
        return $"{SessionCookie}={value}; Path=/; Max-Age={maxAgeSeconds}; HttpOnly; SameSite=Strict";
    }

    internal static bool HasCsrfHeader(EndpointRequest request)
    {
        return !string.IsNullOrWhiteSpace(GetHeader(request, "X-Requested-With"));
    }

    internal static string? RequiredPermission(string route, bool write)
    {
        return route switch
        {
            "/api/states" => SecurityPermissions.StateRead,
            "/api/states/write" => SecurityPermissions.StateWrite,
            "/api/adapters" or "/api/adapters/tabs" => SecurityPermissions.AdapterRead,
            _ when route.StartsWith("/adapter/", StringComparison.OrdinalIgnoreCase) => SecurityPermissions.AdapterRead,
            "/api/adapters/start" => SecurityPermissions.AdapterStart,
            "/api/adapters/stop" => SecurityPermissions.AdapterStop,
            "/api/adapters/restart" => SecurityPermissions.AdapterRestart,
            "/api/adapters/autostart" or "/api/adapters/instances" or "/api/adapters/config" or "/api/adapters/oauth" => SecurityPermissions.SystemConfigure,
            "/api/adapters/install" or "/api/adapters/install-remote" => SecurityPermissions.AdapterInstall,
            "/api/adapters/legacy" => write ? SecurityPermissions.AdapterInstall : SecurityPermissions.AdapterRead,
            "/api/config" => write ? SecurityPermissions.SystemConfigure : SecurityPermissions.SystemRead,
            "/api/logs" or "/api/overview" => SecurityPermissions.SystemRead,
            "/api/i18n/extend" => SecurityPermissions.SystemConfigure,
            "/api/users" => write ? SecurityPermissions.UserManage : SecurityPermissions.UserRead,
            "/api/security/users" => SecurityPermissions.UserManage,
            "/api/security/roles" => SecurityPermissions.RoleManage,
            "/api/roles" or "/api/permissions" => write ? SecurityPermissions.RoleManage : SecurityPermissions.RoleRead,
            _ => SecurityPermissions.SystemAdministration
        };
    }

    internal static bool HasValidLegacyToken(IAdapterContext context, EndpointRequest request)
    {
        var expectedToken = ReadConfig(context, "admin.adminToken", string.Empty);
        if (string.IsNullOrWhiteSpace(expectedToken))
        {
            return false;
        }

        var suppliedToken = GetHeader(request, "X-Admin-Token");
        if (string.IsNullOrWhiteSpace(suppliedToken))
        {
            suppliedToken = GetHeader(request, "Authorization");
            if (suppliedToken?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
            {
                suppliedToken = suppliedToken[7..].Trim();
            }
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedToken),
            Encoding.UTF8.GetBytes(suppliedToken ?? string.Empty));
    }

    internal static bool IsAuthorized(IAdapterContext context, EndpointRequest request)
    {
        Forbidden.Value = false;
        if (!ReadConfig(context, "admin.adminRequireAuthentication", false))
        {
            return true;
        }

        // Optionaler Automations-Token (Header), nur aktiv wenn admin.adminToken gesetzt ist.
        if (HasValidLegacyToken(context, request))
        {
            return true;
        }

        var security = SecurityOf(context);
        var token = ReadSessionToken(request);
        if (security is null || token is null)
        {
            return false;
        }

        var write = request.Body is { Length: > 0 };
        if (write && !HasCsrfHeader(request))
        {
            Forbidden.Value = true;
            return false;
        }

        var session = security.Authentication.ValidateSessionAsync(token).GetAwaiter().GetResult();
        if (session is null)
        {
            return false;
        }

        var user = security.Users.FindAsync(session.Username).GetAwaiter().GetResult();
        if (user is null || !user.IsEnabled || user.MustChangePassword)
        {
            Forbidden.Value = true;
            return false;
        }

        var permission = RequiredPermission(request.Route, write);
        var allowed = permission is not null &&
            security.Authorization.HasPermissionAsync(user.Id, permission).GetAwaiter().GetResult();
        Forbidden.Value = !allowed;
        return allowed;
    }

    internal static async Task<(SecurityService Security, SecuritySession Session, SecurityUser User)?> ResolveSessionAsync(
        IAdapterContext context,
        EndpointRequest request)
    {
        var security = SecurityOf(context);
        var token = ReadSessionToken(request);
        if (security is null || token is null)
        {
            return null;
        }

        var session = await security.Authentication.ValidateSessionAsync(token);
        if (session is null)
        {
            return null;
        }

        var user = await security.Users.FindAsync(session.Username);
        return user is { IsEnabled: true } ? (security, session, user) : null;
    }

    // Erststart: Standardadministrator wurde angelegt, hat sich aber noch nie angemeldet.
    internal static async Task<bool> IsFirstRunAsync(SecurityService security)
    {
        if (!security.Options.CreateDefaultAdmin || !security.Options.ForcePasswordChange)
        {
            return false;
        }

        var user = await security.Users.FindAsync(security.Options.DefaultUsername);
        return user is { IsEnabled: true, MustChangePassword: true, LastLoginUtc: null };
    }

    internal static EndpointResponse Unauthorized()
    {
        if (Forbidden.Value)
        {
            return Response(403, "{\"error\":\"Keine Berechtigung\"}", "application/json; charset=utf-8");
        }

        return Response(401, "{\"error\":\"Authentifizierung erforderlich\"}", "application/json; charset=utf-8");
    }
}
