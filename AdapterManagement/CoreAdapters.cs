namespace ioBroker_NewGen.Admin.AdapterManagement;

/// <summary>Adapter, die das Admin-UI selbst tragen: kein Autostart-Toggle, keine weiteren Instanzen, kein Entfernen.</summary>
internal static class CoreAdapters
{
    internal static bool IsCore(string? name)
    {
        return string.Equals(name, "admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "web", StringComparison.OrdinalIgnoreCase);
    }
}
