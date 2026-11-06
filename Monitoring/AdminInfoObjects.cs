using ioBroker_NewGen.Core.Adapters;
using ioBroker_NewGen.Core.Matrix;

namespace ioBroker_NewGen.Admin.Monitoring;

/// <summary>Legt den Objektbaum admin / admin.0 / admin.0.info mit den schreibgeschützten Info-States an.</summary>
internal static class AdminInfoObjects
{
    public const string Prefix = "admin.0.info.";

    public static void Register(IAdapterContext context)
    {
        var matrix = context.Matrix;
        void Obj(string id, string name, string type, string? role = null, string? valueType = null, string? desc = null, bool readOnly = false)
        {
            var meta = new Dictionary<string, string>();
            if (readOnly)
            {
                meta["read"] = "true";
                meta["write"] = "false";
                meta["readonly"] = "true";
            }

            if (desc is not null)
            {
                meta["desc"] = desc;
            }

            matrix.RegisterObject(new MatrixObject
            {
                Id = id,
                Name = name,
                Type = type,
                Meta = meta,
                StateDefinition = type == "state" ? new StateDefinition { ValueType = valueType, Role = role } : null
            });
        }

        Obj("admin", "admin", "adapter");
        Obj("admin.0", "Benutzerdateien und Bilder für den Hintergrund", "meta");
        Obj("admin.0.info", "Information", "channel");
        Obj(Prefix + "connected", "Informationen über verbundene Socket-Clients", "state", "state", "string", readOnly: true);
        Obj(Prefix + "connection", "HTTP-Server gestartet", "state", "indicator.connected", "bool", readOnly: true);
        Obj(Prefix + "lastUpdateCheck", "Zeitstempel der letzten UpdatePrüfung", "state", "value.time", "string", readOnly: true);
        Obj(Prefix + "newUpdates", "Anzeige, ob neue Adapter-Updates verfügbar sind", "state", "indicator.updates", "bool", readOnly: true);
        Obj(Prefix + "newsETag", "Etag für Newsfeed", "state", "state", "string", readOnly: true);
        Obj(Prefix + "newsFeed", "Letzter Newsfeed als JSON", "state", "state", "string", readOnly: true);
        Obj(Prefix + "newsLastId", "Letzte Nachrichten-ID", "state", "state", "string", readOnly: true);
        Obj(Prefix + "updatesJson", "JSON-String mit Adapteraktualisierungsinformationen", "state", "indicator.updates", "string", readOnly: true);
        Obj(Prefix + "updatesList", "Liste der zu aktualisierenden Adapter", "state", "state", "string", readOnly: true);
        Obj(Prefix + "updatesNumber", "Anzahl der zu aktualisierenden Adapter", "state", "state", "int", readOnly: true);

        matrix.SetState(Prefix + "connected", "[1]admin");
        matrix.SetState(Prefix + "connection", true);
        matrix.SetState(Prefix + "lastUpdateCheck", DateTime.Now.ToString("R"));
        matrix.SetState(Prefix + "newUpdates", false);
        matrix.SetState(Prefix + "updatesJson", "{}");
        matrix.SetState(Prefix + "updatesNumber", 0);
    }

    public static void MarkDisconnected(IAdapterContext context)
        => context.Matrix.SetState(Prefix + "connection", false);
}
