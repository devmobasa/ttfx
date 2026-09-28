using System.Globalization;
using System.Text;

namespace Ttfx.Core;

public readonly record struct InputCell( long Identity, string Symbol, Color? Foreground, Color? Background, bool Bold );

public static class Input
{
    private static readonly string[] Ascii = Enumerable.Range( 0, 128 ).Select( value => (( char ) value).ToString( ) ).ToArray( );

    private static string Symbol( Rune rune ) => rune.Value < Ascii.Length ? Ascii[rune.Value] : rune.ToString( );

    public static List<List<InputCell>> Preprocess( string input, long tabWidth )
    {
        if ( tabWidth <= 0 )
        {
            throw new ArgumentOutOfRangeException( nameof( tabWidth ) );
        }

        Rune[] runes = input.EnumerateRunes( ).ToArray( );
        var screen = new Dictionary<(long, long), InputCell>( );
        long row = 0, column = 0, maxRow = 0, maxColumn = 0, identity = 0;
        Color? foreground = null, background = null;
        bool bold = false;
        long? standardForeground = null;

        for ( int i = 0; i < runes.Length; )
        {
            int current = runes[i].Value;
            if ( current == 27 )
            {
                int end = EscapeEnd( runes, i );
                string sequence = string.Concat( runes.AsSpan( i, end - i ).ToArray( ).Select( r => r.ToString( ) ) );
                if ( !sequence.StartsWith( "\u001b[", StringComparison.Ordinal ) || sequence.Length < 3 )
                {
                    throw new UnsupportedAnsiException( sequence );
                }

                char final = sequence[^1];
                int paramEnd = 2;

                while ( paramEnd < sequence.Length && sequence[paramEnd] is >= '0' and <= '?' )
                {
                    paramEnd++;
                }

                string parameters = sequence[2..paramEnd];
                string intermediate = sequence[paramEnd..^1];
                if ( final == 'm' )
                {
                    long[] values = ParseParameters( parameters );
                    if ( values.Length == 0 )
                    {
                        values = [0];
                    }

                    for ( int index = 0; index < values.Length; index++ )
                    {
                        long p = values[index];
                        switch ( p )
                        {
                            case 0:
                                foreground = background = null;
                                bold = false;
                                standardForeground = null;
                                break;
                            case 1:
                                bold = true;
                                if ( standardForeground is { } normal )
                                {
                                    foreground = Xterm( normal - 30 + 8 );
                                }

                                break;
                            case 22:
                                bold = false;
                                if ( standardForeground is { } normal2 )
                                {
                                    foreground = Xterm( normal2 - 30 );
                                }

                                break;
                            case 39:
                                foreground = null;
                                standardForeground = null;
                                break;
                            case 49:
                                background = null;
                                break;
                            case >= 30 and <= 37:
                                foreground = Xterm( p - 30 + (bold ? 8 : 0) );
                                standardForeground = p;
                                break;
                            case >= 90 and <= 97:
                                foreground = Xterm( p - 90 + 8 );
                                standardForeground = null;
                                break;
                            case >= 40 and <= 47:
                                background = Xterm( p - 40 );
                                break;
                            case >= 100 and <= 107:
                                background = Xterm( p - 100 + 8 );
                                break;
                            case 38 or 48:
                                if ( index + 1 >= values.Length )
                                {
                                    throw new UnsupportedAnsiException( sequence );
                                }

                                Color color;
                                if ( values[index + 1] == 5 && index + 2 < values.Length )
                                {
                                    color = Xterm( values[index + 2] );
                                    index += 2;
                                }
                                else if ( values[index + 1] == 2 && index + 4 < values.Length )
                                {
                                    string hex = $"{values[index + 2]:X2}{values[index + 3]:X2}{values[index + 4]:X2}";
                                    color = Color.FromHex( hex );
                                    index += 4;
                                }
                                else
                                {
                                    throw new UnsupportedAnsiException( sequence );
                                }

                                if ( p == 38 )
                                {
                                    foreground = color;
                                    standardForeground = null;
                                }
                                else
                                {
                                    background = color;
                                }

                                break;
                        }
                    }
                }
                else if ( sequence is "\u001b[?25h" or "\u001b[?25l" or "\u001b[?7h" or "\u001b[?7l" )
                {
                    // Accepted terminal modes have no preprocessing effect.
                }
                else
                {
                    if ( intermediate.Length != 0 || parameters.StartsWith( '?' ) )
                    {
                        throw new UnsupportedAnsiException( sequence );
                    }

                    long[] values = ParseParameters( parameters );
                    long first = values.Length == 0 ? 1 : Math.Max( 1, values[0] );
                    switch ( final )
                    {
                        case 'A':
                            row -= first;
                            break;
                        case 'B':
                            row += first;
                            break;
                        case 'C':
                            column += first;
                            break;
                        case 'D':
                            column -= first;
                            break;
                        case 'E':
                            row += first;
                            column = 0;
                            break;
                        case 'F':
                            row -= first;
                            column = 0;
                            break;
                        case 'G':
                            column = first - 1;
                            break;
                        case 'H' or 'f':
                            row = first - 1;
                            column = values.Length > 1 && values[1] != 0 ? values[1] - 1 : 0;
                            break;
                        default:
                            throw new UnsupportedAnsiException( sequence );
                    }

                    row = Math.Max( 0, row );
                    column = Math.Max( 0, column );
                    maxRow = Math.Max( maxRow, row );
                    maxColumn = Math.Max( maxColumn, column );
                }

                i = end;
            }
            else if ( current == '\n' )
            {
                row++;
                column = 0;
                maxRow = Math.Max( maxRow, row );
                i++;
            }
            else if ( current == '\r' )
            {
                column = 0;
                i++;
            }
            else
            {
                long count = current == '\t' ? tabWidth - column % tabWidth : 1;
                string symbol = current == '\t' ? " " : Symbol( runes[i] );

                for ( long j = 0; j < count; j++ )
                {
                    screen[(row, column)] = new( identity++, symbol, foreground, background, bold );
                    maxRow = Math.Max( maxRow, row );
                    maxColumn = Math.Max( maxColumn, column );
                    column++;
                }

                i++;
            }
        }

        var lines = new List<List<InputCell>>( );

        for ( long r = 0; r <= maxRow; r++ )
        {
            var line = new List<InputCell>( );

            for ( long c = 0; c <= maxColumn; c++ )
            {
                line.Add( screen.TryGetValue( (r, c), out var cell ) ? cell : new( identity++, " ", null, null, false ) );
            }

            while ( line.Count > 0 && line[^1] is { Symbol: " ", Foreground: null, Background: null } )
            {
                line.RemoveAt( line.Count - 1 );
            }

            lines.Add( line );
        }

        while ( lines.Count > 0 && lines[^1].Count == 0 )
        {
            lines.RemoveAt( lines.Count - 1 );
        }

        if ( lines.Count == 0 )
        {
            lines.Add( [new( identity, " ", foreground, background, bold )] );
        }

        return lines;
    }

    private static Color Xterm( long code )
    {
        if ( code is < 0 or > 255 )
        {
            throw new EngineException( $"invalid xterm color code in input: {code}" );
        }

        return Color.FromXterm( ( byte ) code );
    }

    private static long[] ParseParameters( string parameters )
    {
        if ( parameters.Any( c => c is not (>= '0' and <= '9') && c != ';' ) )
        {
            throw new UnsupportedAnsiException( $"\u001b[{parameters}" );
        }

        return parameters.Length == 0 ? [] : parameters.Split( ';' ).Select( p =>
            p.Length == 0 ? 0 : long.Parse( p, NumberStyles.None, CultureInfo.InvariantCulture ) ).ToArray( );
    }

    private static int EscapeEnd( Rune[] runes, int start )
    {
        if ( start + 1 < runes.Length && runes[start + 1].Value == ']' )
        {
            int t = start + 2;

            while ( t < runes.Length && runes[t].Value != 7 )
            {
                t++;
            }

            if ( t < runes.Length )
            {
                return t + 1;
            }

            for ( int p = runes.Length; p >= start + 4; p-- )
            {
                if ( runes[p - 2].Value == 27 && runes[p - 1].Value == '\\' )
                {
                    return p;
                }
            }
        }

        if ( start + 1 < runes.Length && runes[start + 1].Value == '[' )
        {
            int t = start + 2;

            while ( t < runes.Length && runes[t].Value is >= 0x30 and <= 0x3f )
            {
                t++;
            }

            while ( t < runes.Length && runes[t].Value is >= 0x20 and <= 0x2f )
            {
                t++;
            }

            if ( t < runes.Length && runes[t].Value is >= 0x40 and <= 0x7e )
            {
                return t + 1;
            }
        }

        return start + 1 < runes.Length && runes[start + 1].Value != '\n' ? start + 2 : start + 1;
    }
}
