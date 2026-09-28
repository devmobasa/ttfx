using System.Collections;
using System.Runtime.InteropServices;

namespace Ttfx.Core;

// Preserve List ordering and random indices while making front removal amortized O(1).
internal sealed class OrderedQueue<T> : IReadOnlyList<T>
{
    private readonly List<T> items = [];
    private int head;
    public int Count => items.Count - head;
    public T this[int index] => items[checked(head + index)];

    public void Add( T item )
    {
        if ( head > 0 && head >= items.Count / 2 )
        {
            items.RemoveRange( 0, head );
            head = 0;
        }

        items.Add( item );
    }

    public void AddRange( IEnumerable<T> values )
    {
        foreach ( T value in values )
        {
            Add( value );
        }
    }

    public void Clear( )
    {
        items.Clear( );
        head = 0;
    }

    public void RemoveAt( int index )
    {
        ArgumentOutOfRangeException.ThrowIfNegative( index );
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual( index, Count );
        if ( index == 0 )
        {
            items[head++] = default!;
        }
        else
        {
            items.RemoveAt( head + index );
        }
    }

    public void RemoveAll( Predicate<T> predicate )
    {
        if ( head > 0 )
        {
            items.RemoveRange( 0, head );
            head = 0;
        }

        items.RemoveAll( predicate );
    }

    public void Reverse( ) => items.Reverse( head, Count );
    internal Span<T> AsSpan( ) => CollectionsMarshal.AsSpan( items )[head..];

    public Enumerator GetEnumerator( ) => new( this );
    IEnumerator<T> IEnumerable<T>.GetEnumerator( ) => GetEnumerator( );
    IEnumerator IEnumerable.GetEnumerator( ) => GetEnumerator( );

    public struct Enumerator : IEnumerator<T>
    {
        private readonly OrderedQueue<T> owner;
        private int position;
        internal Enumerator( OrderedQueue<T> owner )
        {
            this.owner = owner;
            position = owner.head - 1;
        }

        public T Current => owner.items[position];
        object? IEnumerator.Current => Current;
        public bool MoveNext( ) => ++position < owner.items.Count;
        public void Reset( ) => position = owner.head - 1;
        public void Dispose( ) { }
    }
}
