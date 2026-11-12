using System;
using System.Collections.Generic;

namespace Culto.Desktop.Services;

internal sealed record PowerPointWindowSnapshot(nint Handle, nint Owner, uint ProcessId, uint ThreadId,
    string ClassName, string Title, bool IsVisible);

// Pure attribution policy: being in the PowerPoint process is never sufficient.
internal static class PowerPointToolbarPolicy
{
    public static bool CanHide(nint showHandle, uint showProcessId, nint candidateHandle,
        IReadOnlyDictionary<nint, PowerPointWindowSnapshot> windows)
    {
        if (showHandle == 0 || showProcessId == 0 || candidateHandle == 0 || candidateHandle == showHandle
            || !windows.TryGetValue(showHandle, out var show) || show.Handle != showHandle || show.ProcessId != showProcessId
            || !windows.TryGetValue(candidateHandle, out var candidate) || !candidate.IsVisible
            || !string.Equals(candidate.ClassName, "MsoCommandBar", StringComparison.Ordinal)) return false;

        var visited = new HashSet<nint>();
        for (var current = candidateHandle; current != 0 && visited.Add(current);)
        {
            if (!windows.TryGetValue(current, out var window) || window.Handle != current
                || window.ProcessId != showProcessId) return false;
            if (current == showHandle) return true;
            current = window.Owner;
        }
        return false;
    }
}
