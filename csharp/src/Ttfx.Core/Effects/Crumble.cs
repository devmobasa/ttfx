namespace Ttfx.Core;

public sealed record CrumbleConfig
{

    public Color[] Stops { get; init; } = [Color.FromHex( "5CE1FF" ), Color.FromHex( "FF8C00" )];
    public long[] Steps { get; init; } = [12];
    public GradientDirection GradientDirection { get; init; } = GradientDirection.Diagonal;
}

public sealed class Crumble : EffectBase
{
    private readonly OrderedQueue<int> pending, vacuum;
    private long delay = 12, maximumDelay = 12, minimumDelay = 9, groupSize = 1;
    private int stage;
    private bool reset;

    public Crumble( Terminal terminal, CrumbleConfig config, EngineRng rng ) : base( terminal, rng )
    {
        var final = new Gradient( config.Stops, config.Steps );
        Color gray = Color.FromHex( "808080" ), white = Color.FromHex( "ffffff" );
        bool dynamic = terminal.Config.ExistingColors == ExistingColors.Dynamic;
        foreach ( int id in InputOrder )
        {
            InputCell cell = terminal.Characters[id];
            Color? fg = dynamic ? cell.Foreground : FinalColor( id, final, config.GradientDirection ), bg = dynamic ? cell.Background : null;
            Color? startFg = fg ?? (bg is null ? gray : null);
            Color? weakFg = startFg is { } f ? Color.AdjustBrightness( f, .65 ) : null, weakBg = bg is { } b ? Color.AdjustBrightness( b, .65 ) : null;
            Color? dustFg = startFg is { } df ? Color.AdjustBrightness( df, .55 ) : null, dustBg = bg is { } db ? Color.AdjustBrightness( db, .55 ) : null;
            Engine.AddScene( id, "initial", new( [new( VisualBytes( id, cell.Symbol, weakFg, weakBg ), 1 )] ) );
            Engine.ActivateScene( id, "initial" );
            terminal.Visible[id] = true;
            Gradient? weakeningFg = weakFg is { } wf && dustFg is { } dff ? MakeGradient( [wf, dff], [9] ) : null;
            Gradient? weakeningBg = weakBg is { } wb && dustBg is { } dbb ? MakeGradient( [wb, dbb], [9] ) : null;
            Engine.AddScene( id, "weaken", GradientScene( id, [cell.Symbol], 4, weakeningFg, weakeningBg ) );
            Engine.AddPath( id, "fall", new( [new Waypoint( "0", new( Home[id].Column, 1 ) )], .65, new( EaseKind.OutBounce ) ) );
            Engine.AddPath( id, "top", new( [new Waypoint( "0", new( Home[id].Column, terminal.Height ), [Center] )], 1.0, new( EaseKind.OutQuint ) ) );
            Engine.AddPath( id, "input", new( [new Waypoint( "0", Home[id] )], 1.0 ) );
            Gradient? flashFg = startFg is { } sf ? MakeGradient( [sf, white], [6] ) : null, flashBg = bg is { } sb ? MakeGradient( [sb, white], [6] ) : null;
            Engine.AddScene( id, "flash", GradientScene( id, [cell.Symbol], 4, flashFg, flashBg ) );
            Engine.AddScene( id, "strong", FadeToFinal( id, final, config.GradientDirection, white, 9, 4 ) );
            string[] symbols = ["*", ".", ","];
            var dust = new SceneFrame[5];
            for ( int i = 0; i < 5; i++ )
            {
                dust[i] = new( VisualBytes( id, symbols[rng.ChoiceIndex( 3 )], dustFg, dustBg ), 1 );
            }

            Engine.AddScene( id, "dust", new( dust, sync: SyncMetric.Distance ) );
            Engine.Register( id, EngineEvent.SceneComplete, new( CallerKind.Scene, "weaken" ), static ( engine, character ) => engine.ActivatePath( character, "fall" ) );
            Engine.Register( id, EngineEvent.SceneComplete, new( CallerKind.Scene, "weaken" ), static ( engine, character ) => engine.SetLayer( character, 1 ) );
            Engine.Register( id, EngineEvent.SceneComplete, new( CallerKind.Scene, "weaken" ), static ( engine, character ) => engine.ActivateScene( character, "dust" ) );
            Engine.Register( id, EngineEvent.PathComplete, new( CallerKind.Path, "input" ), static ( engine, character ) => engine.ActivateScene( character, "flash" ) );
            Engine.Register( id, EngineEvent.SceneComplete, new( CallerKind.Scene, "flash" ), static ( engine, character ) => engine.ActivateScene( character, "strong" ) );
        }

        pending = [.. InputOrder];
        rng.Shuffle( pending.AsSpan( ) );
        vacuum = [.. Enumerable.Range( 0, terminal.InputCount )];
        rng.Shuffle( vacuum.AsSpan( ) );
    }

    public override bool NextFrame( )
    {
        if ( stage == 3 )
        {
            return false;
        }

        if ( stage == 0 )
        {
            if ( pending.Count > 0 )
            {
                if ( delay == 0 )
                {
                    long count = Rng.RandInt( 1, groupSize );
                    for ( long i = 0; i < count && pending.Count > 0; i++ )
                    {
                        int id = pending[0];
                        pending.RemoveAt( 0 );
                        Engine.ActivateScene( id, "weaken" );
                        Engine.Activate( id );
                    }

                    delay = Rng.RandInt( minimumDelay, maximumDelay );
                    if ( Rng.RandInt( 1, 10 ) > 4 )
                    {
                        minimumDelay = Math.Max( 0, minimumDelay - 1 );
                        maximumDelay = Math.Max( 0, maximumDelay - 1 );
                        groupSize++;
                    }
                }
                else
                {
                    delay--;
                }
            }
            if ( pending.Count == 0 && !Engine.HasActive )
            {
                stage = 1;
            }
        }
        else if ( stage == 1 )
        {
            long count = vacuum.Count > 0 ? Rng.RandInt( 3, 10 ) : 0;
            for ( long i = 0; i < count && vacuum.Count > 0; i++ )
            {
                int id = vacuum[0];
                vacuum.RemoveAt( 0 );
                Engine.ActivatePath( id, "top" );
                Engine.Activate( id );
            }

            if ( !Engine.HasActive )
            {
                stage = 2;
            }
        }
        else if ( stage == 2 )
        {
            if ( !reset )
            {
                foreach ( int id in InputOrder )
                {
                    Engine.ActivatePath( id, "input" );
                    Engine.Activate( id );
                }

                reset = true;
            }

            if ( !Engine.HasActive )
            {
                stage = 3;
            }
        }

        Engine.Update( );
        return true;
    }
}
