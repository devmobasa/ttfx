using System.Text;

namespace Ttfx.Core;

public sealed record DecryptConfig
{
    public long TypingSpeed { get; init; } = 2;
    public Color[] CipherColors { get; init; } = [Color.FromHex( "008000" ), Color.FromHex( "00cb00" ), Color.FromHex( "00ff00" )];
    public Color[] Stops { get; init; } = [Color.FromHex( "eda000" )];
    public long[] Steps { get; init; } = [12];
    public GradientDirection GradientDirection { get; init; }
}

public sealed class Decrypt : IEffect
{
    private readonly Terminal terminal;
    private readonly DecryptConfig config;
    private readonly EngineRng rng;
    private readonly int[] order;
    private readonly Engine engine;
    private static readonly string[] SceneNames = ["typing", "fast", "slow", "found"];
    private int typingPosition;
    private bool decrypting;

    public Decrypt( Terminal terminal, DecryptConfig config, EngineRng rng )
    {
        this.terminal = terminal;
        this.config = config;
        this.rng = rng;
        order = Enumerable.Range( 0, terminal.InputCount ).OrderByDescending( i => terminal.Coordinates[i].Row )
            .ThenBy( i => terminal.Coordinates[i].Column ).ToArray( );
        int count = terminal.InputCount;
        engine = new( terminal );
        var scenes = new SceneFrame[count][][];
        var symbols = new List<string>( );
        foreach ( var (start, end) in new[] { (33, 127), (9608, 9632), (9472, 9599), (174, 452) } )
        {
            for ( int code = start; code < end; code++ )
            {
                symbols.Add( new Rune( code ).ToString( ) );
            }
        }

        string[] blocks = ["▉", "▓", "▒", "░"];
        byte[] Format( int id, string symbol, Color? fg, Color? bg = null )
        {
            InputCell cell = terminal.Characters[id];
            bool always = terminal.Config.ExistingColors == ExistingColors.Always;
            var visual = new Visual( symbol, always ? cell.Foreground : fg, always ? cell.Background : bg, always && cell.Bold );
            return engine.Intern( visual );
        }
        Color Cipher( ) => config.CipherColors[rng.ChoiceIndex( config.CipherColors.Length )];

        foreach ( int id in order )
        {
            scenes[id] = new SceneFrame[4][];
            var typing = new SceneFrame[5];

            for ( int b = 0; b < 4; b++ )
            {
                typing[b] = new( Format( id, blocks[b], Cipher( ) ), 2 );
            }

            string symbol = symbols[rng.ChoiceIndex( symbols.Count )];
            typing[4] = new( Format( id, symbol, Cipher( ) ), 1 );
            scenes[id][0] = typing;
        }

        var finalGradient = new Gradient( config.Stops, config.Steps );
        Color white = Color.FromHex( "ffffff" );

        foreach ( int id in order )
        {
            Color cipher = Cipher( );
            var fast = new SceneFrame[80];

            for ( int i = 0; i < fast.Length; i++ )
            {
                fast[i] = new( Format( id, symbols[rng.ChoiceIndex( symbols.Count )], cipher ), 2 );
            }

            scenes[id][1] = fast;
            int slowCount = checked(( int ) rng.RandInt( 1, 15 ));
            var slow = new SceneFrame[slowCount];

            for ( int i = 0; i < slowCount; i++ )
            {
                string symbol = symbols[rng.ChoiceIndex( symbols.Count )];
                long duration = rng.RandInt( 0, 100 ) <= 30 ? rng.RandRange( 35, 60 ) : rng.RandRange( 3, 6 );
                slow[i] = new( Format( id, symbol, cipher ), duration );
            }

            scenes[id][2] = slow;
            InputCell cell = terminal.Characters[id];
            if ( terminal.Config.ExistingColors == ExistingColors.Dynamic )
            {
                Gradient? fg = cell.Foreground is { } f ? new( [white, f], [10] ) : null;
                Gradient? bg = cell.Background is { } b ? new( [white, b], [10] ) : null;
                if ( fg is null && bg is null )
                {
                    scenes[id][3] = [new( Format( id, cell.Symbol, null ), 5 )];
                }
                else
                {
                    var found = new SceneFrame[11];

                    for ( int i = 0; i < 11; i++ )
                    {
                        found[i] = new( Format( id, cell.Symbol, fg?.Spectrum[i], bg?.Spectrum[i] ), 5 );
                    }

                    scenes[id][3] = found;
                }
            }
            else
            {
                Color final = finalGradient.AtCoordinate( terminal.TextBottom, terminal.TextTop, terminal.TextLeft, terminal.TextRight,
                    terminal.Coordinates[id], config.GradientDirection );
                var spectrum = new Gradient( [white, final], [10] );
                scenes[id][3] = spectrum.Spectrum.Select( color => new SceneFrame( Format( id, cell.Symbol, color ), 5 ) ).ToArray( );
            }
            // The source prepares fast_decrypt before typing, without adding it to the active set.
            for ( int stage = 0; stage < SceneNames.Length; stage++ )
            {
                engine.AddScene( id, SceneNames[stage], new( scenes[id][stage] ) );
            }

            engine.Register( id, EngineEvent.SceneComplete, new( CallerKind.Scene, "fast" ), static ( context, character ) => context.ActivateScene( character, "slow" ) );
            engine.Register( id, EngineEvent.SceneComplete, new( CallerKind.Scene, "slow" ), static ( context, character ) => context.ActivateScene( character, "found" ) );
            engine.ActivateScene( id, "fast" );
        }
    }

    private void Activate( int id, int stage )
    {
        engine.ActivateScene( id, SceneNames[stage] );
        engine.Activate( id );
    }

    public bool NextFrame( )
    {
        if ( !decrypting )
        {
            if ( typingPosition < order.Length || engine.HasActive )
            {
                if ( typingPosition < order.Length && rng.RandInt( 0, 100 ) <= 75 )
                {
                    for ( long i = 0; i < config.TypingSpeed && typingPosition < order.Length; i++ )
                    {
                        int id = order[typingPosition++];
                        terminal.Visible[id] = true;
                        Activate( id, 0 );
                    }
                }

                engine.Update( );

                return true;
            }

            foreach ( int id in order )
            {
                Activate( id, 1 );
            }

            decrypting = true;
        }

        if ( !engine.HasActive )
        {
            return false;
        }

        engine.Update( );

        return true;
    }

}
