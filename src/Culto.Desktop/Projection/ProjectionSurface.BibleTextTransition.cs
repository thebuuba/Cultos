using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Culto.Core;

namespace Culto.Desktop.Projection;

public sealed partial class ProjectionSurface
{
    private bool _bibleTextAnimating;
    private int _bibleTextGeneration;
    private readonly List<(TextBlock Block, Effect? Effect)> _bibleTextEffects = [];

    internal void BeginBibleTextTransition(string? systemTransition = null)
    {
        CancelBibleTextTransition();
        if (_disposed || _preview || _suppressed || !_presentedBibleStyle.UsesTextTransition) return;
        UpdateLayout();
        _bibleTextAnimating = true;
        var milliseconds = _presentedBibleStyle.TransitionMilliseconds;
        BeginBiblePresetTransition(milliseconds, systemTransition ?? _presentedTransitions.Kind);
        // A common completion clock also covers delayed line/reference animations.
        var generation = _bibleTextGeneration;
        var completion = new DoubleAnimation(1, 1, TimeSpan.FromMilliseconds(milliseconds)) { FillBehavior = FillBehavior.Stop };
        completion.Completed += (_, _) => { if (generation == _bibleTextGeneration) CancelBibleTextTransition(); };
        _words.BeginAnimation(OpacityProperty, completion);
    }

    private void BeginBibleBlockTransition(TextBlock block, string kind, int milliseconds)
    {
        if (kind == "Cut") return;
        if (kind == "TextLines") { BeginBibleLineReveal(block, milliseconds); return; }
        FadeBibleText(block, 0, milliseconds);
        if (kind is "TextRise" or "Slide" or "Move")
        {
            var move = new TranslateTransform(0, 24);
            block.RenderTransform = move;
            AnimateBiblePreset(move, TranslateTransform.YProperty, PresetFade(24, 0, 0, milliseconds,
                new CubicEase { EasingMode = EasingMode.EaseOut }));
        }
        else if (kind == "Zoom")
        {
            var scale = new ScaleTransform(.92, .92);
            block.RenderTransformOrigin = new Point(.5, .5); block.RenderTransform = scale;
            AnimateBiblePreset(scale, ScaleTransform.ScaleXProperty, PresetFade(.92, 1, 0, milliseconds));
            AnimateBiblePreset(scale, ScaleTransform.ScaleYProperty, PresetFade(.92, 1, 0, milliseconds));
        }
        else if (kind == "TextFocus")
        {
            _bibleTextEffects.Add((block, block.Effect));
            var blur = new BlurEffect { Radius = 9, RenderingBias = RenderingBias.Performance };
            block.Effect = blur;
            AnimateBiblePreset(blur, BlurEffect.RadiusProperty, PresetFade(9, 0, 0, milliseconds,
                new CubicEase { EasingMode = EasingMode.EaseOut }));
        }
    }

    private static void FadeBibleText(UIElement element, double delay, double milliseconds)
    {
        element.Opacity = 0;
        element.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(milliseconds))
        {
            BeginTime = TimeSpan.FromMilliseconds(delay),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }

    private static void BeginBibleLineReveal(TextBlock block, int milliseconds)
    {
        // Use WPF's actual wrapped lines, preserving layout, typeface and alignment.
        var tops = new List<double>();
        var line = block.ContentStart.GetLineStartPosition(0);
        while (line is not null)
        {
            var rect = line.GetCharacterRect(LogicalDirection.Forward);
            if (!rect.IsEmpty && (tops.Count == 0 || rect.Top > tops[^1])) tops.Add(rect.Top);
            var next = line.GetLineStartPosition(1);
            if (next is null || next.CompareTo(line) <= 0) break;
            line = next;
        }
        if (tops.Count == 0 || block.ActualWidth < 1 || block.ActualHeight < 1)
        {
            FadeBibleText(block, 0, milliseconds);
            return;
        }
        var drawing = new DrawingGroup();
        for (var index = 0; index < tops.Count; index++)
        {
            var top = index == 0 ? 0 : tops[index];
            var bottom = index + 1 < tops.Count ? tops[index + 1] : block.ActualHeight;
            var brush = new SolidColorBrush(Colors.White) { Opacity = 0 };
            drawing.Children.Add(new GeometryDrawing(brush, null, new RectangleGeometry(new Rect(0, top, block.ActualWidth, Math.Max(0, bottom - top)))));
            var delay = tops.Count == 1 ? 0 : milliseconds * .55 * index / (tops.Count - 1);
            brush.BeginAnimation(Brush.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(milliseconds * .45))
                { BeginTime = TimeSpan.FromMilliseconds(delay) });
        }
        block.OpacityMask = new DrawingBrush(drawing) { Stretch = Stretch.Fill };
    }

    private void CancelBibleTextTransition()
    {
        _bibleTextGeneration++;
        _bibleTextAnimating = false;
        CancelBiblePresetTransition();
        foreach (var (block, effect) in _bibleTextEffects) block.Effect = effect;
        _bibleTextEffects.Clear();
        _words.BeginAnimation(OpacityProperty, null);
        _words.Opacity = 1;
        if (_words.Effect is BlurEffect blur) blur.BeginAnimation(BlurEffect.RadiusProperty, null);
        _words.Effect = null;
        foreach (var block in new[] { _body, _reference, _version })
        {
            block.BeginAnimation(OpacityProperty, null);
            block.Opacity = 1;
            if (block.RenderTransform is TranslateTransform move) move.BeginAnimation(TranslateTransform.YProperty, null);
            if (block.OpacityMask is DrawingBrush { Drawing: DrawingGroup lines })
                foreach (var drawing in lines.Children)
                    if (drawing is GeometryDrawing { Brush: SolidColorBrush brush }) brush.BeginAnimation(Brush.OpacityProperty, null);
            block.RenderTransform = Transform.Identity;
            block.OpacityMask = null;
        }
    }
}
