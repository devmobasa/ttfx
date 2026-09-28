namespace Ttfx.Core;

public sealed record OverflowConfig
{
    public Color[] OverflowColors { get; init; } = [Color.FromHex( "f2ebc0" ), Color.FromHex( "8dbfb3" ), Color.FromHex( "f2ebc0" )];
    public (long Low, long High) Cycles { get; init; } = (2, 4);
    public long Speed { get; init; } = 3;
    public Color[] Stops { get; init; } = [Color.FromHex( "8A008A" ), Color.FromHex( "00D1FF" ), Color.FromHex( "FFFFFF" )];
    public long[] Steps { get; init; } = [12];
    public GradientDirection GradientDirection { get; init; } = GradientDirection.Vertical;
}

public sealed class Overflow : EffectBase
{
    private sealed record Row( int[] Characters, bool Final );
    private readonly Queue<Row> pending = [];
    private readonly List<Row> active = [];
    private readonly Gradient overflow;
    private readonly long speed;
    private long delay;

    public Overflow( Terminal terminal, OverflowConfig config, EngineRng rng ) : base( terminal, rng )
    {
        speed = config.Speed;
        var final = new Gradient( config.Stops, config.Steps );
        int[][] rows = terminal.Groups( CharacterGroup.RowTopToBottom );
        if ( config.Cycles.High > 0 )
        {
            long cycles = rng.RandInt( config.Cycles.Low, config.Cycles.High );
            for ( long cycle = 0; cycle < cycles; cycle++ )
            {
                rng.Shuffle<int[]>( rows );
                foreach ( int[] row in rows )
                {
                    int first = Engine.AddCharacters( row.Select( id => terminal.Characters[id].Symbol ).ToArray( ), default );
                    int[] copies = Enumerable.Range( first, row.Length ).ToArray( );
                    for ( int i = 0; i < row.Length; i++ )
                    {
                        int id = copies[i];
                        long identity = terminal.Characters[id].Identity;
                        terminal.Characters[id] = terminal.Characters[row[i]] with { Identity = identity, Bold = false };
                        terminal.Coordinates[id] = Home[row[i]];
                        PreservedInputColors.Add( id );
                    }

                    pending.Enqueue( new( copies, false ) );
                }
            }
        }

        bool dynamic = terminal.Config.ExistingColors == ExistingColors.Dynamic;
        int[][] finalRows = Enumerable.Range( 0, Home.Length ).OrderBy( id => Home[id].Column ).GroupBy( id => Home[id].Row ).OrderByDescending( group => group.Key ).Select( group => group.ToArray( ) ).ToArray( );
        foreach ( int[] row in finalRows )
        {
            foreach ( int id in row )
            {
                InputCell cell = terminal.Characters[id];
                bool inText = Home[id].Column >= terminal.TextLeft && Home[id].Column <= terminal.TextRight && Home[id].Row >= terminal.TextBottom && Home[id].Row <= terminal.TextTop;
                Color? fg = dynamic ? cell.Foreground : inText ? FinalColor( id, final, config.GradientDirection ) : Color.FromHex( "000000" );
                Appearance( id, fg, dynamic ? cell.Background : null );
            }

            pending.Enqueue( new( row, true ) );
        }

        long steps = Math.Max( RustMath.FloorDiv( terminal.Height, Math.Max( 1, config.OverflowColors.Length - 1 ) ), 1 );
        overflow = new( config.OverflowColors, [steps] );
    }

    private void Move( Row row )
    {
        foreach ( int id in row.Characters )
        {
            Coord coord = Terminal.Coordinates[id];
            Terminal.Coordinates[id] = new( coord.Column, coord.Row + 1 );
        }
    }

    private void Paint( Row row, Color color )
    {
        foreach ( int id in row.Characters )
        {
            Appearance( id, color );
        }
    }

    public override bool NextFrame( )
    {
        if ( pending.Count == 0 )
        {
            return false;
        }

        if ( delay == 0 )
        {
            long count = Rng.RandInt( 1, speed );
            for ( long i = 0; i < count && pending.Count > 0; i++ )
            {
                foreach ( Row row in active )
                {
                    Move( row );
                    if ( !row.Final )
                    {
                        int index = ( int ) Math.Min( Terminal.Coordinates[row.Characters[0]].Row, overflow.Spectrum.Length - 1 );
                        Paint( row, overflow.Spectrum[index] );
                    }
                }

                Row next = pending.Dequeue( );
                foreach ( int id in next.Characters )
                {
                    Coord coord = Terminal.Coordinates[id];
                    Terminal.Coordinates[id] = new( coord.Column, 0 );
                }

                Move( next );
                if ( !next.Final )
                {
                    Paint( next, overflow.Spectrum[0] );
                }

                foreach ( int id in next.Characters )
                {
                    Terminal.Visible[id] = true;
                }

                active.Add( next );
            }

            delay = Rng.RandInt( 0, 3 );
        }
        else
        {
            delay--;
        }

        active.RemoveAll( row => Terminal.Coordinates[row.Characters[0]].Row > Terminal.Height );
        Engine.Update( );
        return true;
    }
}
