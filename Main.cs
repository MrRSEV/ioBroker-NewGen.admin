using ioBroker_NewGen.Admin.AdapterManagement;
using ioBroker_NewGen.Admin.Auth;
using ioBroker_NewGen.Admin.Jobs;
using ioBroker_NewGen.Admin.Monitoring;
using ioBroker_NewGen.Admin.Settings;
using ioBroker_NewGen.Admin.Socket;
using ioBroker_NewGen.Admin.Ui;
using ioBroker_NewGen.Core.Adapters;
using ioBroker_NewGen.Core.Web;
using static ioBroker_NewGen.Admin.Auth.AuthGuard;

namespace ioBroker_NewGen.Admin;

/// <summary>
/// Einstiegspunkt des Admin-Adapters. Admin ist ein normaler CustomAdapter in einer
/// eigenen Assembly; andere Adapter können ihn über den IAdapterContext als
/// Dependency adressieren. Die Endpunkte liegen in den Unter-Namespaces
/// <c>Auth</c>, <c>AdapterManagement</c>, <c>Settings</c>, <c>Monitoring</c>,
/// <c>Jobs</c> und <c>Ui</c>.
/// </summary>
public sealed class Main : DeviceAdapterBase
{
    private bool _enabled = true;
    private bool _registered;
    private bool _authenticationRequired;

    public override string Name => "admin";

    /// <summary>Version aus der .csproj (wird beim Build als <see cref="AdminVersion.Value"/> erzeugt).</summary>
    public string? Version => AdminVersion.Value;

    public override IReadOnlyCollection<string> Dependencies => new[] { "web" };

    protected override Task OnStartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _enabled = Config.Get<bool>("admin.adminEnabled");
            _authenticationRequired = Config.Get<bool>("admin.adminRequireAuthentication");
        }
        catch
        {
        }

        if (!_enabled || _registered)
        {
            return Task.CompletedTask;
        }

        if (_authenticationRequired && SecurityOf(Context) is null)
        {
            Context.RuntimeContext.Logger.LogWarning("[AdminUI] Core.Security ist nicht initialisiert. Anmeldung nur über den optionalen Automations-Token (admin.adminToken) möglich.");
        }

        RegisterEndpoints();
        _registered = true;

        AdminInfoObjects.Register(Context);
        Context.RuntimeContext.Logger.LogInfo($"[AdminUI] Version {AdminVersion.Value}: Endpoints beim separaten Web-Adapter registriert.");
        return Task.CompletedTask;
    }

    protected override Task OnStopAsync(CancellationToken cancellationToken)
    {
        try
        {
            AdminInfoObjects.MarkDisconnected(Context);
        }
        catch
        {
        }

        foreach (var route in Context.WebEndpoints.Registrations
                     .Where(registration => string.Equals(registration.OwnerId, Id, StringComparison.OrdinalIgnoreCase))
                     .Select(registration => registration.Route)
                     .ToArray())
        {
            Context.WebEndpoints.Unregister(Id, route);
        }

        _registered = false;
        return Task.CompletedTask;
    }

    private void RegisterEndpoints()
    {
        var web = Context.WebEndpoints;
        var legacyTabs = new LegacyAdminTabEndpoints(Context);

        foreach (var route in new[] { "/", "/index.html", "/admin" })
        {
            web.Register(Id, new DashboardEndpoint(route));
        }

        web.Register(Id, "/api/adapters/tabs", WebEndpointKind.Http,
            (request, _) => Task.FromResult(legacyTabs.GetTabs(request)));
        web.Register(Id, "/adapter/*", WebEndpointKind.Http,
            (request, _) => Task.FromResult(legacyTabs.ServeAdapterFile(request)));
        web.Register(Id, "/lib/js/socket.io.js", WebEndpointKind.Http,
            (_, _) => Task.FromResult(legacyTabs.ServeSocketClient()));
        web.Register(Id, "/_socket/info.js", WebEndpointKind.Http,
            (request, _) => Task.FromResult(legacyTabs.ServeSocketInfo(request)));
        web.Register(Id, "/", WebEndpointKind.WebSocket,
            new LegacyAdminSocketEndpoint(Context).HandleAsync);

        // Anmeldung und Benutzerkonto
        web.Register(Id, new LoginEndpoint(Context));
        web.Register(Id, new AuthStatusEndpoint(Context));
        web.Register(Id, new FirstRunSetupEndpoint(Context));
        web.Register(Id, new LogoutEndpoint(Context));
        web.Register(Id, new MeEndpoint(Context));
        web.Register(Id, new ChangePasswordEndpoint(Context));

        // Sprachen und Konfiguration
        foreach (var i18nMode in new[] { "languages", "translations", "translate", "extend" })
        {
            web.Register(Id, new I18nEndpoint(i18nMode, Context));
        }

        web.Register(Id, new ConfigurationEndpoint(Context));

        // Adapter- und Instanzverwaltung
        web.Register(Id, new AdaptersEndpoint(Context));
        web.Register(Id, new AdapterConfigEndpoint(Context));
        web.Register(Id, new AdapterOAuthEndpoint(Context));
        web.Register(Id, new AdapterCommandEndpoint("/api/adapters/start", Context, "start"));
        web.Register(Id, new AdapterCommandEndpoint("/api/adapters/stop", Context, "stop"));
        web.Register(Id, new AdapterCommandEndpoint("/api/adapters/restart", Context, "restart"));
        web.Register(Id, new AutostartEndpoint(Context));
        web.Register(Id, new InstanceEndpoint(Context));
        web.Register(Id, new LegacyAdapterEndpoint(Context));
        web.Register(Id, new AdapterInstallEndpoint(Context));
        web.Register(Id, new RemoteAdapterInstallEndpoint(Context));

        // Hintergrundaufgaben und Systemüberblick
        web.Register(Id, new TasksEndpoint(Context));
        web.Register(Id, new StatesEndpoint(Context));
        web.Register(Id, new StateWriteEndpoint(Context));
        web.Register(Id, new LogsEndpoint(Context));
        web.Register(Id, new OverviewEndpoint(Context));

        // Benutzer, Rollen und Berechtigungen
        web.Register(Id, new PermissionEndpoint(Context));
        web.Register(Id, new RoleEndpoint(Context));
        web.Register(Id, new UserRoleEndpoint(Context));
        web.Register(Id, new SecurityUsersEndpoint(Context));
        web.Register(Id, new SecurityRolesEndpoint(Context));
    }
}
