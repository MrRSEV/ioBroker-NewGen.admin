# ioBroker-NewGen Admin UI

> **Unofficial community project.** This project is not affiliated with or endorsed by the official ioBroker project.

The Admin UI is the `admin` custom adapter for ioBroker-NewGen. It is built as
a separate .NET 9 assembly and loaded by the host's custom-adapter runtime; it
is not part of the host executable.

## Responsibilities

- Serve the dashboard at `/`, `/index.html`, and `/admin`.
- Expose the HTTP API for authentication, states, adapters, configuration,
  logs, overview, localization, and user/role administration.
- Use the host's adapter context, `IWebEndpointRegistry`, configuration,
  security services, adapter registry, and matrix instead of owning separate
  runtime services or an HTTP listener. The listener is provided by the
  separate [`ioBroker-NewGen.web`](https://github.com/MrRSEV/ioBroker-NewGen.web)
  adapter.

The dashboard markup, CSS and JavaScript live in [`src/`](./src) and are embedded\nas resources at build time; [`Main.cs`](./Main.cs) is the adapter entry point.

## Build

Requirements:

- .NET SDK 9.
- `lib/ioBroker-NewGen.dll` matching the host API used by this adapter.
- `lib/RSEV.Utilities.dll` matching the runtime library used by the host.

From this directory:

```powershell
dotnet build AdminUI.csproj
```

The project writes `AdminUI.dll` to this repository's `bin` directory, as
configured by `AdminUI.csproj`. Copy that assembly manually to
`Adapters/native/AdminUI.dll` in the runtime deployment. The release builder
includes a separately built DLL automatically when it is available.

## Runtime configuration

The adapter reads the following keys from `config.toml`:

| Key | Default | Purpose |
| --- | --- | --- |
| `admin.adminEnabled` | `true` | Enables or disables endpoint registration. |
| `web.port` | `8080` | Port used by the separate web adapter. |
| `admin.adminRequireAuthentication` | `true` in the default config | Requires the host security service for interactive login. |

The adapter declares `web` as a runtime dependency. The host must therefore
load the separate `web` adapter and enable `admin` when the UI is needed.
Authentication and endpoint permissions are provided by the host's
Core.Security services.

## Project documentation

- [Architecture and development](./doc/architecture.md)
- [HTTP API routes](./doc/api.md)

This project is maintained separately from the runtime and web-adapter
repositories. Keep host, web adapter and Admin UI changes coordinated when
changing adapter contracts or runtime APIs.
