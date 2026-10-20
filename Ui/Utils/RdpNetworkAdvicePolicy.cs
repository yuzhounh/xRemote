using System;

namespace _1RM.Utils;

internal sealed class RdpNetworkAdvicePolicy
{
    private DateTime? _poorSince;
    private bool _offered;

    public void Reset() { _poorSince = null; _offered = false; }

    public void Observe(uint quality, DateTime now)
    {
        // The RDP control reports 1 as its lowest quality level; 0 is unknown.
        if (quality == 1) _poorSince ??= now;
        else _poorSince = null;
    }

    public (uint Width, uint Height)? Recommend(DateTime now, int width, int height)
    {
        if (_offered || _poorSince == null || now - _poorSince.Value < TimeSpan.FromSeconds(20)) return null;
        var pixels = (long)width * height;
        (uint Width, uint Height)? size = pixels > 1600L * 900 ? (1600, 900)
            : pixels > 1280L * 720 ? (1280, 720) : null;
        if (size != null) _offered = true; // At most one recommendation per connection attempt.
        return size;
    }
}
