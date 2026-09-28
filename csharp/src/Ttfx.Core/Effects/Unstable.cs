namespace Ttfx.Core;

public sealed record UnstableConfig
{
    public Color UnstableColor { get; init; } = Color.FromHex( "ff9200" );
    public Easing ExplosionEase { get; init; } = new( EaseKind.OutExpo );
    public double ExplosionSpeed { get; init; } = 1;
    public Easing ReassemblyEase { get; init; } = new( EaseKind.OutExpo );
    public double ReassemblySpeed { get; init; } = 1;
    public Color[] Stops { get; init; } = [Color.FromHex( "8A008A" ), Color.FromHex( "00D1FF" ), Color.FromHex( "FFFFFF" )];
    public long[] Steps { get; init; } = [12];
    public GradientDirection GradientDirection { get; init; } = GradientDirection.Vertical;
}

public sealed class Unstable : EffectBase
{
    private readonly Coord[] jumbled;
    private readonly bool[] active;
    private int phase, rumble;
    private long modDelay = 18, hold = 30;
    private bool restore;

    public Unstable( Terminal terminal, UnstableConfig config, EngineRng rng ) : base( terminal, rng )
    {
        var final = new Gradient( config.Stops, config.Steps );
        jumbled = new Coord[terminal.InputCount];
        active = new bool[terminal.InputCount];
        var available = new OrderedSelection<Coord>( InputOrder.Select( id => Home[id] ) );
        Color gray = Color.FromHex( "808080" );
        bool dynamic = terminal.Config.ExistingColors == ExistingColors.Dynamic;
        foreach ( int id in InputOrder )
        {
            Coord target = rng.RandInt( 0, 3 ) switch
            {
                0 => new( 1, rng.RandInt( 1, terminal.Height ) ),
                1 => new( terminal.Width, rng.RandInt( 1, terminal.Height ) ),
                2 => new( rng.RandInt( 1, terminal.Width ), 1 ),
                _ => new( rng.RandInt( 1, terminal.Width ), terminal.Height )
            };
            int index = rng.ChoiceIndex( available.Count );
            jumbled[id] = available.TakeAt( index );
            terminal.Coordinates[id] = jumbled[id];
            Engine.AddPath( id, "explosion", new( [new Waypoint( "0", target )], config.ExplosionSpeed, config.ExplosionEase ) );
            Engine.AddPath( id, "reassembly", new( [new Waypoint( "0", Home[id] )], config.ReassemblySpeed, config.ReassemblyEase ) );
            InputCell cell = terminal.Characters[id];
            Color start = dynamic ? cell.Foreground ?? gray : FinalColor( id, final, config.GradientDirection );
            Gradient fg = MakeGradient( [start, config.UnstableColor], [12] );
            Gradient? bg = dynamic && cell.Background is { } b ? MakeGradient( [b, config.UnstableColor], [12] ) : null;
            Engine.AddScene( id, "rumble", GradientScene( id, [cell.Symbol], 10, fg, bg ) );
            SceneDefinition ending = FadeToFinal( id, final, config.GradientDirection, config.UnstableColor, 12, 3 );
            if ( dynamic && cell.Foreground is null )
            {
                if ( cell.Background is null )
                {
                    SceneDefinition neutral = GradientScene( id, [cell.Symbol], 3, MakeGradient( [config.UnstableColor, gray], [12] ) );
                    ending = new( [.. neutral.Frames, new( VisualBytes( id, cell.Symbol ), 3 )] );
                }
                else
                {
                    ending = new( [.. ending.Frames, new( VisualBytes( id, cell.Symbol, null, cell.Background ), 3 )] );
                }
            }

            Engine.AddScene( id, "final", ending );
            Engine.ActivateScene( id, "rumble" );
            if ( dynamic )
            {
                Appearance( id, start, cell.Background );
            }

            terminal.Visible[id] = true;
        }
    }

    public override bool NextFrame( )
    {
        if ( restore )
        {
            foreach ( int id in InputOrder )
            {
                Terminal.Coordinates[id] = jumbled[id];
            }

            restore = false;
        }

        if ( phase == 0 )
        {
            if ( rumble < 150 )
            {
                if ( rumble > 30 && rumble % modDelay == 0 )
                {
                    long row = Rng.ChoiceIndex( 3 ) - 1, column = Rng.ChoiceIndex( 3 ) - 1;
                    foreach ( int id in InputOrder )
                    {
                        Coord coord = Terminal.Coordinates[id];
                        Terminal.Coordinates[id] = new( coord.Column + column, coord.Row + row );
                    }

                    restore = true;
                    modDelay = Math.Max( modDelay - 1, 1 );
                }

                foreach ( int id in InputOrder )
                {
                    Engine.Tick( id );
                }

                rumble++;
                return true;
            }

            phase = 1;
            foreach ( int id in InputOrder )
            {
                Engine.ActivatePath( id, "explosion" );
                active[id] = true;
            }
        }

        if ( phase == 1 )
        {
            if ( active.Any( value => value ) )
            {
                for ( int id = 0; id < active.Length; id++ )
                {
                    if ( active[id] )
                    {
                        Engine.Tick( id );
                        active[id] = Terminal.Coordinates[id] != Engine.GetPath( id, "explosion" ).Waypoints[0].Coordinate;
                    }
                }

                return true;
            }

            if ( hold != 0 )
            {
                hold--;
                return true;
            }

            phase = 2;
            foreach ( int id in InputOrder )
            {
                Engine.ActivateScene( id, "final" );
                active[id] = true;
                Engine.ActivatePath( id, "reassembly" );
            }
        }

        bool any = active.Any( value => value );
        if ( any )
        {
            for ( int id = 0; id < active.Length; id++ )
            {
                if ( active[id] )
                {
                    Engine.Tick( id );
                    active[id] = Terminal.Coordinates[id] != Home[id] || Engine.SceneIsActive( id );
                }
            }
        }

        return any;
    }
}
