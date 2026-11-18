using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Culto.Core;

namespace Culto.Desktop;

public partial class MainWindow
{
    private void MovePlanItem(int from, int to)
    {
        if (from == to) return;
        PlanList.UpdateLayout();
        var positions = new Dictionary<Cue, double>();
        foreach (var cue in _plan)
            if (PlanList.ItemContainerGenerator.ContainerFromItem(cue) is ListBoxItem row)
                positions[cue] = row.TranslatePoint(new Point(), PlanList).Y;

        _plan.Move(from, to);
        PlanList.UpdateLayout();
        foreach (var cue in _plan)
        {
            if (PlanList.ItemContainerGenerator.ContainerFromItem(cue) is not ListBoxItem row) continue;
            row.RenderTransform = Transform.Identity;
            if (!SystemParameters.ClientAreaAnimation || !positions.TryGetValue(cue, out var previous)) continue;
            var offset = previous - row.TranslatePoint(new Point(), PlanList).Y;
            if (Math.Abs(offset) < .5) continue;
            var move = new TranslateTransform();
            row.RenderTransform = move;
            move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(offset, 0, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            });
        }
    }
}
