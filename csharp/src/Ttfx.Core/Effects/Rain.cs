namespace Ttfx.Core;

public sealed record RainConfig
{
    public Color[] RainColors { get; init; } = [Color.FromHex( "00315C" ), Color.FromHex( "004C8F" ), Color.FromHex( "0075DB" ), Color.FromHex( "3F91D9" ), Color.FromHex( "78B9F2" ), Color.FromHex( "9AC8F5" ), Color.FromHex( "B8D8F8" ), Color.FromHex( "E3EFFC" )];
    public (double Low, double High) MovementSpeed { get; init; } = (.33, .57);
    public string[] Symbols { get; init; } = ["o", ".", ",", "*", "|"];
    public Easing MovementEase { get; init; } = new( EaseKind.InQuart );
    public Color[] Stops { get; init; } = [Color.FromHex( "488bff" ), Color.FromHex( "b2e7de" ), Color.FromHex( "57eaf7" )];
    public long[] Steps { get; init; } = [12];
    public GradientDirection GradientDirection { get; init; } = GradientDirection.Diagonal;
}

public sealed class Rain : EffectBase
{
    private readonly Queue<int[]> rows;
    private readonly List<int> pending = [];

    public Rain( Terminal terminal, RainConfig config, EngineRng rng ) : base( terminal, rng )
    {
        var gradient = new Gradient( config.Stops, config.Steps );
        foreach ( int id in InputOrder )
        {
            Color color = config.RainColors[rng.ChoiceIndex( config.RainColors.Length )];
            string symbol = config.Symbols[rng.ChoiceIndex( config.Symbols.Length )];
            Engine.AddScene( id, "rain", new( [new( VisualBytes( id, symbol, color ), 1 )] ) );
            Engine.AddScene( id, "fade", FadeToFinal( id, gradient, config.GradientDirection, color, 7, 3 ) );
            Engine.ActivateScene( id, "rain" );
            double speed = rng.Uniform( config.MovementSpeed.Low, config.MovementSpeed.High );
            terminal.Coordinates[id] = new( Home[id].Column, terminal.Height );
            Engine.AddPath( id, "home", new( [new Waypoint( "0", Home[id] )], speed, config.MovementEase ) );
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

        if ( pending.Count > 0 )
        {
            long count = Rng.RandInt( 1, 2 );
            for ( long i = 0; i < count && pending.Count > 0; i++ )
            {
                int index = Rng.ChoiceIndex( pending.Count ), id = pending[index];
                pending.RemoveAt( index );
                Terminal.Visible[id] = true;
                Engine.Activate( id );
            }
        }

        Engine.Update( );
        return true;
    }
}
