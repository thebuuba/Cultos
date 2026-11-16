using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Culto.Core;

namespace Culto.Desktop.Projection;

public sealed partial class ProjectionSurface
{
    private BibleVisualStyle _bibleStyle = new();
    private BibleVisualStyle _presentedBibleStyle = new();
    private static (string Path, DateTime Modified, long Length, BitmapImage Image)? _bibleBackground;
    private readonly Border _bibleDim = new() { Background = Brushes.Black, IsHitTestVisible = false, Visibility = Visibility.Collapsed };

    // Each request captures its own value, including when outgoing audio delays presentation.
    public BibleVisualStyle BibleStyle
    {
        get => _mixer?.BibleStyle ?? _bibleStyle with { };
        set { _bibleStyle = (value ?? new BibleVisualStyle()).Normalize(); if (_mixer is not null) _mixer.BibleStyle = _bibleStyle; }
    }

    private Presentation CreatePresentation(Cue? cue, bool blackout, bool hideText, bool dissolve, bool pauseAfter = false)
        => new(cue, blackout, hideText, dissolve, pauseAfter)
        { BibleStyle = _bibleStyle with { }, Transitions = _transitions with { }, AnimateAudio = dissolve };

    private void ResetVisualStyle()
    {
        _bibleDim.Visibility = Visibility.Collapsed;
        _bibleDim.Opacity = 0;
        _image.Source = null;
        _image.Stretch = Stretch.Uniform;
        foreach (var block in new[] { _body, _reference, _version })
        {
            block.FontFamily = new FontFamily("Segoe UI");
            block.FontWeight = FontWeights.Normal;
            block.FontStyle = FontStyles.Normal;
            block.Foreground = Brushes.White;
            block.TextAlignment = TextAlignment.Center;
            block.Effect = null;
        }
    }

    private void ApplyBibleStyle()
    {
        var style = _presentedBibleStyle;
        _root.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(style.BackgroundColor));
        _image.Stretch = Stretch.UniformToFill;
        if (!string.IsNullOrWhiteSpace(style.BackgroundImagePath))
        {
            try
            {
                _image.Source = ReadBibleBackground(style.BackgroundImagePath);
                _image.Visibility = Visibility.Visible;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException
                or ArgumentException or NotSupportedException or FileFormatException)
            {
                // Optional Bible art never replaces the passage with a playback error.
                _image.Source = null;
                _image.Visibility = Visibility.Collapsed;
            }
        }
        _bibleDim.Opacity = style.BackgroundDim;
        _bibleDim.Visibility = Visibility.Visible;
        FontFamily font;
        try { font = new FontFamily(style.FontFamily); }
        catch (ArgumentException) { font = new FontFamily("Segoe UI"); }
        var foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(style.TextColor));
        var alignment = style.Alignment switch { "Left" => TextAlignment.Left, "Right" => TextAlignment.Right, _ => TextAlignment.Center };
        foreach (var block in new[] { _body, _reference, _version })
        {
            block.FontFamily = font;
            block.FontWeight = style.Bold ? FontWeights.Bold : FontWeights.Normal;
            block.FontStyle = style.Italic ? FontStyles.Italic : FontStyles.Normal;
            block.Foreground = foreground;
            block.TextAlignment = alignment;
            block.Effect = style.TextEffect switch
            {
                "Shadow" => new DropShadowEffect { Color = Colors.Black, BlurRadius = 8, ShadowDepth = 4, Opacity = .9 },
                "Glow" => new DropShadowEffect { Color = foreground.Color, BlurRadius = 18, ShadowDepth = 0, Opacity = .85 },
                _ => null
            };
        }
        if (style.ReferenceFontFamily is { } referenceFont)
        {
            try { _reference.FontFamily = new FontFamily(referenceFont); }
            catch (ArgumentException) { _reference.FontFamily = font; }
        }
    }

    private static BitmapImage ReadBibleBackground(string path)
    {
        // Preview and successive output surfaces share one frozen bitmap. Keep
        // only the current background and reload when its file changes.
        var file = new FileInfo(path);
        var modified = file.LastWriteTimeUtc;
        var length = file.Length;
        if (_bibleBackground is { } cached && StringComparer.OrdinalIgnoreCase.Equals(cached.Path, file.FullName)
            && cached.Modified == modified && cached.Length == length) return cached.Image;
        var image = ReadImage(file.FullName);
        _bibleBackground = (file.FullName, modified, length, image);
        return image;
    }
}
