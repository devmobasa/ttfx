namespace Ttfx.Core;

public sealed record ThunderstormConfig
{
    public Color LightningColor { get; init; } = Color.FromHex( "68A3E8" );
    public Color TextColor { get; init; } = Color.FromHex( "EF5411" );
    public long TextGlowTime { get; init; } = 6;
    public string[] RainSymbols { get; init; } = ["\\", ".", ","];
    public string[] SparkSymbols { get; init; } = ["*", ".", "'"];
    public Color SparkColor { get; init; } = Color.FromHex( "ff4d00" );
    public long SparkGlowTime { get; init; } = 18;
    public long StormTime { get; init; } = 12;
    public long Frames { get; init; } = 3;
    public Color[] Stops { get; init; } = [Color.FromHex( "8A008A" ), Color.FromHex( "00D1FF" ), Color.FromHex( "FFFFFF" )];
    public long[] Steps { get; init; } = [12];
    public GradientDirection GradientDirection { get; init; } = GradientDirection.Vertical;
}

public sealed class Thunderstorm : EffectBase
{
    private readonly ThunderstormConfig config;
    private readonly ParticlePool rain, sparks;
    private readonly Stack<int> availableStrikes = [];
    private readonly List<int> pendingStrikes = [], activeStrikes = [], pendingGlow = [];
    private readonly Dictionary<int, string> strikeSymbols = [];
    private readonly Dictionary<Coord, int> inputAt;
    private long delay, strikeDelay;
    private bool striking;
    private double branchChance = .05, stormStart;
    private int phase;

    public Thunderstorm( Terminal terminal, ThunderstormConfig config, EngineRng rng ) : base( terminal, rng )
    {
        this.config = config;
        inputAt = Enumerable.Range( 0, Home.Length ).ToDictionary( id => Home[id], id => id );
        rain = new( Engine, rng, config.RainSymbols, initialCount: 50, initialize: ( engine, id ) =>
        {
            engine.SetLayer( id, 1 );
            Appearance( id, Color.FromHex( "aaaaff" ) );
        } );
        var sparkGradient = new Gradient( [config.SparkColor, terminal.Config.Background], [7] );
        sparks = new( Engine, rng, config.SparkSymbols, initialCount: 200, maximum: 2000, initialize: ( engine, id ) =>
        {
            engine.SetLayer( id, 2 );
            engine.AddScene( id, "glow", GradientScene( id, [terminal.Characters[id].Symbol], config.SparkGlowTime, sparkGradient, ease: new( EaseKind.InCirc ) ) );
        } );
        stormStart = Engine.Clock.Seconds;
        BuildStrikes( 200 );
        var final = new Gradient( config.Stops, config.Steps );
        bool dynamic = terminal.Config.ExistingColors == ExistingColors.Dynamic;
        foreach ( int id in InputOrder )
        {
            InputCell cell = terminal.Characters[id];
            Color visibleFg = dynamic ? cell.Foreground ?? Color.FromHex( "808080" ) : FinalColor( id, final, config.GradientDirection );
            Color? visibleBg = dynamic ? cell.Background : null;
            Color stormFg = Color.AdjustBrightness( visibleFg, .5 );
            Color? stormBg = visibleBg is { } bg ? Color.AdjustBrightness( bg, .5 ) : null;
            var glow = GradientScene( id, [cell.Symbol], config.TextGlowTime, MakeGradient( [config.TextColor, stormFg], [7] ), stormBg is { } gb ? MakeGradient( [gb], [1] ) : null ).Frames.ToList( );
            if ( dynamic )
            {
                glow.Add( new( VisualBytes( id, cell.Symbol, stormFg, stormBg ), config.TextGlowTime ) );
            }

            Engine.AddScene( id, "glow", new( glow ) );
            if ( dynamic )
            {
                var fade = PairFade( id, visibleFg, visibleBg, stormFg, stormBg );
                fade.Add( new( VisualBytes( id, cell.Symbol, stormFg, stormBg ), 12 ) );
                Engine.AddScene( id, "fade", new( fade ) );
                var unfade = PairFade( id, stormFg, stormBg, visibleFg, visibleBg );
                unfade.Add( new( VisualBytes( id, cell.Symbol, visibleFg, visibleBg ), 12 ) );
                if ( cell.Foreground != visibleFg || cell.Background != visibleBg )
                {
                    unfade.Add( new( VisualBytes( id, cell.Symbol, cell.Foreground, cell.Background ), 12 ) );
                }

                Engine.AddScene( id, "unfade", new( unfade ) );
            }
            else
            {
                Color[] fade = new Gradient( [visibleFg, stormFg], [7] ).Spectrum;
                Engine.AddScene( id, "fade", new( fade.Select( color => new SceneFrame( VisualBytes( id, cell.Symbol, color ), 12 ) ) ) );
                Engine.AddScene( id, "unfade", new( fade.Reverse( ).Select( color => new SceneFrame( VisualBytes( id, cell.Symbol, color ), 12 ) ) ) );
            }

            Engine.AddScene( id, "flash", GradientScene( id, [cell.Symbol], 6, MakeGradient( [stormFg, Color.AdjustBrightness( visibleFg, 1.7 )], [7], loop: true ), stormBg is { } sb ? MakeGradient( [sb], [1] ) : null ) );
            terminal.Visible[id] = true;
        }

        Engine.Register( InputOrder[0], EngineEvent.SceneComplete, new( CallerKind.Scene, "fade" ), ( _, _ ) =>
        {
            phase = 2;
            stormStart = Engine.Clock.Seconds;
        } );
    }

    private List<SceneFrame> PairFade( int id, Color? startFg, Color? startBg, Color? endFg, Color? endBg )
    {
        Color?[] fg = startFg is { } sf && endFg is { } ef ? new Gradient( [sf, ef], [7] ).Spectrum.Select( color => ( Color? ) color ).ToArray( ) : Enumerable.Repeat( endFg ?? startFg, 7 ).ToArray( );
        Color?[] bg = startBg is { } sb && endBg is { } eb ? new Gradient( [sb, eb], [7] ).Spectrum.Select( color => ( Color? ) color ).ToArray( ) : Enumerable.Repeat( endBg ?? startBg, 7 ).ToArray( );
        var frames = new List<SceneFrame>( );
        for ( int i = 0; i < 7; i++ )
        {
            frames.Add( new( VisualBytes( id, Terminal.Characters[id].Symbol, fg[i], bg[i] ), 12 ) );
        }

        return frames;
    }

    private void BuildStrikes( int count )
    {
        int first = Engine.AddCharacters( Enumerable.Repeat( "|", count ).ToArray( ), new( 1, 1 ) );
        for ( int id = first; id < first + count; id++ )
        {
            availableStrikes.Push( id );
        }
    }

    private void SetupStrike( int? neighbor = null )
    {
        long column, row;
        if ( neighbor is { } previous )
        {
            Coord coord = Terminal.Coordinates[previous];
            column = coord.Column;
            row = coord.Row;
        }
        else
        {
            column = Rng.RandInt( 1, Terminal.Width );
            row = Terminal.Height;
        }

        while ( row >= 1 )
        {
            if ( availableStrikes.Count == 0 )
            {
                BuildStrikes( 20 );
            }

            string symbol;
            if ( neighbor is { } id )
            {
                string input = Terminal.Characters[id].Symbol;
                if ( input == "/" )
                {
                    column++;
                    symbol = Rng.ChoiceIndex( 2 ) == 0 ? "|" : "\\";
                }
                else if ( input == "\\" )
                {
                    column--;
                    symbol = Rng.ChoiceIndex( 2 ) == 0 ? "|" : "/";
                }
                else
                {
                    long delta = Rng.ChoiceIndex( 2 ) == 0 ? -1 : 1;
                    column += delta;
                    symbol = delta == 1 ? "\\" : "/";
                }
            }
            else
            {
                string[] choices = ["\\", "/", "|"];
                symbol = choices[Rng.ChoiceIndex( 3 )];
            }

            int strike = availableStrikes.Pop( );
            Engine.ResetCharacter( strike, new( ) { ClearPaths = false, ClearScenes = true, ClearEvents = true, DeactivatePath = false, DeactivateScene = false } );
            Terminal.Coordinates[strike] = new( column, row );
            Appearance( strike, config.LightningColor, symbol: symbol );
            strikeSymbols[strike] = symbol;
            row--;
            if ( symbol == "\\" )
            {
                column++;
            }
            else if ( symbol == "/" )
            {
                column--;
            }

            pendingStrikes.Add( strike );
            if ( Rng.Random( ) < branchChance && neighbor is null )
            {
                branchChance -= .01;
                SetupStrike( strike );
            }

            neighbor = null;
        }

        branchChance = .05;
    }

    private void Lightning( )
    {
        SetupStrike( );
        var flash = new Gradient( [config.LightningColor, Color.AdjustBrightness( config.LightningColor, 1.7 )], [7], loop: true );
        var fade = new Gradient( [config.LightningColor, Terminal.Config.Background], [6] );
        var ease = new Easing( EaseKind.CubicBezier, 0, 1.6, 1, Rng.Uniform( -.6, .4 ) );
        foreach ( int id in pendingStrikes )
        {
            string symbol = strikeSymbols[id];
            Engine.AddScene( id, "flash", GradientScene( id, [symbol], 6, flash, ease: ease ) );
            Engine.AddScene( id, "fade", GradientScene( id, [symbol], 2, fade ) );
            Engine.SetLayer( id, 1 );
            Engine.Register( id, EngineEvent.SceneComplete, new( CallerKind.Scene, "flash" ), static ( engine, character ) => engine.ActivateScene( character, "fade" ) );
            Engine.Register( id, EngineEvent.SceneComplete, new( CallerKind.Scene, "fade" ), ( _, character ) => Terminal.Visible[character] = false );
            Engine.Register( id, EngineEvent.SceneComplete, new( CallerKind.Scene, "fade" ), ( engine, character ) =>
            {
                if ( inputAt.TryGetValue( Terminal.Coordinates[character], out int input ) && Terminal.Visible[input] )
                {
                    engine.ActivateScene( input, "glow" );
                    pendingGlow.Add( input );
                }
            } );
            Engine.Register( id, EngineEvent.SceneComplete, new( CallerKind.Scene, "fade" ), ( _, character ) => availableStrikes.Push( character ) );
        }

        foreach ( int id in InputOrder )
        {
            Engine.GetScene( id, "flash" ).Ease = ease;
        }
    }

    private void StepStrike( )
    {
        if ( strikeDelay != 0 )
        {
            strikeDelay--;
            return;
        }

        if ( pendingStrikes.Count == 0 )
        {
            return;
        }

        long count = Rng.RandInt( 1, 3 );
        for ( long i = 0; i < count && pendingStrikes.Count > 0; i++ )
        {
            int id = pendingStrikes[0];
            pendingStrikes.RemoveAt( 0 );
            activeStrikes.Add( id );
            Terminal.Visible[id] = true;
            strikeDelay = 1;
            if ( pendingStrikes.Count != 0 )
            {
                continue;
            }

            long sparkCount = Rng.RandInt( 12, 18 );
            for ( long spark = 0; spark < sparkCount; spark++ )
            {
                sparks.Emit( Terminal.Coordinates[activeStrikes[^1]], reset: new( ) { ClearEvents = true }, onEmit: SetupSpark );
            }

            Engine.Register( id, EngineEvent.SceneComplete, new( CallerKind.Scene, "fade" ), ( _, _ ) => striking = false );
            foreach ( int strike in activeStrikes )
            {
                Engine.ActivateScene( strike, "flash" );
                Engine.Activate( strike );
            }

            activeStrikes.Clear( );
            foreach ( int input in InputOrder )
            {
                Engine.ActivateScene( input, "flash" );
                Engine.Activate( input );
            }
        }
    }

    private void SetupSpark( Engine engine, int id )
    {
        Coord impact = Terminal.Coordinates[id];
        double speed = Rng.Uniform( .1, .25 );
        long offset = Rng.RandInt( 4, 20 ) * (Rng.ChoiceIndex( 2 ) == 0 ? 1 : -1);
        var target = new Coord( impact.Column + offset, 1 );
        var control = new Coord( impact.Column - RustMath.FloorDiv( impact.Column - target.Column, 2 ), Rng.RandInt( 1, Terminal.Height ) );
        engine.AddPath( id, "spark", new( [new Waypoint( "0", target, [control] )], speed, new( EaseKind.OutQuint ), hold: 30 ) );
        engine.Register( id, EngineEvent.SceneComplete, new( CallerKind.Scene, "glow" ), ( _, character ) => sparks.Reclaim( character ) );
        engine.ActivateScene( id, "glow" );
        engine.ActivatePath( id, "spark" );
    }

    private void Rain( )
    {
        if ( delay != 0 )
        {
            delay--;
            return;
        }

        long count = Rng.RandInt( 1, 6 );
        for ( long i = 0; i < count; i++ )
        {
            long column = Rng.RandInt( 1 - Terminal.Height, Terminal.Width );
            rain.Emit( new( column - 1, Terminal.Height + 1 ), reset: new( ) { ClearEvents = true }, onEmit: ( engine, id ) =>
            {
                Coord origin = Terminal.Coordinates[id];
                double speed = Rng.Uniform( .5, 1.5 );
                engine.AddPath( id, "rain", new( [new Waypoint( "0", new( origin.Column + Terminal.Height + 1, 0 ) )], speed ) );
                engine.Register( id, EngineEvent.PathComplete, new( CallerKind.Path, "rain" ), ( _, character ) => rain.Reclaim( character ) );
                engine.ActivatePath( id, "rain" );
            } );
        }

        delay = Rng.RandInt( 1, 7 );
    }

    public override bool NextFrame( )
    {
        if ( phase == 3 && !Engine.HasActive )
        {
            return false;
        }

        if ( phase == 0 )
        {
            foreach ( int id in InputOrder )
            {
                Engine.ActivateScene( id, "fade" );
                Engine.Activate( id );
            }

            phase = 1;
        }
        else if ( phase == 2 )
        {
            Rain( );
            if ( !striking && Rng.Random( ) < .008 )
            {
                striking = true;
                Lightning( );
            }

            if ( striking )
            {
                StepStrike( );
            }

            foreach ( int id in pendingGlow )
            {
                Engine.Activate( id );
            }

            pendingGlow.Clear( );
            if ( Engine.Clock.Seconds - stormStart >= config.StormTime && !striking )
            {
                foreach ( int id in InputOrder )
                {
                    Engine.ActivateScene( id, "unfade" );
                    Engine.Activate( id );
                }

                phase = 3;
            }
        }

        Engine.Update( );
        return true;
    }
}
