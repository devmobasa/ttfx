namespace Ttfx.Core;

public sealed record SwarmConfig
{
    public Color[] BaseColors { get; init; } = [Color.FromHex( "31a0d4" )];
    public Color FlashColor { get; init; } = Color.FromHex( "f2ea79" );
    public double Size { get; init; } = .1;
    public double Coordination { get; init; } = .8;
    public (long Low, long High) Areas { get; init; } = (2, 4);
    public Color[] Stops { get; init; } = [Color.FromHex( "31b900" ), Color.FromHex( "f0ff65" )];
    public long[] Steps { get; init; } = [12];
    public GradientDirection GradientDirection { get; init; } = GradientDirection.Horizontal;
}

public sealed class Swarm : EffectBase
{
    private readonly SwarmConfig config;
    private readonly List<int[]> swarms = [];
    private int[] current = [];
    private bool callNext = true;
    private string activeArea = "0_swarm_area";

    public Swarm( Terminal terminal, SwarmConfig config, EngineRng rng ) : base( terminal, rng )
    {
        this.config = config;
        long size = Math.Max( RustMath.RoundHalfEven( terminal.InputCount * config.Size ), 1 );
        for ( int start = 0; start < InputOrder.Length; start += ( int ) size )
        {
            swarms.Add( InputOrder.Skip( start ).Take( ( int ) size ).ToArray( ) );
        }

        if ( swarms[^1].Length < RustMath.FloorDiv( size, 2 ) )
        {
            int[] last = swarms[^1];
            swarms.RemoveAt( swarms.Count - 1 );
            swarms[^1] = [.. swarms[^1], .. last];
        }

        var final = new Gradient( config.Stops, config.Steps );
        var cache = new Dictionary<Coord, List<Coord>>( );
        foreach ( int[] swarm in swarms )
        {
            Color color = config.BaseColors[rng.ChoiceIndex( config.BaseColors.Length )];
            var gradient = new Gradient( [color, config.FlashColor], [7] );
            Color[] mirror = [.. gradient.Spectrum, .. Enumerable.Repeat( config.FlashColor, 10 ), .. gradient.Spectrum.Reverse( )];
            Coord spawn = RandomCoord( outside: true );
            long areaCount = rng.RandInt( config.Areas.Low, config.Areas.High );
            var areas = new List<(Coord Focus, List<Coord> Coordinates)>( );
            Coord focus = spawn;
            long radius = Math.Max( RustMath.FloorDiv( Math.Min( terminal.Width, terminal.Height ), 2 ), 1 );
            for ( long area = 0; area < areaCount; area++ )
            {
                if ( !cache.TryGetValue( focus, out List<Coord>? coords ) )
                {
                    coords = Geometry.OnCircle( focus, radius, 0, true );
                    cache.Add( focus, coords );
                }

                rng.Shuffle( System.Runtime.InteropServices.CollectionsMarshal.AsSpan( coords ) );
                Coord? next = coords.Where( c => c.Column >= 1 && c.Column <= terminal.Width && c.Row >= 1 && c.Row <= terminal.Height ).Select( c => ( Coord? ) c ).FirstOrDefault( );
                Coord nextFocus = next ?? RandomCoord( );
                List<Coord> inner = Geometry.InCircle( focus, Math.Max( RustMath.FloorDiv( Math.Min( terminal.Width, terminal.Height ), 6 ), 1 ) * 2 );
                int existing = areas.FindIndex( entry => entry.Focus == focus );
                if ( existing >= 0 )
                {
                    areas[existing] = (focus, inner);
                }
                else
                {
                    areas.Add( (focus, inner) );
                }

                focus = nextFocus;
            }

            Engine.ReserveAdditional( checked(swarm.Length * 3), checked(swarm.Length * (areas.Count * 3 + 1)), checked(swarm.Length * (areas.Count * 3 + 4)) );
            foreach ( int id in swarm )
            {
                terminal.Coordinates[id] = spawn;
                InputCell cell = terminal.Characters[id];
                Engine.AddScene( id, "flash", new( mirror.Select( c => new SceneFrame( VisualBytes( id, cell.Symbol, c ), 1 ) ), sync: SyncMetric.Distance ) );
                var paths = new List<string>( );
                for ( int area = 0; area < areas.Count; area++ )
                {
                    List<Coord> coords = areas[area].Coordinates;
                    string name = $"{area}_swarm_area";
                    Coord origin = coords[rng.ChoiceIndex( coords.Count )];
                    Engine.AddPath( id, name, new( [new Waypoint( name, origin )], .4, new( EaseKind.OutSine ) ) );
                    paths.Add( name );
                    Engine.Register( id, EngineEvent.PathActivated, new( CallerKind.Path, name ), static ( engine, character ) => engine.ActivateScene( character, "flash" ) );
                    Engine.Register( id, EngineEvent.PathActivated, new( CallerKind.Path, name ), static ( engine, character ) => engine.SetLayer( character, 1 ) );
                    Engine.Register( id, EngineEvent.PathComplete, new( CallerKind.Path, name ), static ( engine, character ) => engine.DeactivateScene( character ) );
                    for ( int inner = 0; inner < 2; inner++ )
                    {
                        Coord next = coords[rng.ChoiceIndex( coords.Count )];
                        string pathName = paths.Count.ToString( System.Globalization.CultureInfo.InvariantCulture );
                        Engine.AddPath( id, pathName, new( [new Waypoint( (paths.Count + 1).ToString( System.Globalization.CultureInfo.InvariantCulture ), next )], .18, new( EaseKind.InOutSine ) ) );
                        paths.Add( pathName );
                    }
                }

                string input = paths.Count.ToString( System.Globalization.CultureInfo.InvariantCulture );
                Engine.AddPath( id, input, new( [new Waypoint( "0", Home[id] )], .45, new( EaseKind.InOutQuad ) ) );
                paths.Add( input );
                SceneDefinition ending = FadeToFinal( id, final, config.GradientDirection, config.FlashColor, 10, 3 );
                if ( terminal.Config.ExistingColors == ExistingColors.Dynamic && cell.Foreground is null && cell.Background is null )
                {
                    SceneDefinition clear = GradientScene( id, [cell.Symbol], 3, MakeGradient( [config.FlashColor, Color.FromHex( "ffffff" )], [10] ) );
                    ending = new( [.. clear.Frames, new( VisualBytes( id, cell.Symbol ), 3 )] );
                }

                Engine.AddScene( id, "land", ending );
                Engine.Register( id, EngineEvent.PathComplete, new( CallerKind.Path, input ), static ( engine, character ) => engine.ActivateScene( character, "land" ) );
                Engine.Register( id, EngineEvent.PathComplete, new( CallerKind.Path, input ), static ( engine, character ) => engine.SetLayer( character, 0 ) );
                Engine.Register( id, EngineEvent.PathActivated, new( CallerKind.Path, input ), static ( engine, character ) => engine.ActivateScene( character, "flash" ) );
                ChainPaths( id, paths.ToArray( ) );
            }
        }
    }

    public override bool NextFrame( )
    {
        if ( swarms.Count == 0 && !Engine.HasActive )
        {
            return false;
        }

        if ( swarms.Count > 0 && callNext )
        {
            callNext = false;
            current = swarms[^1];
            swarms.RemoveAt( swarms.Count - 1 );
            activeArea = "0_swarm_area";
            foreach ( int id in current )
            {
                Engine.ActivatePath( id, activeArea );
                Terminal.Visible[id] = true;
                Engine.Activate( id );
            }
        }

        if ( Engine.ActiveCount < current.Length )
        {
            callNext = true;
        }

        foreach ( int id in current )
        {
            string? path = Engine.ActivePathName( id );
            if ( path is not null && path != activeArea && path.Contains( "swarm_area", StringComparison.Ordinal ) && path[0] > activeArea[0] )
            {
                activeArea = path;
                foreach ( int other in current )
                {
                    if ( other != id && Rng.Random( ) < config.Coordination )
                    {
                        Engine.ActivatePath( other, activeArea );
                    }
                }

                break;
            }
        }

        Engine.Update( );
        return true;
    }
}
