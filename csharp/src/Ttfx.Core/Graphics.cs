using System.Globalization;
using System.Text;

namespace Ttfx.Core;

// Argument identity is observable: xterm 15, "ffffff" and "FFFFFF" are distinct colors.
public readonly record struct Color
{
    public string Hex { get; }
    public byte? Xterm { get; }
    public byte Red { get; }
    public byte Green { get; }
    public byte Blue { get; }

    private Color( string hex, byte? xterm )
    {
        Hex = hex;
        Xterm = xterm;
        Red = byte.Parse( hex.AsSpan( 0, 2 ), NumberStyles.HexNumber, CultureInfo.InvariantCulture );
        Green = byte.Parse( hex.AsSpan( 2, 2 ), NumberStyles.HexNumber, CultureInfo.InvariantCulture );
        Blue = byte.Parse( hex.AsSpan( 4, 2 ), NumberStyles.HexNumber, CultureInfo.InvariantCulture );
    }

    public static Color FromHex( string value )
    {
        string hex = value.Trim( '#' );
        if ( hex.Length is not (6 or 7) || !long.TryParse( hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out _ ) )
        {
            throw new ArgumentException( "Invalid color value. Color must be an XTerm-256 color code or an RGB hex color string. Example: 255 or 'ffffff' or '#ffffff'" );
        }

        return new( hex, null );
    }

    private Color( string hex, byte? xterm, byte r, byte g, byte b )
    {
        Hex = hex;
        Xterm = xterm;
        Red = r;
        Green = g;
        Blue = b;
    }

    public static Color FromRgb( byte r, byte g, byte b )
    {
        string hex = string.Create( 6, (r, g, b), static ( text, rgb ) =>
        {
            const string digits = "0123456789abcdef";
            text[0] = digits[rgb.r >> 4];
            text[1] = digits[rgb.r & 15];
            text[2] = digits[rgb.g >> 4];
            text[3] = digits[rgb.g & 15];
            text[4] = digits[rgb.b >> 4];
            text[5] = digits[rgb.b & 15];
        } );

        return new( hex, null, r, g, b );
    }
    private static readonly uint[] Standard =
    [
        0x000000, 0x800000, 0x008000, 0x808000, 0x000080, 0x800080, 0x008080, 0xc0c0c0,
        0x808080, 0xff0000, 0x00ff00, 0xffff00, 0x0000ff, 0xff00ff, 0x00ffff, 0xffffff
    ];
    private static readonly byte[] Cube = [0, 95, 135, 175, 215, 255];

    private static readonly Color[] Palette = Enumerable.Range( 0, 256 ).Select( i => CreateXterm( ( byte ) i ) ).ToArray( );

    public static Color FromXterm( byte code ) => Palette[code];

    private static Color CreateXterm( byte code )
    {
        uint rgb;
        if ( code < 16 )
        {
            rgb = Standard[code];
        }
        else if ( code < 232 )
        {
            int index = code - 16;
            rgb = ( uint ) (Cube[index / 36] << 16 | Cube[index / 6 % 6] << 8 | Cube[index % 6]);
        }
        else
        {
            uint level = ( uint ) (8 + (code - 232) * 10);
            rgb = level << 16 | level << 8 | level;
        }

        return new( rgb.ToString( "x6", CultureInfo.InvariantCulture ), code, ( byte ) (rgb >> 16), ( byte ) (rgb >> 8), ( byte ) rgb );
    }

    public byte ClosestXterm( )
    {
        if ( Xterm is { } code )
        {
            return code;
        }

        int best = int.MaxValue;
        byte result = 0;

        for ( int i = 0; i < 256; i++ )
        {
            Color candidate = FromXterm( ( byte ) i );
            int distance = Math.Abs( Red - candidate.Red ) + Math.Abs( Green - candidate.Green ) + Math.Abs( Blue - candidate.Blue );
            if ( distance < best )
            {
                best = distance;
                result = ( byte ) i;
            }
        }

        return result;
    }

    public static Color AdjustBrightness( Color color, double brightness )
    {
        double r = color.Red / 255.0, g = color.Green / 255.0, b = color.Blue / 255.0;
        double maximum = RustMath.Max( RustMath.Max( r, g ), b ), minimum = RustMath.Min( RustMath.Min( r, g ), b );
        double lightness = (maximum + minimum) / 2.0, hue = 0, saturation = 0;
        if ( maximum != minimum )
        {
            double difference = maximum - minimum;
            saturation = lightness > .5 ? difference / (2.0 - maximum - minimum) : difference / (maximum + minimum);
            hue = maximum == r ? (g - b) / difference + (g < b ? 6 : 0) : maximum == g ? (b - r) / difference + 2 : (r - g) / difference + 4;
            hue /= 6.0;
        }

        lightness = RustMath.Max( RustMath.Min( lightness * brightness, 1 ), 0 );
        if ( saturation == 0 )
        {
            r = g = b = lightness;
        }
        else
        {
            double intensity = lightness < .5 ? lightness * (1.0 + saturation) : lightness + saturation - lightness * saturation;
            double scaled = 2.0 * lightness - intensity;
            r = HueToRgb( scaled, intensity, hue + 1.0 / 3.0 );
            g = HueToRgb( scaled, intensity, hue );
            b = HueToRgb( scaled, intensity, hue - 1.0 / 3.0 );
        }

        return FromRgb( unchecked(( byte ) RustMath.RoundHalfEven( r * 255.0 )), unchecked(( byte ) RustMath.RoundHalfEven( g * 255.0 )), unchecked(( byte ) RustMath.RoundHalfEven( b * 255.0 )) );
    }

    private static double HueToRgb( double scaled, double intensity, double hue )
    {
        if ( hue < 0 )
        {
            hue += 1;
        }

        if ( hue > 1 )
        {
            hue -= 1;
        }

        if ( hue < 1.0 / 6.0 )
        {
            return scaled + (intensity - scaled) * 6.0 * hue;
        }

        if ( hue < .5 )
        {
            return intensity;
        }

        if ( hue < 2.0 / 3.0 )
        {
            return scaled + (intensity - scaled) * (2.0 / 3.0 - hue) * 6.0;
        }

        return scaled;
    }

    public static Color ShiftTowards( Color color, Color target, double factor )
    {
        static long Channel( byte start, byte end, double f ) =>
            ( long ) ((start / 255.0 + (end / 255.0 - start / 255.0) * f) * 255.0);
        long r = Channel( color.Red, target.Red, factor ), g = Channel( color.Green, target.Green, factor ), b = Channel( color.Blue, target.Blue, factor );
        if ( r is >= 0 and <= 255 && g is >= 0 and <= 255 && b is >= 0 and <= 255 )
        {
            return FromRgb( ( byte ) r, ( byte ) g, ( byte ) b );
        }

        static string Hex( long v ) => v < 0 ? "-" + (-v).ToString( "x", CultureInfo.InvariantCulture ) : v.ToString( "x2", CultureInfo.InvariantCulture );

        return FromHex( Hex( r ) + Hex( g ) + Hex( b ) );
    }

    public static Color Parse( string value )
    {
        if ( Encoding.UTF8.GetByteCount( value ) > 3 )
        {
            return FromHex( value );
        }

        if ( value.Length == 0 || value.Any( c => c is not (>= '0' and <= '9') && c != '+' ) ||
            !byte.TryParse( value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out byte code ) )
        {
            throw new ArgumentException( $"invalid color value: '{value}'" );
        }

        return FromXterm( code );
    }
}

public enum GradientDirection
{
    Vertical, Horizontal, Radial, Diagonal
}

public sealed class Gradient
{
    public Color[] Spectrum { get; }

    public Gradient( ReadOnlySpan<Color> stops, ReadOnlySpan<long> steps, bool loop = false )
    {
        if ( stops.IsEmpty )
        {
            throw new ArgumentException( "At least one stop must be provided." );
        }

        if ( steps.IsEmpty )
        {
            throw new ArgumentException( "At least one step must be provided." );
        }

        var colors = new List<Color>( );
        if ( stops.Length == 1 )
        {
            for ( long i = 0; i < steps[0]; i++ )
            {
                colors.Add( stops[0] );
            }
        }
        else
        {
            int pairs = stops.Length - 1 + (loop ? 1 : 0);

            for ( int pair = 0; pair < pairs; pair++ )
            {
                long count = steps[Math.Min( pair, steps.Length - 1 )];
                if ( count < 1 )
                {
                    throw new ArgumentException( $"Invalid steps: {count} | Steps must be greater than 0." );
                }

                Color start = stops[pair], end = stops[(pair + 1) % stops.Length];
                long dr = RustMath.FloorDiv( end.Red - start.Red, count );
                long dg = RustMath.FloorDiv( end.Green - start.Green, count );
                long db = RustMath.FloorDiv( end.Blue - start.Blue, count );

                for ( long i = colors.Count == 0 ? 0 : 1; i < count; i++ )
                {
                    colors.Add( Color.FromRgb( ( byte ) Math.Clamp( start.Red + dr * i, 0, 255 ),
                        ( byte ) Math.Clamp( start.Green + dg * i, 0, 255 ), ( byte ) Math.Clamp( start.Blue + db * i, 0, 255 ) ) );
                }

                colors.Add( end );
            }
        }

        Spectrum = colors.ToArray( );
    }

    public Color AtFraction( double fraction )
    {
        if ( !(fraction >= 0 && fraction <= 1) )
        {
            throw new ArgumentException( "Fraction must be 0 <= fraction <= 1." );
        }

        int length = Spectrum.Length;
        int index = Math.Min( ( int ) (fraction * length), length - 1 );

        while ( index > 0 && fraction <= ( double ) index / length )
        {
            index--;
        }

        while ( index + 1 < length && fraction > ( double ) (index + 1) / length )
        {
            index++;
        }

        return Spectrum[index];
    }

    public Color AtCoordinate( long bottom, long top, long left, long right, Coord coord, GradientDirection direction )
    {
        long row = coord.Row - (bottom - 1), col = coord.Column - (left - 1);
        long height = top - (bottom - 1), width = right - (left - 1);
        double fraction = direction switch
        {
            GradientDirection.Vertical => ( double ) row / height,
            GradientDirection.Horizontal => ( double ) col / width,
            GradientDirection.Diagonal => ( double ) (row * 2 + col) / (height * 2 + width),
            GradientDirection.Radial => Geometry.NormalizedDistance( bottom, top, left, right, coord ),
            _ => throw new ArgumentOutOfRangeException( nameof( direction ) )
        };

        return AtFraction( fraction );
    }
}

public readonly record struct Visual( string Symbol, Color? Foreground = null, Color? Background = null, bool Bold = false )
{
    public byte[] Format( bool noColor, bool xterm )
    {
        var result = new StringBuilder( );
        if ( Bold )
        {
            result.Append( "\u001b[1m" );
        }

        if ( !noColor )
        {
            AppendColor( result, Foreground, true, xterm );
            AppendColor( result, Background, false, xterm );
        }

        bool styled = result.Length > 0;
        result.Append( Symbol );
        if ( styled )
        {
            result.Append( "\u001b[0m" );
        }

        return Encoding.UTF8.GetBytes( result.ToString( ) );
    }

    private static void AppendColor( StringBuilder result, Color? value, bool foreground, bool xterm )
    {
        if ( value is not { } color )
        {
            return;
        }

        int selector = foreground ? 38 : 48;
        if ( xterm )
        {
            result.Append( $"\u001b[{selector};5;{color.ClosestXterm( )}m" );
        }
        else
        {
            result.Append( $"\u001b[{selector};2;{color.Red};{color.Green};{color.Blue}m" );
        }
    }
}
