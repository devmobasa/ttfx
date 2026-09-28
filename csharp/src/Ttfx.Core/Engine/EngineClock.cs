using System.Diagnostics;

namespace Ttfx.Core;

public sealed class EngineClock( bool virtualClock, long frameRate )
{
    private readonly double frameSeconds = 1.0 / (frameRate > 0 ? frameRate : 60);
    private readonly long started = Stopwatch.GetTimestamp( );
    private double elapsed;
    public bool IsVirtual { get; } = virtualClock;
    public double Seconds => IsVirtual ? elapsed : Stopwatch.GetElapsedTime( started ).TotalSeconds;

    public void AdvanceFrame( )
    {
        if ( IsVirtual )
        {
            elapsed += frameSeconds;
        }
    }
}
