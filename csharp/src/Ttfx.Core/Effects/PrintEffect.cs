namespace Ttfx.Core;

public sealed record PrintEffectConfig
{
    public double ReturnSpeed { get; init; } = 1.5;
    public long Speed { get; init; } = 2;
    public Easing Ease { get; init; } = new( EaseKind.InOutQuad );
    public Color[] Stops { get; init; } = [Color.FromHex( "02b8bd" ), Color.FromHex( "c1f0e3" ), Color.FromHex( "00ffa0" )];
    public long[] Steps { get; init; } = [12];
    public GradientDirection GradientDirection { get; init; } = GradientDirection.Diagonal;
}

public sealed class PrintEffect : EffectBase
{
    private sealed class Row( int[] characters )
    {
        internal readonly OrderedQueue<int> Pending = [.. characters];
        internal readonly List<int> Typed = [];
    }

    private readonly PrintEffectConfig config;
    private readonly Queue<Row> pending = [];
    private readonly List<Row> processed = [];
    private readonly int head;
    private Row current;
    private bool typing = true;
    private long lastColumn;

    public PrintEffect( Terminal terminal, PrintEffectConfig config, EngineRng rng ) : base( terminal, rng )
    {
        this.config = config;
        head = Engine.AddCharacter( "█", new( 1, 1 ) );
        var final = new Gradient( config.Stops, config.Steps );
        bool dynamic = terminal.Config.ExistingColors == ExistingColors.Dynamic;
        Color white = Color.FromHex( "ffffff" );
        int[][] rows = Enumerable.Range( 0, Home.Length ).OrderBy( id => Home[id].Column ).GroupBy( id => Home[id].Row ).OrderByDescending( group => group.Key ).Select( group => group.ToArray( ) ).ToArray( );
        foreach ( int[] row in rows )
        {
            bool spaces = row.All( id => terminal.Characters[id].Symbol == " " );
            long extent = spaces ? Home[row[0]].Column : row.Where( id => id < terminal.InputCount ).Max( id => Home[id].Column );
            int[] chars = row.Where( id => Home[id].Column <= extent ).ToArray( );
            foreach ( int id in chars )
            {
                InputCell cell = terminal.Characters[id];
                terminal.Coordinates[id] = new( Home[id].Column, 1 );
                bool inText = Home[id].Column >= terminal.TextLeft && Home[id].Column <= terminal.TextRight && Home[id].Row >= terminal.TextBottom && Home[id].Row <= terminal.TextTop;
                Color? fg = dynamic ? cell.Foreground : inText ? FinalColor( id, final, config.GradientDirection ) : white;
                Color? bg = dynamic ? cell.Background : null;
                Gradient? foreground = fg is { } f ? MakeGradient( [white, f], [5] ) : null;
                Gradient? background = bg is { } b ? MakeGradient( [white, b], [5] ) : null;
                SceneDefinition scene;
                if ( foreground is not null || background is not null )
                {
                    scene = GradientScene( id, ["█", "▓", "▒", "░", cell.Symbol], 3, foreground, background );
                }
                else
                {
                    SceneDefinition blocks = GradientScene( id, ["█", "▓", "▒", "░"], 3, MakeGradient( [white, white], [4] ) );
                    scene = new( [.. blocks.Frames, new( VisualBytes( id, cell.Symbol ), 3 )] );
                }

                Engine.AddScene( id, "typed", scene );
                Engine.ActivateScene( id, "typed" );
            }

            pending.Enqueue( new( chars ) );
        }

        current = pending.Dequeue( );
        Engine.Register( head, EngineEvent.PathComplete, new( CallerKind.Path, "return" ), static ( engine, character ) => engine.Terminal.Visible[character] = false );
    }

    public override bool NextFrame( )
    {
        if ( !typing && !Engine.HasActive )
        {
            return false;
        }

        if ( Engine.PathIsActive( head ) )
        {
            // Printing resumes when the carriage return finishes.
        }
        else if ( current.Pending.Count > 0 )
        {
            long count = Math.Min( current.Pending.Count, config.Speed );
            for ( long i = 0; i < count; i++ )
            {
                int id = current.Pending[0];
                current.Pending.RemoveAt( 0 );
                current.Typed.Add( id );
                Terminal.Visible[id] = true;
                Engine.Activate( id );
                lastColumn = Home[id].Column;
            }
        }
        else
        {
            processed.Add( current );
            if ( pending.TryDequeue( out Row? next ) )
            {
                foreach ( Row row in processed )
                {
                    foreach ( int id in row.Typed )
                    {
                        Coord coord = Terminal.Coordinates[id];
                        Terminal.Coordinates[id] = new( coord.Column, coord.Row + 1 );
                    }
                }

                current = next;
                if ( !processed[^1].Typed.All( id => id >= Terminal.InputCount ) && !current.Pending.All( id => id >= Terminal.InputCount ) )
                {
                    long left = current.Pending.Where( id => id < Terminal.InputCount ).Min( id => Home[id].Column );
                    current.Pending.RemoveAll( id => Home[id].Column < left || Home[id].Column > Terminal.TextRight );
                }

                Terminal.Coordinates[head] = new( lastColumn, 1 );
                Terminal.Visible[head] = true;
                Engine.RemovePath( head, "return" );
                Engine.AddPath( head, "return", new( [new Waypoint( "0", new( Home[current.Pending[0]].Column, 1 ) )], config.ReturnSpeed, config.Ease ) );
                Engine.ActivatePath( head, "return" );
                Engine.Activate( head );
            }
            else
            {
                typing = false;
            }
        }

        Engine.Update( );
        return true;
    }
}
