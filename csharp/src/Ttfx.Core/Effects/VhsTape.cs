namespace Ttfx.Core;

public sealed record VhsTapeConfig
{
    public Color[] LineColors { get; init; } = [Color.FromHex( "ffffff" ), Color.FromHex( "ff0000" ), Color.FromHex( "00ff00" ), Color.FromHex( "0000ff" ), Color.FromHex( "ffffff" )];
    public Color[] WaveColors { get; init; } = [Color.FromHex( "ffffff" ), Color.FromHex( "ff0000" ), Color.FromHex( "00ff00" ), Color.FromHex( "0000ff" ), Color.FromHex( "ffffff" )];
    public Color[] NoiseColors { get; init; } = [Color.FromHex( "1e1e1f" ), Color.FromHex( "3c3b3d" ), Color.FromHex( "6d6c70" ), Color.FromHex( "a2a1a6" ), Color.FromHex( "cbc9cf" ), Color.FromHex( "ffffff" )];
    public double LineChance { get; init; } = .05;
    public double NoiseChance { get; init; } = .004;
    public long GlitchTime { get; init; } = 600;
    public Color[] Stops { get; init; } = [Color.FromHex( "ab48ff" ), Color.FromHex( "e7b2b2" ), Color.FromHex( "fffebd" )];
    public long[] Steps { get; init; } = [12];
    public GradientDirection GradientDirection { get; init; } = GradientDirection.Vertical;
}

public sealed class VhsTape : EffectBase
{
    private readonly VhsTapeConfig config;
    private readonly List<int[]> lines;
    private List<int> wave = [];
    private readonly List<int> glitch = [];
    private readonly Stack<int> redraw;
    private long? waveTop;
    private long elapsed;
    private int phase;
    private bool redrawing;

    public VhsTape( Terminal terminal, VhsTapeConfig config, EngineRng rng ) : base( terminal, rng )
    {
        this.config = config;
        lines = [.. terminal.Groups( CharacterGroup.RowBottomToTop )];
        redraw = new( Enumerable.Range( 0, lines.Count ) );
        var final = new Gradient( config.Stops, config.Steps );
        bool dynamic = terminal.Config.ExistingColors == ExistingColors.Dynamic;
        string[] snowSymbols = ["#", "*", ".", ":"];
        foreach ( int[] line in lines )
        {
            long offset = rng.RandInt( 4, 25 ), direction = rng.ChoiceIndex( 2 ) == 0 ? -1 : 1, hold = rng.RandInt( 1, 50 );
            foreach ( int id in line )
            {
                InputCell cell = terminal.Characters[id];
                Color? finalFg = dynamic ? cell.Foreground : FinalColor( id, final, config.GradientDirection ), bg = dynamic ? cell.Background : null;
                Color? stableFg = dynamic ? cell.Foreground ?? Color.FromHex( "808080" ) : finalFg;
                Engine.AddPath( id, "glitch", new( [new Waypoint( "glitch", new( Home[id].Column + offset * direction, Home[id].Row ) )], 2, hold: hold ) );
                Engine.AddPath( id, "restore", new( [new Waypoint( "restore", Home[id] )], 2 ) );
                Engine.AddPath( id, "mid", new( [new Waypoint( "mid", new( Home[id].Column + 8, Home[id].Row ) )], 2 ) );
                Engine.AddPath( id, "end", new( [new Waypoint( "end", new( Home[id].Column + 14, Home[id].Row ) )], 2 ) );
                Engine.AddScene( id, "base", new( [new( VisualBytes( id, cell.Symbol, stableFg, bg ), 1 )] ) );
                Engine.AddScene( id, "forward", new( config.LineColors.Select( color => new SceneFrame( VisualBytes( id, cell.Symbol, color ), 1 ) ), sync: SyncMetric.Step ) );
                Engine.AddScene( id, "backward", new( config.LineColors.Reverse( ).Select( color => new SceneFrame( VisualBytes( id, cell.Symbol, color ), 1 ) ), sync: SyncMetric.Step ) );
                var snow = new List<SceneFrame>( );
                for ( int i = 0; i < 25; i++ )
                {
                    string symbol = snowSymbols[rng.ChoiceIndex( snowSymbols.Length )];
                    Color color = config.NoiseColors[rng.ChoiceIndex( config.NoiseColors.Length )];
                    snow.Add( new( VisualBytes( id, symbol, color ), 2 ) );
                }

                snow.Add( new( VisualBytes( id, cell.Symbol, stableFg, bg ), 1 ) );
                Engine.AddScene( id, "snow", new( snow ) );
                Engine.AddScene( id, "redraw", new( [new( VisualBytes( id, "█", Color.FromHex( "ffffff" ) ), 6 ), new( VisualBytes( id, cell.Symbol, finalFg, bg ), 1 )] ) );
                var finalSnow = new List<SceneFrame>( );
                for ( int i = 0; i < 30; i++ )
                {
                    string symbol = snowSymbols[rng.ChoiceIndex( snowSymbols.Length )];
                    Color color = config.NoiseColors[rng.ChoiceIndex( config.NoiseColors.Length )];
                    finalSnow.Add( new( VisualBytes( id, symbol, color ), 2 ) );
                }

                Engine.AddScene( id, "final_snow", new( finalSnow ) );
                Engine.Register( id, EngineEvent.PathComplete, new( CallerKind.Path, "glitch" ), static ( engine, character ) => engine.ActivatePath( character, "restore" ) );
                Engine.Register( id, EngineEvent.PathActivated, new( CallerKind.Path, "glitch" ), static ( engine, character ) => engine.ActivateScene( character, "forward" ) );
                Engine.Register( id, EngineEvent.PathActivated, new( CallerKind.Path, "restore" ), static ( engine, character ) => engine.ActivateScene( character, "backward" ) );
                Engine.Register( id, EngineEvent.PathActivated, new( CallerKind.Path, "mid" ), static ( engine, character ) => engine.ActivateScene( character, "forward" ) );
                Engine.Register( id, EngineEvent.PathActivated, new( CallerKind.Path, "end" ), static ( engine, character ) => engine.ActivateScene( character, "forward" ) );
                Engine.Register( id, EngineEvent.SceneComplete, new( CallerKind.Scene, "backward" ), static ( engine, character ) => engine.ActivateScene( character, "base" ) );
            }
        }

        foreach ( int id in InputOrder )
        {
            terminal.Visible[id] = true;
            Engine.ActivateScene( id, "base" );
        }
    }

    private bool Complete( int line ) => lines[line].All( id => !Engine.PathIsActive( id ) );

    private void ActivateLine( int line )
    {
        foreach ( int id in lines[line] )
        {
            Engine.Activate( id );
        }
    }

    private void Restore( int line )
    {
        foreach ( int id in lines[line] )
        {
            Engine.GetPath( id, "restore" ).Speed = 40.0 / Rng.RandInt( 20, 40 );
            Engine.ActivatePath( id, "restore" );
        }
    }

    private void GlitchWave( )
    {
        if ( waveTop is null or 0 )
        {
            long height = Terminal.TextTop - Terminal.TextBottom + 1;
            if ( height < 3 )
            {
                return;
            }

            waveTop = Terminal.TextBottom + Rng.RandInt( Math.Max( 3, RustMath.RoundHalfEven( height * .5 ) ), height );
        }

        if ( !wave.All( Complete ) )
        {
            return;
        }

        if ( wave.Count > 0 )
        {
            bool move = Rng.Random( ) < .3;
            long delta = move ? (Rng.Random( ) < .3 ? 1 : -1) : 0;
            waveTop = Math.Max( 2, Math.Min( waveTop.Value + delta, Terminal.TextTop ) );
        }

        var next = new List<int>( );
        for ( long row = waveTop.Value - 2; row <= waveTop.Value; row++ )
        {
            long index = row - (Terminal.TextBottom - 1);
            if ( index >= 0 && index < lines.Count )
            {
                next.Add( ( int ) index );
            }
        }

        foreach ( int line in wave )
        {
            if ( !next.Contains( line ) )
            {
                Restore( line );
                ActivateLine( line );
            }
        }

        wave = next;
        if ( waveTop < Terminal.TextBottom + 2 )
        {
            foreach ( int line in wave )
            {
                Restore( line );
                ActivateLine( line );
            }

            wave.Clear( );
            waveTop = null;
        }
        else
        {
            string[] paths = ["mid", "end", "mid"];
            for ( int i = 0; i < wave.Count; i++ )
            {
                foreach ( int id in lines[wave[i]] )
                {
                    Engine.ActivatePath( id, paths[i] );
                }

                ActivateLine( wave[i] );
            }
        }
    }

    public override bool NextFrame( )
    {
        if ( phase == 3 && !Engine.HasActive )
        {
            return false;
        }

        if ( phase == 0 )
        {
            if ( wave.Count == 0 || wave.All( Complete ) )
            {
                GlitchWave( );
            }

            glitch.RemoveAll( Complete );
            if ( Rng.Random( ) < config.LineChance && glitch.Count < 3 )
            {
                int line = Rng.ChoiceIndex( lines.Count );
                if ( !wave.Contains( line ) && !glitch.Contains( line ) )
                {
                    long hold = Rng.RandInt( 20, 75 );
                    foreach ( int id in lines[line] )
                    {
                        Engine.GetPath( id, "glitch" ).Hold = hold;
                    }

                    glitch.Add( line );
                    foreach ( int id in lines[line] )
                    {
                        Engine.GetPath( id, "glitch" ).Speed = 40.0 / Rng.RandInt( 20, 40 );
                        Engine.GetPath( id, "restore" ).Speed = 40.0 / Rng.RandInt( 20, 40 );
                        Engine.ActivatePath( id, "glitch" );
                    }

                    ActivateLine( line );
                }
            }

            if ( Rng.Random( ) < config.NoiseChance )
            {
                for ( int line = 0; line < lines.Count; line++ )
                {
                    foreach ( int id in lines[line] )
                    {
                        Engine.ActivateScene( id, "snow" );
                    }

                    if ( !wave.Contains( line ) && !glitch.Contains( line ) )
                    {
                        ActivateLine( line );
                    }
                }
            }

            elapsed++;
            if ( elapsed >= config.GlitchTime )
            {
                foreach ( int line in wave )
                {
                    Restore( line );
                }

                foreach ( int line in glitch )
                {
                    Restore( line );
                }

                phase = 1;
            }
        }
        else if ( phase == 1 && !Engine.HasActive )
        {
            foreach ( int id in InputOrder )
            {
                Engine.ActivateScene( id, "final_snow" );
                Engine.Activate( id );
            }

            phase = 2;
        }
        else if ( phase == 2 && (redrawing || !Engine.HasActive) )
        {
            redrawing = true;
            if ( redraw.TryPop( out int line ) )
            {
                foreach ( int id in lines[line] )
                {
                    Engine.ActivateScene( id, "redraw" );
                    Engine.Activate( id );
                }
            }
            else
            {
                phase = 3;
            }
        }

        Engine.Update( );
        return true;
    }
}
