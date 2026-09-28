using System.Numerics;

namespace Ttfx.Core;

public enum EngineEvent
{
    SegmentEntered, SegmentExited, PathActivated, PathComplete, PathHolding, SceneActivated, SceneComplete
}

public enum CallerKind
{
    Scene, Path, Waypoint
}

public readonly record struct EventCaller( CallerKind Kind, string Name, Coord Coordinate = default, string? Controls = null );
public delegate void EngineCallback( Engine engine, int character );

public sealed partial class Engine
{
    public Terminal Terminal { get; }
    public EngineClock Clock => Terminal.Clock;
    public int[] InnerFill { get; }
    public int[] OuterFill { get; }
    private readonly SlotStore<ScenePlayback> scenes = new( );
    private readonly Dictionary<(int Character, int Name), int> sceneSlots = [];
    private readonly Dictionary<(int Character, EngineEvent Event, int Caller), EventActions> events = [];
    private sealed class EventActions( EngineCallback first )
    {
        private EngineCallback? first = first;
        private List<EngineCallback>? following;

        internal void Add( EngineCallback action ) => (following ??= []).Add( action );

        internal void Invoke( Engine engine, int character )
        {
            first?.Invoke( engine, character );
            if ( following is { } actions )
            {
                for ( int i = 0; i < actions.Count; i++ )
                {
                    actions[i]( engine, character );
                }
            }
        }

        internal void Clear( )
        {
            first = null;
            following?.Clear( );
        }
    }

    private readonly Dictionary<VisualKey, byte[]> visuals = [];
    private readonly Dictionary<string, int> names = new( StringComparer.Ordinal );
    private readonly Dictionary<EventCaller, int> callers = [];
    private readonly List<int> activeCharacters = [];
    private int[] activePositions;
    private int[] tickRanks;
    private ulong[] activeBits;
    private int characterCount;
    private byte[] eventKinds;
    private List<int>?[] ownedScenes;
    private List<int>?[] ownedPaths;
    private List<(EngineEvent Kind, int Caller)>?[] ownedEvents;
    private Easing cachedEase;
    private long cachedProgressBits;
    private double cachedEaseValue;
    private bool hasCachedEase;

    internal void Reserve( int sceneCount, int pathCount, int eventCount )
    {
        sceneSlots.EnsureCapacity( sceneCount );
        pathSlots.EnsureCapacity( pathCount );
        events.EnsureCapacity( eventCount );
        scenes.EnsureCapacity( sceneCount );
        paths.EnsureCapacity( pathCount );
    }

    internal void ReserveAdditional( int sceneCount, int pathCount, int eventCount ) =>
        Reserve( checked(sceneSlots.Count + sceneCount), checked(pathSlots.Count + pathCount), checked(events.Count + eventCount) );

    private double ApplyEase( Easing ease, double progress )
    {
        long bits = BitConverter.DoubleToInt64Bits( progress );
        if ( !hasCachedEase || cachedEase != ease || cachedProgressBits != bits )
        {
            cachedEase = ease;
            cachedProgressBits = bits;
            cachedEaseValue = ease.Apply( progress );
            hasCachedEase = true;
        }

        return cachedEaseValue;
    }
    private readonly record struct VisualKey( string Symbol, ulong Foreground, ulong Background, bool Bold );
    private int[] activeScene;
    private bool[] active;
    private int[] snapshot;
    private int[] tickOrder;
    private int activeCount;
    public int ActiveCount => activeCount;
    public bool HasActive => activeCount != 0;

    public Engine( Terminal terminal, bool createFill = true )
    {
        Terminal = terminal;
        (InnerFill, OuterFill) = createFill ? terminal.AddFillCharacters( ) : ([], []);
        characterCount = terminal.Count;
        int capacity = terminal.Characters.Length;
        activeScene = Enumerable.Repeat( -1, capacity ).ToArray( );
        activePath = Enumerable.Repeat( -1, capacity ).ToArray( );
        activePositions = Enumerable.Repeat( -1, capacity ).ToArray( );
        active = new bool[capacity];
        snapshot = new int[capacity];
        tickOrder = new int[capacity];
        tickRanks = new int[capacity];
        activeBits = new ulong[(capacity + 63) / 64];
        eventKinds = new byte[capacity];
        ownedScenes = new List<int>?[capacity];
        ownedPaths = new List<int>?[capacity];
        ownedEvents = new List<(EngineEvent, int)>?[capacity];
        int[] ordered = Enumerable.Range( 0, characterCount ).OrderBy( id => terminal.Characters[id].Identity ).ToArray( );
        for ( int rank = 0; rank < ordered.Length; rank++ )
        {
            tickOrder[rank] = ordered[rank];
            tickRanks[ordered[rank]] = rank;
        }
    }

    private int NameId( string name )
    {
        if ( !names.TryGetValue( name, out int id ) )
        {
            id = names.Count;
            names.Add( name, id );
        }

        return id;
    }

    private int CallerId( EventCaller caller )
    {
        if ( !callers.TryGetValue( caller, out int id ) )
        {
            id = callers.Count;
            callers.Add( caller, id );
        }

        return id;
    }

    public byte[] Intern( Visual visual )
    {
        static ulong ColorKey( Color? color ) => color is { } c ?
            0x1000000UL | (( ulong ) c.Red << 16) | (( ulong ) c.Green << 8) | c.Blue | (c.Xterm is { } x ? 0x2000000UL | (( ulong ) x << 26) : 0) : 0;
        var key = new VisualKey( visual.Symbol, Terminal.Config.NoColor ? 0 : ColorKey( visual.Foreground ),
            Terminal.Config.NoColor ? 0 : ColorKey( visual.Background ), visual.Bold );
        if ( !visuals.TryGetValue( key, out byte[]? bytes ) )
        {
            bytes = visual.Format( Terminal.Config.NoColor, Terminal.Config.XtermColors );
            visuals.Add( key, bytes );
        }

        return bytes;
    }

    public void AddScene( int character, string name, SceneDefinition definition )
    {
        var key = (character, NameId( name ));
        if ( sceneSlots.ContainsKey( key ) )
        {
            throw new EngineException( $"duplicate scene id: {name}" );
        }

        int slot = scenes.Add( new( character, name, definition ) { NameId = key.Item2, Caller = CallerId( new( CallerKind.Scene, name ) ) } );
        sceneSlots.Add( key, slot );
        (ownedScenes[character] ??= []).Add( slot );
    }

    public void ActivateScene( int character, string name )
    {
        int slot = sceneSlots[(character, NameId( name ))];
        ScenePlayback state = scenes[slot];
        if ( state.Definition.Frames.Length == 0 )
        {
            throw new EngineException( $"Scene {name} has no frames." );
        }

        activeScene[character] = slot;
        Terminal.VisualBytes[character] = state.Definition.Frames[state.Frame].Visual;
        Emit( character, EngineEvent.SceneActivated, state.Caller );
    }

    public void DeactivateScene( int character, string? name = null, bool reset = false )
    {
        int slot = activeScene[character];
        if ( slot < 0 || (name is not null && scenes[slot].Name != name) )
        {
            return;
        }

        activeScene[character] = -1;
        if ( reset )
        {
            scenes[slot].Reset( );
        }
    }

    public SceneDefinition GetScene( int character, string name ) => scenes[sceneSlots[(character, NameId( name ))]].Definition;

    public void ResetScene( int character, string name ) => scenes[sceneSlots[(character, NameId( name ))]].Reset( );
    public string? ActiveSceneName( int character ) => activeScene[character] < 0 ? null : scenes[activeScene[character]].Name;
    public bool SceneIsActive( int character ) => activeScene[character] >= 0;
    public bool IsActive( int character ) => activePath[character] >= 0 || (activeScene[character] >= 0 && !scenes[activeScene[character]].Definition.Loop);

    public void Activate( int character )
    {
        if ( !active[character] )
        {
            active[character] = true;
            activePositions[character] = activeCharacters.Count;
            activeCharacters.Add( character );
            int rank = tickRanks[character];
            activeBits[rank / 64] |= 1UL << (rank % 64);
            activeCount++;
        }
    }

    public void Register( int character, EngineEvent kind, EventCaller caller, EngineCallback action )
    {
        var key = (character, kind, CallerId( caller ));
        if ( !events.TryGetValue( key, out var actions ) )
        {
            actions = new( action );
            events.Add( key, actions );
            (ownedEvents[character] ??= []).Add( (kind, key.Item3) );
            eventKinds[character] |= ( byte ) (1 << ( int ) kind);
        }
        else
        {
            actions.Add( action );
        }
    }

    private bool Emit( int character, EngineEvent kind, int caller )
    {
        if ( (eventKinds[character] & (1 << ( int ) kind)) == 0 || !events.TryGetValue( (character, kind, caller), out var actions ) )
        {
            return false;
        }

        // The action object survives dictionary growth and runs appended callbacks inline.
        actions.Invoke( this, character );

        return true;
    }

    public void Update( )
    {
        int count = 0;
        for ( int word = 0; word < activeBits.Length; word++ )
        {
            ulong bits = activeBits[word];
            while ( bits != 0 )
            {
                int rank = word * 64 + BitOperations.TrailingZeroCount( bits );
                snapshot[count++] = tickOrder[rank];
                bits &= bits - 1;
            }
        }

        for ( int i = 0; i < count; i++ )
        {
            int character = snapshot[i];
            Tick( character );
        }

        for ( int index = 0; index < activeCharacters.Count; )
        {
            int character = activeCharacters[index];
            if ( !IsActive( character ) )
            {
                RemoveActive( character );
            }
            else
            {
                index++;
            }
        }
    }

    public void Tick( int character )
    {
        Move( character );
        StepScene( character );
    }

    private void StepScene( int character )
    {
        int slot = activeScene[character];
        if ( slot < 0 )
        {
            return;
        }

        ScenePlayback state = scenes[slot];
        SceneDefinition definition = state.Definition;
        bool complete = false;
        if ( definition.Sync is { } sync )
        {
            int pathSlot = activePath[character];
            if ( pathSlot < 0 )
            {
                Terminal.VisualBytes[character] = definition.Frames[^1].Visual;
                complete = true;
            }
            else
            {
                var path = paths[pathSlot];
                double progress = sync == SyncMetric.Step ? ( double ) Math.Max( 1, path.Step ) / Math.Max( 1, path.MaxSteps ) :
                    Math.Max( 1.0, Math.Max( 1.0, path.Distance ) - Math.Max( 1.0, path.Distance - path.Reached ) ) / Math.Max( 1.0, path.Distance );
                int frame = checked(( int ) Math.Clamp( RustMath.RoundHalfEven( (definition.Frames.Length - 1) * progress ), 0, definition.Frames.Length - 1 ));
                Terminal.VisualBytes[character] = definition.Frames[frame].Visual;
            }
        }
        else if ( definition.Ease is { } ease )
        {
            long tick = Math.Clamp( RustMath.RoundHalfEven( ApplyEase( ease, ( double ) state.EasedStep / definition.Duration ) * (definition.Duration - 1) ), 0, definition.Duration - 1 );
            Terminal.VisualBytes[character] = definition.Frames[definition.FrameAt( tick )].Visual;
            state.EasedStep++;
            complete = state.EasedStep == definition.Duration;
            if ( complete && definition.Loop )
            {
                state.EasedStep = 0;
            }
        }
        else
        {
            SceneFrame frame = definition.Frames[state.Frame];
            Terminal.VisualBytes[character] = frame.Visual;
            state.Elapsed++;
            if ( state.Elapsed == frame.Duration )
            {
                state.Elapsed = 0;
                state.Frame++;
                complete = state.Frame == definition.Frames.Length;
                if ( complete && definition.Loop )
                {
                    state.Frame = 0;
                }
            }
        }

        if ( complete || definition.Loop )
        {
            if ( !definition.Loop )
            {
                state.Reset( );
                activeScene[character] = -1;
            }

            Emit( character, EngineEvent.SceneComplete, state.Caller );
        }
    }
}
