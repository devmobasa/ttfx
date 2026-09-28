namespace Ttfx.Core;

public sealed record SmokeConfig
{
    public Color StartingColor { get; init; } = Color.FromHex( "7A7A7A" );
    public string[] Symbols { get; init; } = ["░", "▒", "▓", "▒", "░"];
    public Color[] SmokeColors { get; init; } = [Color.FromHex( "242424" ), Color.FromHex( "FFFFFF" )];
    public bool WholeCanvas { get; init; }
    public Color[] Stops { get; init; } = [Color.FromHex( "8A008A" ), Color.FromHex( "00D1FF" ), Color.FromHex( "FFFFFF" )];
    public long[] Steps { get; init; } = [12];
    public GradientDirection GradientDirection { get; init; } = GradientDirection.Vertical;
}

public sealed class Smoke : EffectBase
{
    private readonly SortedSet<int>[] links;
    private List<int> frontier;
    private readonly HashSet<int> explored;
    private bool complete;

    public Smoke( Terminal terminal, SmokeConfig config, EngineRng rng ) : base( terminal, rng )
    {
        int start = Array.IndexOf( Home, RandomCoord( text: !config.WholeCanvas ) );
        var tree = new PrimsWeighted( terminal, rng, Home, start, !config.WholeCanvas );
        int fillStart = Array.IndexOf( Home, RandomCoord( text: !config.WholeCanvas ) );
        frontier = [fillStart];
        explored = [fillStart];
        links = tree.Links;
        var final = new Gradient( config.Stops, config.Steps );
        Color black = Color.FromHex( "000000" );
        var smoke = new Gradient( [.. config.SmokeColors, .. config.Stops.Reverse( )], [3, 4] );
        bool dynamic = terminal.Config.ExistingColors == ExistingColors.Dynamic;
        foreach ( int id in Enumerable.Range( 0, Home.Length ).OrderByDescending( id => Home[id].Row ).ThenBy( id => Home[id].Column ) )
        {
            InputCell cell = terminal.Characters[id];
            bool inText = Home[id].Column >= terminal.TextLeft && Home[id].Column <= terminal.TextRight && Home[id].Row >= terminal.TextBottom && Home[id].Row <= terminal.TextTop;
            Color? fg = dynamic ? cell.Foreground : inText ? FinalColor( id, final, config.GradientDirection ) : black;
            Color? bg = dynamic ? cell.Background : null;
            SceneDefinition paint = dynamic ? new( [new( VisualBytes( id, cell.Symbol, fg, bg ), 5 )] ) : GradientScene( id, [cell.Symbol], 5, MakeGradient( [.. config.Stops, fg!.Value], [5] ) );
            Engine.AddScene( id, "paint", paint );
            SceneDefinition scene = dynamic ? new( config.Symbols.Select( symbol => new SceneFrame( VisualBytes( id, symbol, fg, bg ), 10 ) ) ) : GradientScene( id, config.Symbols, 3, smoke );
            Engine.AddScene( id, "smoke", scene );
            Engine.Register( id, EngineEvent.SceneComplete, new( CallerKind.Scene, "smoke" ), static ( engine, character ) => engine.ActivateScene( character, "paint" ) );
            Appearance( id, dynamic ? black : config.StartingColor );
            terminal.Visible[id] = true;
        }

        while ( !tree.Complete )
        {
            tree.Step( );
        }

        Engine.ActivateScene( fillStart, "smoke" );
        Engine.Activate( fillStart );
    }

    public override bool NextFrame( )
    {
        if ( complete && !Engine.HasActive )
        {
            return false;
        }

        if ( !complete )
        {
            if ( frontier.Count == 0 )
            {
                complete = true;
            }
            else
            {
                var next = new List<int>( );
                foreach ( int position in frontier )
                {
                    foreach ( int id in links[position] )
                    {
                        if ( explored.Add( id ) )
                        {
                            next.Add( id );
                            Engine.ActivateScene( id, "smoke" );
                            Engine.Activate( id );
                        }
                    }
                }

                frontier = next;
            }
        }

        Engine.Update( );
        return true;
    }
}
