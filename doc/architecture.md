# Admin UI architecture

## Runtime boundary

`Main` derives from `DeviceAdapterBase` and identifies itself as
`admin`. It is an external custom-adapter assembly, not a second host process.
The host supplies the `IAdapterContext`, including configuration, logging, the
`IWebEndpointRegistry`, security services, adapter registry, and state matrix.

The adapter registers its endpoints during `OnStartAsync`. The separate
[`ioBroker-NewGen.web`](https://github.com/MrRSEV/ioBroker-NewGen.web) adapter
owns the HTTP listener on `web.listenHost`/`web.port` (default `127.0.0.1:8080`).
When stopped, Admin UI unregisters its routes so it can be restarted cleanly.
`admin.adminEnabled` controls whether the service is started.

The dashboard document and client-side code are embedded in
`src/` (index.html, admin.css, js/*.js) and embedded as resources; `Ui/DashboardPage.cs`\nassembles them. No separate asset pipeline is required.

## Responsibilities and ownership

| Concern | Owner |
| --- | --- |
| Process lifecycle, adapter loading and configuration | NewGen host |
| Adapter API, registry and state matrix | NewGen host |
| Authentication, sessions, users, roles and permissions | Host Core.Security |
| Dashboard and Admin-specific HTTP endpoints | Admin UI adapter |
| HTTP/WebSocket listener | External `ioBroker-NewGen.web` adapter |

The adapter should use host contracts rather than introducing duplicate
registries, state stores, or security implementations. Changes to
`ioBroker-NewGen.dll` or `RSEV.Utilities.dll` must be coordinated with the host.

## Source and build layout

- `Main.cs` contains adapter lifecycle and endpoint registration; handlers live in\n  `ioBroker_NewGen.Admin.*` namespaces (folders `AdapterManagement`, `Auth`, `Jobs`,\n  `Monitoring`, `Settings`, `Http`, `Ui`). `AdminVersion.Value` is generated from the csproj `Version`.
- `AdminUI.csproj` targets .NET 9 and references the host/runtime assemblies
  under `lib`.
- `lib/ioBroker-NewGen.dll` is the host API contract used at compile time.
- `lib/RSEV.Utilities.dll` is the shared runtime dependency.
- The project output is `bin/AdminUI.dll` in this repository.
- The host loads the deployed assembly from `Adapters/native`. The host project
  copies that directory to its build and publish output, preserving nested
  paths.

Build the adapter from this directory with `dotnet build AdminUI.csproj`.
Deploy the resulting DLL manually to the runtime's `Adapters/native` directory.

## Authentication and permissions

Interactive authentication is integrated with the host security service.
Endpoints use host authentication/session helpers and map protected operations
to host permissions. The Admin adapter does not own user credentials or
permission persistence. If authentication is required but Core.Security is
unavailable, the adapter logs a warning; the optional automation token is a
separate integration path.

For detailed routes, see the [API route reference](./api.md).
