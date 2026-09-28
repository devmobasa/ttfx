namespace Ttfx.Core;

public sealed record FireworksConfig
{
    public bool Anywhere { get; init; }
    public Color[] Colors { get; init; } = [Color.FromHex( "88F7E2" ), Color.FromHex( "44D492" ), Color.FromHex( "F5EB67" ), Color.FromHex( "FFA15C" ), Color.FromHex( "FA233E" )];
    public string Symbol { get; init; } = "o";
    public double Volume { get; init; } = .05;
    public long Delay { get; init; } = 45;
    public double Distance { get; init; } = .2;
    public Color[] Stops { get; init; } = [Color.FromHex( "8A008A" ), Color.FromHex( "00D1FF" ), Color.FromHex( "FFFFFF" )];
    public long[] Steps { get; init; } = [12];
    public GradientDirection GradientDirection { get; init; } = GradientDirection.Horizontal;
}

public sealed class Fireworks : EffectBase
{
    private readonly FireworksConfig config;
    private readonly List<int[]> shells = [];
    private long delay;

    public Fireworks( Terminal terminal, FireworksConfig config, EngineRng rng ) : base( terminal, rng )
    {
        this.config = config;
        long volume = Math.Max( 1, RustMath.RoundHalfEven( config.Volume * terminal.InputCount ) );
        long distance = Math.Min( 15, Math.Max( 1, RustMath.RoundHalfEven( terminal.Width * config.Distance ) ) );
        var shell = new List<int>( );
        long x = 0;
        Coord origin = default;
        List<Coord> explosions = [];
        foreach ( int id in InputOrder )
        {
            if ( shell.Count == volume || shell.Count == 0 )
            {
                x = rng.RandInt( 0, terminal.Width - 1 );
                shells.Add( shell.ToArray( ) );
                shell.Clear( );
                long y = rng.RandInt( config.Anywhere ? 1 : Home[id].Row, terminal.Height );
                origin = new( x, y );
                explosions = Geometry.InCircle( origin, distance );
            }

            terminal.Coordinates[id] = new( x, 1 );
            Engine.AddPath( id, "apex", new( [new Waypoint( "0", origin )], .35, new( EaseKind.OutExpo ), 2 ) );
            double speed = rng.Uniform( .2, .4 );
            Coord explosion = explosions[rng.ChoiceIndex( explosions.Count )];
            Coord control = Geometry.Extrapolate( origin, explosion, RustMath.FloorDiv( distance, 2 ) );
            Coord bloom = new( control.Column, Math.Max( 1, control.Row - 7 ) );
            Engine.AddPath( id, "explode", new( [new Waypoint( "0", explosion ), new Waypoint( "1", bloom, [control] )], speed, new( EaseKind.OutCirc ), 2 ) );
            Engine.AddPath( id, "home", new( [new Waypoint( "0", Home[id], [new( bloom.Column, 1 )] )], .6, new( EaseKind.InOutQuart ), 2 ) );
            ChainPaths( id, ["apex", "explode", "home"] );
            Engine.Register( id, EngineEvent.PathComplete, new( CallerKind.Path, "home" ), static ( engine, character ) => engine.SetLayer( character, 0 ) );
            Engine.ActivatePath( id, "apex" );
            shell.Add( id );
        }

        if ( shell.Count > 0 )
        {
            shells.Add( shell.ToArray( ) );
        }

        var final = new Gradient( config.Stops, config.Steps );
        Color white = Color.FromHex( "ffffff" );
        foreach ( int[] group in shells )
        {
            Color color = config.Colors[rng.ChoiceIndex( config.Colors.Length )];
            var bloom = new Gradient( [color, white, color], [5] );
            foreach ( int id in group )
            {
                Engine.AddScene( id, "launch", new( [new( VisualBytes( id, config.Symbol, color ), 2 ), new( VisualBytes( id, config.Symbol, white ), 1 )], loop: true ) );
                Engine.AddScene( id, "bloom", GradientScene( id, [terminal.Characters[id].Symbol], 2, bloom, sync: SyncMetric.Step ) );
                Engine.AddScene( id, "fall", FadeToFinal( id, final, config.GradientDirection, color, 15, 10 ) );
                Engine.ActivateScene( id, "launch" );
                Engine.Register( id, EngineEvent.PathComplete, new( CallerKind.Path, "apex" ), static ( engine, character ) => engine.ActivateScene( character, "bloom" ) );
                Engine.Register( id, EngineEvent.PathActivated, new( CallerKind.Path, "home" ), static ( engine, character ) => engine.ActivateScene( character, "fall" ) );
            }
        }
    }

    public override bool NextFrame( )
    {
        if ( shells.Count == 0 && !Engine.HasActive )
        {
            return false;
        }

        if ( shells.Count > 0 && delay <= 0 )
        {
            int[] group = shells[^1];
            shells.RemoveAt( shells.Count - 1 );
            foreach ( int id in group )
            {
                Terminal.Visible[id] = true;
                Engine.Activate( id );
            }

            delay = ( long ) (config.Delay * Rng.Uniform( .5, 1.5 ));
        }

        delay--;
        Engine.Update( );
        return true;
    }
}
