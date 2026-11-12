using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace Culto.Desktop.Services;

// Local projection already hears the browser through Windows. Attenuate those
// sessions in place: recording and playing them again would produce an echo.
// All COM/session work lives on one MTA thread, never on the video/UI timer.
internal sealed class CapturedApplicationAudio : IDisposable
{
    private static readonly Lazy<CapturedApplicationAudio> SharedInstance = new(() => new());
    public static CapturedApplicationAudio Shared => SharedInstance.Value;
    public static void ShutdownShared() { if (SharedInstance.IsValueCreated) SharedInstance.Value.Dispose(); }
    public static void RestoreShared() { if (SharedInstance.IsValueCreated) SharedInstance.Value.RestoreAll(); }

    private readonly ConcurrentQueue<Action> _commands = new();
    private readonly AutoResetEvent _wake = new(false);
    private readonly Thread _thread;
    private readonly Dictionary<ProcessIdentity, Group> _groups = [];
    private readonly Dictionary<string, Endpoint> _endpoints = [];
    private readonly Dictionary<string, Session> _sessions = [];
    private int _disposed, _discoverRequested = 1;
    private volatile bool _stopping;
    private volatile string? _failure;

    public CapturedApplicationAudio()
    {
        _thread = new Thread(Run) { Name = "Culto captured application audio", IsBackground = true };
        _thread.SetApartmentState(ApartmentState.MTA);
        _thread.Start();
    }

    public Lease Acquire(int processId)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (_failure is { } failure) throw new InvalidOperationException(failure);
        if (processId <= 0 || processId == Environment.ProcessId)
            throw new ArgumentException("Selecciona el audio de otra aplicación.", nameof(processId));
        var identity = ProcessIdentity.Read(processId)
            ?? throw new InvalidOperationException("La aplicación capturada ya se cerró.");
        var lease = new Lease(this, identity);
        _commands.Enqueue(() =>
        {
            if (!_groups.TryGetValue(identity, out var group))
                _groups.Add(identity, group = new Group(identity));
            group.Leases.Add(lease);
            lease.Group = group;
            Interlocked.Exchange(ref _discoverRequested, 1);
        });
        _wake.Set();
        return lease;
    }

    // Explicitly give the browser its original audio back. Leaving a cue alone
    // does not call this: an off-screen YouTube tab must not become audible again.
    public void RestoreAll()
    {
        if (Volatile.Read(ref _disposed) != 0) return;
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _commands.Enqueue(() => { RestoreSessions(); done.TrySetResult(); });
        _wake.Set();
        if (!done.Task.Wait(TimeSpan.FromSeconds(5)))
            throw new TimeoutException("No se pudo restaurar a tiempo el audio del navegador.");
    }

    public sealed class Lease : IDisposable
    {
        private readonly CapturedApplicationAudio _owner;
        private double _gain;
        private int _disposed;
        internal readonly ProcessIdentity Identity;
        internal Group? Group;
        internal Lease(CapturedApplicationAudio owner, ProcessIdentity identity) { _owner = owner; Identity = identity; }
        public bool HasSessions => Volatile.Read(ref _disposed) == 0 && Volatile.Read(ref Group)?.SessionCount > 0;
        public string? LastError => Volatile.Read(ref Group)?.Error ?? _owner._failure;
        internal double Gain => Volatile.Read(ref _disposed) == 0 ? Volatile.Read(ref _gain) : 0;
        public void SetGain(double gain)
        {
            if (!double.IsFinite(gain)) throw new ArgumentOutOfRangeException(nameof(gain));
            if (Volatile.Read(ref _disposed) != 0 || Volatile.Read(ref _owner._disposed) != 0) return;
            Volatile.Write(ref _gain, Math.Clamp(gain, 0, 1));
            _owner._wake.Set();
        }
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0 || Volatile.Read(ref _owner._disposed) != 0) return;
            _owner._commands.Enqueue(() => Group?.Leases.Remove(this));
            _owner._wake.Set();
        }
    }

    internal sealed class Group(ProcessIdentity identity)
    {
        public readonly ProcessIdentity Identity = identity;
        public readonly List<Lease> Leases = [];
        public volatile int SessionCount;
        public volatile string? Error;
        public double Gain => Math.Clamp(Leases.Sum(lease => lease.Gain), 0, 1);
    }
    private sealed class Session(AudioSessionControl control, ProcessIdentity process, float volume, bool mute, List<Group> owners)
    {
        public readonly AudioSessionControl Control = control;
        public readonly ProcessIdentity Process = process;
        public readonly float OriginalVolume = volume;
        public readonly bool OriginalMute = mute;
        public List<Group> Owners = owners;
    }
    private sealed class Endpoint(MMDevice device, AudioSessionManager.SessionCreatedDelegate created) : IDisposable
    {
        public readonly MMDevice Device = device;
        public readonly AudioSessionManager Manager = device.AudioSessionManager;
        public void Dispose()
        {
            Manager.OnSessionCreated -= created;
            try { Manager.Dispose(); } catch (COMException) { }
            Device.Dispose();
        }
    }

    private void Run()
    {
        var clock = Stopwatch.StartNew();
        long nextDiscovery = 0;
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            while (!_stopping)
            {
                while (_commands.TryDequeue(out var command)) command();
                if (_groups.Count != 0 && (clock.ElapsedMilliseconds >= nextDiscovery || Interlocked.Exchange(ref _discoverRequested, 0) != 0))
                {
                    Discover(enumerator);
                    nextDiscovery = clock.ElapsedMilliseconds + 500;
                }
                ApplyGains();
                _wake.WaitOne(15);
            }
        }
        catch (Exception error)
        {
            _failure = "No se pudo controlar el audio: " + error.Message;
            foreach (var group in _groups.Values) group.Error = _failure;
            Debug.WriteLine(error);
        }
        finally
        {
            RestoreSessions();
            foreach (var endpoint in _endpoints.Values) endpoint.Dispose();
            _endpoints.Clear();
        }
    }

    private void Discover(MMDeviceEnumerator enumerator)
    {
        foreach (var group in _groups.Values.ToArray())
        {
            if (ProcessIdentity.Read(group.Identity.Id) == group.Identity) continue;
            group.Error = "La aplicación capturada se cerró.";
            group.SessionCount = 0;
            _groups.Remove(group.Identity);
        }
        foreach (var key in _sessions.Keys.ToArray())
        {
            var session = _sessions[key];
            session.Owners.RemoveAll(owner => !_groups.ContainsKey(owner.Identity));
            bool expired;
            try { expired = session.Control.State == AudioSessionState.AudioSessionStateExpired; }
            catch (COMException) { expired = true; }
            if (!expired && session.Owners.Count != 0 && ProcessIdentity.Read(session.Process.Id) == session.Process) continue;
            Restore(session); _sessions.Remove(key);
        }
        if (_groups.Count == 0) return;

        var parents = ReadParents();
        var liveEndpoints = new HashSet<string>();
        try
        {
            foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                var id = device.ID;
                liveEndpoints.Add(id);
                if (!_endpoints.TryGetValue(id, out var endpoint))
                {
                    AudioSessionManager.SessionCreatedDelegate onCreated = (_, _) =>
                    {
                        Interlocked.Exchange(ref _discoverRequested, 1);
                        _wake.Set();
                    };
                    endpoint = new Endpoint(device, onCreated);
                    endpoint.Manager.OnSessionCreated += onCreated;
                    _endpoints.Add(id, endpoint);
                }
                else device.Dispose();
                try
                {
                    endpoint.Manager.RefreshSessions();
                    var controls = endpoint.Manager.Sessions;
                    for (int index = 0; index < controls.Count; index++)
                    {
                        AudioSessionControl? control = controls[index];
                        try
                        {
                            int pid = checked((int)control.GetProcessID);
                            if (pid == Environment.ProcessId || pid <= 0 || control.IsSystemSoundsSession) continue;
                            var owners = _groups.Values.Where(group => IsDescendant(pid, group.Identity, parents)).ToList();
                            if (owners.Count == 0) continue;
                            var key = id + "|" + control.GetSessionInstanceIdentifier;
                            if (_sessions.TryGetValue(key, out var existing)) { existing.Owners = owners; continue; }
                            var identity = ProcessIdentity.Read(pid);
                            if (identity is null) continue;
                            var volume = control.SimpleAudioVolume;
                            _sessions.Add(key, new Session(control, identity.Value, volume.Volume, volume.Mute, owners));
                            control = null; // Retain this control until restored or expired.
                        }
                        catch (Exception error) when (error is COMException or InvalidOperationException or System.ComponentModel.Win32Exception)
                        { Debug.WriteLine(error); }
                        finally { control?.Dispose(); }
                    }
                }
                catch (COMException error) { foreach (var group in _groups.Values) group.Error = error.Message; }
            }
            foreach (var id in _endpoints.Keys.Except(liveEndpoints).ToArray())
            {
                _endpoints[id].Dispose(); _endpoints.Remove(id);
            }
            foreach (var group in _groups.Values)
                group.SessionCount = _sessions.Values.Count(session => session.Owners.Contains(group));
        }
        catch (COMException error) { foreach (var group in _groups.Values) group.Error = error.Message; }
    }

    private void ApplyGains()
    {
        foreach (var session in _sessions.Values)
        {
            try
            {
                // The same browser session can belong to both sides of a visual
                // crossfade. Combine gains once, never let one side mute the other.
                var gain = Math.Clamp(session.Owners.Sum(owner => owner.Gain), 0, 1);
                var volume = session.Control.SimpleAudioVolume;
                float target = session.OriginalVolume * (float)gain;
                bool muted = session.OriginalMute || gain <= 0;
                if (Math.Abs(volume.Volume - target) > .00001f) volume.Volume = target;
                if (volume.Mute != muted) volume.Mute = muted;
            }
            catch (COMException error) { foreach (var group in session.Owners) group.Error = error.Message; }
        }
    }

    private void RestoreSessions()
    {
        foreach (var session in _sessions.Values) Restore(session);
        _sessions.Clear();
        foreach (var group in _groups.Values) { group.SessionCount = 0; group.Leases.Clear(); }
        _groups.Clear();
    }
    private static void Restore(Session session)
    {
        try
        {
            session.Control.SimpleAudioVolume.Volume = session.OriginalVolume;
            session.Control.SimpleAudioVolume.Mute = session.OriginalMute;
        }
        catch (COMException error) { Debug.WriteLine(error); }
        finally { session.Control.Dispose(); }
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _stopping = true; _wake.Set();
        // No UI dispatcher or browser cooperation is needed to restore sessions.
        if (Thread.CurrentThread != _thread && !_thread.Join(TimeSpan.FromSeconds(5)))
            Debug.WriteLine("Timed out restoring captured application audio.");
    }

    internal readonly record struct ProcessIdentity(int Id, long Started)
    {
        public static ProcessIdentity? Read(int id)
        {
            try { using var process = Process.GetProcessById(id); return new(id, process.StartTime.ToUniversalTime().Ticks); }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            { return null; }
        }
    }
    private static bool IsDescendant(int pid, ProcessIdentity root, Dictionary<int, int> parents)
    {
        var seen = new HashSet<int>();
        var child = ProcessIdentity.Read(pid);
        while (child is { } current && seen.Add(current.Id))
        {
            if (current == root) return true;
            if (current.Started < root.Started || !parents.TryGetValue(current.Id, out int parentId)) return false;
            var parent = ProcessIdentity.Read(parentId);
            // A newer process reusing a dead parent's PID is not an ancestor.
            if (parent is null || parent.Value.Started > current.Started) return false;
            child = parent;
        }
        return false;
    }
    private static Dictionary<int, int> ReadParents()
    {
        var result = new Dictionary<int, int>();
        var snapshot = CreateToolhelp32Snapshot(2, 0);
        if (snapshot == new IntPtr(-1)) return result;
        try
        {
            var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf<ProcessEntry>() };
            if (Process32First(snapshot, ref entry))
                do { result[(int)entry.ProcessId] = (int)entry.ParentProcessId; } while (Process32Next(snapshot, ref entry));
        }
        finally { CloseHandle(snapshot); }
        return result;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry
    {
        public uint Size, Usage, ProcessId;
        public UIntPtr Heap;
        public uint ModuleId, Threads, ParentProcessId;
        public int Priority;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Executable;
    }
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Process32First(IntPtr snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr handle);
}
