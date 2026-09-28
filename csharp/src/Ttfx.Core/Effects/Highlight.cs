namespace Ttfx.Core;

public sealed record HighlightConfig
{
    public double Brightness { get; init; } = 1.75;
    public CharacterGroup Direction { get; init; } = CharacterGroup.DiagonalBottomLeftToTopRight;
    public long Width { get; init; } = 8;
    public Color[] Stops { get; init; } = [Color.FromHex( "8A008A" ), Color.FromHex( "00D1FF" ), Color.FromHex( "FFFFFF" )];
    public long[] Steps { get; init; } = [12];
    public GradientDirection GradientDirection { get; init; } = GradientDirection.Vertical;
}

public sealed class Highlight : EffectBase
{
    private readonly int[][] groups;
    private readonly EasingTracker tracker = new( new( EaseKind.InOutCirc ), 100, true );
    private int revealed;

    public Highlight( Terminal terminal, HighlightConfig config, EngineRng rng ) : base( terminal, rng )
    {
        groups = terminal.Groups( config.Direction );
        var gradient = new Gradient( config.Stops, config.Steps );
        foreach ( int id in InputOrder )
        {
            InputCell cell = terminal.Characters[id];
            bool dynamic = terminal.Config.ExistingColors == ExistingColors.Dynamic;
            Color? fg = dynamic ? cell.Foreground : FinalColor( id, gradient, config.GradientDirection );
            Color? bg = dynamic ? cell.Background : null;
            Appearance( id, fg, bg );
            SceneDefinition scene;
            if ( fg is { } color )
            {
                Color bright = Color.AdjustBrightness( color, config.Brightness );
                var highlight = new Gradient( [color, bright, bright, color], [3, config.Width, 3] );
                scene = new( highlight.Spectrum.Select( c => new SceneFrame( VisualBytes( id, cell.Symbol, c, bg ), 2 ) ) );
            }
            else
            {
                scene = new( [new( VisualBytes( id, cell.Symbol, fg, bg ), 2 )] );
            }

            Engine.AddScene( id, "highlight", scene );
            terminal.Visible[id] = true;
        }
    }

    public override bool NextFrame( )
    {
        if ( tracker.IsComplete && !Engine.HasActive )
        {
            return false;
        }

        int next = ( int ) (tracker.Step( ) * groups.Length);
        for ( int group = revealed; group < next; group++ )
        {
            foreach ( int id in groups[group] )
            {
                Engine.ActivateScene( id, "highlight" );
                Engine.Activate( id );
            }
        }

        revealed = next;
        Engine.Update( );
        return true;
    }
}
