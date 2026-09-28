namespace Ttfx.Core;

public sealed class PrimsSimple
{
    private readonly Terminal terminal;
    private readonly EngineRng rng;
    private readonly Coord[] coordinates;
    private readonly Dictionary<Coord, int> atCoordinate;
    private readonly bool[] linked;
    private readonly List<int> frontier;
    public List<int> Order { get; }
    public bool Complete { get; private set; }

    public PrimsSimple( Terminal terminal, EngineRng rng, Coord[] coordinates, int startingCharacter )
    {
        this.terminal = terminal;
        this.rng = rng;
        this.coordinates = coordinates;
        atCoordinate = Enumerable.Range( 0, coordinates.Length ).ToDictionary( id => coordinates[id], id => id );
        linked = new bool[coordinates.Length];
        frontier = [startingCharacter];
        Order = [startingCharacter];
    }

    private List<int> Neighbors( int id )
    {
        Coord origin = coordinates[id];
        Coord[] candidates = [new( origin.Column, origin.Row + 1 ), new( origin.Column + 1, origin.Row ), new( origin.Column, origin.Row - 1 ), new( origin.Column - 1, origin.Row )];
        var result = new List<int>( 4 );
        foreach ( Coord coordinate in candidates )
        {
            if ( coordinate.Column >= terminal.TextLeft && coordinate.Column <= terminal.TextRight && coordinate.Row >= terminal.TextBottom && coordinate.Row <= terminal.TextTop && atCoordinate.TryGetValue( coordinate, out int neighbor ) && !linked[neighbor] )
            {
                result.Add( neighbor );
            }
        }

        return result;
    }

    public void Step( )
    {
        if ( frontier.Count == 0 )
        {
            Complete = true;
            return;
        }

        int index = rng.ChoiceIndex( frontier.Count ), current = frontier[index];
        frontier.RemoveAt( index );
        List<int> neighbors = Neighbors( current );
        if ( neighbors.Count == 0 )
        {
            return;
        }

        index = rng.ChoiceIndex( neighbors.Count );
        int next = neighbors[index];
        neighbors.RemoveAt( index );
        linked[current] = linked[next] = true;
        Order.Add( next );
        if ( neighbors.Count != 0 )
        {
            frontier.Add( current );
        }

        if ( Neighbors( next ).Count != 0 )
        {
            frontier.Add( next );
        }
    }
}
