namespace Ttfx.Core;

public sealed record BubblesConfig
{
    public bool Rainbow { get; init; }
    public Color[] Colors { get; init; } = [Color.FromHex( "d33aff" ), Color.FromHex( "7395c4" ), Color.FromHex( "43c2a7" ), Color.FromHex( "02ff7f" )];
    public Color PopColor { get; init; } = Color.FromHex( "ffffff" );
    public double Speed { get; init; } = .5;
    public long Delay { get; init; } = 20;
    public string Condition { get; init; } = "row";
    public Easing Ease { get; init; } = new( EaseKind.InOutSine );
    public Color[] Stops { get; init; } = [Color.FromHex( "d33aff" ), Color.FromHex( "02ff7f" )];
    public long[] Steps { get; init; } = [12];
    public GradientDirection GradientDirection { get; init; } = GradientDirection.Diagonal;
}

public sealed class Bubbles : EffectBase
{
    private sealed class Bubble( int[] characters, long radius, int anchor, long lowest )
    {
        internal readonly int[] Characters = characters;
        internal readonly long Radius = radius, Lowest = lowest;
        internal readonly int Anchor = anchor;
        internal bool Landed;
    }

    private readonly BubblesConfig config;
    private readonly Queue<Bubble> pending = [];
    private readonly List<Bubble> animating = [];
    private long sinceLast;

    public Bubbles( Terminal terminal, BubblesConfig config, EngineRng rng ) : base( terminal, rng )
    {
        this.config = config;
        var final = new Gradient( config.Stops, config.Steps );
        foreach ( int id in InputOrder )
        {
            Engine.SetLayer( id, 1 );
            Engine.AddScene( id, "pop1", new( [new( VisualBytes( id, "*", config.PopColor ), 9 )] ) );
            Engine.AddScene( id, "pop2", new( [new( VisualBytes( id, "'", config.PopColor ), 9 )] ) );
            Engine.AddScene( id, "final", FadeToFinal( id, final, config.GradientDirection, config.PopColor, 8, 6 ) );
            Engine.Register( id, EngineEvent.SceneComplete, new( CallerKind.Scene, "pop1" ), static ( engine, character ) => engine.ActivateScene( character, "pop2" ) );
            Engine.Register( id, EngineEvent.SceneComplete, new( CallerKind.Scene, "pop2" ), static ( engine, character ) => engine.ActivateScene( character, "final" ) );
            Engine.AddPath( id, "final", new( [new Waypoint( "0", Home[id] )], .3, new( EaseKind.InOutExpo ) ) );
            Engine.Register( id, EngineEvent.PathComplete, new( CallerKind.Path, "final" ), static ( engine, character ) => engine.SetLayer( character, 0 ) );
        }

        var unbubbled = new Queue<int>( terminal.Groups( CharacterGroup.RowBottomToTop ).SelectMany( group => group ) );
        var rainbow = new Gradient( [Color.FromHex( "e81416" ), Color.FromHex( "ffa500" ), Color.FromHex( "faeb36" ), Color.FromHex( "79c314" ), Color.FromHex( "487de7" ), Color.FromHex( "4b369d" ), Color.FromHex( "70369d" )], [5] );
        while ( unbubbled.Count > 0 )
        {
            long count = unbubbled.Count < 5 ? unbubbled.Count : rng.RandInt( 5, Math.Min( unbubbled.Count, 20 ) );
            int[] chars = new int[count];
            for ( int i = 0; i < chars.Length; i++ )
            {
                chars[i] = unbubbled.Dequeue( );
            }

            Coord origin = new( rng.RandInt( 1, terminal.Width ), terminal.Height + 10 );
            long radius = Math.Max( chars.Length / 5, 1 );
            int anchor = Engine.AddCharacter( " ", origin );
            long lowest = config.Condition == "row" ? chars.Min( id => Home[id].Row ) : 1;
            var bubble = new Bubble( chars, radius, anchor, lowest );
            SetCoordinates( bubble );
            bubble.Landed = false;
            long column = rng.RandInt( 1, terminal.Width );
            Engine.AddPath( anchor, "floor", new( [new Waypoint( "0", new( column, lowest ) )], config.Speed ) );
            Engine.ActivatePath( anchor, "floor" );
            if ( config.Rainbow )
            {
                Color[] colors = ( Color[] ) rainbow.Spectrum.Clone( );
                int offset = 0;
                foreach ( int id in chars )
                {
                    Engine.AddScene( id, "sheen", new( colors.Select( color => new SceneFrame( VisualBytes( id, terminal.Characters[id].Symbol, color ), 4 ) ), loop: true ) );
                    offset = (offset + 2) % colors.Length;
                    colors = [.. colors[offset..], .. colors[..offset]];
                    Engine.ActivateScene( id, "sheen" );
                }
            }
            else
            {
                Color color = config.Colors[rng.ChoiceIndex( config.Colors.Length )];
                foreach ( int id in chars )
                {
                    Engine.AddScene( id, "sheen", new( [new( VisualBytes( id, terminal.Characters[id].Symbol, color ), 1 )] ) );
                    Engine.ActivateScene( id, "sheen" );
                }
            }

            pending.Enqueue( bubble );
        }
    }

    private void SetCoordinates( Bubble bubble )
    {
        List<Coord> coords = Geometry.OnCircle( Terminal.Coordinates[bubble.Anchor], bubble.Radius, bubble.Characters.Length, false );
        for ( int i = 0; i < bubble.Characters.Length; i++ )
        {
            Terminal.Coordinates[bubble.Characters[i]] = coords[i];
            if ( coords[i].Row == bubble.Lowest )
            {
                bubble.Landed = true;
            }
        }

        if ( config.Condition == "anywhere" && Rng.Random( ) < .002 )
        {
            bubble.Landed = true;
        }
    }

    public override bool NextFrame( )
    {
        if ( animating.Count == 0 && pending.Count == 0 && !Engine.HasActive )
        {
            return false;
        }

        if ( pending.Count > 0 && sinceLast >= config.Delay )
        {
            Bubble bubble = pending.Dequeue( );
            foreach ( int id in bubble.Characters )
            {
                Terminal.Visible[id] = true;
            }

            animating.Add( bubble );
            sinceLast = 0;
        }

        sinceLast++;
        foreach ( Bubble bubble in animating )
        {
            if ( bubble.Landed )
            {
                List<Coord> coords = Geometry.OnCircle( Terminal.Coordinates[bubble.Anchor], bubble.Radius + 3, bubble.Characters.Length, true );
                for ( int i = 0; i < Math.Min( coords.Count, bubble.Characters.Length ); i++ )
                {
                    int id = bubble.Characters[i];
                    Engine.AddPath( id, "pop", new( [new Waypoint( "0", coords[i] )], .3, new( EaseKind.OutExpo ) ) );
                    ChainPaths( id, ["pop", "final"] );
                }

                foreach ( int id in bubble.Characters )
                {
                    Engine.ActivateScene( id, "pop1" );
                    Engine.ActivatePath( id, "pop" );
                    Engine.Activate( id );
                }
            }
        }

        animating.RemoveAll( bubble => bubble.Landed );
        foreach ( Bubble bubble in animating )
        {
            Engine.Tick( bubble.Anchor );
            SetCoordinates( bubble );
            foreach ( int id in bubble.Characters )
            {
                Engine.Tick( id );
            }
        }

        Engine.Update( );
        return true;
    }
}
