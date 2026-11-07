# Changelog

## [1.3.0] - 2026-11-07

### Added
- Objects tab: tree is collapsed by default, with expand-all/collapse-all buttons and deep links (`#tab-objects/select/<id>`).
- Objects tab: columns ID, name, type, role, room, function and value; values refresh live.
- Writable states can be changed directly: switch for booleans, number/text input for other types. Inputs are locked until double-clicked and are written with Enter (Esc cancels).
- New endpoint `POST /api/states/write` (permission `StateWrite`), writes with `ack=false`.

### Changed
- Built against Runtime `2.4.0-beta`.

## [1.2.0] - 2026-11-07

### Added
- Native Admin discovers legacy adapter tabs from `common.adminTab` and displays them in the sidebar in an iframe.
- Legacy adapter admin files are served through an authenticated route; the ioBroker WebSocket client is embedded in the Admin assembly.

### Changed
- Built against Runtime `2.3.0-beta`.

## [1.1.1] - 2026-11-06

### Fixed
- Anmeldung: Der Zugriff auf Core.Security funktioniert wieder (Adapter erhalten einen Runtime-Proxy statt des MainRuntimeContext).

### Changed
- Die Adapter `web` und `admin` haben keinen Button "Instanz entfernen" mehr, keinen Autostart-Toggle und keine weiteren Instanzen (serverseitig geprüft, `CoreAdapters`).
- Stoppen/Neustarten von `web.0` zeigt dieselbe Warnung wie `admin.0`.
- Gebaut gegen Runtime `2.1.1-beta`.

## [1.1.0] - 2026-11-06

### Added
- The adapter reports its version (from the csproj `Version`) to the runtime
  registry via `IAdapter.Version`, so it is readable in the Admin UI. The
  constant `AdminVersion.Value` is generated at build time.
- Dashboard sources (HTML, CSS, JavaScript) live in `src/` and are embedded as
  resources; `Ui/DashboardPage.cs` assembles them.

### Changed
- `AdminAdapter.cs` (about 2700 lines) was split into classes under
  `ioBroker_NewGen.Admin.*` (`AdapterManagement`, `Auth`, `Jobs`, `Monitoring`,
  `Settings`, `Http`, `Ui`).
- The main class and file were renamed to `Main` / `Main.cs`.
- The project is now `ioBroker-NewGen.admin.csproj`; the output assembly is
  `ioBroker-NewGen.admin-<Version>.dll`, and `lib\*.dll` is referenced
  automatically.
- Requires runtime `2.1.0-beta` or newer for the version API.

## [1.0.0]

- Initial release.
