namespace Ttfx.Core;

public sealed record MiddleOutConfig
{
    public Color StartingColor { get; init; } = Color.FromHex( "ffffff" );
    public bool Horizontal { get; init; }
    public double CenterSpeed { get; init; } = .6;
    public double FullSpeed { get; init; } = .6;
    public Easing CenterEase { get; init; } = new( EaseKind.InOutSine );
    public Easing FullEase { get; init; } = new( EaseKind.InOutSine );
    public Color[] Stops { get; init; } = [Color.FromHex( "8A008A" ), Color.FromHex( "00D1FF" ), Color.FromHex( "FFFFFF" )];
    public long[] Steps { get; init; } = [12];
    public GradientDirection GradientDirection { get; init; } = GradientDirection.Vertical;
}

public sealed class MiddleOut : EffectBase
{
    private bool full;

    public MiddleOut( Terminal terminal, MiddleOutConfig config, EngineRng rng ) : base( terminal, rng )
    {
        var gradient = new Gradient( config.Stops, config.Steps );
        foreach ( int id in InputOrder )
        {
            InputCell cell = terminal.Characters[id];
            terminal.Coordinates[id] = Center;
            Engine.AddPath( id, "center", new( [new Waypoint( "0", config.Horizontal ? new( Center.Column, Home[id].Row ) : new( Home[id].Column, Center.Row ) )], config.CenterSpeed, config.CenterEase ) );
            Engine.AddPath( id, "full", new( [new Waypoint( "full", Home[id] )], config.FullSpeed, config.FullEase ) );
            bool dynamic = terminal.Config.ExistingColors == ExistingColors.Dynamic;
            Gradient? fg = dynamic ? (cell.Foreground is { } f ? MakeGradient( [config.StartingColor, f], [10] ) : null) : MakeGradient( [config.StartingColor, FinalColor( id, gradient, config.GradientDirection )], [10] );
            Gradient? bg = dynamic && cell.Background is { } b ? MakeGradient( [config.StartingColor, b], [10] ) : null;
            Engine.AddScene( id, "full", fg is not null || bg is not null ? GradientScene( id, [cell.Symbol], 6, fg, bg ) : new( [new( VisualBytes( id, cell.Symbol ), 6 )] ) );
            Engine.ActivatePath( id, "center" );
            Appearance( id, config.StartingColor );
            terminal.Visible[id] = true;
            Engine.Activate( id );
        }
    }

    public override bool NextFrame( )
    {
        if ( !full && !Engine.HasActive )
        {
            full = true;
            foreach ( int id in InputOrder.Order( ) )
            {
                Engine.Activate( id );
                Engine.ActivatePath( id, "full" );
                Engine.ActivateScene( id, "full" );
            }
        }

        if ( !Engine.HasActive )
        {
            return false;
        }

        Engine.Update( );
        return true;
    }
}
