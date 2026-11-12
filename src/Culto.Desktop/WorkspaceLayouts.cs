using System.Xml.Linq;

namespace Culto.Desktop;

internal static class WorkspaceLayouts
{
    internal const double PrincipalWindowWidth = 1420;
    private static readonly string[] LegacyIds = ["media", "order", "output", "preview", "sequence", "sources"];
    private static readonly string[] SourceIds = ["sources", "bible", "text", "youtube"];

    internal static string Title(string id) => id switch
    {
        "sources" => "Himnario", "bible" => "Biblia", "text" => "Texto", "youtube" => "YouTube",
        "preview" => "Vista previa", "output" => "En pantalla", "order" => "Orden del culto",
        "sequence" => "Secuencia", "media" => "Medios", _ => id
    };

    internal static double MinimumWidth(string? id) => id switch
    {
        "sources" or "text" or "media" => 180, "youtube" => 190,
        "bible" or "preview" or "output" or "order" or "sequence" => 220, _ => 180
    };

    internal static double MinimumHeight(string? id) => id switch
    {
        "sources" or "bible" => 400, "youtube" => 320, "text" => 280,
        "preview" or "output" => 240, "media" => 260, "sequence" => 150, _ => 160
    };

    internal static XDocument Create(string preset)
    {
        var monitors = Group("Horizontal", "*", preset == "proyeccion" ? "1.8*" : "*", Pane("*", "preview"), Pane("*", "output"));
        XElement root = preset switch
        {
            "principal" => Group("Horizontal", "*", "*",
                Pane("220*", "media"), Pane("240*", "sources", "text"), Pane("240*", "bible"), Pane("220*", "youtube"),
                Group("Vertical", "800*", "*", monitors,
                    Group("Horizontal", "*", "*", Pane("*", "order"), Pane("*", "sequence")))),
            "compacta" => Group("Horizontal", "*", "*",
                Group("Vertical", "2.2*", "*", monitors,
                    Group("Horizontal", "*", "1.2*", Pane("0.9*", "order"), Pane("1.3*", "media", "sequence"))),
                Pane("1.1*", SourceIds)),
            "proyeccion" => Group("Horizontal", "*", "*", Pane("0.85*", SourceIds),
                Group("Vertical", "2.8*", "*", monitors,
                    Group("Horizontal", "*", "*", Pane("*", "media", "sequence"), Pane("*", "order")))),
            _ => throw new ArgumentException("Distribución de paneles desconocida.", nameof(preset))
        };
        root.Name = "RootPanel";
        return new XDocument(new XElement("LayoutRoot", root, new XElement("TopSide"), new XElement("RightSide"),
            new XElement("LeftSide"), new XElement("BottomSide"), new XElement("FloatingWindows"), new XElement("Hidden")));
    }

    private static XElement Group(string orientation, string width, string height, params XElement[] children) =>
        new("LayoutPanel", new XAttribute("Orientation", orientation), new XAttribute("DockWidth", width),
            new XAttribute("DockHeight", height), children);

    private static XElement Pane(string width, params string[] ids) => new("LayoutAnchorablePane",
        new XAttribute("DockWidth", width), new XAttribute("DockMinWidth", ids.Max(MinimumWidth)),
        new XAttribute("DockMinHeight", ids.Max(MinimumHeight)), ids.Select((id, index) => Content(id, index == 0)));

    private static XElement Content(string id, bool selected) => new("LayoutAnchorable",
        new XAttribute("Title", Title(id)), new XAttribute("ContentId", id), new XAttribute("IsSelected", selected),
        new XAttribute("CanClose", false), new XAttribute("CanHide", false), new XAttribute("CanAutoHide", false),
        new XAttribute("CanDockAsTabbedDocument", false));

    internal static bool ExpandLegacySources(XDocument document)
    {
        var contents = document.Descendants().Where(e => e.Attribute("ContentId") is not null).ToArray();
        // Only expand complete former layouts. Missing, duplicate and unknown IDs
        // still fail validation before the live workspace is replaced.
        if (!contents.Select(e => (string)e.Attribute("ContentId")!).Order().SequenceEqual(LegacyIds)) return false;
        var sources = contents.Single(e => (string?)e.Attribute("ContentId") == "sources");
        if (sources.Name != "LayoutAnchorable" || sources.Parent is not { Name.LocalName: "LayoutAnchorablePane" } pane) return false;
        sources.SetAttributeValue("Title", Title("sources"));
        foreach (var id in SourceIds.Skip(1)) pane.Add(Content(id, false));
        return true;
    }

    internal static string Arrangement(XDocument document)
    {
        // Tab activation and measured pixel sizes do not make a preset custom.
        // Moving, regrouping, floating or resizing a separator does.
        XElement Copy(XElement element) => new(element.Name,
            element.Attributes().Where(a => a.Name.LocalName is "Orientation" or "DockWidth" or "DockHeight" or "ContentId"),
            element.Elements().Select(Copy));
        return Copy(document.Root!).ToString(SaveOptions.DisableFormatting);
    }
}
