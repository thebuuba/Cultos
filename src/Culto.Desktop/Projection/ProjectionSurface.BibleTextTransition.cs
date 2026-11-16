using System;
using System.Collections.Generic;
using System.Windows;
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

    internal void BeginBibleTextTransition()
    {
        CancelBibleTextTransition();
        if (_disposed || _preview || _suppressed || !_presentedBibleStyle.UsesTextTransition) return;
        UpdateLayout();
        _bibleTextAnimating = true;
        var milliseconds = _presentedBibleStyle.TransitionMilliseconds;
        var kind = _presentedBibleStyle.Transition;
        if (kind.StartsWith("TextPreset:", StringComparison.Ordinal) && BibleTextTransitionCatalog.TryGet(kind, out var preset))
            BeginBiblePresetTransition(preset, milliseconds);
        else if (kind == "TextLines") BeginBibleLineReveal(milliseconds);
        else FadeBibleText(_body, 0, milliseconds * .75);
        foreach (var block in new[] { _reference, _version }) FadeBibleText(block, milliseconds * .35, milliseconds * .65);
        if (kind == "TextRise")
        {
            foreach (var block in new[] { _body, _reference, _version })
            {
                var move = new TranslateTransform(0, 24);
                block.RenderTransform = move;
                move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(24, 0, TimeSpan.FromMilliseconds(milliseconds))
                    { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            }
        }
        else if (kind == "TextFocus")
        {
            var blur = new BlurEffect { Radius = 9, RenderingBias = RenderingBias.Performance };
            _words.Effect = blur;
            blur.BeginAnimation(BlurEffect.RadiusProperty, new DoubleAnimation(9, 0, TimeSpan.FromMilliseconds(milliseconds))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }
        // A common completion clock also covers delayed line/reference animations.
        var generation = _bibleTextGeneration;
        var completion = new DoubleAnimation(1, 1, TimeSpan.FromMilliseconds(milliseconds)) { FillBehavior = FillBehavior.Stop };
        completion.Completed += (_, _) => { if (generation == _bibleTextGeneration) CancelBibleTextTransition(); };
        _words.BeginAnimation(OpacityProperty, completion);
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

    private void BeginBibleLineReveal(int milliseconds)
    {
        // Use WPF's actual wrapped lines, preserving layout, typeface and alignment.
        var tops = new List<double>();
        var line = _body.ContentStart.GetLineStartPosition(0);
        while (line is not null)
        {
            var rect = line.GetCharacterRect(LogicalDirection.Forward);
            if (!rect.IsEmpty && (tops.Count == 0 || rect.Top > tops[^1])) tops.Add(rect.Top);
            var next = line.GetLineStartPosition(1);
            if (next is null || next.CompareTo(line) <= 0) break;
            line = next;
        }
        if (tops.Count == 0 || _body.ActualWidth < 1 || _body.ActualHeight < 1)
        {
            FadeBibleText(_body, 0, milliseconds);
            return;
        }
        var drawing = new DrawingGroup();
        for (var index = 0; index < tops.Count; index++)
        {
            var top = index == 0 ? 0 : tops[index];
            var bottom = index + 1 < tops.Count ? tops[index + 1] : _body.ActualHeight;
            var brush = new SolidColorBrush(Colors.White) { Opacity = 0 };
            drawing.Children.Add(new GeometryDrawing(brush, null, new RectangleGeometry(new Rect(0, top, _body.ActualWidth, Math.Max(0, bottom - top)))));
            var delay = tops.Count == 1 ? 0 : milliseconds * .55 * index / (tops.Count - 1);
            brush.BeginAnimation(Brush.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(milliseconds * .45))
                { BeginTime = TimeSpan.FromMilliseconds(delay) });
        }
        _body.OpacityMask = new DrawingBrush(drawing) { Stretch = Stretch.Fill };
    }

    private void CancelBibleTextTransition()
    {
        _bibleTextGeneration++;
        _bibleTextAnimating = false;
        CancelBiblePresetTransition();
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
