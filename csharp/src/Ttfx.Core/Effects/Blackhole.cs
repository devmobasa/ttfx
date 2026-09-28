namespace Ttfx.Core;

public sealed record BlackholeConfig
{
    public Color HoleColor { get; init; } = Color.FromHex( "ffffff" );
    public Color[] StarColors { get; init; } = [Color.FromHex( "ffcc0d" ), Color.FromHex( "ff7326" ), Color.FromHex( "ff194d" ), Color.FromHex( "bf2669" ), Color.FromHex( "702a8c" ), Color.FromHex( "049dbf" )];
    public Color[] Stops { get; init; } = [Color.FromHex( "8A008A" ), Color.FromHex( "00D1FF" ), Color.FromHex( "ffffff" )];
    public long[] Steps { get; init; } = [9];
    public GradientDirection GradientDirection { get; init; } = GradientDirection.Diagonal;
}

public sealed class Blackhole : EffectBase
{
    private readonly BlackholeConfig config;
    private readonly Gradient final;
    private readonly List<int> hole = [], consuming = [];
    private readonly Queue<int> forming;
    private readonly long radius, formationDelay;
    private long delay;
    private int phase;

    public Blackhole( Terminal terminal, BlackholeConfig config, EngineRng rng ) : base( terminal, rng )
    {
        this.config = config;
        radius = Math.Max( Math.Min( RustMath.RoundHalfEven( terminal.Width * .3 ), RustMath.RoundHalfEven( terminal.Height * .20 ) ), 3 );
        final = new( config.Stops, config.Steps );
        var field = new Gradient( [Color.FromHex( "4a4a4d" ), Color.FromHex( "ffffff" )], [6] );
        List<int> available = [.. Enumerable.Range( 0, terminal.InputCount )];
        while ( hole.Count < radius * 3 && available.Count > 0 )
        {
            int index = rng.ChoiceIndex( available.Count );
            hole.Add( available[index] );
            available.RemoveAt( index );
        }

        List<Coord> ring = Geometry.OnCircle( Center, radius, hole.Count, true );
        for ( int index = 0; index < hole.Count; index++ )
        {
            int id = hole[index];
            Engine.AddPath( id, "hole", new( [new Waypoint( "0", ring[index] )], .7, new( EaseKind.InOutSine ) ) );
            Engine.AddScene( id, "hole", new( [new( VisualBytes( id, "*", config.HoleColor ), 1 )] ) );
            Engine.Register( id, EngineEvent.PathActivated, new( CallerKind.Path, "hole" ), static ( engine, character ) => engine.SetLayer( character, 1 ) );
            Coord[] coords = [.. ring.Skip( index ), .. ring.Take( index )];
            Engine.AddPath( id, "rotation", new( coords.Select( ( c, i ) => new Waypoint( i.ToString( System.Globalization.CultureInfo.InvariantCulture ), c ) ), .45, loop: true ) );
        }

        string[] symbols = ["*", "'", "`", "¤", "•", "°", "·"];
        foreach ( int id in InputOrder )
        {
            terminal.Visible[id] = true;
            string symbol = symbols[rng.ChoiceIndex( symbols.Length )];
            Color color = field.Spectrum[rng.ChoiceIndex( field.Spectrum.Length )];
            Engine.AddScene( id, "start", new( [new( VisualBytes( id, symbol, color ), 1 )] ) );
            Engine.ActivateScene( id, "start" );
            if ( !hole.Contains( id ) )
            {
                terminal.Coordinates[id] = RandomCoord( );
                double speed = rng.Uniform( .17, .30 );
                Engine.AddPath( id, "singularity", new( [new Waypoint( "0", Center )], speed, new( EaseKind.InExpo ) ) );
                SceneDefinition fade = GradientScene( id, [symbol], 1, MakeGradient( [color, Color.FromHex( "000000" )], [10] ) );
                Engine.AddScene( id, "consumed", new( [.. fade.Frames, new( VisualBytes( id, " " ), 1 )], sync: SyncMetric.Distance ) );
                Engine.Register( id, EngineEvent.PathActivated, new( CallerKind.Path, "singularity" ), static ( engine, character ) => engine.SetLayer( character, 2 ) );
                Engine.Register( id, EngineEvent.PathActivated, new( CallerKind.Path, "singularity" ), static ( engine, character ) => engine.ActivateScene( character, "consumed" ) );
                consuming.Add( id );
            }
        }

        rng.Shuffle( System.Runtime.InteropServices.CollectionsMarshal.AsSpan( consuming ) );
        formationDelay = Math.Max( RustMath.FloorDiv( 100, hole.Count ), 6 );
        delay = formationDelay;
        forming = new( hole );
    }

    private void Collapse( )
    {
        List<Coord> coords = Geometry.OnCircle( Center, radius + 3, hole.Count, true );
        string[] symbols = ["◦", "◎", "◉", "●", "◉", "◎", "◦"];
        for ( int index = 0; index < hole.Count; index++ )
        {
            int id = hole[index];
            Engine.AddPath( id, "expand", new( [new Waypoint( "0", coords[index] )], .2, new( EaseKind.InExpo ) ) );
            Engine.AddPath( id, "collapse", new( [new Waypoint( "0", Center )], .3, new( EaseKind.InExpo ) ) );
            ChainPaths( id, ["expand", "collapse"] );
            if ( index == 0 )
            {
                var frames = new List<SceneFrame>( );
                for ( int repeat = 0; repeat < 3; repeat++ )
                {
                    foreach ( string symbol in symbols )
                    {
                        frames.Add( new( VisualBytes( id, symbol, config.StarColors[Rng.ChoiceIndex( config.StarColors.Length )] ), 3 ) );
                    }
                }

                Engine.AddScene( id, "point", new( frames ) );
                Engine.Register( id, EngineEvent.PathComplete, new( CallerKind.Path, "collapse" ), static ( engine, character ) => engine.ActivateScene( character, "point" ) );
                Engine.Register( id, EngineEvent.PathComplete, new( CallerKind.Path, "collapse" ), static ( engine, character ) => engine.SetLayer( character, 3 ) );
            }

            Engine.ActivatePath( id, "expand" );
            Engine.Activate( id );
        }
    }

    private void Explode( )
    {
        Color[] colors = [Color.FromHex( "ffcc0d" ), Color.FromHex( "ff7326" ), Color.FromHex( "ff194d" ), Color.FromHex( "bf2669" ), Color.FromHex( "702a8c" ), Color.FromHex( "049dbf" )];
        bool dynamic = Terminal.Config.ExistingColors == ExistingColors.Dynamic && InputOrder.Any( id => Terminal.Characters[id].Foreground is not null || Terminal.Characters[id].Background is not null );
        foreach ( int id in InputOrder )
        {
            List<Coord> circle = Geometry.OnCircle( Home[id], 3, 5, true );
            Coord nearby = circle[Rng.ChoiceIndex( 5 )];
            double speed = Rng.RandInt( 3, 4 ) / 10.0;
            Engine.AddPath( id, "nearby", new( [new Waypoint( "0", nearby )], speed, new( EaseKind.OutExpo ) ) );
            double inputSpeed = Rng.RandInt( 4, 6 ) / 100.0;
            Engine.AddPath( id, "input", new( [new Waypoint( "0", Home[id] )], inputSpeed, new( EaseKind.InCubic ) ) );
            Color color = colors[Rng.ChoiceIndex( colors.Length )];
            InputCell cell = Terminal.Characters[id];
            Engine.AddScene( id, "explode", new( [new( VisualBytes( id, cell.Symbol, color ), 1 )] ) );
            SceneDefinition cooling;
            if ( dynamic )
            {
                cooling = cell.Foreground is null && cell.Background is null ? new( [new( VisualBytes( id, cell.Symbol ), 1 )] ) : FadeToFinal( id, final, config.GradientDirection, color, 10, 20 );
            }
            else
            {
                cooling = GradientScene( id, [cell.Symbol], 20, MakeGradient( [color, FinalColor( id, final, config.GradientDirection )], [10] ) );
            }

            Engine.AddScene( id, "cool", cooling );
            Engine.Register( id, EngineEvent.PathComplete, new( CallerKind.Path, "nearby" ), static ( engine, character ) => engine.ActivatePath( character, "input" ) );
            Engine.Register( id, EngineEvent.PathComplete, new( CallerKind.Path, "nearby" ), static ( engine, character ) => engine.ActivateScene( character, "cool" ) );
            Engine.ActivateScene( id, "explode" );
            Engine.ActivatePath( id, "nearby" );
            Engine.Activate( id );
        }
    }

    public override bool NextFrame( )
    {
        if ( phase == 4 && !Engine.HasActive )
        {
            return false;
        }

        if ( phase == 0 )
        {
            if ( forming.Count > 0 )
            {
                if ( delay == 0 )
                {
                    int id = forming.Dequeue( );
                    Engine.ActivatePath( id, "hole" );
                    Engine.ActivateScene( id, "hole" );
                    Engine.Activate( id );
                    delay = formationDelay;
                }
                else
                {
                    delay--;
                }
            }
            else if ( !Engine.HasActive )
            {
                foreach ( int id in hole )
                {
                    Engine.ActivatePath( id, "rotation" );
                    Engine.Activate( id );
                }

                phase = 1;
            }
        }
        else if ( phase == 1 )
        {
            if ( consuming.Count > 0 )
            {
                foreach ( int id in consuming )
                {
                    Engine.ActivatePath( id, "singularity" );
                    Engine.Activate( id );
                }

                consuming.Clear( );
            }
            else if ( InputOrder.Where( id => !hole.Contains( id ) ).All( id => !Engine.IsActive( id ) ) )
            {
                phase = 2;
            }
        }
        else if ( phase == 2 )
        {
            Collapse( );
            phase = 3;
        }
        else if ( phase == 3 && hole.All( id => !Engine.PathIsActive( id ) && !Engine.SceneIsActive( id ) ) )
        {
            Explode( );
            phase = 4;
        }

        Engine.Update( );
        return true;
    }
}
