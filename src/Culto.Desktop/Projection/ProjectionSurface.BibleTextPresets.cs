using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Culto.Core;

namespace Culto.Desktop.Projection;

public sealed partial class ProjectionSurface
{
    private readonly Canvas _bibleTextOverlay = new() { IsHitTestVisible = false };
    private readonly List<(IAnimatable Target, DependencyProperty Property)> _biblePresetAnimations = [];
    private const double BiblePresetPadding = 32;
    private static readonly Vector[] BibleEntryDirections =
    [new(0, 1), new(0, -1), new(-1, 0), new(1, 0), new(-.707, .707), new(.707, .707), new(-.707, -.707), new(.707, -.707)];

    private void BeginBiblePresetTransition(int milliseconds, string systemTransition)
    {
        var textKind = _presentedBibleStyle.Transition == "System" ? systemTransition : _presentedBibleStyle.Transition;
        var referenceKind = _presentedBibleStyle.ReferenceTransition ?? textKind;
        RenderTargetBitmap? bitmap = null;
        if (textKind.StartsWith("TextPreset:", StringComparison.Ordinal) || referenceKind.StartsWith("TextPreset:", StringComparison.Ordinal))
        {
            var textCanvas = (FrameworkElement)_text.Child;
            // Capture before hiding or animating any block, retaining each one's own style.
            bitmap = new RenderTargetBitmap((int)textCanvas.ActualWidth, (int)textCanvas.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(textCanvas); bitmap.Freeze();
        }
        var blocks = new[] { _body, _reference, _version }
            .Where(block => block.Visibility == Visibility.Visible && block.ActualWidth >= 1 && block.ActualHeight >= 1).ToArray();
        for (var blockIndex = 0; blockIndex < blocks.Length; blockIndex++)
        {
            var block = blocks[blockIndex];
            var kind = ReferenceEquals(block, _reference) ? referenceKind : textKind;
            if (bitmap is null || !kind.StartsWith("TextPreset:", StringComparison.Ordinal) || !BibleTextTransitionCatalog.TryGet(kind, out var preset))
            {
                BeginBibleBlockTransition(block, kind, milliseconds);
                continue;
            }
            var origin = block.TranslatePoint(new Point(), _bibleTextOverlay);
            // Partition the gaps too, so shadows never appear twice in adjacent fragments.
            var top = blockIndex == 0 ? 0 : (origin.Y + blocks[blockIndex - 1].TranslatePoint(new Point(), _bibleTextOverlay).Y
                + blocks[blockIndex - 1].ActualHeight) / 2;
            var bottom = blockIndex + 1 == blocks.Length ? bitmap.PixelHeight
                : (origin.Y + block.ActualHeight + blocks[blockIndex + 1].TranslatePoint(new Point(), _bibleTextOverlay).Y) / 2;
            var canvasBounds = new Rect(0, top, bitmap.PixelWidth, bottom - top);
            BeginBiblePresetBlock(block, bitmap, canvasBounds, preset, milliseconds);
        }
    }

    private void BeginBiblePresetBlock(TextBlock block, BitmapSource bitmap, Rect canvasBounds, BibleTextTransitionPreset preset, int milliseconds)
    {
        var bounds = new Rect(-BiblePresetPadding, -BiblePresetPadding,
            block.ActualWidth + BiblePresetPadding * 2, block.ActualHeight + BiblePresetPadding * 2);
        var fragments = preset.Scope switch
        {
            BibleTextScope.Words => BibleWordBounds(block),
            BibleTextScope.Lines => BibleLineBounds(block),
            _ => new List<Rect> { bounds }
        };
        // Bound the active visuals for large selected passages; never omit text.
        if (fragments.Count > 80) fragments = BibleLineBounds(block);
        if (fragments.Count is 0 or > 80) fragments = [bounds];
        var origin = block.TranslatePoint(new Point(), _bibleTextOverlay);
        block.Opacity = 0;
        for (var index = 0; index < fragments.Count; index++)
        {
            var rect = fragments[index]; rect.Offset(origin.X, origin.Y);
            rect.Intersect(canvasBounds);
            if (rect.IsEmpty || rect.Width < .1 || rect.Height < .1) continue;
            // Shared integer boundaries give each captured pixel exactly one owner.
            var left = Math.Max(0, (int)Math.Round(rect.Left));
            var top = Math.Max(0, (int)Math.Round(rect.Top));
            var width = Math.Min(bitmap.PixelWidth, (int)Math.Round(rect.Right)) - left;
            var height = Math.Min(bitmap.PixelHeight, (int)Math.Round(rect.Bottom)) - top;
            if (width < 1 || height < 1) continue;
            var crop = new CroppedBitmap(bitmap, new Int32Rect(left, top, width, height)); crop.Freeze();
            var image = new Image { Source = crop, Width = width, Height = height, Opacity = 0,
                RenderTransformOrigin = new Point(.5, .5), Tag = block };
            Canvas.SetLeft(image, left); Canvas.SetTop(image, top);
            _bibleTextOverlay.Children.Add(image);
            var delay = fragments.Count == 1 ? 0 : milliseconds * .50 * index / (fragments.Count - 1);
            AnimateBiblePreset(image, OpacityProperty, PresetFade(0, 1, delay, milliseconds - delay));
            ApplyBiblePresetMotion(image, preset, delay, milliseconds - delay, block.FontSize);
        }
    }

    private static List<Rect> BibleLineBounds(TextBlock block)
    {
        var tops = new List<double>();
        var pointer = block.ContentStart.GetLineStartPosition(0);
        while (pointer is not null)
        {
            var rect = pointer.GetCharacterRect(LogicalDirection.Forward);
            if (!rect.IsEmpty && (tops.Count == 0 || rect.Top > tops[^1])) tops.Add(rect.Top);
            var next = pointer.GetLineStartPosition(1);
            if (next is null || next.CompareTo(pointer) <= 0) break;
            pointer = next;
        }
        return tops.Select((top, index) => new Rect(-BiblePresetPadding, index == 0 ? -BiblePresetPadding : top,
            block.ActualWidth + BiblePresetPadding * 2,
            Math.Max(0, (index + 1 < tops.Count ? tops[index + 1] : block.ActualHeight + BiblePresetPadding)
                - (index == 0 ? -BiblePresetPadding : top)))).ToList();
    }

    private static List<Rect> BibleWordBounds(TextBlock block)
    {
        var result = new List<Rect>();
        var pointer = block.ContentStart;
        while (pointer is not null && pointer.CompareTo(block.ContentEnd) < 0)
        {
            if (pointer.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
            {
                var text = pointer.GetTextInRun(LogicalDirection.Forward);
                foreach (Match word in Regex.Matches(text, @"\S+"))
                {
                    var first = pointer.GetPositionAtOffset(word.Index)!.GetCharacterRect(LogicalDirection.Forward);
                    var last = pointer.GetPositionAtOffset(word.Index + word.Length)!.GetCharacterRect(LogicalDirection.Backward);
                    // WPF can wrap an unusually long unbroken word. Lines preserve its full shape.
                    if (Math.Abs(first.Top - last.Top) > 1) return BibleLineBounds(block);
                    if (!first.IsEmpty && !last.IsEmpty)
                        result.Add(new Rect(new Point(Math.Min(first.Left, last.Left), Math.Min(first.Top, last.Top)),
                            new Point(Math.Max(first.Right, last.Right), Math.Max(first.Bottom, last.Bottom))));
                    if (result.Count > 80) return BibleLineBounds(block);
                }
            }
            pointer = pointer.GetNextContextPosition(LogicalDirection.Forward);
        }
        var rows = result.GroupBy(rect => Math.Round(rect.Top, 2)).OrderBy(row => row.Key).ToArray();
        var partitions = new List<Rect>();
        for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            var words = rows[rowIndex].OrderBy(rect => rect.Left).ToArray();
            var top = rowIndex == 0 ? -BiblePresetPadding : rows[rowIndex].Key;
            var bottom = rowIndex + 1 < rows.Length ? rows[rowIndex + 1].Key : block.ActualHeight + BiblePresetPadding;
            for (var wordIndex = 0; wordIndex < words.Length; wordIndex++)
            {
                var left = wordIndex == 0 ? -BiblePresetPadding : (words[wordIndex - 1].Right + words[wordIndex].Left) / 2;
                var right = wordIndex + 1 == words.Length ? block.ActualWidth + BiblePresetPadding
                    : (words[wordIndex].Right + words[wordIndex + 1].Left) / 2;
                partitions.Add(new Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top)));
            }
        }
        return partitions;
    }

    private void ApplyBiblePresetMotion(Image image, BibleTextTransitionPreset preset, double delay, double duration, double fontSize)
    {
        var variant = preset.Variant;
        var direction = BibleEntryDirections[variant];
        var distance = Math.Clamp(fontSize * .6, 18, 44);
        var transforms = new TransformGroup(); image.RenderTransform = transforms;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        void Animate(IAnimatable target, DependencyProperty property, double from, double to, IEasingFunction? easing = null) =>
            AnimateBiblePreset(target, property, PresetFade(from, to, delay, duration, easing ?? ease));
        void Move(IEasingFunction? easing = null)
        {
            var move = new TranslateTransform(direction.X * distance, direction.Y * distance); transforms.Children.Add(move);
            Animate(move, TranslateTransform.XProperty, move.X, 0, easing);
            Animate(move, TranslateTransform.YProperty, move.Y, 0, easing);
        }
        void Scale(double fromX, double fromY)
        {
            var scale = new ScaleTransform(fromX, fromY); transforms.Children.Add(scale);
            Animate(scale, ScaleTransform.ScaleXProperty, fromX, 1); Animate(scale, ScaleTransform.ScaleYProperty, fromY, 1);
        }
        void Wipe()
        {
            var width = image.Width; var height = image.Height;
            var from = new Rect(direction.X > 0 ? width : 0, direction.Y > 0 ? height : 0,
                direction.X == 0 ? width : 0, direction.Y == 0 ? height : 0);
            var clip = new RectangleGeometry(from); image.Clip = clip;
            AnimateBiblePreset(clip, RectangleGeometry.RectProperty, new RectAnimation(from, new Rect(0, 0, width, height), TimeSpan.FromMilliseconds(duration))
                { BeginTime = TimeSpan.FromMilliseconds(delay), EasingFunction = ease });
        }
        switch (preset.Motion)
        {
            case BibleTextMotion.Slide: Move(); break;
            case BibleTextMotion.Bounce: Move(new BounceEase { Bounces = 2, Bounciness = 3, EasingMode = EasingMode.EaseOut }); break;
            case BibleTextMotion.Spring: Move(new ElasticEase { Oscillations = 2, Springiness = 6, EasingMode = EasingMode.EaseOut }); break;
            case BibleTextMotion.Wipe: Wipe(); break;
            case BibleTextMotion.Unroll: Wipe(); Move(); break;
            case BibleTextMotion.Radial:
                Point[] anchors = [new(.5, .5), new(0, .5), new(1, .5), new(.5, 0), new(.5, 1), new(0, 0), new(1, 0), new(1, 1)];
                var anchor = anchors[variant];
                var circle = new EllipseGeometry(new Point(image.Width * anchor.X, image.Height * anchor.Y), 0, 0); image.Clip = circle;
                var radius = Math.Sqrt(image.Width * image.Width + image.Height * image.Height);
                Animate(circle, EllipseGeometry.RadiusXProperty, 0, radius); Animate(circle, EllipseGeometry.RadiusYProperty, 0, radius);
                break;
            case BibleTextMotion.Zoom:
                double[] scales = [.92, .75, 1.08, 1.35, .8, .8, .8, .8];
                image.RenderTransformOrigin = variant switch { 4 => new(0, .5), 5 => new(1, .5), 6 => new(.5, 0), 7 => new(.5, 1), _ => new(.5, .5) };
                Scale(scales[variant], scales[variant]); break;
            case BibleTextMotion.Rotate:
                double[] angles = [-12, -6, 6, 12];
                if (variant >= 4) image.RenderTransformOrigin = new Point(0, .5);
                var rotation = new RotateTransform(angles[variant % 4]); transforms.Children.Add(rotation);
                Animate(rotation, RotateTransform.AngleProperty, rotation.Angle, 0); break;
            case BibleTextMotion.Skew:
                double[] slopes = [-16, -8, 8, 16];
                var skew = new SkewTransform(variant < 4 ? slopes[variant % 4] : 0, variant >= 4 ? slopes[variant % 4] : 0); transforms.Children.Add(skew);
                Animate(skew, SkewTransform.AngleXProperty, skew.AngleX, 0); Animate(skew, SkewTransform.AngleYProperty, skew.AngleY, 0); break;
            case BibleTextMotion.Blur:
                double[] radii = [4, 8, 12, 18];
                var blur = new BlurEffect { Radius = radii[variant % 4], RenderingBias = RenderingBias.Performance }; image.Effect = blur;
                Animate(blur, BlurEffect.RadiusProperty, blur.Radius, 0);
                if (variant >= 4) Scale(.94, .94);
                break;
            case BibleTextMotion.Fold:
                image.RenderTransformOrigin = new Point((direction.X + 1) / 2, (direction.Y + 1) / 2);
                Scale(direction.X == 0 ? 1 : .04, direction.Y == 0 ? 1 : .04);
                break;
            case BibleTextMotion.Wave:
                var wave = new TranslateTransform(direction.X * distance, direction.Y * distance); transforms.Children.Add(wave);
                foreach (var (property, start) in new[] { (TranslateTransform.XProperty, wave.X), (TranslateTransform.YProperty, wave.Y) })
                {
                    var animation = new DoubleAnimationUsingKeyFrames { BeginTime = TimeSpan.FromMilliseconds(delay), Duration = TimeSpan.FromMilliseconds(duration) };
                    foreach (var (progress, value) in new[] { (0d, start), (.36, -start * .4), (.7, start * .14), (1d, 0d) })
                        animation.KeyFrames.Add(new SplineDoubleKeyFrame(value, KeyTime.FromPercent(progress), new KeySpline(.2, .8, .2, 1)));
                    AnimateBiblePreset(wave, property, animation);
                }
                break;
        }
    }

    private static DoubleAnimation PresetFade(double from, double to, double delay, double milliseconds, IEasingFunction? easing = null) =>
        new(from, to, TimeSpan.FromMilliseconds(milliseconds)) { BeginTime = TimeSpan.FromMilliseconds(delay), EasingFunction = easing };

    private void AnimateBiblePreset(IAnimatable target, DependencyProperty property, AnimationTimeline animation)
    {
        _biblePresetAnimations.Add((target, property)); target.BeginAnimation(property, animation);
    }

    private void CancelBiblePresetTransition()
    {
        foreach (var (target, property) in _biblePresetAnimations) target.BeginAnimation(property, null);
        _biblePresetAnimations.Clear(); _bibleTextOverlay.Children.Clear();
    }
}
