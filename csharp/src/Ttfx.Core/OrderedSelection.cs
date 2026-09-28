using System.Numerics;

namespace Ttfx.Core;

// Order-statistic removal: the same surviving index as List.RemoveAt, without shifting.
internal sealed class OrderedSelection<T>
{
    private readonly T[] values;
    private readonly int[] counts;
    public int Count { get; private set; }

    internal OrderedSelection( IEnumerable<T> source )
    {
        values = source.ToArray( );
        Count = values.Length;
        counts = new int[Count + 1];
        for ( int i = 1; i < counts.Length; i++ )
        {
            counts[i] = i & -i;
        }
    }

    internal T TakeAt( int index )
    {
        ArgumentOutOfRangeException.ThrowIfNegative( index );
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual( index, Count );
        int position = 0, target = index + 1;
        int bit = 1 << BitOperations.Log2( ( uint ) values.Length );
        for ( ; bit != 0; bit >>= 1 )
        {
            int next = position + bit;
            if ( next < counts.Length && counts[next] < target )
            {
                position = next;
                target -= counts[next];
            }
        }

        T result = values[position];
        values[position] = default!;
        for ( int i = position + 1; i < counts.Length; i += i & -i )
        {
            counts[i]--;
        }

        Count--;

        return result;
    }
}
