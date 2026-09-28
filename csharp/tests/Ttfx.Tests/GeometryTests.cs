using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ttfx.Core;

namespace Ttfx.Tests;

[TestClass]
public sealed class GeometryTests
{
    [TestMethod]
    public void SharedGeometryGoldens( )
    {
        foreach ( string line in File.ReadLines( Fixtures.PathFor( "geometry_goldens.txt" ) ) )
        {
            string[] fields = line.Split( ": ", 2, StringSplitOptions.None );
            string label = fields[0], expected = fields[1];
            long[] numbers = Regex.Matches( label, @"-?\d+" ).Select( m => long.Parse( m.Value, CultureInfo.InvariantCulture ) ).ToArray( );
            IEnumerable<Coord>? points = null;
            double? length = null;
            if ( label.StartsWith( "on_circle", StringComparison.Ordinal ) )
            {
                points = Geometry.OnCircle( new( 10, 10 ), numbers[0], numbers[1], label.EndsWith( "True", StringComparison.Ordinal ) );
            }
            else if ( label.StartsWith( "in_circle", StringComparison.Ordinal ) )
            {
                points = Geometry.InCircle( new( 5, -3 ), numbers[0] );
            }
            else if ( label.StartsWith( "in_rect", StringComparison.Ordinal ) )
            {
                points = Geometry.InRect( new( 3, 4 ), numbers[0] );
            }
            else if ( label.StartsWith( "on_rect", StringComparison.Ordinal ) )
            {
                points = Geometry.OnRect( new( 0, 0 ), numbers[0], numbers[1] );
            }
            else if ( label.StartsWith( "extrapolate", StringComparison.Ordinal ) )
            {
                string[] parts = label["extrapolate ".Length..].Split( '+' );
                string[] coords = parts[0].Split( "->" );
                points = [Geometry.Extrapolate( ParseCoord( coords[0] ), ParseCoord( coords[1] ), double.Parse( parts[1], CultureInfo.InvariantCulture ) )];
            }
            else if ( label.StartsWith( "bezier", StringComparison.Ordinal ) )
            {
                (Coord start, Coord[] control, Coord end) = numbers[0] switch
                {
                    1 => (new Coord( 0, 0 ), new Coord[] { new( 5, 10 ) }, new Coord( 10, 0 )),
                    2 => (new Coord( 0, 0 ), new Coord[] { new( 3, 8 ), new( 7, -2 ) }, new Coord( 12, 4 )),
                    3 => (new Coord( -4, -4 ), new Coord[] { new( 0, 20 ), new( 9, 9 ), new( -3, 2 ) }, new Coord( 6, -6 )),
                    _ => throw new InvalidDataException( label )
                };
                if ( label.StartsWith( "bezier_len", StringComparison.Ordinal ) )
                {
                    length = Geometry.BezierLength( start, control, end );
                }
                else
                {
                    points = Enumerable.Range( 0, 21 ).Select( i => Geometry.OnBezier( start, control, end, i / 20.0 ) );
                }
            }
            else if ( label == "on_line" )
            {
                points = Enumerable.Range( -5, 31 ).Select( i => Geometry.OnLine( new( -3, 7 ), new( 14, -2 ), i / 20.0 ) );
            }
            else if ( label.StartsWith( "line_len", StringComparison.Ordinal ) )
            {
                length = Geometry.LineLength( new( 1, 2 ), new( -7, 11 ), label.EndsWith( "True", StringComparison.Ordinal ) );
            }
            else if ( label.StartsWith( "norm_dist", StringComparison.Ordinal ) )
            {
                length = Geometry.NormalizedDistance( 1, 8, 1, 10, new( numbers[0], numbers[1] ) );
            }
            else
            {
                Assert.Fail( $"Unknown fixture line: {label}" );
            }

            string actual = points is not null ? string.Join( ';', points.Select( c => $"{c.Column},{c.Row}" ) ) :
                Convert.ToHexStringLower( BitConverter.GetBytes( length!.Value ) );
            Assert.AreEqual( expected, actual, label );
        }
    }

    private static Coord ParseCoord( string value )
    {
        string[] parts = value.Split( ',' );

        return new( long.Parse( parts[0], CultureInfo.InvariantCulture ), long.Parse( parts[1], CultureInfo.InvariantCulture ) );
    }
}
