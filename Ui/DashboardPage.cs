using System.Reflection;

namespace ioBroker_NewGen.Admin.Ui;

/// <summary>
/// Baut die Admin-Oberfläche aus den eingebetteten Dateien unter <c>src/</c>
/// (index.html, admin.css, js/*.js) zu einer einzigen HTML-Seite zusammen.
/// </summary>
internal static class DashboardPage
{
    private const string ResourcePrefix = "ui/";
    private const string StylePlaceholder = "<!--@style-->";
    private const string ScriptPlaceholder = "<!--@script-->";

    private static readonly Lazy<string> Page = new(Build);

    public static string Render() => Page.Value;

    private static string Build()
    {
        var assembly = typeof(DashboardPage).Assembly;
        var resources = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .ToDictionary(name => name.Replace('\\', '/'), name => name, StringComparer.Ordinal);

        string Read(string path)
        {
            using var stream = assembly.GetManifestResourceStream(resources[ResourcePrefix + path])
                ?? throw new InvalidOperationException($"UI-Ressource '{path}' fehlt.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        var scripts = resources.Keys
            .Where(key => key.StartsWith(ResourcePrefix + "js/", StringComparison.Ordinal) && key.EndsWith(".js", StringComparison.Ordinal))
            .OrderBy(key => key, StringComparer.Ordinal)
            .Select(key => Read(key[ResourcePrefix.Length..]));

        return Read("index.html")
            .Replace(StylePlaceholder, "<style>\n" + Read("admin.css") + "</style>", StringComparison.Ordinal)
            .Replace(ScriptPlaceholder, "<script>\n" + string.Concat(scripts) + "</script>", StringComparison.Ordinal);
    }
}
