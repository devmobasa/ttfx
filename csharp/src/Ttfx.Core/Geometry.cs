namespace Ttfx.Core;

public readonly record struct Coord( long Column, long Row );

public static class Geometry
{
    public static Coord OnLine( Coord start, Coord end, double t ) => new(
        RustMath.RoundHalfEven( (1.0 - t) * start.Column + t * end.Column ),
        RustMath.RoundHalfEven( (1.0 - t) * start.Row + t * end.Row ) );

    public static double LineLength( Coord start, Coord end, bool doubleRow ) =>
        RustMath.Hypot( end.Column - start.Column, (end.Row - start.Row) * (doubleRow ? 2.0 : 1.0) );

    public static Coord Extrapolate( Coord origin, Coord target, double offset )
    {
        double length = LineLength( origin, target, false );
        if ( length + offset == 0.0 || origin == target )
        {
            return target;
        }

        return OnLine( origin, target, (length + offset) / length );
    }

    public static Coord OnBezier( Coord start, ReadOnlySpan<Coord> controls, Coord end, double t )
    {
        int length = checked(controls.Length + 2);
        Span<(double X, double Y)> points = length <= 32 ? stackalloc (double, double)[length] : new (double, double)[length];
        points[0] = (start.Column, start.Row);

        for ( int i = 0; i < controls.Length; i++ )
        {
            points[i + 1] = (controls[i].Column, controls[i].Row);
        }

        points[^1] = (end.Column, end.Row);

        for ( int remaining = length; remaining > 1; remaining-- )
        {
            for ( int i = 0; i < remaining - 1; i++ )
            {
                points[i] = ((1.0 - t) * points[i].X + t * points[i + 1].X,
                    (1.0 - t) * points[i].Y + t * points[i + 1].Y);
            }
        }

        return new( RustMath.RoundHalfEven( points[0].X ), RustMath.RoundHalfEven( points[0].Y ) );
    }

    public static double BezierLength( Coord start, ReadOnlySpan<Coord> controls, Coord end )
    {
        double length = 0;
        Coord previous = start;
        // The reference intentionally omits the last 0.9 -> 1.0 segment.
        for ( int i = 1; i < 10; i++ )
        {
            Coord current = OnBezier( start, controls, end, i / 10.0 );
            length += LineLength( previous, current, true );
            previous = current;
        }

        return length;
    }

    public static List<Coord> OnCircle( Coord origin, long radius, long limit, bool unique )
    {
        var result = new List<Coord>( );
        if ( radius == 0 )
        {
            return result;
        }

        if ( limit == 0 )
        {
            limit = RustMath.RoundHalfEven( 2.0 * Math.PI * radius );
        }

        double step = 2.0 * Math.PI / limit;
        var seen = unique ? new HashSet<Coord>( ) : null;

        for ( long i = 0; i < limit; i++ )
        {
            double angle = step * i;
            double x = origin.Column + radius * RustMath.Cos( angle );
            x += x - origin.Column;
            Coord coord = new( RustMath.RoundHalfEven( x ), RustMath.RoundHalfEven( origin.Row + radius * RustMath.Sin( angle ) ) );
            if ( seen is null || seen.Add( coord ) )
            {
                result.Add( coord );
            }
        }

        return result;
    }

    public static List<Coord> InCircle( Coord center, long diameter )
    {
        var result = new List<Coord>( );
        if ( diameter <= 0 )
        {
            return result;
        }

        double a2 = RustMath.Square( diameter );
        double b2 = RustMath.Square( diameter / 2.0 );

        for ( long x = center.Column - diameter; x <= center.Column + diameter; x++ )
        {
            double component = RustMath.Square( x - center.Column ) / a2;
            long offset = ( long ) RustMath.PowHalf( b2 * (1.0 - component) );

            for ( long y = center.Row - offset; y <= center.Row + offset; y++ )
            {
                result.Add( new( x, y ) );
            }
        }

        return result;
    }

    public static List<Coord> InRect( Coord origin, long distance )
    {
        var result = new List<Coord>( );
        if ( distance == 0 )
        {
            return result;
        }

        for ( long x = origin.Column - distance; x <= origin.Column + distance; x++ )
        {
            for ( long y = origin.Row - distance; y <= origin.Row + distance; y++ )
            {
                result.Add( new( x, y ) );
            }
        }

        return result;
    }

    public static List<Coord> OnRect( Coord origin, long halfWidth, long halfHeight )
    {
        var result = new List<Coord>( );
        if ( halfWidth == 0 || halfHeight == 0 )
        {
            return result;
        }

        for ( long x = origin.Column - halfWidth; x <= origin.Column + halfWidth; x++ )
        {
            if ( x == origin.Column - halfWidth || x == origin.Column + halfWidth )
            {
                for ( long y = origin.Row - halfHeight; y <= origin.Row + halfHeight; y++ )
                {
                    result.Add( new( x, y ) );
                }
            }
            else
            {
                result.Add( new( x, origin.Row - halfHeight ) );
                result.Add( new( x, origin.Row + halfHeight ) );
            }
        }

        return result;
    }

    public static double NormalizedDistance( long bottom, long top, long left, long right, Coord coord )
    {
        if ( coord.Column < left || coord.Column > right || coord.Row < bottom || coord.Row > top )
        {
            throw new ArgumentOutOfRangeException( nameof( coord ), "Coordinate is not within the rectangle." );
        }

        long col = coord.Column - (left - 1), row = coord.Row - (bottom - 1);
        right -= left - 1;
        top -= bottom - 1;
        double max = RustMath.PowHalf( RustMath.Square( right ) + RustMath.Square( top * 2 ) );
        double distance = RustMath.PowHalf( RustMath.Square( col - right / 2.0 ) + RustMath.Square( (row - top / 2.0) * 2.0 ) );

        return distance / (max / 2.0);
    }
}
