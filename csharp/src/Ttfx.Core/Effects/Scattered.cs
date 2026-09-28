namespace Ttfx.Core;

public sealed record ScatteredConfig
{
    public double MovementSpeed { get; init; } = .5;
    public Easing MovementEase { get; init; } = new( EaseKind.InOutBack );
    public long Frames { get; init; } = 9;
    public Color[] Stops { get; init; } = [Color.FromHex( "ff9048" ), Color.FromHex( "ab9dff" ), Color.FromHex( "bdffea" )];
    public long[] Steps { get; init; } = [12];
    public GradientDirection GradientDirection { get; init; } = GradientDirection.Vertical;
}

public sealed class Scattered : EffectBase
{
    private long hold = 25;

    public Scattered( Terminal terminal, ScatteredConfig config, EngineRng rng ) : base( terminal, rng )
    {
        var gradient = new Gradient( config.Stops, config.Steps );
        foreach ( int id in InputOrder )
        {
            InputCell cell = terminal.Characters[id];
            terminal.Coordinates[id] = terminal.Width < 2 || terminal.Height < 2 ? new( 1, 1 ) : RandomCoord( );
            terminal.Visible[id] = true;
            Engine.AddPath( id, "home", new( [new Waypoint( "0", Home[id] )], config.MovementSpeed, config.MovementEase ) );
            Engine.Register( id, EngineEvent.PathActivated, new( CallerKind.Path, "home" ), static ( engine, character ) => engine.SetLayer( character, 1 ) );
            Engine.Register( id, EngineEvent.PathComplete, new( CallerKind.Path, "home" ), static ( engine, character ) => engine.SetLayer( character, 0 ) );
            Engine.ActivatePath( id, "home" );
            SceneDefinition scene = terminal.Config.ExistingColors == ExistingColors.Dynamic ? new( [new( VisualBytes( id, cell.Symbol, cell.Foreground, cell.Background ), config.Frames )], sync: SyncMetric.Distance ) : GradientScene( id, [cell.Symbol], config.Frames, MakeGradient( [gradient.Spectrum[0], FinalColor( id, gradient, config.GradientDirection )], [10] ), sync: SyncMetric.Distance );
            Engine.AddScene( id, "gradient", scene );
            Engine.ActivateScene( id, "gradient" );
            Engine.Activate( id );
        }
    }

    public override bool NextFrame( )
    {
        if ( !Engine.HasActive )
        {
            return false;
        }

        if ( hold > 0 )
        {
            hold--;
        }
        else
        {
            Engine.Update( );
        }

        return true;
    }
}
