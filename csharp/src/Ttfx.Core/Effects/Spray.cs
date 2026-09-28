namespace Ttfx.Core;

public sealed record SprayConfig
{
    public string Position { get; init; } = "e";
    public double Volume { get; init; } = .005;
    public (double Low, double High) MovementSpeed { get; init; } = (.6, 1.4);
    public Easing MovementEase { get; init; } = new( EaseKind.OutExpo );
    public Color[] Stops { get; init; } = [Color.FromHex( "8A008A" ), Color.FromHex( "00D1FF" ), Color.FromHex( "FFFFFF" )];
    public long[] Steps { get; init; } = [12];
    public GradientDirection GradientDirection { get; init; } = GradientDirection.Vertical;
}

public sealed class Spray : EffectBase
{
    private readonly List<int> pending;
    private readonly long volume;

    public Spray( Terminal terminal, SprayConfig config, EngineRng rng ) : base( terminal, rng )
    {
        var gradient = new Gradient( config.Stops, config.Steps );
        Coord origin = config.Position switch
        {
            "center" => Center,
            "n" => new( RustMath.FloorDiv( terminal.Width, 2 ), terminal.Height ),
            "nw" => new( 1, terminal.Height ),
            "w" => new( 1, RustMath.FloorDiv( terminal.Height, 2 ) ),
            "sw" => new( 1, 1 ),
            "s" => new( RustMath.FloorDiv( terminal.Width, 2 ), 1 ),
            "se" => new( terminal.Width - 1, 1 ),
            "ne" => new( terminal.Width - 1, terminal.Height ),
            _ => new( terminal.Width - 1, RustMath.FloorDiv( terminal.Height, 2 ) )
        };
        foreach ( int id in InputOrder )
        {
            double speed = rng.Uniform( config.MovementSpeed.Low, config.MovementSpeed.High );
            terminal.Coordinates[id] = origin;
            Engine.AddPath( id, "home", new( [new Waypoint( "0", Home[id] )], speed, config.MovementEase ) );
            Engine.Register( id, EngineEvent.PathActivated, new( CallerKind.Path, "home" ), static ( engine, character ) => engine.SetLayer( character, 1 ) );
            Engine.Register( id, EngineEvent.PathComplete, new( CallerKind.Path, "home" ), static ( engine, character ) => engine.SetLayer( character, 0 ) );
            InputCell cell = terminal.Characters[id];
            SceneDefinition scene;
            if ( terminal.Config.ExistingColors == ExistingColors.Dynamic )
            {
                scene = new( Enumerable.Repeat( new SceneFrame( VisualBytes( id, cell.Symbol, cell.Foreground, cell.Background ), 20 ), 7 ) );
            }
            else
            {
                Color start = gradient.Spectrum[rng.ChoiceIndex( gradient.Spectrum.Length )];
                scene = FadeToFinal( id, gradient, config.GradientDirection, start, 7, 20 );
            }

            Engine.AddScene( id, "droplet", scene );
            Engine.ActivateScene( id, "droplet" );
            Engine.ActivatePath( id, "home" );
        }

        pending = [.. InputOrder];
        rng.Shuffle( System.Runtime.InteropServices.CollectionsMarshal.AsSpan( pending ) );
        volume = Math.Max( ( long ) (pending.Count * config.Volume), 1 );
    }

    public override bool NextFrame( )
    {
        if ( pending.Count == 0 && !Engine.HasActive )
        {
            return false;
        }

        if ( pending.Count > 0 )
        {
            long count = Rng.RandInt( 1, volume );
            for ( long i = 0; i < count && pending.Count > 0; i++ )
            {
                int id = pending[^1];
                pending.RemoveAt( pending.Count - 1 );
                Terminal.Visible[id] = true;
                Engine.Activate( id );
            }
        }

        Engine.Update( );
        return true;
    }
}
