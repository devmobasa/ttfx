using System.Globalization;

namespace Ttfx.Core;

public static class Numbers
{
    public static bool TryFloat( string value, out double number )
    {
        number = 0;
        if ( value.Length == 0 || value.Any( char.IsWhiteSpace ) )
        {
            return false;
        }

        bool negative = value[0] == '-';
        string unsigned = value[0] is '+' or '-' ? value[1..] : value;
        if ( unsigned.Equals( "inf", StringComparison.OrdinalIgnoreCase ) || unsigned.Equals( "infinity", StringComparison.OrdinalIgnoreCase ) )
        {
            number = negative ? double.NegativeInfinity : double.PositiveInfinity;

            return true;
        }

        if ( unsigned.Equals( "nan", StringComparison.OrdinalIgnoreCase ) )
        {
            number = double.NaN;

            return true;
        }

        return double.TryParse( value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent,
            CultureInfo.InvariantCulture, out number );
    }

    // Rust Display for validator messages uses shortest digits without scientific notation.
    public static string FormatFloat( double number )
    {
        if ( double.IsNaN( number ) )
        {
            return "NaN";
        }

        if ( double.IsPositiveInfinity( number ) )
        {
            return "inf";
        }

        if ( double.IsNegativeInfinity( number ) )
        {
            return "-inf";
        }

        string text = number.ToString( "R", CultureInfo.InvariantCulture );
        int e = text.IndexOf( 'E' );
        if ( e < 0 )
        {
            return text;
        }

        string digits = text[..e];
        bool negative = digits[0] == '-';
        if ( negative )
        {
            digits = digits[1..];
        }

        int dot = digits.IndexOf( '.' );
        int decimalPlace = (dot < 0 ? digits.Length : dot) + int.Parse( text[(e + 1)..], CultureInfo.InvariantCulture );
        digits = digits.Replace( ".", "", StringComparison.Ordinal );
        string expanded = decimalPlace <= 0 ? "0." + new string( '0', -decimalPlace ) + digits :
            decimalPlace >= digits.Length ? digits + new string( '0', decimalPlace - digits.Length ) : digits.Insert( decimalPlace, "." );

        return negative ? "-" + expanded : expanded;
    }
}
