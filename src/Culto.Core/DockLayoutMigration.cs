using System.Xml.Linq;

namespace Culto.Core;

public static class DockLayoutMigration
{
    public static bool AddMediaTab(XDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        string[] legacyIds = ["order", "output", "preview", "sequence", "sources"];
        var contents = document.Descendants().Where(e => e.Attribute("ContentId") is not null).ToArray();
        // Migrate only a complete former layout. Duplicates, missing panels and
        // unknown IDs must still fail normal restore validation, not be repaired.
        if (!contents.Select(e => (string)e.Attribute("ContentId")!).Order().SequenceEqual(legacyIds)) return false;
        var sequence = contents.Single(e => (string?)e.Attribute("ContentId") == "sequence");
        if (sequence.Name.LocalName != "LayoutAnchorable" || sequence.Parent is not { } pane
            || pane.Name.LocalName != "LayoutAnchorablePane") return false;

        var media = new XElement(sequence.Name,
            new XAttribute("Title", "Medios"), new XAttribute("ContentId", "media"),
            new XAttribute("IsSelected", "true"), new XAttribute("CanClose", "false"),
            new XAttribute("CanHide", "false"), new XAttribute("CanAutoHide", "false"),
            new XAttribute("CanDockAsTabbedDocument", "false"));
        foreach (var sibling in pane.Elements().Where(e => e.Name.LocalName == "LayoutAnchorable"))
            sibling.SetAttributeValue("IsSelected", "false");
        sequence.AddAfterSelf(media);
        return true;
    }
}
