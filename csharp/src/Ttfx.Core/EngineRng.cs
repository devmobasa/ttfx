using System.Numerics;
using System.Runtime.CompilerServices;

namespace Ttfx.Core;

public sealed class EngineRng
{
    private ulong s0, s1, s2, s3;

    public EngineRng( ulong seed )
    {
        s0 = SplitMix( ref seed );
        s1 = SplitMix( ref seed );
        s2 = SplitMix( ref seed );
        s3 = SplitMix( ref seed );
    }

    public EngineRng( ulong a, ulong b, ulong c, ulong d ) => (s0, s1, s2, s3) = (a, b, c, d);
    public (ulong, ulong, ulong, ulong) State => (s0, s1, s2, s3);

    private static ulong SplitMix( ref ulong seed )
    {
        unchecked
        {
            seed += 0x9E3779B97F4A7C15;
            ulong z = seed;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EB;

            return z ^ (z >> 31);
        }
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public ulong NextUInt64( )
    {
        unchecked
        {
            ulong result = BitOperations.RotateLeft( s0 + s3, 23 ) + s0;
            ulong t = s1 << 17;
            s2 ^= s0;
            s3 ^= s1;
            s1 ^= s2;
            s0 ^= s3;
            s2 ^= t;
            s3 = BitOperations.RotateLeft( s3, 45 );

            return result;
        }
    }

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public double Random( ) => (NextUInt64( ) >> 11) * (1.0 / (1UL << 53));
    // Exactly Random() < chance with the same 53-bit draw. Scaling by a power
    // of two is exact; ceil preserves strict comparison between integer draws.
    internal static ulong ChanceThreshold( double chance ) => !(chance > 0) ? 0 :
        chance >= 1 ? 1UL << 53 : ( ulong ) Math.Ceiling( chance * (1UL << 53) );

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    internal bool Chance( ulong threshold ) => (NextUInt64( ) >> 11) < threshold;

    public double Uniform( double a, double b ) => a + (b - a) * Random( );

    private ulong Below( ulong n )
    {
        ArgumentOutOfRangeException.ThrowIfZero( n );
        int bits = Math.Max( 1, 64 - BitOperations.LeadingZeroCount( n - 1 ) );

        while ( true )
        {
            ulong value = NextUInt64( ) >> (64 - bits);
            if ( value < n )
            {
                return value;
            }
        }
    }

    public long RandInt( long a, long b )
    {
        if ( a > b )
        {
            throw new ArgumentOutOfRangeException( nameof( b ) );
        }

        return unchecked(a + ( long ) Below( unchecked(( ulong ) (b - a + 1)) ));
    }

    public long RandRange( long a, long b )
    {
        if ( a >= b )
        {
            throw new ArgumentOutOfRangeException( nameof( b ) );
        }

        return unchecked(a + ( long ) Below( unchecked(( ulong ) (b - a)) ));
    }

    public int ChoiceIndex( int length ) => checked(( int ) Below( checked(( ulong ) length) ));

    public void Shuffle<T>( Span<T> values )
    {
        for ( int i = values.Length - 1; i > 0; i-- )
        {
            int j = ChoiceIndex( i + 1 );
            (values[i], values[j]) = (values[j], values[i]);
        }
    }
}
