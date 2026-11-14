using System.Text.Json.Serialization;

namespace Culto.Desktop.Services;

public sealed class WorkspaceNote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.Now;
    [JsonIgnore] public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? "Nueva nota" : Title.Trim();
    [JsonIgnore] public string Preview => Body.Replace('\r', ' ').Replace('\n', ' ');
    [JsonIgnore] public string ModifiedLabel => ModifiedAt.ToLocalTime().ToString("g");
}
