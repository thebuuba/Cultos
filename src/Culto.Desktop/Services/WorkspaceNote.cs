using System.Text.Json.Serialization;

namespace Culto.Desktop.Services;

public sealed class WorkspaceNote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string Category { get; set; } = "sermon";
    public bool IsPinned { get; set; }
    public bool ShowInModerator { get; set; } = true;
    public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.Now;
    [JsonIgnore] public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? "Nueva nota" : Title.Trim();
    [JsonIgnore] public string Preview => string.IsNullOrWhiteSpace(Body) ? "Nota vacía" : Body.Replace("\r", "");
    [JsonIgnore] public string GroupLabel => IsPinned ? "FIJADAS" : "OTRAS";
    [JsonIgnore] public string CategoryLabel => Category switch { "anuncio" => "Anuncio", "oracion" => "Oración", "recordatorio" => "Recordatorio", _ => "Sermón" };
    [JsonIgnore] public string CategoryColor => Category switch { "anuncio" => "#38BDF8", "oracion" => "#C084FC", "recordatorio" => "#FBBF24", _ => "#BEF264" };
    [JsonIgnore] public string CategoryBackground => "#22" + CategoryColor[1..];
    [JsonIgnore] public string ModifiedLabel
    {
        get
        {
            var elapsed = DateTimeOffset.Now - ModifiedAt;
            var date = ModifiedAt.ToLocalTime();
            return elapsed.TotalMinutes < 1 ? "Ahora" : elapsed.TotalHours < 1 ? $"Hace {(int)elapsed.TotalMinutes} min"
                : date.Date == DateTime.Today ? "Hoy, " + date.ToString("H:mm")
                : date.Date == DateTime.Today.AddDays(-1) ? "Ayer" : date.ToString("dd MMM", System.Globalization.CultureInfo.GetCultureInfo("es-DO"));
        }
    }
}
