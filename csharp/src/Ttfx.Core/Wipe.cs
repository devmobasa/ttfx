namespace Ttfx.Core;

public sealed record WipeConfig
{
    public CharacterGroup Direction { get; init; } = CharacterGroup.DiagonalTopLeftToBottomRight;
    public long Delay { get; init; }
    public Easing Ease { get; init; } = new( EaseKind.InOutCirc );
    public Color[] Stops { get; init; } = [Color.FromHex( "833ab4" ), Color.FromHex( "fd1d1d" ), Color.FromHex( "fcb045" )];
    public long[] Steps { get; init; } = [12];
    public long Frames { get; init; } = 3;
    public GradientDirection GradientDirection { get; init; } = GradientDirection.Vertical;
}

public sealed class Wipe : IEffect
{
    private readonly Terminal terminal;
    private readonly WipeConfig config;
    private readonly int[][] groups;
    private readonly EasingTracker tracker;
    private readonly Engine engine;
    private long delay;
    private int revealed;

    public Wipe( Terminal terminal, WipeConfig config )
    {
        this.terminal = terminal;
        this.config = config;
        groups = terminal.Groups( config.Direction );
        tracker = new( config.Ease, 100, true );
        delay = config.Delay;
        engine = new( terminal );
        int count = terminal.InputCount;
        var gradient = new Gradient( config.Stops, config.Steps );
        var memo = new Dictionary<(string, Color?, Color?, bool), SceneDefinition>( );

        for ( int i = 0; i < count; i++ )
        {
            InputCell cell = terminal.Characters[i];
            bool dynamic = terminal.Config.ExistingColors == ExistingColors.Dynamic;
            bool always = terminal.Config.ExistingColors == ExistingColors.Always;
            Color? fg = dynamic || always ? cell.Foreground :
                gradient.AtCoordinate( terminal.TextBottom, terminal.TextTop, terminal.TextLeft, terminal.TextRight, terminal.Coordinates[i], config.GradientDirection );
            Color? bg = dynamic || always ? cell.Background : null;
            bool bold = always && cell.Bold;
            var key = (cell.Symbol, fg, bg, bold);
            if ( !memo.TryGetValue( key, out SceneDefinition? scene ) )
            {
                if ( config.Frames < 1 )
                {
                    throw new EngineException( $"Frame duration must be at least 1. Received: {config.Frames}" );
                }

                byte[][] bytes;
                if ( dynamic )
                {
                    long frameCount = checked(config.Steps.Sum( ) + 1);
                    byte[] visual = engine.Intern( new Visual( cell.Symbol, fg, bg ) );
                    bytes = Enumerable.Repeat( visual, checked(( int ) frameCount) ).ToArray( );
                }
                else
                {
                    // Always handling replaces gradient colors after scene construction.
                    Color final = gradient.AtCoordinate( terminal.TextBottom, terminal.TextTop, terminal.TextLeft, terminal.TextRight, terminal.Coordinates[i], config.GradientDirection );
                    var wipe = new Gradient( [gradient.Spectrum[0], final], config.Steps );
                    bytes = wipe.Spectrum.Select( color => engine.Intern( new Visual( cell.Symbol, always ? fg : color, bg, bold ) ) ).ToArray( );
                }

                scene = new( bytes.Select( visual => new SceneFrame( visual, config.Frames ) ) );
                memo.Add( key, scene );
            }

            engine.AddScene( i, "wipe", scene );
        }
    }

    public bool NextFrame( )
    {
        if ( tracker.IsComplete && !engine.HasActive )
        {
            return false;
        }

        if ( delay == 0 )
        {
            int next = ( int ) (tracker.Step( ) * groups.Length);
            if ( next > revealed )
            {
                for ( int g = revealed; g < next; g++ )
                {
                    foreach ( int id in groups[g] )
                    {
                        engine.ActivateScene( id, "wipe" );
                        engine.Activate( id );
                        terminal.Visible[id] = true;
                    }
                }
            }
            else
            {
                for ( int g = next; g < revealed; g++ )
                {
                    foreach ( int id in groups[g] )
                    {
                        engine.DeactivateScene( id, "wipe", reset: true );
                        terminal.Visible[id] = false;
                    }
                }
            }

            revealed = next;
            delay = config.Delay;
        }
        else
        {
            delay--;
        }

        engine.Update( );

        return true;
    }
}
