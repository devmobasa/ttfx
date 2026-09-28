namespace Ttfx.Core;

public sealed record ExpandConfig
{
    public Easing ExpandEasing { get; init; } = new( EaseKind.InOutQuart );
    public double MovementSpeed { get; init; } = .35;
    public Color[] Stops { get; init; } = [Color.FromHex( "8A008A" ), Color.FromHex( "00D1FF" ), Color.FromHex( "FFFFFF" )];
    public long[] Steps { get; init; } = [12];
    public GradientDirection GradientDirection { get; init; } = GradientDirection.Vertical;
}

public sealed class Expand : EffectBase
{
    public Expand( Terminal terminal, ExpandConfig config, EngineRng rng ) : base( terminal, rng )
    {
        var gradient = new Gradient( config.Stops, config.Steps );
        foreach ( int id in InputOrder )
        {
            InputCell cell = terminal.Characters[id];
            terminal.Coordinates[id] = Center;
            terminal.Visible[id] = true;
            Engine.AddPath( id, "home", new( [new Waypoint( "0", Home[id] )], config.MovementSpeed, config.ExpandEasing ) );
            Engine.Register( id, EngineEvent.PathActivated, new( CallerKind.Path, "home" ), static ( engine, character ) => engine.SetLayer( character, 1 ) );
            Engine.Register( id, EngineEvent.PathComplete, new( CallerKind.Path, "home" ), static ( engine, character ) => engine.SetLayer( character, 0 ) );
            Engine.ActivatePath( id, "home" );
            bool dynamic = terminal.Config.ExistingColors == ExistingColors.Dynamic;
            Gradient? fg = dynamic ? (cell.Foreground is { } f ? MakeGradient( [gradient.Spectrum[0], f], [10] ) : null) : MakeGradient( [gradient.Spectrum[0], FinalColor( id, gradient, config.GradientDirection )], [10] );
            Gradient? bg = dynamic && cell.Background is { } b ? MakeGradient( [gradient.Spectrum[0], b], [10] ) : null;
            Engine.AddScene( id, "gradient", fg is not null || bg is not null ? GradientScene( id, [cell.Symbol], dynamic ? 1 : 5, fg, bg, sync: SyncMetric.Distance ) : new( [new( VisualBytes( id, cell.Symbol ), 1 )], sync: SyncMetric.Distance ) );
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

        Engine.Update( );
        return true;
    }
}
