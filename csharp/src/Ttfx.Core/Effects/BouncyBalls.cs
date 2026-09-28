namespace Ttfx.Core;

public sealed record BouncyBallsConfig
{
    public Color[] BallColors { get; init; } = [Color.FromHex( "d1f4a5" ), Color.FromHex( "96e2a4" ), Color.FromHex( "5acda9" )];
    public string[] Symbols { get; init; } = ["*", "o", "O", "0", "."];
    public long Delay { get; init; } = 4;
    public double MovementSpeed { get; init; } = .45;
    public Easing MovementEase { get; init; } = new( EaseKind.OutBounce );
    public Color[] Stops { get; init; } = [Color.FromHex( "f8ffae" ), Color.FromHex( "43c6ac" )];
    public long[] Steps { get; init; } = [12];
    public GradientDirection GradientDirection { get; init; } = GradientDirection.Diagonal;
}

public sealed class BouncyBalls : EffectBase
{

    private readonly Queue<int[]> rows;
    private readonly List<int> pending = [];
    private readonly long delay;
    private long remaining;

    public BouncyBalls( Terminal terminal, BouncyBallsConfig config, EngineRng rng ) : base( terminal, rng )
    {
        delay = config.Delay;
        var gradient = new Gradient( config.Stops, config.Steps );
        foreach ( int id in InputOrder )
        {
            Color color = config.BallColors[rng.ChoiceIndex( config.BallColors.Length )];
            string symbol = config.Symbols[rng.ChoiceIndex( config.Symbols.Length )];
            Engine.AddScene( id, "rain", new( [new( VisualBytes( id, symbol, color ), 1 )] ) );
            Engine.AddScene( id, "fade", FadeToFinal( id, gradient, config.GradientDirection, color, 10, 6 ) );
            Engine.ActivateScene( id, "rain" );
            long dropRow = ( long ) (terminal.Height * rng.Uniform( 1.0, 1.5 ));
            terminal.Coordinates[id] = new( Home[id].Column, dropRow );
            Engine.AddPath( id, "home", new( [new Waypoint( "0", Home[id] )], config.MovementSpeed, config.MovementEase ) );
            Engine.Register( id, EngineEvent.PathComplete, new( CallerKind.Path, "home" ), static ( engine, character ) => engine.ActivateScene( character, "fade" ) );
            Engine.ActivatePath( id, "home" );
        }

        rows = new( InputOrder.OrderBy( id => Home[id].Row ).GroupBy( id => Home[id].Row ).Select( group => group.ToArray( ) ) );
    }

    public override bool NextFrame( )
    {
        if ( rows.Count == 0 && pending.Count == 0 && !Engine.HasActive )
        {
            return false;
        }

        if ( pending.Count == 0 && rows.TryDequeue( out int[]? row ) )
        {
            pending.AddRange( row );
        }

        bool emitted = pending.Count > 0 && remaining == 0;
        if ( emitted )
        {
            long count = Rng.RandInt( 2, 6 );
            for ( long i = 0; i < count && pending.Count > 0; i++ )
            {
                int index = Rng.ChoiceIndex( pending.Count ), id = pending[index];
                pending.RemoveAt( index );
                Terminal.Visible[id] = true;
                Engine.Activate( id );
            }
        }

        if ( emitted )
        {
            remaining = delay;
        }
        else if ( pending.Count > 0 && remaining > 0 )
        {
            remaining--;
        }

        Engine.Update( );
        return true;
    }
}
