using System.Text.Json;
using Culto.Core;
using Xunit;

namespace Culto.Core.Tests;

public sealed class OrderAnnouncerTests
{
    [Fact]
    public void AnnouncerSurvivesPlanSavingAndCopying()
    {
        var cue = new Cue { Title = "Ofrendas", Announcer = "María" };
        var plan = new ServicePlan { Items = [cue] };
        var restored = JsonSerializer.Deserialize<ServicePlan>(JsonSerializer.Serialize(plan))!;
        Assert.Equal("María", restored.Items[0].Announcer);
        Assert.Equal("María", cue.Copy(newId: true).Announcer);
        Assert.Equal("", JsonSerializer.Deserialize<Cue>("{\"Title\":\"Culto anterior\"}")!.Announcer);
    }
}
