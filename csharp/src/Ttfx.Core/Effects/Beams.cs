namespace Ttfx.Core;

public sealed record BeamsConfig
{
    public string[] RowSymbols { get; init; } = ["▂", "▁", "_"];
    public string[] ColumnSymbols { get; init; } = ["▌", "▍", "▎", "▏"];
    public long Delay { get; init; } = 6;
    public (long Low, long High) RowSpeed { get; init; } = (15, 60);
    public (long Low, long High) ColumnSpeed { get; init; } = (9, 15);
    public Color[] BeamColors { get; init; } = [Color.FromHex( "ffffff" ), Color.FromHex( "00D1FF" ), Color.FromHex( "8A008A" )];
    public long[] BeamSteps { get; init; } = [2, 6];
    public long BeamFrames { get; init; } = 2;
    public long Frames { get; init; } = 4;
    public long WipeSpeed { get; init; } = 3;
    public Color[] Stops { get; init; } = [Color.FromHex( "8A008A" ), Color.FromHex( "00D1FF" ), Color.FromHex( "ffffff" )];
    public long[] Steps { get; init; } = [12];
    public GradientDirection GradientDirection { get; init; } = GradientDirection.Vertical;
}

public sealed class Beams : EffectBase
{
    private sealed class Group( int[] characters, bool row, double speed )
    {
        internal readonly Queue<int> Characters = new( characters );
        internal readonly string Scene = row ? "row" : "column";
        internal readonly double Speed = speed;
        internal double Counter;
    }

    private readonly BeamsConfig config;
    private readonly OrderedQueue<Group> pending = [];
    private readonly List<Group> active = [];
    private readonly Queue<int[]> wipe;
    private long delay;
    private int phase;

    public Beams( Terminal terminal, BeamsConfig config, EngineRng rng ) : base( terminal, rng )
    {
        this.config = config;
        wipe = new( terminal.Groups( CharacterGroup.DiagonalTopLeftToBottomRight ) );
        var final = new Gradient( config.Stops, config.Steps );
        var beam = new Gradient( config.BeamColors, config.BeamSteps );
        void AddGroups( bool row, CharacterGroup direction )
        {
            foreach ( int[] ids in terminal.Groups( direction, includeFill: true ) )
            {
                var range = row ? config.RowSpeed : config.ColumnSpeed;
                double speed = rng.RandInt( range.Low, range.High ) * .1;
                int[] ordered = ids.OrderBy( id => row ? Home[id].Column : Home[id].Row ).ToArray( );
                if ( rng.ChoiceIndex( 2 ) == 0 )
                {
                    Array.Reverse( ordered );
                }

                pending.Add( new( ordered, row, speed ) );
            }
        }

        AddGroups( true, CharacterGroup.RowTopToBottom );
        AddGroups( false, CharacterGroup.ColumnLeftToRight );
        bool dynamic = terminal.Config.ExistingColors == ExistingColors.Dynamic;
        foreach ( int id in Enumerable.Range( 0, Home.Length ) )
        {
            InputCell cell = terminal.Characters[id];
            Color? fg = id >= terminal.InputCount ? Color.FromHex( "000000" ) : dynamic ? cell.Foreground : FinalColor( id, final, config.GradientDirection );
            Color? bg = id < terminal.InputCount && dynamic ? cell.Background : null;
            Gradient? fadeFg = fg is { } f ? MakeGradient( [f, Color.AdjustBrightness( f, .3 )], [10] ) : null;
            Gradient? fadeBg = bg is { } b ? MakeGradient( [b, Color.AdjustBrightness( b, .3 )], [10] ) : null;
            Gradient? brightFg = fg is { } bf ? MakeGradient( [Color.AdjustBrightness( bf, .3 ), bf], [10] ) : null;
            Gradient? brightBg = bg is { } bb ? MakeGradient( [Color.AdjustBrightness( bb, .3 ), bb], [10] ) : null;
            SceneDefinition fade = fadeFg is not null || fadeBg is not null ? GradientScene( id, [cell.Symbol], 2, fadeFg, fadeBg ) : new( [new( VisualBytes( id, cell.Symbol ), 2 )] );
            foreach ( bool row in new[] { true, false } )
            {
                SceneDefinition start = GradientScene( id, row ? config.RowSymbols : config.ColumnSymbols, config.BeamFrames, beam );
                Engine.AddScene( id, row ? "row" : "column", new( [.. start.Frames, .. fade.Frames] ) );
            }

            Engine.AddScene( id, "brighten", brightFg is not null || brightBg is not null ? GradientScene( id, [cell.Symbol], config.Frames, brightFg, brightBg ) : new( [new( VisualBytes( id, cell.Symbol ), config.Frames )] ) );
        }

        rng.Shuffle( pending.AsSpan( ) );
    }

    public override bool NextFrame( )
    {
        if ( phase == 2 && !Engine.HasActive )
        {
            return false;
        }

        if ( phase == 0 )
        {
            if ( delay == 0 )
            {
                if ( pending.Count > 0 )
                {
                    long count = Rng.RandInt( 1, 5 );
                    for ( long i = 0; i < count && pending.Count > 0; i++ )
                    {
                        active.Add( pending[0] );
                        pending.RemoveAt( 0 );
                    }
                }

                delay = config.Delay;
            }
            else
            {
                delay--;
            }

            foreach ( Group group in active )
            {
                group.Counter += group.Speed;
                long count = ( long ) group.Counter;
                if ( count > 1 )
                {
                    for ( long i = 0; i < count && group.Characters.TryDequeue( out int id ); i++ )
                    {
                        group.Counter -= 1;
                        if ( Engine.ActiveSceneName( id ) is { } name )
                        {
                            Engine.ResetScene( id, name );
                        }
                        else
                        {
                            Terminal.Visible[id] = true;
                            Engine.Activate( id );
                        }

                        Engine.ActivateScene( id, group.Scene );
                    }
                }
            }

            active.RemoveAll( group => group.Characters.Count == 0 );
            if ( pending.Count == 0 && active.Count == 0 && !Engine.HasActive )
            {
                phase = 1;
            }
        }
        else if ( phase == 1 )
        {
            if ( wipe.Count > 0 )
            {
                for ( long i = 0; i < config.WipeSpeed && wipe.TryDequeue( out int[]? group ); i++ )
                {
                    foreach ( int id in group )
                    {
                        Engine.ActivateScene( id, "brighten" );
                        Terminal.Visible[id] = true;
                        Engine.Activate( id );
                    }
                }
            }
            else
            {
                phase = 2;
            }
        }

        Engine.Update( );
        return true;
    }
}
