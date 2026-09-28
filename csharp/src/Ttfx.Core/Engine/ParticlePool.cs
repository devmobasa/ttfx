namespace Ttfx.Core;

public sealed record ParticleReset
{
    public bool ClearPaths { get; init; } = true;
    public bool ClearScenes { get; init; }
    public bool ClearEvents { get; init; }
    public bool DeactivatePath { get; init; } = true;
    public bool DeactivateScene { get; init; } = true;
    public bool ResetAppearance { get; init; }
}

public sealed class ParticlePool
{
    private readonly Engine engine;
    private readonly EngineRng rng;
    private readonly string[] symbols;
    private readonly Coord origin;
    private readonly int? maximum;
    private readonly EngineCallback? initialize;
    private readonly Stack<int> available = [];
    private readonly HashSet<int> availableSet = [];
    private readonly HashSet<int> owned = [];
    public int Count => owned.Count;

    public ParticlePool( Engine engine, EngineRng rng, string[] symbols, int initialCount = 0, int? maximum = null, Coord origin = default, EngineCallback? initialize = null )
    {
        if ( symbols.Length == 0 )
        {
            throw new EngineException( "ParticlePool requires at least one symbol." );
        }

        if ( maximum < initialCount )
        {
            throw new EngineException( "max_size must be greater than or equal to initial_count." );
        }

        this.engine = engine;
        this.rng = rng;
        this.symbols = ( string[] ) symbols.Clone( );
        this.maximum = maximum;
        this.origin = origin;
        this.initialize = initialize;
        for ( int i = 0; i < initialCount; i++ )
        {
            int character = Create( null );
            available.Push( character );
            availableSet.Add( character );
        }
    }

    private int Create( string? symbol )
    {
        int character = engine.AddCharacter( symbol ?? symbols[rng.ChoiceIndex( symbols.Length )], origin );
        owned.Add( character );
        initialize?.Invoke( engine, character );

        return character;
    }

    public int? Acquire( string? symbol = null, ParticleReset? reset = null )
    {
        reset ??= new( );
        if ( available.TryPop( out int character ) )
        {
            availableSet.Remove( character );
            engine.ResetCharacter( character, reset );
            if ( symbol is not null )
            {
                engine.Terminal.Characters[character] = engine.Terminal.Characters[character] with { Symbol = symbol };
                engine.ResetAppearance( character );
            }

            return character;
        }

        if ( maximum is { } limit && owned.Count >= limit )
        {
            return null;
        }

        character = Create( symbol );
        engine.ResetCharacter( character, reset );

        return character;
    }

    public int? Emit( Coord coordinate, string? symbol = null, bool visible = true, ParticleReset? reset = null, EngineCallback? onEmit = null )
    {
        int? character = Acquire( symbol, reset );
        if ( character is not { } id )
        {
            return null;
        }

        engine.SetCoordinate( id, coordinate );
        onEmit?.Invoke( engine, id );
        engine.Terminal.Visible[id] = visible;
        engine.Activate( id );

        return id;
    }

    public void Reclaim( int character, bool hide = true, bool deactivate = true )
    {
        if ( !owned.Contains( character ) )
        {
            throw new EngineException( "Character does not belong to this particle pool." );
        }

        if ( hide )
        {
            engine.Terminal.Visible[character] = false;
        }

        if ( deactivate )
        {
            engine.DeactivatePath( character );
            engine.DeactivateScene( character );
        }

        engine.RemoveActive( character );
        if ( availableSet.Add( character ) )
        {
            available.Push( character );
        }
    }

    public void Extend( IEnumerable<int> characters )
    {
        foreach ( int character in characters )
        {
            if ( owned.Add( character ) && availableSet.Add( character ) )
            {
                available.Push( character );
            }
        }
    }
}
