namespace Ttfx.Core;

public abstract class EffectBase : IEffect
{
    protected readonly Terminal Terminal;
    protected readonly Engine Engine;
    protected readonly EngineRng Rng;
    protected readonly Coord[] Home;
    protected readonly int[] InputOrder;
    protected readonly HashSet<int> PreservedInputColors = [];
    private readonly Dictionary<(Color First, Color Last, long Steps, bool Loop), Gradient> gradients = [];
    private readonly Dictionary<string, EngineCallback> pathCallbacks = new( StringComparer.Ordinal );
    private readonly Dictionary<SceneKey, SceneDefinition> gradientScenes = [];
    private readonly record struct SceneKey( string Symbols, int SymbolCount, Gradient? Foreground, Gradient? Background, long Duration, bool Loop, Easing? Ease, SyncMetric? Sync, Color? PreservedFg, Color? PreservedBg, bool PreservedBold, bool Preserve );

    protected Gradient MakeGradient( Color[] stops, long[] steps, bool loop = false )
    {
        if ( stops.Length != 2 || steps.Length != 1 )
        {
            return new( stops, steps, loop );
        }

        var key = (stops[0], stops[1], steps[0], loop);
        if ( !gradients.TryGetValue( key, out Gradient? gradient ) )
        {
            gradient = new( stops, steps, loop );
            gradients.Add( key, gradient );
        }

        return gradient;
    }

    protected EngineCallback ActivatePathCallback( string name )
    {
        if ( !pathCallbacks.TryGetValue( name, out EngineCallback? callback ) )
        {
            callback = ( engine, character ) => engine.ActivatePath( character, name );
            pathCallbacks.Add( name, callback );
        }

        return callback;
    }

    protected Coord Center => new( Math.Max( RustMath.FloorDiv( Terminal.Width, 2 ), 1 ) + (Terminal.Width % 2 != 0 && Terminal.Width > 1 ? 1 : 0), Math.Max( RustMath.FloorDiv( Terminal.Height, 2 ), 1 ) + (Terminal.Height % 2 != 0 && Terminal.Height > 1 ? 1 : 0) );

    protected EffectBase( Terminal terminal, EngineRng rng )
    {
        Terminal = terminal;
        Rng = rng;
        Engine = new( terminal );
        Home = terminal.Coordinates[..terminal.Count];
        InputOrder = Enumerable.Range( 0, terminal.InputCount ).OrderByDescending( id => Home[id].Row ).ThenBy( id => Home[id].Column ).ToArray( );
    }

    protected Coord RandomCoord( bool outside = false, bool text = false )
    {
        if ( outside )
        {
            Coord[] choices = [new( Rng.RandInt( 1, Terminal.Width ), Terminal.Height + 1 ), new( Rng.RandInt( 1, Terminal.Width ), 0 ), new( 0, Rng.RandInt( 1, Terminal.Height ) ), new( Terminal.Width + 1, Rng.RandInt( 1, Terminal.Height ) )];
            return choices[Rng.ChoiceIndex( 4 )];
        }

        return new( Rng.RandInt( text ? Terminal.TextLeft : 1, text ? Terminal.TextRight : Terminal.Width ), Rng.RandInt( text ? Terminal.TextBottom : 1, text ? Terminal.TextTop : Terminal.Height ) );
    }

    protected Color FinalColor( int id, Gradient gradient, GradientDirection direction ) => gradient.AtCoordinate( Terminal.TextBottom, Terminal.TextTop, Terminal.TextLeft, Terminal.TextRight, Home[id], direction );

    protected byte[] VisualBytes( int id, string symbol, Color? foreground = null, Color? background = null, bool bold = false )
    {
        InputCell cell = Terminal.Characters[id];
        if ( Terminal.Config.ExistingColors == ExistingColors.Always && (id < Terminal.InputCount || PreservedInputColors.Contains( id )) )
        {
            foreground = cell.Foreground;
            background = cell.Background;
            bold = cell.Bold;
        }

        return Engine.Intern( new( symbol, foreground, background, bold ) );
    }

    protected void Appearance( int id, Color? foreground = null, Color? background = null, string? symbol = null ) => Terminal.VisualBytes[id] = VisualBytes( id, symbol ?? Terminal.Characters[id].Symbol, foreground, background );

    protected SceneDefinition GradientScene( int id, string[] symbols, long duration, Gradient? foreground, Gradient? background = null, bool loop = false, Easing? ease = null, SyncMetric? sync = null )
    {
        InputCell cell = Terminal.Characters[id];
        bool preserve = Terminal.Config.ExistingColors == ExistingColors.Always && (id < Terminal.InputCount || PreservedInputColors.Contains( id ));
        // Length prefixes keep symbol sequences distinct even for multi-codepoint symbols.
        string symbolKey = symbols.Length == 1 ? symbols[0] : string.Concat( symbols.Select( symbol => symbol.Length.ToString( System.Globalization.CultureInfo.InvariantCulture ) + ":" + symbol ) );
        var key = new SceneKey( symbolKey, symbols.Length, foreground, background, duration, loop, ease, sync,
            preserve ? cell.Foreground : null, preserve ? cell.Background : null, preserve && cell.Bold, preserve );
        if ( gradientScenes.TryGetValue( key, out SceneDefinition? cached ) )
        {
            // Ease is publicly mutable, so only the immutable frame data is shared.
            return cached.CopyForPlayback( ease );
        }

        Color[] fg = foreground?.Spectrum ?? [], bg = background?.Spectrum ?? [];
        int colors = Math.Max( fg.Length, bg.Length );
        if ( colors == 0 )
        {
            throw new EngineException( "Foreground and background gradient are None. At least one gradient must be provided." );
        }

        int count = Math.Max( symbols.Length, colors );
        int[] symbolIndices = Distribute( count, symbols.Length ), colorIndices = Distribute( count, colors );
        int[] fgIndices = fg.Length == 0 ? [] : Distribute( colors, fg.Length ), bgIndices = bg.Length == 0 ? [] : Distribute( colors, bg.Length );
        var frames = new SceneFrame[count];
        for ( int i = 0; i < count; i++ )
        {
            int color = colorIndices[i];
            frames[i] = new( VisualBytes( id, symbols[symbolIndices[i]], fg.Length == 0 ? null : fg[fgIndices[color]], bg.Length == 0 ? null : bg[bgIndices[color]] ), duration );
        }

        var scene = new SceneDefinition( frames, loop, ease, sync );
        gradientScenes.Add( key, scene );

        return scene;
    }

    private static int[] Distribute( int count, int smaller )
    {
        var indices = new int[count];
        int repeats = count / smaller, overflow = count % smaller, position = 0;
        for ( int index = 0; index < smaller; index++ )
        {
            int length = repeats + (index < overflow ? 1 : 0);
            for ( int repeat = 0; repeat < length; repeat++ )
            {
                indices[position++] = index;
            }
        }

        return indices;
    }

    protected SceneDefinition FadeToFinal( int id, Gradient final, GradientDirection direction, Color start, long steps, long duration ) => FadeToFinal( id, final, direction, start, [steps], duration );

    protected SceneDefinition FadeToFinal( int id, Gradient final, GradientDirection direction, Color start, long[] steps, long duration )
    {
        InputCell cell = Terminal.Characters[id];
        bool dynamic = Terminal.Config.ExistingColors == ExistingColors.Dynamic;
        Color? fg = dynamic ? cell.Foreground : FinalColor( id, final, direction );
        Color? bg = dynamic ? cell.Background : null;
        Gradient? foreground = fg is { } f ? MakeGradient( [start, f], steps ) : null;
        Gradient? background = bg is { } b ? MakeGradient( [start, b], steps ) : null;

        return foreground is not null || background is not null ? GradientScene( id, [cell.Symbol], duration, foreground, background ) : new( [new( VisualBytes( id, cell.Symbol ), duration )] );
    }

    protected void ChainPaths( int id, string[] paths, bool loop = false )
    {
        for ( int index = 0; index < paths.Length; index++ )
        {
            if ( index + 1 == paths.Length && !loop )
            {
                break;
            }

            string next = paths[(index + 1) % paths.Length];
            Engine.Register( id, EngineEvent.PathComplete, new( CallerKind.Path, paths[index] ), ActivatePathCallback( next ) );
        }
    }

    public abstract bool NextFrame( );
}
