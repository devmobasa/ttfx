namespace Ttfx.Core;

internal sealed class SlotStore<T> where T : class
{
    private readonly List<T?> items = [];
    private readonly Stack<int> free = [];
    internal T this[int slot] => items[slot] ?? throw new EngineException( "Engine slot has been released." );

    internal void EnsureCapacity( int capacity ) => items.EnsureCapacity( capacity );

    internal int Add( T item )
    {
        if ( free.TryPop( out int slot ) )
        {
            items[slot] = item;

            return slot;
        }

        items.Add( item );

        return items.Count - 1;
    }

    internal void Remove( int slot )
    {
        if ( items[slot] is null )
        {
            return;
        }

        items[slot] = null;
        free.Push( slot );
    }
}
