namespace Ttfx.Core;

public sealed record ErrorCorrectConfig
{
    public double Pairs { get; init; } = .1;
    public long Delay { get; init; } = 6;
    public Color ErrorColor { get; init; } = Color.FromHex( "e74c3c" );
    public Color CorrectColor { get; init; } = Color.FromHex( "45bf55" );
    public double MovementSpeed { get; init; } = .9;
    public Color[] Stops { get; init; } = [Color.FromHex( "8A008A" ), Color.FromHex( "00D1FF" ), Color.FromHex( "FFFFFF" )];
    public long[] Steps { get; init; } = [12];
    public GradientDirection GradientDirection { get; init; } = GradientDirection.Vertical;
}

public sealed class ErrorCorrect : EffectBase
{
    private readonly ErrorCorrectConfig config;
    private readonly Queue<(int First, int Second)> swapped = [];
    private long delay;

    public ErrorCorrect( Terminal terminal, ErrorCorrectConfig config, EngineRng rng ) : base( terminal, rng )
    {
        this.config = config;
        var final = new Gradient( config.Stops, config.Steps );
        bool dynamic = terminal.Config.ExistingColors == ExistingColors.Dynamic;
        foreach ( int id in InputOrder )
        {
            InputCell cell = terminal.Characters[id];
            Engine.AddScene( id, "spawn", new( [new( VisualBytes( id, cell.Symbol, dynamic ? cell.Foreground : FinalColor( id, final, config.GradientDirection ), dynamic ? cell.Background : null ), 1 )] ) );
            Engine.ActivateScene( id, "spawn" );
            terminal.Visible[id] = true;
        }

        var available = new OrderedSelection<int>( Enumerable.Range( 0, terminal.InputCount ) );
        var correcting = new Gradient( [config.ErrorColor, config.CorrectColor], [10] );
        long pairs = ( long ) (config.Pairs * terminal.InputCount);
        for ( long pair = 0; pair < pairs && available.Count >= 2; pair++ )
        {
            int index = rng.ChoiceIndex( available.Count ), first = available.TakeAt( index );
            index = rng.ChoiceIndex( available.Count );
            int second = available.TakeAt( index );
            terminal.Coordinates[first] = Home[second];
            terminal.Coordinates[second] = Home[first];
            swapped.Enqueue( (first, second) );
            foreach ( int id in new[] { first, second } )
            {
                Engine.AddPath( id, "home", new( [new Waypoint( "0", Home[id] )], config.MovementSpeed ) );
                InputCell cell = terminal.Characters[id];
                string[] start = ["▁", "▂", "▃", "▄", "▅", "▆", "▇", "█"], end = ["▇", "▆", "▅", "▄", "▃", "▂", "▁"];
                Engine.AddScene( id, "first", new( start.Select( symbol => new SceneFrame( VisualBytes( id, symbol, config.ErrorColor ), 3 ) ) ) );
                Engine.AddScene( id, "last", new( end.Select( ( symbol, i ) => new SceneFrame( VisualBytes( id, symbol, dynamic && i == end.Length - 1 ? cell.Foreground : config.CorrectColor, dynamic && i == end.Length - 1 ? cell.Background : null ), 3 ) ) ) );
                Engine.AddScene( id, "initial", new( [new( VisualBytes( id, cell.Symbol, config.ErrorColor ), 1 )] ) );
                Engine.ActivateScene( id, "initial" );
                var errors = new List<SceneFrame>( );
                for ( int blink = 0; blink < 10; blink++ )
                {
                    errors.Add( new( VisualBytes( id, "▓", config.ErrorColor ), 3 ) );
                    errors.Add( new( VisualBytes( id, cell.Symbol, Color.FromHex( "ffffff" ) ), 3 ) );
                }

                Engine.AddScene( id, "error", new( errors ) );
                Engine.AddScene( id, "correcting", GradientScene( id, ["█"], 3, correcting, sync: SyncMetric.Distance ) );
                Engine.AddScene( id, "final", FadeToFinal( id, final, config.GradientDirection, config.CorrectColor, 10, 3 ) );
                Engine.Register( id, EngineEvent.SceneComplete, new( CallerKind.Scene, "error" ), static ( engine, character ) => engine.ActivateScene( character, "first" ) );
                Engine.Register( id, EngineEvent.SceneComplete, new( CallerKind.Scene, "first" ), static ( engine, character ) => engine.ActivateScene( character, "correcting" ) );
                Engine.Register( id, EngineEvent.SceneComplete, new( CallerKind.Scene, "first" ), static ( engine, character ) => engine.ActivatePath( character, "home" ) );
                Engine.Register( id, EngineEvent.PathActivated, new( CallerKind.Path, "home" ), static ( engine, character ) => engine.SetLayer( character, 1 ) );
                Engine.Register( id, EngineEvent.PathComplete, new( CallerKind.Path, "home" ), static ( engine, character ) => engine.SetLayer( character, 0 ) );
                Engine.Register( id, EngineEvent.PathComplete, new( CallerKind.Path, "home" ), static ( engine, character ) => engine.ActivateScene( character, "last" ) );
                Engine.Register( id, EngineEvent.SceneComplete, new( CallerKind.Scene, "last" ), static ( engine, character ) => engine.ActivateScene( character, "final" ) );
            }
        }
    }

    public override bool NextFrame( )
    {
        if ( swapped.Count > 0 && delay == 0 )
        {
            (int first, int second) = swapped.Dequeue( );
            foreach ( int id in new[] { first, second } )
            {
                Engine.ActivateScene( id, "error" );
                Engine.Activate( id );
            }

            delay = config.Delay;
        }
        else if ( delay != 0 )
        {
            delay--;
        }

        if ( !Engine.HasActive )
        {
            return false;
        }

        Engine.Update( );
        return true;
    }
}
