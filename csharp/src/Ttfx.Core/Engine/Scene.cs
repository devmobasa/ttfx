namespace Ttfx.Core;

public enum SyncMetric
{
    Step, Distance
}

public readonly record struct SceneFrame( byte[] Visual, long Duration );

public sealed class SceneDefinition
{
    public SceneFrame[] Frames { get; }
    public bool Loop { get; }
    public Easing? Ease { get; set; }
    public SyncMetric? Sync { get; }
    public long Duration { get; }
    internal long[] Ends { get; }

    public SceneDefinition( IEnumerable<SceneFrame> frames, bool loop = false, Easing? ease = null, SyncMetric? sync = null )
    {
        Frames = frames.ToArray( );
        Loop = loop;
        Ease = ease;
        Sync = sync;
        Ends = new long[Frames.Length];
        long duration = 0;
        for ( int i = 0; i < Frames.Length; i++ )
        {
            if ( Frames[i].Duration < 1 )
            {
                throw new EngineException( $"Frame duration must be at least 1. Received: {Frames[i].Duration}" );
            }

            duration = checked(duration + Frames[i].Duration);
            Ends[i] = duration;
        }

        Duration = duration;
    }

    private SceneDefinition( SceneDefinition source, Easing? ease )
    {
        Frames = source.Frames;
        Ends = source.Ends;
        Loop = source.Loop;
        Ease = ease;
        Sync = source.Sync;
        Duration = source.Duration;
    }

    internal SceneDefinition CopyForPlayback( Easing? ease ) => new( this, ease );

    internal int FrameAt( long tick )
    {
        int index = Array.BinarySearch( Ends, tick + 1 );

        return index >= 0 ? index : ~index;
    }
}

internal sealed class ScenePlayback( int character, string name, SceneDefinition definition )
{
    internal readonly int Character = character;
    internal readonly string Name = name;
    internal readonly SceneDefinition Definition = definition;
    internal int NameId, Caller;
    internal int Frame;
    internal long Elapsed;
    internal long EasedStep;

    internal void Reset( )
    {
        Frame = 0;
        Elapsed = EasedStep = 0;
    }
}
