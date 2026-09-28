using System.Buffers;

namespace Ttfx.Core;

public sealed class Terminal
{
    public TerminalConfig Config { get; }
    public EngineClock Clock { get; }
    public long Width { get; }
    public long Height { get; }
    public long TextLeft { get; }
    public long TextRight { get; }
    public long TextBottom { get; }
    public long TextTop { get; }
    public Coord TextCenter => new( TextLeft + RustMath.FloorDiv( TextRight - TextLeft, 2 ), TextBottom + RustMath.FloorDiv( TextTop - TextBottom, 2 ) );
    public long VisibleTop { get; }
    public long VisibleRight { get; }
    private readonly long visibleLeft, visibleBottom, offsetColumn, offsetRow;
    public InputCell[] Characters { get; private set; }
    public Coord[] Coordinates { get; private set; }
    public bool[] Visible { get; private set; }
    public byte[][] VisualBytes { get; private set; }
    public long[] Layers { get; private set; }
    public int InputCount { get; }
    public int Count { get; private set; }
    private long nextIdentity;
    private long[] identities;
    private readonly Dictionary<Visual, byte[]> initialVisuals = [];

    private byte[] InitialVisual( Visual visual )
    {
        if ( !initialVisuals.TryGetValue( visual, out byte[]? bytes ) )
        {
            bytes = visual.Format( Config.NoColor, Config.XtermColors );
            initialVisuals.Add( visual, bytes );
        }

        return bytes;
    }
    private readonly int[] cells;
    private readonly byte[]?[] paintedVisuals;
    private readonly ArrayBufferWriter<byte>[] rowBuffers;
    private readonly ArrayBufferWriter<byte> output = new( );
    private readonly ReadOnlyMemory<byte>[] renderedRows;
    public int RenderedLength { get; private set; }

    public Terminal( string input, TerminalConfig config, long terminalWidth, long terminalHeight )
    {
        Config = config;
        Clock = new( config.UseVirtualClock, config.FrameRate );
        List<List<InputCell>> lines = Input.Preprocess( input, config.TabWidth );
        nextIdentity = lines.SelectMany( line => line ).Select( cell => cell.Identity ).DefaultIfEmpty( -1 ).Max( ) + 1;
        lineLengths = lines.Select( l => ( long ) l.Count ).ToArray( );
        layout = ComputeLayout( config, lineLengths, terminalWidth, terminalHeight );
        Width = layout.Width;
        Height = layout.Height;
        offsetColumn = layout.ColumnOffset;
        offsetRow = layout.RowOffset;
        VisibleTop = layout.VisibleTop;
        VisibleRight = layout.VisibleRight;
        visibleBottom = layout.VisibleBottom;
        visibleLeft = layout.VisibleLeft;

        if ( Width <= 0 || Height <= 0 )
        {
            throw new EngineException( "all input characters fall outside the canvas after anchoring" );
        }

        if ( config.WrapText )
        {
            var wrapped = new List<List<InputCell>>( );

            foreach ( var line in lines )
            {
                int start = 0;

                while ( line.Count - start > Width )
                {
                    wrapped.Add( line.GetRange( start, checked(( int ) Width) ) );
                    start += checked(( int ) Width);
                }

                wrapped.Add( line.GetRange( start, line.Count - start ) );
            }

            lines = wrapped;
        }

        var characters = new List<InputCell>( );
        var coordinates = new List<Coord>( );

        for ( int r = 0; r < lines.Count; r++ )
        {
            for ( int c = 0; c < lines[r].Count; c++ )
            {
                if ( lines[r][c] is not { Symbol: " ", Foreground: null, Background: null } )
                {
                    characters.Add( lines[r][c] );
                    coordinates.Add( new( c + 1, lines.Count - r ) );
                }
            }
        }

        if ( characters.Count == 0 )
        {
            throw new EngineException( "no input characters to anchor" );
        }

        long actualWidth = coordinates.Max( c => c.Column ), actualHeight = coordinates.Max( c => c.Row );
        long centerColumn = Math.Max( RustMath.FloorDiv( Width, 2 ), 1 ) + (Width % 2 != 0 && Width > 1 ? 1 : 0);
        long centerRow = Math.Max( RustMath.FloorDiv( Height, 2 ), 1 ) + (Height % 2 != 0 && Height > 1 ? 1 : 0);
        long dx = actualWidth == Width ? 0 : config.AnchorText switch
        {
            Anchor.S or Anchor.N or Anchor.C => centerColumn - RustMath.FloorDiv( actualWidth, 2 ),
            Anchor.Se or Anchor.E or Anchor.Ne => Width - actualWidth,
            _ => 0
        };

        long dy = actualHeight == Height ? 0 : config.AnchorText switch
        {
            Anchor.W or Anchor.E or Anchor.C => centerRow - RustMath.FloorDiv( actualHeight, 2 ),
            Anchor.Nw or Anchor.N or Anchor.Ne => Height - actualHeight,
            _ => 0
        };

        var kept = new List<InputCell>( );
        var keptCoords = new List<Coord>( );

        for ( int i = 0; i < characters.Count; i++ )
        {
            Coord c = new( coordinates[i].Column + dx, coordinates[i].Row + dy );
            if ( c.Column < 1 || c.Column > Width || c.Row < 1 || c.Row > Height )
            {
                continue;
            }

            kept.Add( characters[i] );
            keptCoords.Add( c );
        }

        if ( kept.Count == 0 )
        {
            throw new EngineException( "all input characters fall outside the canvas after anchoring" );
        }

        Characters = kept.ToArray( );
        InputCount = Count = Characters.Length;
        identities = Characters.Select( cell => cell.Identity ).ToArray( );
        Layers = new long[InputCount];
        Coordinates = keptCoords.ToArray( );
        TextLeft = Coordinates.Min( c => c.Column );
        TextRight = Coordinates.Max( c => c.Column );
        TextBottom = Coordinates.Min( c => c.Row );
        TextTop = Coordinates.Max( c => c.Row );
        Visible = new bool[Characters.Length];
        VisualBytes = new byte[Characters.Length][];

        for ( int i = 0; i < Count; i++ )
        {
            InputCell c = Characters[i];
            bool always = config.ExistingColors == ExistingColors.Always;
            VisualBytes[i] = InitialVisual( new( c.Symbol, always ? c.Foreground : null, always ? c.Background : null, always && c.Bold ) );
        }

        cells = new int[checked(( int ) (Math.Max( 0, VisibleTop ) * Math.Max( 0, VisibleRight )))];
        paintedVisuals = new byte[]?[cells.Length];
        rowBuffers = new ArrayBufferWriter<byte>[checked(( int ) Math.Max( 0, VisibleTop ))];
        renderedRows = new ReadOnlyMemory<byte>[Math.Max( 0, rowBuffers.Length * 2 - 1 )];
        for ( int r = 0; r < rowBuffers.Length; r++ )
        {
            rowBuffers[r] = new( checked(( int ) Math.Max( 1, VisibleRight )) );
        }
    }

    private readonly long[] lineLengths;
    private readonly Layout layout;
    private readonly record struct Layout( long Width, long Height, long ColumnOffset, long RowOffset,
        long VisibleTop, long VisibleBottom, long VisibleRight, long VisibleLeft );

    public bool LayoutChanged( long columns, long rows ) => layout != ComputeLayout( Config, lineLengths, columns, rows );

    private static Layout ComputeLayout( TerminalConfig config, long[] lengths, long columns, long rows )
    {
        long inputWidth = lengths.Max( );
        long width = config.CanvasWidth > 0 ? config.CanvasWidth : config.CanvasWidth == 0 ? columns :
            config.IgnoreTerminalDimensions ? inputWidth : Math.Min( columns, inputWidth );
        long wrappedHeight = lengths.Sum( length => width > 0 ? Math.Max( 1, (length + width - 1) / width ) : 1 );
        long height = config.CanvasHeight > 0 ? config.CanvasHeight : config.CanvasHeight == 0 ? rows :
            config.IgnoreTerminalDimensions ? lengths.Length : Math.Min( rows, config.WrapText ? wrappedHeight : lengths.Length );
        long dx = 0, dy = 0;
        if ( config.IgnoreTerminalDimensions )
        {
            columns = width;
            rows = height;
        }
        else
        {
            dx = config.AnchorCanvas switch
            {
                Anchor.S or Anchor.N or Anchor.C => RustMath.FloorDiv( columns, 2 ) - RustMath.FloorDiv( width, 2 ),
                Anchor.Se or Anchor.E or Anchor.Ne => columns - width,
                _ => 0
            };
            dy = config.AnchorCanvas switch
            {
                Anchor.W or Anchor.E or Anchor.C => RustMath.FloorDiv( rows, 2 ) - RustMath.FloorDiv( height, 2 ),
                Anchor.Nw or Anchor.N or Anchor.Ne => rows - height,
                _ => 0
            };
        }

        return new( width, height, dx, dy, Math.Min( height + dy, rows ), Math.Max( 1 + dy, 1 ),
            Math.Min( width + dx, columns ), Math.Max( 1 + dx, 1 ) );
    }

    public ReadOnlySpan<byte> Render( )
    {
        ReadOnlyMemory<byte>[] rows = RenderRows( );
        output.ResetWrittenCount( );
        foreach ( ReadOnlyMemory<byte> row in rows )
        {
            output.Write( row.Span );
        }

        return output.WrittenSpan;
    }

    public ReadOnlyMemory<byte>[] RenderRows( )
    {
        int[] cellMap = cells;
        Array.Fill( cellMap, -1 );
        int width = checked(( int ) Math.Max( 0, VisibleRight ));
        int height = checked(( int ) Math.Max( 0, VisibleTop ));
        bool[] visible = Visible;
        Coord[] coordinates = Coordinates;
        long[] layers = Layers;
        long[] identity = identities;
        byte[][] visualBytes = VisualBytes;
        int count = Count;

        for ( int i = 0; i < count; i++ )
        {
            if ( !visible[i] )
            {
                continue;
            }

            long row = coordinates[i].Row + offsetRow, column = coordinates[i].Column + offsetColumn;
            if ( row < visibleBottom || row > VisibleTop || column < visibleLeft || column > VisibleRight )
            {
                continue;
            }

            int index = checked(( int ) ((row - 1) * width + column - 1));
            int previous = cellMap[index];
            if ( previous == -1 || layers[i] > layers[previous] || (layers[i] == layers[previous] && identity[i] > identity[previous]) )
            {
                cellMap[index] = i;
            }
        }

        RenderedLength = 0;
        int part = 0;
        Span<byte[]?> painted = paintedVisuals;

        for ( int r = height - 1; r >= 0; r-- )
        {
            var row = rowBuffers[r];
            bool dirty = row.WrittenCount == 0;
            Span<byte[]?> rowVisuals = painted.Slice( r * width, width );
            ReadOnlySpan<int> rowCells = cellMap.AsSpan( r * width, width );
            int rowLength = r == 0 ? 0 : 1;

            for ( int c = 0; c < width; c++ )
            {
                int character = rowCells[c];
                byte[]? visual = character < 0 ? null : visualBytes[character];
                if ( !ReferenceEquals( visual, rowVisuals[c] ) )
                {
                    dirty = true;
                    rowVisuals[c] = visual;
                }

                rowLength = checked(rowLength + (visual?.Length ?? 1));
            }

            if ( dirty )
            {
                row.ResetWrittenCount( );
                Span<byte> destination = row.GetSpan( rowLength );
                int offset = 0;
                foreach ( byte[]? visual in rowVisuals )
                {
                    if ( visual is null )
                    {
                        destination[offset++] = ( byte ) ' ';
                    }
                    else
                    {
                        visual.CopyTo( destination.Slice( offset ) );
                        offset += visual.Length;
                    }
                }

                if ( r != 0 )
                {
                    destination[offset++] = ( byte ) '\n';
                }

                row.Advance( offset );
            }

            renderedRows[part++] = row.WrittenMemory;
            RenderedLength = checked(RenderedLength + row.WrittenCount);
        }

        return renderedRows;
    }

    public int AddCharacter( string symbol, Coord coordinate )
    {
        return AppendCharacters( [new InputCell( nextIdentity++, symbol, null, null, false )], [coordinate] );
    }

    public int AddCharacters( string[] symbols, Coord coordinate )
    {
        var cells = new InputCell[symbols.Length];
        var positions = new Coord[symbols.Length];
        for ( int i = 0; i < symbols.Length; i++ )
        {
            cells[i] = new( nextIdentity++, symbols[i], null, null, false );
            positions[i] = coordinate;
        }

        return AppendCharacters( cells, positions );
    }

    public (int[] Inner, int[] Outer) AddFillCharacters( )
    {
        var occupied = Coordinates.Take( Count ).ToHashSet( );
        var characters = new List<InputCell>( );
        var coordinates = new List<Coord>( );
        var inner = new List<int>( );
        var outer = new List<int>( );
        int first = Count;
        for ( long row = 1; row <= Height; row++ )
        {
            for ( long column = 1; column <= Width; column++ )
            {
                Coord coordinate = new( column, row );
                if ( occupied.Contains( coordinate ) )
                {
                    continue;
                }

                int id = first + characters.Count;
                characters.Add( new( nextIdentity++, " ", null, null, false ) );
                coordinates.Add( coordinate );
                (column >= TextLeft && column <= TextRight && row >= TextBottom && row <= TextTop ? inner : outer).Add( id );
            }
        }

        AppendCharacters( characters.ToArray( ), coordinates.ToArray( ) );

        return (inner.ToArray( ), outer.ToArray( ));
    }

    private int AppendCharacters( InputCell[] characters, Coord[] coordinates )
    {
        int start = Count;
        int length = checked(start + characters.Length);
        if ( length > Characters.Length )
        {
            int capacity = Math.Max( length, checked(Math.Max( 4, Characters.Length ) * 2) );
            var all = Characters;
            Array.Resize( ref all, capacity );
            Characters = all;
            Array.Resize( ref identities, capacity );
            var positions = Coordinates;
            Array.Resize( ref positions, capacity );
            Coordinates = positions;
            var visible = Visible;
            Array.Resize( ref visible, capacity );
            Visible = visible;
            var visuals = VisualBytes;
            Array.Resize( ref visuals, capacity );
            VisualBytes = visuals;
            var layers = Layers;
            Array.Resize( ref layers, capacity );
            Layers = layers;
        }

        characters.CopyTo( Characters, start );
        coordinates.CopyTo( Coordinates, start );
        Count = length;
        for ( int id = start; id < length; id++ )
        {
            identities[id] = Characters[id].Identity;
            VisualBytes[id] = InitialVisual( new( Characters[id].Symbol ) );
        }

        return start;
    }

    public int[][] Groups( CharacterGroup group, bool includeFill = false )
    {
        var ordered = Enumerable.Range( 0, includeFill ? Count : InputCount ).OrderBy( i => Coordinates[i].Row ).ThenBy( i => Coordinates[i].Column );
        long Key( int i )
        {
            Coord c = Coordinates[i];

            return group switch
            {
                CharacterGroup.ColumnLeftToRight or CharacterGroup.ColumnRightToLeft => c.Column,
                CharacterGroup.RowTopToBottom or CharacterGroup.RowBottomToTop => c.Row,
                CharacterGroup.DiagonalBottomLeftToTopRight or CharacterGroup.DiagonalTopRightToBottomLeft => c.Row + c.Column,
                CharacterGroup.DiagonalTopLeftToBottomRight or CharacterGroup.DiagonalBottomRightToTopLeft => c.Column - c.Row,
                _ => Math.Abs( c.Column - TextCenter.Column ) + Math.Abs( c.Row - TextCenter.Row )
            };
        }
        int[][] groups = ordered.GroupBy( Key ).OrderBy( g => g.Key ).Select( g => g.ToArray( ) ).ToArray( );
        if ( group is CharacterGroup.ColumnRightToLeft or CharacterGroup.RowTopToBottom or CharacterGroup.DiagonalTopRightToBottomLeft or CharacterGroup.DiagonalBottomRightToTopLeft or CharacterGroup.OutsideToCenter )
        {
            Array.Reverse( groups );
        }

        return groups;
    }
}
