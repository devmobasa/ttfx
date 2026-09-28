namespace Ttfx.Core;

public enum SlideGrouping
{
    Row, Column, Diagonal
}

public sealed record SlideConfig
{
    public double MovementSpeed { get; init; } = 0.8;
    public SlideGrouping Grouping { get; init; }
    public long Gap { get; init; } = 2;
    public bool ReverseDirection { get; init; }
    public bool Merge { get; init; }
    public Easing MovementEase { get; init; } = new( EaseKind.InOutQuad );
    public Color[] Stops { get; init; } = [Color.FromHex( "833ab4" ), Color.FromHex( "fd1d1d" ), Color.FromHex( "fcb045" )];
    public long[] Steps { get; init; } = [12];
    public long Frames { get; init; } = 6;
    public GradientDirection GradientDirection { get; init; }
}

public sealed class Slide : IEffect
{
    private readonly Terminal terminal;
    private readonly SlideConfig config;
    private readonly int[][] groups;
    private readonly int[] groupPositions;
    private readonly Engine engine;
    private int nextGroup;
    private long gap;

    public Slide( Terminal terminal, SlideConfig config )
    {
        this.terminal = terminal;
        this.config = config;
        if ( !(config.MovementSpeed > 0) )
        {
            throw new EngineException( "Path speed must be greater than 0." );
        }

        if ( config.Frames < 1 )
        {
            throw new EngineException( $"Frame duration must be at least 1. Received: {config.Frames}" );
        }

        CharacterGroup grouping = config.Grouping switch
        {
            SlideGrouping.Row => CharacterGroup.RowTopToBottom,
            SlideGrouping.Column => CharacterGroup.ColumnLeftToRight,
            _ => CharacterGroup.DiagonalTopLeftToBottomRight
        };
        groups = terminal.Groups( grouping );
        groupPositions = new int[groups.Length];
        var destinations = terminal.Coordinates[..terminal.Count];
        engine = new( terminal );
        var gradient = new Gradient( config.Stops, config.Steps );
        var memo = new Dictionary<(string, Color?, Color?, bool), SceneDefinition>( );

        for ( int g = 0; g < groups.Length; g++ )
        {
            int[] group = groups[g];
            bool flip = config.Merge ? g % 2 == 0 : config.ReverseDirection;
            Coord diagonal = default;
            if ( config.Grouping == SlideGrouping.Diagonal )
            {
                Coord edge = flip ? destinations[group[0]] : destinations[group[^1]];
                long distance = flip ? terminal.Height + 1 - edge.Row : edge.Row;
                diagonal = flip ? new( edge.Column + distance, edge.Row + distance ) : new( edge.Column - distance, edge.Row - distance );
            }

            foreach ( int id in group )
            {
                Coord dest = destinations[id];
                Coord start = config.Grouping switch
                {
                    SlideGrouping.Row => new( flip ? terminal.Width + 1 : 0, dest.Row ),
                    SlideGrouping.Column => new( dest.Column, flip ? 0 : terminal.Height + 1 ),
                    _ => diagonal
                };
                terminal.Coordinates[id] = start;
                engine.AddPath( id, "final", new( [new Waypoint( "final", dest )], config.MovementSpeed, config.MovementEase ) );
                engine.ActivatePath( id, "final" );
                InputCell cell = terminal.Characters[id];
                bool dynamic = terminal.Config.ExistingColors == ExistingColors.Dynamic;
                bool always = terminal.Config.ExistingColors == ExistingColors.Always;
                Color? fg = dynamic || always ? cell.Foreground : gradient.AtCoordinate( terminal.TextBottom, terminal.TextTop,
                    terminal.TextLeft, terminal.TextRight, dest, config.GradientDirection );
                Color? bg = dynamic || always ? cell.Background : null;
                bool bold = always && cell.Bold;
                var key = (cell.Symbol, fg, bg, bold);
                if ( !memo.TryGetValue( key, out SceneDefinition? scene ) )
                {
                    byte[][] bytes;
                    if ( dynamic )
                    {
                        bytes = [engine.Intern( new Visual( cell.Symbol, fg, bg ) )];
                    }
                    else
                    {
                        Color final = gradient.AtCoordinate( terminal.TextBottom, terminal.TextTop, terminal.TextLeft, terminal.TextRight, dest, config.GradientDirection );
                        var spectrum = new Gradient( [config.Stops[0], final], [10] );
                        bytes = spectrum.Spectrum.Select( color => engine.Intern( new Visual( cell.Symbol, always ? fg : color, bg, bold ) ) ).ToArray( );
                    }

                    scene = new( bytes.Select( visual => new SceneFrame( visual, config.Frames ) ) );
                    memo.Add( key, scene );
                }

                engine.AddScene( id, "gradient", scene );
                engine.ActivateScene( id, "gradient" );
            }

            if ( config.Grouping == SlideGrouping.Diagonal ? flip : !flip )
            {
                Array.Reverse( group );
            }
        }
    }

    public bool NextFrame( )
    {
        bool pendingGroups = nextGroup < groups.Length;
        bool releasing = false;

        for ( int g = 0; g < nextGroup; g++ )
        {
            releasing |= groupPositions[g] < groups[g].Length;
        }

        if ( !pendingGroups && !releasing && !engine.HasActive )
        {
            return false;
        }

        if ( pendingGroups )
        {
            if ( gap == config.Gap )
            {
                nextGroup++;
                gap = 0;
            }
            else
            {
                gap++;
            }
        }

        for ( int g = 0; g < nextGroup; g++ )
        {
            if ( groupPositions[g] == groups[g].Length )
            {
                continue;
            }

            int id = groups[g][groupPositions[g]++];
            terminal.Visible[id] = true;
            engine.Activate( id );
        }

        engine.Update( );

        return true;
    }
}
