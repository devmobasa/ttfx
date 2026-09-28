using System.Globalization;

namespace Ttfx.Core;

public sealed record RingsConfig
{
    public Color[] RingColors { get; init; } = [Color.FromHex( "ab48ff" ), Color.FromHex( "e7b2b2" ), Color.FromHex( "fffebd" )];
    public double RingGap { get; init; } = .1;
    public long SpinDuration { get; init; } = 200;
    public (double Low, double High) SpinSpeed { get; init; } = (.25, 1);
    public long DisperseDuration { get; init; } = 200;
    public long Cycles { get; init; } = 3;
    public Color[] Stops { get; init; } = [Color.FromHex( "ab48ff" ), Color.FromHex( "e7b2b2" ), Color.FromHex( "fffebd" )];
    public long[] Steps { get; init; } = [12];
    public GradientDirection GradientDirection { get; init; } = GradientDirection.Vertical;
}

public sealed class Rings : EffectBase
{
    private sealed class Ring( Coord[] coordinates, long gap, Color color, double speed )
    {
        internal readonly Coord[] Coordinates = coordinates;
        internal readonly long Gap = gap;
        internal readonly Color Color = color;
        internal readonly double Speed = speed;
        internal readonly PathDefinition[] Paths = coordinates.Select( coordinate => new PathDefinition( [new Waypoint( "0", coordinate )], speed ) ).ToArray( );
        internal readonly string[] Names = Enumerable.Range( 0, coordinates.Length ).Select( index => index.ToString( CultureInfo.InvariantCulture ) ).ToArray( );
        internal readonly List<int> Characters = [];
        internal readonly Dictionary<int, string> Last = [];
    }

    private enum Phase { Start, Disperse, Spin, Final, Complete }
    private readonly RingsConfig config;
    private readonly List<Ring> rings = [];
    private readonly List<int> outside = [];
    private Phase phase;
    private bool initiallyDispersed;
    private long startRemaining = 100, spinRemaining, disperseRemaining, cyclesRemaining, pathSerial;

    public Rings( Terminal terminal, RingsConfig config, EngineRng rng ) : base( terminal, rng )
    {
        this.config = config;
        spinRemaining = config.SpinDuration;
        disperseRemaining = config.DisperseDuration;
        cyclesRemaining = config.Cycles;
        long gap = Math.Max( RustMath.RoundHalfEven( Math.Min( terminal.Height, terminal.Width ) * config.RingGap ), 1 );
        var gradient = new Gradient( config.Stops, config.Steps );
        var colors = new (Color? Fg, Color? Bg)[terminal.InputCount];
        foreach ( int id in InputOrder )
        {
            InputCell cell = terminal.Characters[id];
            colors[id] = terminal.Config.ExistingColors == ExistingColors.Dynamic ? (cell.Foreground, cell.Background) : (FinalColor( id, gradient, config.GradientDirection ), null);
            Engine.AddScene( id, "start", new( [new( VisualBytes( id, cell.Symbol, colors[id].Fg, colors[id].Bg ), 1 )] ) );
            Engine.AddPath( id, "home", new( [new Waypoint( "0", Home[id] )], .8, new( EaseKind.OutQuad ) ) );
            Engine.ActivateScene( id, "start" );
            terminal.Visible[id] = true;
        }

        int[] shuffled = ( int[] ) InputOrder.Clone( );
        rng.Shuffle<int>( shuffled );
        long limit = Math.Max( terminal.Width, terminal.Height );
        for ( long radius = 1; radius < limit; radius = unchecked(radius + gap) )
        {
            Coord[] coords = Geometry.OnCircle( Center, radius, unchecked(7 * radius), true ).ToArray( );
            int inCanvas = coords.Count( c => c.Column >= 1 && c.Column <= terminal.Width && c.Row >= 1 && c.Row <= terminal.Height );
            if ( ( double ) inCanvas / coords.Length < .25 )
            {
                break;
            }

            Color color = config.RingColors[rings.Count % config.RingColors.Length];
            double speed = rng.Uniform( config.SpinSpeed.Low, config.SpinSpeed.High );
            rings.Add( new( coords, gap, color, speed ) );
        }

        int remainingCharacters = shuffled.Length, ringPaths = 0;
        foreach ( Ring ring in rings )
        {
            int count = Math.Min( remainingCharacters, ring.Coordinates.Length );
            ringPaths = checked(ringPaths + count * ring.Coordinates.Length);
            remainingCharacters -= count;
        }

        Engine.Reserve( terminal.InputCount * 3, checked(ringPaths + terminal.InputCount * 5), checked(ringPaths + terminal.InputCount * 3) );
        int next = 0;
        var assigned = new HashSet<int>( );
        for ( int ringIndex = 0; ringIndex < rings.Count; ringIndex++ )
        {
            Ring ring = rings[ringIndex];
            Coord[] coords = ringIndex % 2 == 1 ? ring.Coordinates.Reverse( ).ToArray( ) : ring.Coordinates;
            for ( int slot = 0; slot < coords.Length && next < shuffled.Length; slot++ )
            {
                int id = shuffled[next++];
                InputCell cell = terminal.Characters[id];
                if ( terminal.Config.ExistingColors == ExistingColors.Dynamic )
                {
                    var scene = new SceneDefinition( [new( VisualBytes( id, cell.Symbol, colors[id].Fg, colors[id].Bg ), 1 )] );
                    Engine.AddScene( id, "gradient", scene );
                    Engine.AddScene( id, "disperse", scene );
                }
                else
                {
                    Engine.AddScene( id, "gradient", GradientScene( id, [cell.Symbol], 3, MakeGradient( [colors[id].Fg!.Value, ring.Color], [8] ) ) );
                    Engine.AddScene( id, "disperse", GradientScene( id, [cell.Symbol], 10, MakeGradient( [ring.Color, colors[id].Fg!.Value], [8] ) ) );
                }

                for ( int i = 0; i < coords.Length; i++ )
                {
                    int target = (slot + i) % coords.Length;
                    if ( ringIndex % 2 == 1 )
                    {
                        target = coords.Length - 1 - target;
                    }

                    Engine.AddPath( id, ring.Names[i], ring.Paths[target] );
                }

                ring.Last[id] = "0";
                ChainPaths( id, ring.Names, loop: true );
                ring.Characters.Add( id );
                assigned.Add( id );
            }
        }

        foreach ( int id in InputOrder )
        {
            if ( assigned.Contains( id ) )
            {
                continue;
            }

            Coord target = RandomCoord( outside: true );
            Engine.AddPath( id, "external", new( [new Waypoint( "0", target )], .8, new( EaseKind.OutSine ) ) );
            outside.Add( id );
            Engine.Register( id, EngineEvent.PathComplete, new( CallerKind.Path, "external" ), static ( engine, character ) => engine.Terminal.Visible[character] = false );
        }
    }

    private Coord DispersePath( Ring ring, int id, Coord origin )
    {
        List<Coord> coords = Geometry.InRect( origin, ring.Gap );
        var waypoints = new Waypoint[5];
        for ( int i = 0; i < waypoints.Length; i++ )
        {
            waypoints[i] = new( i.ToString( CultureInfo.InvariantCulture ), coords[Rng.ChoiceIndex( coords.Count )] );
        }

        Engine.RemovePath( id, "disperse" );
        Engine.AddPath( id, "disperse", new( waypoints, .14, loop: true ) );
        return waypoints[0].Coordinate;
    }

    private void StartDisperse( )
    {
        initiallyDispersed = true;
        foreach ( Ring ring in rings )
        {
            foreach ( int id in ring.Characters )
            {
                Coord origin = Engine.GetPath( id, "0" ).Waypoints[0].Coordinate;
                Coord target = DispersePath( ring, id, origin );
                Engine.AddPath( id, "initial", new( [new Waypoint( "0", target )], .3, new( EaseKind.OutCubic ) ) );
                Engine.Register( id, EngineEvent.PathComplete, new( CallerKind.Path, "initial" ), static ( engine, character ) => engine.ActivatePath( character, "disperse" ) );
                Engine.ActivateScene( id, "disperse" );
                Engine.ActivatePath( id, "initial" );
                Engine.Activate( id );
            }
        }

        foreach ( int id in outside )
        {
            Engine.ActivatePath( id, "external" );
            Engine.Activate( id );
        }
    }

    private void Spin( )
    {
        foreach ( Ring ring in rings )
        {
            foreach ( int id in ring.Characters )
            {
                string last = ring.Last[id];
                Coord target = Engine.GetPath( id, last ).Waypoints[0].Coordinate;
                string name = "condense" + (pathSerial++).ToString( CultureInfo.InvariantCulture );
                Engine.AddPath( id, name, new( [new Waypoint( "0", target )], .1 ) );
                Engine.Register( id, EngineEvent.PathComplete, new( CallerKind.Path, name ), ActivatePathCallback( last ) );
                Engine.ActivatePath( id, name );
                Engine.ActivateScene( id, "gradient" );
            }
        }
    }

    private void Disperse( )
    {
        foreach ( Ring ring in rings )
        {
            foreach ( int id in ring.Characters )
            {
                ring.Last[id] = Engine.ActivePathName( id ) ?? "0";
                DispersePath( ring, id, Terminal.Coordinates[id] );
                Engine.ActivatePath( id, "disperse" );
                Engine.ActivateScene( id, "disperse" );
            }
        }
    }

    public override bool NextFrame( )
    {
        switch ( phase )
        {
            case Phase.Complete:
                return false;
            case Phase.Start:
                if ( startRemaining == 0 )
                {
                    phase = Phase.Disperse;
                }
                else
                {
                    startRemaining--;
                }
                break;
            case Phase.Disperse:
                if ( !initiallyDispersed )
                {
                    StartDisperse( );
                }
                else if ( disperseRemaining == 0 )
                {
                    phase = Phase.Spin;
                    cyclesRemaining--;
                    spinRemaining = config.SpinDuration;
                    Spin( );
                }
                else
                {
                    disperseRemaining--;
                }
                break;
            case Phase.Spin:
                if ( spinRemaining == 0 )
                {
                    if ( cyclesRemaining == 0 )
                    {
                        phase = Phase.Final;
                        foreach ( int id in InputOrder )
                        {
                            Terminal.Visible[id] = true;
                            Engine.ActivatePath( id, "home" );
                            Engine.Activate( id );
                            if ( !outside.Contains( id ) )
                            {
                                Engine.ActivateScene( id, "disperse" );
                            }
                        }
                    }
                    else
                    {
                        disperseRemaining = config.DisperseDuration;
                        Disperse( );
                        phase = Phase.Disperse;
                    }
                }
                else
                {
                    spinRemaining--;
                }
                break;
            case Phase.Final:
                if ( !Engine.HasActive )
                {
                    phase = Phase.Complete;
                }
                break;
        }

        Engine.Update( );
        return true;
    }
}
