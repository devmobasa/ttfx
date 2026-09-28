using System.Runtime.InteropServices;

namespace Ttfx.Core;

public static partial class RustMath
{
    [SuppressGCTransition]
    [LibraryImport( "libm.so.6", EntryPoint = "exp2" )]
    public static partial double Exp2( double value );
    [SuppressGCTransition]
    [LibraryImport( "libm.so.6", EntryPoint = "hypot" )]
    public static partial double Hypot( double x, double y );

    public static double Pow( double value, double exponent ) => Math.Pow( value, exponent );
    public static double Square( double value ) => value * value;
    public static double Sqrt( double value ) => Math.Sqrt( value );
    public static double PowHalf( double value ) => value == double.NegativeInfinity ? double.PositiveInfinity : Math.Abs( Math.Sqrt( value ) );
    public static double Sin( double value ) => Math.Sin( value );
    public static double Cos( double value ) => Math.Cos( value );
    public static double Min( double x, double y ) => double.MinNumber( x, y );
    public static double Max( double x, double y ) => double.MaxNumber( x, y );

    public static long RoundHalfEven( double value )
    {
        if ( double.IsFinite( value ) )
        {
            return ( long ) Math.Round( value, MidpointRounding.ToEven );
        }

        long floor = ( long ) Math.Floor( value );

        return floor % 2 == 0 ? floor : unchecked(floor + 1);
    }

    public static long FloorDiv( long a, long b )
    {
        long quotient = a / b;

        return a % b != 0 && (a < 0) != (b < 0) ? quotient - 1 : quotient;
    }

    public static long Mod( long a, long b )
    {
        long remainder = a % b;

        return remainder != 0 && (remainder < 0) != (b < 0) ? remainder + b : remainder;
    }
}
