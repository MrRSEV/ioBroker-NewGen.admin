# Changelog

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
