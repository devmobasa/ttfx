namespace Ttfx.Core;

public enum EaseKind
{
    Linear, InSine, OutSine, InOutSine, InQuad, OutQuad, InOutQuad,
    InCubic, OutCubic, InOutCubic, InQuart, OutQuart, InOutQuart,
    InQuint, OutQuint, InOutQuint, InExpo, OutExpo, InOutExpo,
    InCirc, OutCirc, InOutCirc, InBack, OutBack, InOutBack,
    InElastic, OutElastic, InOutElastic, InBounce, OutBounce, InOutBounce, CubicBezier
}

public readonly record struct Easing( EaseKind Kind, double X1 = 0, double Y1 = 0, double X2 = 0, double Y2 = 0 )
{
    private static readonly string[] Names =
    [
        "linear", "in_sine", "out_sine", "in_out_sine", "in_quad", "out_quad", "in_out_quad",
        "in_cubic", "out_cubic", "in_out_cubic", "in_quart", "out_quart", "in_out_quart",
        "in_quint", "out_quint", "in_out_quint", "in_expo", "out_expo", "in_out_expo",
        "in_circ", "out_circ", "in_out_circ", "in_back", "out_back", "in_out_back",
        "in_elastic", "out_elastic", "in_out_elastic", "in_bounce", "out_bounce", "in_out_bounce"
    ];

    public static bool TryParse( string value, out Easing easing )
    {
        // These are the only Unicode lowercase mappings that can form a registered name.
        string folded = string.Create( value.Length, value, static ( result, original ) =>
        {
            for ( int i = 0; i < result.Length; i++ )
            {
                result[i] = original[i] switch
                {
                    >= 'A' and <= 'Z' => ( char ) (original[i] + 32),
                    '\u212a' => 'k',
                    _ => original[i]
                };
            }
        } );
        int index = Array.IndexOf( Names, folded );
        easing = index < 0 ? default : new( ( EaseKind ) index );

        return index >= 0;
    }

    public double Apply( double p )
    {
        const double pi = Math.PI;
        const double c1 = 1.70158;
        const double c3 = c1 + 1.0;
        const double c2 = c1 * 1.525;
        const double c4 = (2.0 * pi) / 3.0;
        const double c5 = (2.0 * pi) / 4.5;

        return Kind switch
        {
            EaseKind.Linear => p,
            EaseKind.InSine => 1.0 - RustMath.Cos( (p * pi) / 2.0 ),
            EaseKind.OutSine => RustMath.Sin( (p * pi) / 2.0 ),
            EaseKind.InOutSine => -(RustMath.Cos( pi * p ) - 1.0) / 2.0,
            EaseKind.InQuad => RustMath.Square( p ),
            EaseKind.OutQuad => 1.0 - (1.0 - p) * (1.0 - p),
            EaseKind.InOutQuad => p < 0.5 ? 2.0 * RustMath.Square( p ) : 1.0 - RustMath.Square( -2.0 * p + 2.0 ) / 2.0,
            EaseKind.InCubic => RustMath.Pow( p, 3.0 ),
            EaseKind.OutCubic => 1.0 - RustMath.Pow( 1.0 - p, 3.0 ),
            EaseKind.InOutCubic => p < 0.5 ? 4.0 * RustMath.Pow( p, 3.0 ) : 1.0 - RustMath.Pow( -2.0 * p + 2.0, 3.0 ) / 2.0,
            EaseKind.InQuart => RustMath.Pow( p, 4.0 ),
            EaseKind.OutQuart => 1.0 - RustMath.Pow( 1.0 - p, 4.0 ),
            EaseKind.InOutQuart => p < 0.5 ? 8.0 * RustMath.Pow( p, 4.0 ) : 1.0 - RustMath.Pow( -2.0 * p + 2.0, 4.0 ) / 2.0,
            EaseKind.InQuint => RustMath.Pow( p, 5.0 ),
            EaseKind.OutQuint => 1.0 - RustMath.Pow( 1.0 - p, 5.0 ),
            EaseKind.InOutQuint => p < 0.5 ? 16.0 * RustMath.Pow( p, 5.0 ) : 1.0 - RustMath.Pow( -2.0 * p + 2.0, 5.0 ) / 2.0,
            EaseKind.InExpo => p == 0.0 ? 0.0 : RustMath.Exp2( 10.0 * p - 10.0 ),
            EaseKind.OutExpo => p == 1.0 ? 1.0 : 1.0 - RustMath.Exp2( -10.0 * p ),
            EaseKind.InOutExpo => p == 0.0 ? 0.0 : p == 1.0 ? 1.0 : p < 0.5 ? RustMath.Exp2( 20.0 * p - 10.0 ) / 2.0 : (2.0 - RustMath.Exp2( -20.0 * p + 10.0 )) / 2.0,
            EaseKind.InCirc => 1.0 - RustMath.Sqrt( 1.0 - RustMath.Square( p ) ),
            EaseKind.OutCirc => RustMath.Sqrt( 1.0 - RustMath.Square( p - 1.0 ) ),
            EaseKind.InOutCirc => p < 0.5 ? (1.0 - RustMath.Sqrt( 1.0 - RustMath.Square( 2.0 * p ) )) / 2.0 : (RustMath.Sqrt( 1.0 - RustMath.Square( -2.0 * p + 2.0 ) ) + 1.0) / 2.0,
            EaseKind.InBack => c3 * RustMath.Pow( p, 3.0 ) - c1 * RustMath.Square( p ),
            EaseKind.OutBack => 1.0 + c3 * RustMath.Pow( p - 1.0, 3.0 ) + c1 * RustMath.Square( p - 1.0 ),
            EaseKind.InOutBack => p < 0.5 ? (RustMath.Square( 2.0 * p ) * ((c2 + 1.0) * 2.0 * p - c2)) / 2.0 : (RustMath.Square( 2.0 * p - 2.0 ) * ((c2 + 1.0) * (p * 2.0 - 2.0) + c2) + 2.0) / 2.0,
            EaseKind.InElastic => p == 0.0 ? 0.0 : p == 1.0 ? 1.0 : -RustMath.Exp2( 10.0 * p - 10.0 ) * RustMath.Sin( (p * 10.0 - 10.75) * c4 ),
            EaseKind.OutElastic => p == 0.0 ? 0.0 : p == 1.0 ? 1.0 : RustMath.Exp2( -10.0 * p ) * RustMath.Sin( (p * 10.0 - 0.75) * c4 ) + 1.0,
            EaseKind.InOutElastic => p == 0.0 ? 0.0 : p == 1.0 ? 1.0 : p < 0.5 ? -(RustMath.Exp2( 20.0 * p - 10.0 ) * RustMath.Sin( (20.0 * p - 11.125) * c5 )) / 2.0 : (RustMath.Exp2( -20.0 * p + 10.0 ) * RustMath.Sin( (20.0 * p - 11.125) * c5 )) / 2.0 + 1.0,
            EaseKind.InBounce => 1.0 - OutBounce( 1.0 - p ),
            EaseKind.OutBounce => OutBounce( p ),
            EaseKind.InOutBounce => p < 0.5 ? (1.0 - OutBounce( 1.0 - 2.0 * p )) / 2.0 : (1.0 + OutBounce( 2.0 * p - 1.0 )) / 2.0,
            EaseKind.CubicBezier => Bezier( p ),
            _ => throw new ArgumentOutOfRangeException( nameof( Kind ) )
        };
    }

    private static double OutBounce( double p )
    {
        const double n = 7.5625, d = 2.75;
        if ( p < 1.0 / d )
        {
            return n * RustMath.Square( p );
        }

        if ( p < 2.0 / d )
        {
            return n * RustMath.Square( p - 1.5 / d ) + 0.75;
        }

        if ( p < 2.5 / d )
        {
            return n * RustMath.Square( p - 2.25 / d ) + 0.9375;
        }

        return n * RustMath.Square( p - 2.625 / d ) + 0.984375;
    }

    private double Bezier( double progress )
    {
        if ( progress <= 0.0 )
        {
            return 0.0;
        }

        if ( progress >= 1.0 )
        {
            return 1.0;
        }

        double t = progress;

        for ( int i = 0; i < 20; i++ )
        {
            double dx = 3.0 * X1 * RustMath.Square( 1.0 - t ) * t + 3.0 * X2 * (1.0 - t) * RustMath.Square( t ) + RustMath.Pow( t, 3.0 ) - progress;
            if ( Math.Abs( dx ) < 1e-5 )
            {
                break;
            }

            double derivative = 3.0 * RustMath.Square( 1.0 - t ) * X1 + 6.0 * (1.0 - t) * t * (X2 - X1) + 3.0 * RustMath.Square( t ) * (1.0 - X2);
            if ( Math.Abs( derivative ) < 1e-6 )
            {
                break;
            }

            t -= dx / derivative;
        }

        return 3.0 * Y1 * RustMath.Square( 1.0 - t ) * t + 3.0 * Y2 * (1.0 - t) * RustMath.Square( t ) + RustMath.Pow( t, 3.0 );
    }
}

public sealed class EasingTracker( Easing easing, long totalSteps, bool clamp )
{
    public long CurrentStep { get; private set; }
    public double Value { get; private set; }
    public double Delta { get; private set; }
    public bool IsComplete => CurrentStep >= totalSteps;

    public double Step( )
    {
        if ( !IsComplete )
        {
            CurrentStep++;
            double previous = Value;
            Value = easing.Apply( ( double ) CurrentStep / totalSteps );
            if ( clamp )
            {
                Value = RustMath.Max( RustMath.Min( Value, 1.0 ), 0.0 );
            }

            Delta = Value - previous;
        }

        return Value;
    }

    public void Reset( ) => (CurrentStep, Value, Delta) = (0, 0, 0);
}
