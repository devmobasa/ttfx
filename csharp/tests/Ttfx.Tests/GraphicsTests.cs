using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ttfx.Core;

namespace Ttfx.Tests;

[TestClass]
public sealed class GraphicsTests
{
    [TestMethod]
    public void SharedGraphicsGoldens( )
    {
        var gradient = new Gradient( [Color.FromHex( "8A008A" ), Color.FromHex( "00D1FF" ), Color.FromHex( "FFFFFF" )], [12] );

        foreach ( string line in File.ReadLines( Fixtures.PathFor( "graphics_goldens.txt" ) ) )
        {
            string[] fields = line.Split( ": ", 2 );
            string label = fields[0], actual;
            if ( label.StartsWith( "grad ", StringComparison.Ordinal ) )
            {
                string[] parts = label.Split( ' ' );
                Color[] colors = parts[1].Split( '+' ).Select( Color.FromHex ).ToArray( );
                string stepText = label[(label.IndexOf( "s=(", StringComparison.Ordinal ) + 3)..label.IndexOf( ')' )];
                long[] steps = stepText.Split( ',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries )
                    .Select( s => long.Parse( s, CultureInfo.InvariantCulture ) ).ToArray( );
                var g = new Gradient( colors, steps, label.EndsWith( "True", StringComparison.Ordinal ) );
                actual = string.Join( ';', g.Spectrum.Select( c => c.Hex ) );
            }
            else if ( label.StartsWith( "frac ", StringComparison.Ordinal ) )
            {
                actual = gradient.AtFraction( double.Parse( label[5..], CultureInfo.InvariantCulture ) ).Hex;
            }
            else if ( label.StartsWith( "shift ", StringComparison.Ordinal ) )
            {
                actual = Color.ShiftTowards( Color.FromHex( "ff8040" ), Color.FromHex( "103050" ),
                    double.Parse( label[6..], CultureInfo.InvariantCulture ) ).Hex;
            }
            else if ( label.StartsWith( "mapping", StringComparison.Ordinal ) )
            {
                string direction = label.Split( ' ' )[1];
                GradientDirection kind = direction switch
                {
                    "VERTICAL" => GradientDirection.Vertical,
                    "HORIZONTAL" => GradientDirection.Horizontal,
                    "RADIAL" => GradientDirection.Radial,
                    "DIAGONAL" => GradientDirection.Diagonal,
                    _ => throw new InvalidDataException( label )
                };
                bool offset = label.StartsWith( "mapping_offset", StringComparison.Ordinal );
                long bottom = offset ? 2 : 1, top = offset ? 6 : 5, left = offset ? 3 : 1, right = offset ? 9 : 8;
                var entries = new List<string>( );
                void Add( long c, long r ) => entries.Add( $"{c},{r}={gradient.AtCoordinate( bottom, top, left, right, new( c, r ), kind ).Hex}" );
                if ( kind == GradientDirection.Horizontal )
                {
                    for ( long c = left; c <= right; c++ )
                    {
                        for ( long r = bottom; r <= top; r++ )
                        {
                            Add( c, r );
                        }
                    }
                }
                else
                {
                    for ( long r = bottom; r <= top; r++ )
                    {
                        for ( long c = left; c <= right; c++ )
                        {
                            Add( c, r );
                        }
                    }
                }

                actual = string.Join( ';', entries );
            }
            else
            {
                throw new InvalidDataException( label );
            }

            Assert.AreEqual( fields[1], actual, label );
        }
    }

    [TestMethod]
    public void ColorIdentityRetainsArgumentTypeAndHexCase( )
    {
        Assert.AreNotEqual( Color.FromHex( "ffffff" ), Color.FromHex( "FFFFFF" ) );
        Assert.AreNotEqual( Color.FromHex( "ffffff" ), Color.FromXterm( 15 ) );
        Assert.AreEqual( ( byte ) 9, Color.FromHex( "ff0000" ).ClosestXterm( ) );
        Assert.AreEqual( "1234567", Color.FromHex( "###1234567##" ).Hex );
    }
}
