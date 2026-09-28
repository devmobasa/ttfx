namespace Ttfx.Core;

public sealed partial class Engine
{
    public int AddCharacter( string symbol, Coord coordinate )
    {
        int character = Terminal.AddCharacter( symbol, coordinate );
        ResizeCharacters( );

        return character;
    }

    public int AddCharacters( string[] symbols, Coord coordinate )
    {
        int first = Terminal.AddCharacters( symbols, coordinate );
        ResizeCharacters( );

        return first;
    }

    private void ResizeCharacters( )
    {
        int oldCount = characterCount;
        int capacity = Terminal.Characters.Length;
        if ( capacity > active.Length )
        {
            Array.Resize( ref activeScene, capacity );
            Array.Resize( ref activePath, capacity );
            Array.Resize( ref active, capacity );
            Array.Resize( ref activePositions, capacity );
            Array.Resize( ref snapshot, capacity );
            Array.Resize( ref tickOrder, capacity );
            Array.Resize( ref tickRanks, capacity );
            Array.Resize( ref activeBits, (capacity + 63) / 64 );
            Array.Resize( ref eventKinds, capacity );
            Array.Resize( ref ownedScenes, capacity );
            Array.Resize( ref ownedPaths, capacity );
            Array.Resize( ref ownedEvents, capacity );
        }

        characterCount = Terminal.Count;
        for ( int id = oldCount; id < characterCount; id++ )
        {
            activeScene[id] = activePath[id] = activePositions[id] = -1;
            tickOrder[id] = tickRanks[id] = id;
        }
    }

    public void RemoveActive( int character )
    {
        if ( !active[character] )
        {
            return;
        }

        active[character] = false;
        int position = activePositions[character];
        int last = activeCharacters[^1];
        activeCharacters[position] = last;
        activePositions[last] = position;
        activeCharacters.RemoveAt( activeCharacters.Count - 1 );
        activePositions[character] = -1;
        int rank = tickRanks[character];
        activeBits[rank / 64] &= ~(1UL << (rank % 64));
        activeCount--;
    }

    public void ResetCharacter( int character, ParticleReset reset )
    {
        if ( reset.DeactivateScene )
        {
            activeScene[character] = -1;
        }

        if ( reset.DeactivatePath )
        {
            activePath[character] = -1;
        }

        if ( reset.ClearScenes )
        {
            foreach ( int slot in ownedScenes[character] ?? [] )
            {
                sceneSlots.Remove( (character, scenes[slot].NameId) );
                scenes.Remove( slot );
            }

            ownedScenes[character]?.Clear( );
        }

        if ( reset.ClearPaths )
        {
            foreach ( int slot in ownedPaths[character] ?? [] )
            {
                pathSlots.Remove( (character, paths[slot].NameId) );
                paths.Remove( slot );
            }

            ownedPaths[character]?.Clear( );
        }

        if ( reset.ClearEvents )
        {
            foreach ( var entry in ownedEvents[character] ?? [] )
            {
                var key = (character, entry.Kind, entry.Caller);
                events[key].Clear( );
                events.Remove( key );
            }

            ownedEvents[character]?.Clear( );
            eventKinds[character] = 0;
        }

        if ( reset.ResetAppearance )
        {
            ResetAppearance( character );
        }
    }

    public void ResetAppearance( int character )
    {
        InputCell input = Terminal.Characters[character];
        bool always = Terminal.Config.ExistingColors == ExistingColors.Always;
        Terminal.VisualBytes[character] = Intern( new( input.Symbol, always ? input.Foreground : null, always ? input.Background : null, always && input.Bold ) );
    }
}
