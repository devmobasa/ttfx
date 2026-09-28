namespace Ttfx.Core;

public sealed record SynthGridConfig
{
    public Color[] GridColors { get; init; } = [Color.FromHex( "CC00CC" ), Color.FromHex( "ffffff" )];
    public long[] GridSteps { get; init; } = [12];
    public GradientDirection GridDirection { get; init; } = GradientDirection.Diagonal;
    public string RowSymbol { get; init; } = "─";
    public string ColumnSymbol { get; init; } = "│";
    public string[] Symbols { get; init; } = ["░", "▒", "▓"];
    public double Maximum { get; init; } = .1;
    public Color[] Stops { get; init; } = [Color.FromHex( "8A008A" ), Color.FromHex( "00D1FF" ), Color.FromHex( "FFFFFF" )];
    public long[] Steps { get; init; } = [12];
    public GradientDirection GradientDirection { get; init; } = GradientDirection.Vertical;
}

public sealed class SynthGrid : EffectBase
{
    private sealed class Line( int[] characters, bool horizontal )
    {
        internal readonly OrderedQueue<int> Collapsed = [.. characters], Extended = [];
        internal readonly int Speed = horizontal ? 3 : 1;
    }

    private readonly SynthGridConfig config;
    private readonly List<Line> lines = [];
    private readonly OrderedQueue<(int Number, int[] Characters)> pending = [];
    private readonly long[] tracker;
    private readonly int total;
    private int phase, activeGroups;

    private static long Gap( long dimension )
    {
        dimension -= 2;
        if ( dimension <= 0 )
        {
            return 0;
        }

        long best = 4, distance = long.MaxValue;
        for ( long gap = dimension; gap > 4; gap-- )
        {
            if ( dimension % gap <= 1 && Math.Abs( gap - RustMath.FloorDiv( dimension, 5 ) ) < distance )
            {
                best = gap;
                distance = Math.Abs( gap - RustMath.FloorDiv( dimension, 5 ) );
            }
        }

        return best;
    }

    public SynthGrid( Terminal terminal, SynthGridConfig config, EngineRng rng ) : base( terminal, rng )
    {
        this.config = config;
        var grid = new Gradient( config.GridColors, config.GridSteps );
        var text = new Gradient( config.Stops, config.Steps );
        void AddLine( Coord origin, bool horizontal )
        {
            long count = horizontal ? terminal.Width : terminal.Height - 1;
            string symbol = horizontal ? config.RowSymbol : config.ColumnSymbol;
            int first = Engine.AddCharacters( Enumerable.Repeat( symbol, checked(( int ) count) ).ToArray( ), default );
            int[] ids = Enumerable.Range( first, checked(( int ) count) ).ToArray( );
            for ( int i = 0; i < ids.Length; i++ )
            {
                int id = ids[i];
                Coord coord = horizontal ? new( i + 1, origin.Row ) : new( origin.Column, i + 1 );
                Engine.AddScene( id, "grid", new( [new( VisualBytes( id, symbol, grid.AtCoordinate( 1, terminal.Height, 1, terminal.Width, coord, config.GridDirection ) ), 1 )] ) );
                Engine.ActivateScene( id, "grid" );
                Engine.SetLayer( id, 2 );
                terminal.Coordinates[id] = coord;
            }

            lines.Add( new( ids, horizontal ) );
        }

        AddLine( new( 1, 1 ), true );
        AddLine( new( 1, terminal.Height ), true );
        AddLine( new( 1, 1 ), false );
        AddLine( new( terminal.Width, 1 ), false );
        long rowGap, columnGap;
        if ( terminal.Height > 2 * terminal.Width )
        {
            rowGap = Gap( terminal.Height ) + 1;
            columnGap = rowGap * 2;
        }
        else
        {
            columnGap = Gap( terminal.Width ) + 1;
            rowGap = RustMath.FloorDiv( columnGap, 2 );
        }

        var rows = new List<long>( );
        var columns = new List<long>( );
        for ( long row = 1 + rowGap; row < terminal.Height; row += Math.Max( rowGap, 1 ) )
        {
            if ( terminal.Height - row >= 2 )
            {
                rows.Add( row );
                AddLine( new( 1, row ), true );
            }
        }

        for ( long column = 1 + columnGap; column < terminal.Width; column += Math.Max( columnGap, 1 ) )
        {
            if ( terminal.Width - column >= 2 )
            {
                columns.Add( column );
                AddLine( new( column, 1 ), false );
            }
        }

        rows.Add( terminal.Height + 1 );
        columns.Add( terminal.Width + 1 );
        var at = Enumerable.Range( 0, Home.Length ).ToDictionary( id => Home[id], id => id );
        long previousRow = 1;
        foreach ( long rowValue in rows )
        {
            long rowIndex = rowValue, previousColumn = 1;
            foreach ( long columnIndex in columns )
            {
                var chars = new List<int>( );
                if ( rowIndex == terminal.Height )
                {
                    rowIndex++;
                }

                for ( long row = previousRow; row < rowIndex; row++ )
                {
                    for ( long column = previousColumn; column < columnIndex; column++ )
                    {
                        if ( at.TryGetValue( new( column, row ), out int id ) )
                        {
                            chars.Add( id );
                        }
                    }
                }

                if ( chars.Count > 0 )
                {
                    pending.Add( (pending.Count, chars.ToArray( )) );
                }

                previousColumn = columnIndex;
            }

            previousRow = rowIndex;
        }

        tracker = new long[pending.Count];
        bool dynamic = terminal.Config.ExistingColors == ExistingColors.Dynamic;
        foreach ( var group in pending )
        {
            int number = group.Number;
            foreach ( int id in group.Characters )
            {
                InputCell cell = terminal.Characters[id];
                var frames = new List<SceneFrame>( );
                long count = rng.RandInt( 15, 30 );
                for ( long frame = 0; frame < count; frame++ )
                {
                    string symbol = config.Symbols[rng.ChoiceIndex( config.Symbols.Length )];
                    Color fg = text.Spectrum[rng.ChoiceIndex( text.Spectrum.Length )];
                    frames.Add( new( VisualBytes( id, symbol, fg ), 2 ) );
                }

                Color? finalFg = id >= terminal.InputCount ? null : dynamic ? cell.Foreground : cell.Symbol == " " ? null : FinalColor( id, text, config.GradientDirection );
                Color? finalBg = id < terminal.InputCount && dynamic ? cell.Background : null;
                frames.Add( new( VisualBytes( id, cell.Symbol, finalFg, finalBg ), 1 ) );
                Engine.AddScene( id, "dissolve", new( frames ) );
                Engine.ActivateScene( id, "dissolve" );
                Engine.Register( id, EngineEvent.SceneComplete, new( CallerKind.Scene, "dissolve" ), ( _, _ ) => tracker[number]-- );
            }
        }

        rng.Shuffle( pending.AsSpan( ) );
        total = pending.Count;
        if ( total == 0 )
        {
            foreach ( int id in InputOrder )
            {
                terminal.Visible[id] = true;
                Engine.Activate( id );
            }
        }
    }

    public override bool NextFrame( )
    {
        if ( pending.Count == 0 && !Engine.HasActive && phase == 3 )
        {
            return false;
        }

        if ( phase == 0 )
        {
            if ( lines.All( line => line.Collapsed.Count == 0 ) )
            {
                phase = 1;
            }
            else
            {
                foreach ( Line line in lines )
                {
                    for ( int i = 0; i < line.Speed && line.Collapsed.Count > 0; i++ )
                    {
                        int id = line.Collapsed[0];
                        line.Collapsed.RemoveAt( 0 );
                        Terminal.Visible[id] = true;
                        line.Extended.Add( id );
                    }
                }
            }
        }
        else if ( phase == 1 )
        {
            if ( pending.Count > 0 && activeGroups < total * config.Maximum )
            {
                var group = pending[0];
                pending.RemoveAt( 0 );
                foreach ( int id in group.Characters )
                {
                    Terminal.Visible[id] = true;
                    Engine.Activate( id );
                    tracker[group.Number]++;
                }
            }

            if ( pending.Count == 0 && !Engine.HasActive && activeGroups == 0 )
            {
                phase = 2;
            }
        }
        else if ( phase == 2 )
        {
            if ( lines.All( line => line.Extended.Count == 0 ) )
            {
                phase = 3;
            }
            else
            {
                foreach ( Line line in lines )
                {
                    if ( line.Extended.Count == 0 )
                    {
                        continue;
                    }

                    if ( line.Collapsed.Count == 0 )
                    {
                        line.Extended.Reverse( );
                    }

                    for ( int i = 0; i < line.Speed && line.Extended.Count > 0; i++ )
                    {
                        int id = line.Extended[0];
                        line.Extended.RemoveAt( 0 );
                        Terminal.Visible[id] = false;
                        line.Collapsed.Add( id );
                    }
                }
            }
        }

        Engine.Update( );
        activeGroups = tracker.Count( count => count != 0 );
        return true;
    }
}
