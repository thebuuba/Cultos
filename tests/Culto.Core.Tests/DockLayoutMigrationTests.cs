using System.Xml.Linq;
using Culto.Core;
using Xunit;

namespace Culto.Core.Tests;

public sealed class DockLayoutMigrationTests
{
    private static XDocument Legacy() => XDocument.Parse("""
        <LayoutRoot><RootPanel Orientation="Horizontal">
          <LayoutPanel DockWidth="2.6*" Orientation="Vertical">
            <LayoutAnchorablePane DockHeight="1.7*"><LayoutAnchorable ContentId="preview" IsSelected="true" /></LayoutAnchorablePane>
            <LayoutAnchorablePane DockWidth="0.7*"><LayoutAnchorable ContentId="order" IsSelected="true" /><LayoutAnchorable ContentId="sequence" IsSelected="false" /></LayoutAnchorablePane>
          </LayoutPanel>
          <LayoutAnchorablePane><LayoutAnchorable ContentId="sources" IsSelected="true" /></LayoutAnchorablePane>
        </RootPanel><FloatingWindows><LayoutAnchorableFloatingWindow><LayoutAnchorablePane FloatingWidth="480" FloatingLeft="320">
          <LayoutAnchorable ContentId="output" IsSelected="true" />
        </LayoutAnchorablePane></LayoutAnchorableFloatingWindow></FloatingWindows></LayoutRoot>
        """);

    [Fact]
    public void LegacyLayoutAddsMediaBesideSequenceAndSelectsItWithoutResettingOtherGeometry()
    {
        var document = Legacy();
        var floating = document.Descendants("FloatingWindows").Single().ToString();
        var sources = document.Descendants().Single(e => (string?)e.Attribute("ContentId") == "sources").Parent!.ToString();
        Assert.True(DockLayoutMigration.AddMediaTab(document));
        var sequence = document.Descendants().Single(e => (string?)e.Attribute("ContentId") == "sequence");
        var media = document.Descendants().Single(e => (string?)e.Attribute("ContentId") == "media");
        Assert.Same(sequence.Parent, media.Parent);
        Assert.Equal(new[] { "order", "sequence", "media" }, media.Parent!.Elements().Select(e => (string)e.Attribute("ContentId")!));
        Assert.Equal("0.7*", (string?)media.Parent.Attribute("DockWidth"));
        Assert.Equal("Medios", (string?)media.Attribute("Title"));
        Assert.Equal("true", (string?)media.Attribute("IsSelected"));
        Assert.All(media.Parent.Elements().Where(e => e != media), e => Assert.Equal("false", (string?)e.Attribute("IsSelected")));
        Assert.Equal(floating, document.Descendants("FloatingWindows").Single().ToString());
        Assert.Equal(sources, document.Descendants().Single(e => (string?)e.Attribute("ContentId") == "sources").Parent!.ToString());
    }

    [Fact]
    public void AlreadyMigratedLayoutPreservesSelectedTabAndDoesNotDuplicateMedia()
    {
        var document = Legacy();
        Assert.True(DockLayoutMigration.AddMediaTab(document));
        var media = document.Descendants().Single(e => (string?)e.Attribute("ContentId") == "media");
        media.SetAttributeValue("IsSelected", "false");
        media.Parent!.Elements().First().SetAttributeValue("IsSelected", "true");
        var saved = document.ToString();
        Assert.False(DockLayoutMigration.AddMediaTab(document));
        Assert.Equal(saved, document.ToString());
    }

    [Theory]
    [InlineData("output", "unknown")]
    [InlineData("output", "sequence")]
    [InlineData("sequence", "unknown")]
    public void InvalidContentSetsAreNotRepairedIntoApparentlyValidLayouts(string existing, string replacement)
    {
        var document = Legacy();
        document.Descendants().Single(e => (string?)e.Attribute("ContentId") == existing).SetAttributeValue("ContentId", replacement);
        var before = document.ToString();
        Assert.False(DockLayoutMigration.AddMediaTab(document));
        Assert.Equal(before, document.ToString());
    }

    [Fact]
    public void MissingPanelAndInvalidSequenceParentRemainUnchanged()
    {
        foreach (var remove in new[] { true, false })
        {
            var document = Legacy();
            var sequence = document.Descendants().Single(e => (string?)e.Attribute("ContentId") == "sequence");
            if (remove) sequence.Remove(); else sequence.Parent!.Name = "Hidden";
            var before = document.ToString();
            Assert.False(DockLayoutMigration.AddMediaTab(document));
            Assert.Equal(before, document.ToString());
        }
    }
}
