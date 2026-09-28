namespace Ttfx.Core;

public sealed class PrimsWeighted
{
    private readonly Terminal terminal;
    private readonly EngineRng rng;
    private readonly Coord[] home;
    private readonly bool textOnly;
    private readonly Dictionary<Coord, int> at;
    private readonly long[] weights;
    private readonly SortedDictionary<long, List<(int From, int To)>> pending = [];
    public SortedSet<int>[] Links { get; }
    public bool Complete { get; private set; }

    public PrimsWeighted( Terminal terminal, EngineRng rng, Coord[] home, int start, bool textOnly )
    {
        this.terminal = terminal;
        this.rng = rng;
        this.home = home;
        this.textOnly = textOnly;
        at = Enumerable.Range( 0, home.Length ).ToDictionary( id => home[id], id => id );
        weights = new long[home.Length];
        Links = Enumerable.Range( 0, home.Length ).Select( _ => new SortedSet<int>( ) ).ToArray( );
        foreach ( int id in Enumerable.Range( 0, home.Length ).OrderByDescending( id => home[id].Row ).ThenBy( id => home[id].Column ) )
        {
            weights[id] = rng.RandInt( 0, 99 );
        }

        AddLinks( start );
    }

    private void AddLinks( int id )
    {
        Coord coord = home[id];
        Coord[] neighbors = [new( coord.Column, coord.Row + 1 ), new( coord.Column + 1, coord.Row ), new( coord.Column, coord.Row - 1 ), new( coord.Column - 1, coord.Row )];
        foreach ( Coord next in neighbors )
        {
            bool within = next.Column >= terminal.TextLeft && next.Column <= terminal.TextRight && next.Row >= terminal.TextBottom && next.Row <= terminal.TextTop;
            if ( (!textOnly || within) && at.TryGetValue( next, out int other ) && Links[other].Count == 0 )
            {
                long weight = weights[other];
                if ( !pending.TryGetValue( weight, out var links ) )
                {
                    links = [];
                    pending.Add( weight, links );
                }

                links.Add( (id, other) );
            }
        }
    }

    public void Step( )
    {
        while ( pending.Count > 0 )
        {
            var bucket = pending.First( );
            int index = rng.ChoiceIndex( bucket.Value.Count );
            (int from, int to) = bucket.Value[index];
            bucket.Value.RemoveAt( index );
            if ( bucket.Value.Count == 0 )
            {
                pending.Remove( bucket.Key );
            }

            if ( Links[to].Count != 0 )
            {
                continue;
            }

            Links[from].Add( to );
            Links[to].Add( from );
            AddLinks( to );
            return;
        }

        Complete = true;
    }
}
