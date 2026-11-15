using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Culto.Core;
using LibVLCSharp.Shared;

namespace Culto.Desktop.Projection;

// Owns the one live decoder/browser for each stable source identity.
public sealed class LayerSourcePool : IDisposable
{
    public sealed class Entry
    {
        public required string SourceId { get; init; }
        public Cue Cue { get; internal set; } = new();
        public required ProjectionSurface Surface { get; init; }
        public int Width { get; internal set; }
        public int Height { get; internal set; }
        internal string Signature = "";
        internal readonly Dictionary<object, (int Volume, bool Paused, bool Loop, double Gain)> Owners = new();
    }
    private readonly Grid _depot = new() { IsHitTestVisible = false, ClipToBounds = true };
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly Func<LibVLC>? _sharedVlc;
    private readonly List<Task> _retirements = new();
    private bool _disposed;
    public LayerSourcePool(Func<LibVLC>? sharedVlc = null) => _sharedVlc = sharedVlc;
    public FrameworkElement Depot => _depot;
    public IEnumerable<Entry> Entries => _entries.Values;
    public event Action<string>? PlaybackError;

    public Entry Acquire(object owner, string sourceId, Cue cue)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (cue.Kind is CueKind.Scene or CueKind.PowerPoint)
            throw new ArgumentException("Las fuentes de una escena no pueden ser escenas ni PowerPoint.");
        var signatureCue = cue.Copy();
        signatureCue.Id = Guid.Empty;
        var signature = JsonSerializer.Serialize(signatureCue);
        if (!_entries.TryGetValue(sourceId, out var entry))
        {
            var surface = new ProjectionSurface(sharedVlc: _sharedVlc, transparentBackground: true) { Volume = 0 };
            surface.PlaybackError += ForwardError;
            entry = new Entry { SourceId = sourceId, Cue = cue.Copy(), Surface = surface };
            _entries.Add(sourceId, entry);
            _depot.Children.Add(surface);
        }
        entry.Owners.TryAdd(owner, (0, true, false, 1));
        if (entry.Signature != signature)
        {
            entry.Signature = signature;
            entry.Cue = cue.Copy();
            entry.Width = cue.Browser?.Width ?? 1280;
            entry.Height = cue.Browser?.Height ?? 720;
            entry.Surface.Width = entry.Width;
            entry.Surface.Height = entry.Height;
            // Register a silent owner before starting any native audio.
            entry.Surface.ShowCue(entry.Cue);
            ApplyEntry(entry);
        }
        return entry;
    }
    private void ForwardError(string message) => PlaybackError?.Invoke(message);
    public void ApplyPlayback(object owner, string sourceId, int volume, bool paused, bool loop, double gain = 1)
    {
        if (!_entries.TryGetValue(sourceId, out var entry) || !entry.Owners.ContainsKey(owner)) return;
        entry.Owners[owner] = (Math.Clamp(volume, 0, 100), paused, loop, Math.Clamp(gain, 0, 1));
        ApplyEntry(entry);
    }
    private static void ApplyEntry(Entry entry)
    {
        // A departing scene must never pause or attenuate an incoming shared source.
        var owner = entry.Owners.Values.Where(x => !x.Paused)
            .OrderByDescending(x => x.Gain * (entry.Cue.Kind is CueKind.Video or CueKind.Audio ? Math.Pow(x.Volume / 100d, 3) : x.Volume / 100d)).FirstOrDefault();
        entry.Surface.SetMixVolume(owner.Volume, owner.Gain);
        entry.Surface.Loop = entry.Owners.Values.Any(x => x.Loop);
        var paused = entry.Owners.Values.All(x => x.Paused);
        if (entry.Surface.IsPaused != paused) entry.Surface.SetPaused(paused);
    }
    public void Release(object owner, string sourceId)
    {
        if (!_entries.TryGetValue(sourceId, out var entry)) return;
        entry.Owners.Remove(owner);
        if (entry.Owners.Count > 0) { ApplyEntry(entry); return; }
        _entries.Remove(sourceId);
        _depot.Children.Remove(entry.Surface);
        entry.Surface.PlaybackError -= ForwardError;
        _retirements.RemoveAll(task => task.IsCompletedSuccessfully);
        _retirements.Add(entry.Surface.RetireAsync());
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var entry in _entries.Values)
        {
            entry.Surface.PlaybackError -= ForwardError;
            entry.Surface.Dispose();
        }
        _entries.Clear();
        _depot.Children.Clear();
        Task.WhenAll(_retirements).GetAwaiter().GetResult();
        _retirements.Clear();
    }
}
