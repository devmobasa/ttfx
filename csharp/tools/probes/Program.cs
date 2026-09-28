using System.Globalization;
using System.Text.Json;
using Ttfx.Core;

if ( args.Length != 1 )
{
    return 2;
}

using var file = new FileStream( args[0], FileMode.Create, FileAccess.Write );
using var output = new Utf8JsonWriter( file, new( ) { Indented = true } );
output.WriteStartObject( );
output.WriteString( "runtime", Environment.Version.ToString( ) );
output.WriteStartArray( "samples" );
double[] values = [double.NegativeInfinity, -double.MaxValue, -9_223_372_036_854_775_808.0, -1.5, -0.5, -0.0, 0, double.Epsilon, 0.5, 1.5, 9_223_372_036_854_775_808.0, double.MaxValue, double.PositiveInfinity, double.NaN];
foreach ( double value in values )
{
    output.WriteStartObject( );
    output.WriteString( "input_bits", Bits( value ) );
    output.WriteNumber( "round_half_even", RustMath.RoundHalfEven( value ) );
    output.WriteString( "square_bits", Bits( RustMath.Square( value ) ) );
    output.WriteString( "pow_half_bits", Bits( RustMath.PowHalf( value ) ) );
    output.WriteString( "exp2_bits", Bits( RustMath.Exp2( value ) ) );
    output.WriteString( "hypot_bits", Bits( RustMath.Hypot( value, 2 ) ) );
    output.WriteString( "min_zero_bits", Bits( RustMath.Min( value, 0 ) ) );
    output.WriteString( "max_zero_bits", Bits( RustMath.Max( value, 0 ) ) );
    output.WriteEndObject( );
}

output.WriteEndArray( );
output.WriteEndObject( );
output.Flush( );
return 0;

static string Bits( double value ) => BitConverter.DoubleToUInt64Bits( value ).ToString( "x16", CultureInfo.InvariantCulture );
