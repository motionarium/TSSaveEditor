using System;

namespace Ets2SaveEditor.Core;

/// <summary>Progress for long scans (map sectors / mod folder).</summary>
public sealed class ScanProgressInfo
{
    public string Message { get; init; } = "";
    /// <summary>1-based completed units, or 0 if unknown.</summary>
    public int Current { get; init; }
    /// <summary>Total units; 0 means indeterminate.</summary>
    public int Total { get; init; }

    public double Fraction => Total > 0 ? Math.Clamp(Current / (double)Total, 0, 1) : 0;
}
