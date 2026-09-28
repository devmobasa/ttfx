namespace Ttfx.Core;

public sealed record SpotlightsConfig
{
    public double BeamWidthRatio { get; init; } = 2;
    public double BeamFalloff { get; init; } = .3;
    public long SearchDuration { get; init; } = 550;
    public (double Low, double High) SearchSpeedRange { get; init; } = (.35, .75);
    public long SpotlightCount { get; init; } = 3;
    public Color[] Stops { get; init; } = [Color.FromHex( "ab48ff" ), Color.FromHex( "e7b2b2" ), Color.FromHex( "fffebd" )];
    public long[] Steps { get; init; } = [12];
    public GradientDirection GradientDirection { get; init; } = GradientDirection.Vertical;
}

public sealed class Spotlights : EffectBase
{
    private readonly SpotlightsConfig config;
    private readonly Dictionary<(long X, long Y), double> distances = [];
    private readonly byte[][] brightBytes, darkBytes, expandingBrightBytes, expandingDarkBytes;
    private readonly List<int> lights = [];
    private readonly int[] atHome;
    private readonly bool[] lit;
    private readonly Coord[] lastLightCoordinates;
    private List<int> illuminated = [], current = [];
    private long lastRange = -1;
    private int lastLightCount = -1;
    private bool lastExpanding;
    private readonly (Color? Fg, Color? Bg)[] bright, dark;
    private long range, remaining;
    private bool searching = true, expanding, complete;

    public Spotlights( Terminal terminal, SpotlightsConfig config, EngineRng rng ) : base( terminal, rng )
    {
        this.config = config;
        atHome = new int[checked(( int ) (terminal.Width * terminal.Height))];
        Array.Fill( atHome, -1 );
        lit = new bool[Home.Length];
        lastLightCoordinates = new Coord[checked(( int ) config.SpotlightCount)];
        for ( int id = 0; id < Home.Length; id++ )
        {
            InputCell cell = terminal.Characters[id];
            if ( cell.Symbol != " " || cell.Foreground is not null || cell.Background is not null )
            {
                atHome[checked(( int ) ((Home[id].Row - 1) * terminal.Width + Home[id].Column - 1))] = id;
            }
        }
        bright = new (Color?, Color?)[Home.Length];
        dark = new (Color?, Color?)[Home.Length];
        brightBytes = new byte[Home.Length][];
        darkBytes = new byte[Home.Length][];
        expandingBrightBytes = new byte[Home.Length][];
        expandingDarkBytes = new byte[Home.Length][];
        long minimum = RustMath.FloorDiv( terminal.Width, 4 );
        for ( long index = 0; index < config.SpotlightCount; index++ )
        {
            int light = Engine.AddCharacter( "O", RandomCoord( outside: true ) );
            lights.Add( light );
            var targets = new List<Coord>( ) { RandomCoord( ) };
            for ( int i = 0; i < 10; i++ )
            {
                Coord next;
                do
                {
                    next = RandomCoord( );
                }
                while ( Geometry.LineLength( targets[^1], next, false ) < minimum );
                targets.Add( next );
            }

            var paths = new List<string>( );
            foreach ( Coord target in targets )
            {
                double speed = rng.Uniform( config.SearchSpeedRange.Low, config.SearchSpeedRange.High );
                string name = paths.Count.ToString( System.Globalization.CultureInfo.InvariantCulture );
                Coord control = RandomCoord( outside: true );
                Engine.AddPath( light, name, new( [new Waypoint( "0", target, [control] )], speed, new( EaseKind.InOutQuad ) ) );
                paths.Add( name );
            }

            ChainPaths( light, paths.ToArray( ), loop: true );
            Engine.AddPath( light, "center", new( [new Waypoint( "0", Center )], .5, new( EaseKind.InOutSine ) ) );
        }

        var gradient = new Gradient( config.Stops, config.Steps );
        Color gray = Color.FromHex( "#808080" );
        foreach ( int id in InputOrder )
        {
            InputCell cell = terminal.Characters[id];
            bright[id] = terminal.Config.ExistingColors == ExistingColors.Dynamic ? (cell.Foreground ?? gray, cell.Background) : (FinalColor( id, gradient, config.GradientDirection ), null);
            dark[id] = Adjust( bright[id], .2 );
            terminal.Visible[id] = true;
            brightBytes[id] = VisualBytes( id, cell.Symbol, bright[id].Fg, bright[id].Bg );
            darkBytes[id] = VisualBytes( id, cell.Symbol, dark[id].Fg, dark[id].Bg );
            bool neutral = terminal.Config.ExistingColors == ExistingColors.Dynamic && cell.Foreground is null;
            expandingBrightBytes[id] = neutral ? VisualBytes( id, cell.Symbol, null, cell.Background ) : brightBytes[id];
            expandingDarkBytes[id] = neutral ? expandingBrightBytes[id] : darkBytes[id];
            terminal.VisualBytes[id] = darkBytes[id];
        }

        long dimension = Math.Min( terminal.Width, terminal.Height );
        range = Math.Max( ( long ) Math.Min( Math.Floor( dimension / config.BeamWidthRatio ), dimension ), 1 );
        remaining = config.SearchDuration;
        foreach ( int light in lights )
        {
            Engine.ActivatePath( light, "0" );
            Engine.Activate( light );
        }
    }

    private static (Color? Fg, Color? Bg) Adjust( (Color? Fg, Color? Bg) pair, double factor ) => (pair.Fg is { } fg ? Color.AdjustBrightness( fg, factor ) : null, pair.Bg is { } bg ? Color.AdjustBrightness( bg, factor ) : null);

    private void Paint( int id, (Color? Fg, Color? Bg) pair )
    {
        InputCell cell = Terminal.Characters[id];
        if ( expanding && Terminal.Config.ExistingColors == ExistingColors.Dynamic && cell.Foreground is null )
        {
            pair = (null, cell.Background);
        }

        Appearance( id, pair.Fg, pair.Bg );
    }

    private void Illuminate( )
    {
        bool changed = lastRange != range || lastLightCount != lights.Count || lastExpanding != expanding;
        for ( int index = 0; index < lights.Count; index++ )
        {
            Coord coordinate = Terminal.Coordinates[lights[index]];
            changed |= lastLightCoordinates[index] != coordinate;
            lastLightCoordinates[index] = coordinate;
        }

        if ( !changed )
        {
            return;
        }

        lastRange = range;
        lastLightCount = lights.Count;
        lastExpanding = expanding;
        current.Clear( );
        double a2 = RustMath.Square( range );
        double b2 = RustMath.Square( range / 2.0 );
        foreach ( int light in lights )
        {
            Coord center = Terminal.Coordinates[light];
            for ( long x = Math.Max( 1, center.Column - range ); x <= Math.Min( Terminal.Width, center.Column + range ); x++ )
            {
                double component = RustMath.Square( x - center.Column ) / a2;
                long offset = ( long ) RustMath.PowHalf( b2 * (1.0 - component) );
                for ( long y = Math.Max( 1, center.Row - offset ); y <= Math.Min( Terminal.Height, center.Row + offset ); y++ )
                {
                    int id = atHome[checked(( int ) ((y - 1) * Terminal.Width + x - 1))];
                    if ( id >= 0 && !lit[id] )
                    {
                        lit[id] = true;
                        current.Add( id );
                    }
                }
            }
        }

        foreach ( int id in illuminated )
        {
            if ( !lit[id] )
            {
                Terminal.VisualBytes[id] = expanding ? expandingDarkBytes[id] : darkBytes[id];
            }
        }

        double edge = range * (1.0 - config.BeamFalloff);
        bool narrow = Terminal.Width <= 500_000_000 && Terminal.Height <= 500_000_000;
        foreach ( int light in lights )
        {
            Coord coordinate = Terminal.Coordinates[light];
            narrow &= coordinate.Column is >= -500_000_000 and <= 500_000_000 && coordinate.Row is >= -500_000_000 and <= 500_000_000;
        }

        foreach ( int id in current )
        {
            ulong best = ulong.MaxValue;
            Int128 wideBest = Int128.MaxValue;
            Coord nearest = default;
            foreach ( int light in lights )
            {
                Coord coordinate = Terminal.Coordinates[light];
                if ( narrow )
                {
                    long dx = Home[id].Column - coordinate.Column, dy = Home[id].Row - coordinate.Row;
                    ulong squared = ( ulong ) (dx * dx + 4 * dy * dy);
                    if ( squared < best )
                    {
                        best = squared;
                        nearest = coordinate;
                    }
                }
                else
                {
                    Int128 dx = ( Int128 ) Home[id].Column - coordinate.Column;
                    Int128 dy = ( Int128 ) Home[id].Row - coordinate.Row;
                    Int128 squared = dx * dx + 4 * dy * dy;
                    if ( squared < wideBest )
                    {
                        wideBest = squared;
                        nearest = coordinate;
                    }
                }
            }

            var delta = (unchecked(Home[id].Column - nearest.Column), unchecked(Home[id].Row - nearest.Row));
            if ( !distances.TryGetValue( delta, out double distance ) )
            {
                distance = Geometry.LineLength( nearest, Home[id], true );
                if ( distances.Count == 16384 )
                {
                    distances.Clear( );
                }

                distances.Add( delta, distance );
            }

            if ( distance > edge )
            {
                Paint( id, Adjust( bright[id], RustMath.Max( 1.0 - (distance - edge) / (range * config.BeamFalloff), .2 ) ) );
            }
            else
            {
                Terminal.VisualBytes[id] = expanding ? expandingBrightBytes[id] : brightBytes[id];
            }
            lit[id] = false;
        }

        (illuminated, current) = (current, illuminated);
    }

    public override bool NextFrame( )
    {
        if ( complete )
        {
            return false;
        }

        Illuminate( );
        if ( searching && --remaining == 0 )
        {
            foreach ( int light in lights )
            {
                Engine.ActivatePath( light, "center" );
            }

            searching = false;
        }

        if ( !lights.Any( Engine.PathIsActive ) )
        {
            while ( lights.Count > 1 )
            {
                lights.RemoveAt( lights.Count - 1 );
            }

            expanding = true;
            range++;
            complete = range > Math.Floor( Math.Max( Terminal.Width, Terminal.Height ) / 1.5 );
        }

        Engine.Update( );
        return true;
    }
}
