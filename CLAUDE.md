# Claude instructions for ioBroker-NewGen.admin

## Repository scope

This repository contains the native C# `admin` adapter. It serves the Admin
dashboard, authenticated Admin APIs, legacy adapter UI assets, and the legacy
Socket.IO-compatible WebSocket endpoint. The HTTP/WebSocket listener belongs to
the separate `ioBroker-NewGen.web` adapter.

## Build

- Target framework: .NET 9.
- Build from this repository with:

  ```powershell
  dotnet build AdminUI.csproj --no-restore
  ```

- The project references host contract assemblies from this repository's
  `/lib` directory. Keep those DLLs synchronized with the runtime before
  building.
- Record every change in `CHANGELOG.md`. If the change affects an Admin API,
  route, legacy UI behavior, configuration, architecture, or debugging/build
  workflow, update the relevant documentation under `/Doku` or `doc/` too.

## Required post-build synchronization

After every successful Admin build, copy the resulting Admin DLL to:

```text
ioBroker-NewGen.runtime/bin/debug/Adapters/native
```

On the current .NET output layout, the effective directory may include the
target framework as `ioBroker-NewGen.runtime/bin/Debug/net9.0/Adapters/native`.
Use the directory used by the runtime that will actually be debugged, and
replace the existing Admin DLL rather than leaving an older same-version DLL in
place. Restart the runtime after replacing a loaded assembly and hard-refresh
the browser when testing Admin UI changes.

Example from this repository:

```powershell
$runtimeNative = '..\..\ioBroker-NewGen.runtime\bin\Debug\net9.0\Adapters\native'
Copy-Item 'bin\ioBroker-NewGen.admin-*.dll' $runtimeNative -Force
```

## Runtime Core dependency rule

If debugging changes any class, interface, or resource in the runtime core,
the dependent resources must also be refreshed after the runtime build:

1. Build `ioBroker-NewGen.runtime` first.
2. Copy/replace the fresh runtime contract and dependency assemblies in this
   repository's `/lib` directory.
3. Copy/replace the same updated resources in the relevant dependent `/lib`
   directories (especially the Web Adapter repository) before rebuilding.
4. Rebuild the Admin DLL.
5. Copy the rebuilt Admin DLL to
   `ioBroker-NewGen.runtime/bin/debug/Adapters/native` (or its active
   `bin/Debug/net9.0/...` equivalent).

Never debug against stale `/lib` assemblies after changing runtime core
classes; otherwise the build or runtime may load an older contract and produce
misleading errors.

## Implementation rules

- Preserve authentication and permission checks for every Admin endpoint.
- Keep legacy adapter file paths and Socket.IO compatibility behavior working;
  test both old `adapter-settings.js` pages and modern React admin builds.
- Use `IWebEndpointRegistry` for routes and WebSockets; do not add a listener to
  the Admin adapter.
- Use `apply_patch` for source/documentation edits and inspect `git diff` before
  handing off.
