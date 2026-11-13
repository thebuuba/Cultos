using System.Xml.Linq;

namespace Culto.Desktop;

internal static class WorkspaceLayouts
{
    internal const double PrincipalWindowWidth = 1420;
    private static readonly string[] LegacyIds = ["media", "order", "output", "preview", "sequence", "sources"];
    private static readonly string[] SourceIds = ["sources", "bible", "text", "youtube"];
    private static readonly string[] TabbedSourceIds = ["sources", "bible", "youtube", "media", "text"];

    internal static string Title(string id) => id switch
    {
        "sources" => "Himnario", "bible" => "Biblia", "text" => "Texto", "youtube" => "YouTube",
        "preview" => "Monitores", "output" => "En pantalla", "order" => "Orden del culto",
        "media" => "Medios", "scenes" => "Escenas", _ => id
    };

    internal static double MinimumWidth(string? id) => id switch
    {
        "scenes" => 156, "bible" => 200, "preview" => 620, "output" => 220, _ => 180
    };

    internal static double MinimumHeight(string? id) => id switch
    {
        "scenes" or "preview" or "output" => 240, "order" => 160, _ => 180
    };

    internal static XDocument Create(string preset)
    {
        var monitors = Group("Horizontal", "*", preset == "proyeccion" ? "1.8*" : "1.15*", Pane("0.4*", "scenes"), Pane("2*", "preview"));
        XElement root = preset switch
        {
            "principal" => Group("Horizontal", "*", "*",
                Pane("1.1*", TabbedSourceIds),
                Group("Vertical", "2.8*", "*", monitors,
                    Pane("*", "order"))),
            "compacta" => Group("Horizontal", "*", "*",
                Pane("1.1*", TabbedSourceIds),
                Group("Vertical", "2.2*", "*", monitors,
                    Pane("*", "order"))),
            "proyeccion" => Group("Horizontal", "*", "*", Pane("0.6*", SourceIds),
                Group("Vertical", "2.8*", "*", monitors,
                    Group("Horizontal", "*", "*", Pane("*", "media"), Pane("*", "order")))),
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
        new XAttribute("CanClose", false), new XAttribute("CanHide", true), new XAttribute("CanAutoHide", false),
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

    internal static bool PlacePanel(XDocument document, string sourceId, string targetId, string position)
    {
        var source = document.Descendants("LayoutAnchorable").SingleOrDefault(e => (string?)e.Attribute("ContentId") == sourceId);
        var target = document.Descendants("LayoutAnchorable").SingleOrDefault(e => (string?)e.Attribute("ContentId") == targetId);
        if (source is null || target is null || source == target || position is not ("top" or "bottom" or "left" or "right" or "tab")) return false;
        var oldPane = source.Parent!;
        var targetPane = target.Parent!;
        source.Remove();
        oldPane.Attribute("SelectedContentIndex")?.Remove();
        if (!oldPane.Elements("LayoutAnchorable").Any(e => (bool?)e.Attribute("IsSelected") == true))
            oldPane.Elements("LayoutAnchorable").FirstOrDefault()?.SetAttributeValue("IsSelected", true);
        if (position == "tab")
        {
            foreach (var sibling in targetPane.Elements("LayoutAnchorable")) sibling.SetAttributeValue("IsSelected", false);
            targetPane.Attribute("SelectedContentIndex")?.Remove();
            targetPane.Add(source);
        }
        else
        {
            var pane = new XElement("LayoutAnchorablePane", new XAttribute("DockWidth", "*"), new XAttribute("DockHeight", "*"), source);
            var horizontal = position is "left" or "right";
            var orientation = horizontal ? "Horizontal" : "Vertical";
            var parent = targetPane.Parent!;
            // Keep the target's share of the outer layout, splitting just its
            // rectangle. This works inside docked and floating pane groups.
            var group = new XElement(parent.Name.LocalName == "LayoutAnchorablePaneGroup" ? "LayoutAnchorablePaneGroup" : "LayoutPanel",
                new XAttribute("Orientation", orientation),
                new XAttribute("DockWidth", (string?)targetPane.Attribute("DockWidth") ?? "*"),
                new XAttribute("DockHeight", (string?)targetPane.Attribute("DockHeight") ?? "*"));
            targetPane.ReplaceWith(group);
            targetPane.SetAttributeValue("DockWidth", "*"); targetPane.SetAttributeValue("DockHeight", "*");
            if (position is "top" or "left") group.Add(pane, targetPane); else group.Add(targetPane, pane);
        }
        source.SetAttributeValue("IsSelected", true);
        foreach (var node in document.Descendants().Reverse().ToArray())
            if (node.Name.LocalName is "LayoutAnchorablePane" or "LayoutPanel" or "LayoutAnchorablePaneGroup" or "LayoutAnchorableFloatingWindow"
                && !node.Descendants("LayoutAnchorable").Any()) node.Remove();
        // Moving between the same two panels must not keep nesting halves of
        // halves. Collapse redundant groups, retaining their outer proportions.
        foreach (var group in document.Descendants("LayoutPanel").Reverse().ToArray())
        {
            if (group.Elements().Count() != 1) continue;
            var child = group.Elements().Single(); child.Remove();
            foreach (var name in new[] { "DockWidth", "DockHeight" })
                child.SetAttributeValue(name, (string?)group.Attribute(name) ?? "*");
            group.ReplaceWith(child);
        }
        document.Root!.Attribute("WorkspacePreset")?.Remove();
        return true;
    }

    internal static void RemoveRetiredSequence(XDocument document)
    {
        string[] oldIds = ["bible", "media", "order", "output", "preview", "sequence", "sources", "text", "youtube"];
        var contents = document.Descendants().Where(e => e.Attribute("ContentId") is not null).ToArray();
        // Only migrate a complete former layout; malformed/duplicate IDs still
        // fail validation before replacing the user's current workspace.
        if (!contents.Select(e => (string)e.Attribute("ContentId")!).Where(id => id != "scenes").Order().SequenceEqual(oldIds)) return;
        contents.Single(e => (string?)e.Attribute("ContentId") == "sequence").Remove();
        var previousContainers = document.Descendants().Attributes("PreviousContainerId").Select(a => a.Value).ToHashSet();
        foreach (var node in document.Descendants().Reverse().ToArray())
        {
            if (node.Name.LocalName is "LayoutAnchorablePane" or "LayoutPanel" or "LayoutAnchorablePaneGroup" or "LayoutAnchorableFloatingWindow"
                && !node.Descendants("LayoutAnchorable").Any()
                && !node.DescendantsAndSelf().Any(e => e.Attribute("Id") is { } id && previousContainers.Contains(id.Value)))
            { node.Remove(); continue; }
            if (node.Name == "LayoutAnchorablePane")
            {
                node.Attribute("SelectedContentIndex")?.Remove();
                if (!node.Elements().Any(e => (bool?)e.Attribute("IsSelected") == true)) node.Elements().FirstOrDefault()?.SetAttributeValue("IsSelected", true);
            }
        }
        document.Root?.Attribute("WorkspacePreset")?.Remove();
    }

    internal static void AddScenesPanel(XDocument document)
    {
        string[] previousIds = ["bible", "media", "order", "output", "preview", "sources", "text", "youtube"];
        var contents = document.Descendants().Where(e => e.Attribute("ContentId") is not null).ToArray();
        if (!contents.Select(e => (string)e.Attribute("ContentId")!).Order().SequenceEqual(previousIds)) return;
        var preview = contents.Single(e => (string?)e.Attribute("ContentId") == "preview");
        var output = contents.Single(e => (string?)e.Attribute("ContentId") == "output");
        var target = new[] { preview.Parent, output.Parent }.FirstOrDefault(e => e?.Name == "LayoutAnchorablePane");
        var scenes = Pane("0.4*", "scenes");
        if (target is not null) target.AddBeforeSelf(scenes);
        else document.Root?.Element("RootPanel")?.Add(scenes);
    }

    internal static void MergeMonitors(XDocument document)
    {
        var output = document.Descendants("LayoutAnchorable").SingleOrDefault(e => (string?)e.Attribute("ContentId") == "output");
        if (output is null) return;
        output.Remove();
        foreach (var node in document.Descendants().Reverse().ToArray())
            if (node.Name.LocalName is "LayoutAnchorablePane" or "LayoutPanel" or "LayoutAnchorablePaneGroup" or "LayoutAnchorableFloatingWindow"
                && !node.Descendants("LayoutAnchorable").Any()) node.Remove();
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
