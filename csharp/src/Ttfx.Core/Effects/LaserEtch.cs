namespace Ttfx.Core;

public sealed record LaserEtchConfig
{
    public bool Algorithm { get; init; } = true;
    public long Speed { get; init; } = 1;
    public long Delay { get; init; } = 1;
    public Color[] CoolColors { get; init; } = [Color.FromHex( "ffe680" ), Color.FromHex( "ff7b00" )];
    public Color[] LaserColors { get; init; } = [Color.FromHex( "ffffff" ), Color.FromHex( "376cff" )];
    public Color[] SparkColors { get; init; } = [Color.FromHex( "ffffff" ), Color.FromHex( "ffe680" ), Color.FromHex( "ff7b00" ), Color.FromHex( "1a0900" )];
    public long CoolingFrames { get; init; } = 7;
    public long Frames { get; init; } = 4;
    public Color[] Stops { get; init; } = [Color.FromHex( "8A008A" ), Color.FromHex( "00D1FF" ), Color.FromHex( "ffffff" )];
    public long[] Steps { get; init; } = [8];
    public GradientDirection GradientDirection { get; init; } = GradientDirection.Vertical;
}

public sealed class LaserEtch : EffectBase
{
    private readonly LaserEtchConfig config;
    private readonly Queue<int> pending = [];
    private readonly ParticlePool sparks;
    private readonly int[] beam;
    private long delay;

    public LaserEtch( Terminal terminal, LaserEtchConfig config, EngineRng rng ) : base( terminal, rng )
    {
        this.config = config;
        var final = new Gradient( config.Stops, config.Steps );
        bool dynamic = terminal.Config.ExistingColors == ExistingColors.Dynamic;
        foreach ( int id in InputOrder )
        {
            InputCell cell = terminal.Characters[id];
            var cool = new Gradient( dynamic ? config.CoolColors : [.. config.CoolColors, FinalColor( id, final, config.GradientDirection )], [8] );
            var frames = new List<SceneFrame>( ) { new( VisualBytes( id, "^", Color.FromHex( "ffe680" ) ), 3 ) };
            frames.AddRange( cool.Spectrum.Select( c => new SceneFrame( VisualBytes( id, cell.Symbol, c ), 3 ) ) );
            if ( dynamic )
            {
                if ( cell.Foreground is not null || cell.Background is not null )
                {
                    frames.AddRange( FadeToFinal( id, final, config.GradientDirection, cool.Spectrum[^1], 8, 3 ).Frames );
                }
                else
                {
                    frames.AddRange( GradientScene( id, [cell.Symbol], 3, MakeGradient( [cool.Spectrum[^1], Color.FromHex( "ffffff" )], [8] ) ).Frames );
                    frames.Add( new( VisualBytes( id, cell.Symbol ), 3 ) );
                }
            }

            Engine.AddScene( id, "spawn", new( frames ) );
            Engine.ActivateScene( id, "spawn" );
        }

        if ( config.Algorithm )
        {
            var at = Enumerable.Range( 0, Home.Length ).ToDictionary( id => Home[id], id => id );
            int current = at[RandomCoord( text: true )];
            var stack = new List<int>( ) { current };
            var linked = new bool[Home.Length];
            pending.Enqueue( current );
            while ( stack.Count > 0 )
            {
                Coord c = Home[current];
                Coord[] neighbors = [new( c.Column, c.Row + 1 ), new( c.Column + 1, c.Row ), new( c.Column, c.Row - 1 ), new( c.Column - 1, c.Row )];
                int[] candidates = neighbors.Where( n => n.Column >= terminal.TextLeft && n.Column <= terminal.TextRight && n.Row >= terminal.TextBottom && n.Row <= terminal.TextTop && at.ContainsKey( n ) && !linked[at[n]] ).Select( n => at[n] ).ToArray( );
                if ( candidates.Length > 0 )
                {
                    int next = candidates[rng.ChoiceIndex( candidates.Length )];
                    linked[current] = linked[next] = true;
                    pending.Enqueue( next );
                    stack.Add( next );
                    current = next;
                }
                else
                {
                    stack.RemoveAt( stack.Count - 1 );
                    if ( stack.Count > 0 )
                    {
                        current = stack[^1];
                    }
                }
            }
        }

        var sparkGradient = new Gradient( config.SparkColors, [3, 8] );
        sparks = new( Engine, rng, [".", ",", "*"], initialCount: 2000, initialize: ( engine, id ) =>
        {
            engine.SetLayer( id, 2 );
            engine.AddScene( id, "spark", GradientScene( id, [terminal.Characters[id].Symbol], config.CoolingFrames, sparkGradient ) );

        } );
        // Upstream registers reclaim handlers only for the preallocated sparks.
        for ( int id = terminal.Count - 2000; id < terminal.Count; id++ )
        {
            Engine.Register( id, EngineEvent.SceneComplete, new( CallerKind.Scene, "spark" ), ( _, character ) => ReclaimSpark( character ) );
        }

        Color[] laser = new Gradient( config.LaserColors, [6], loop: true ).Spectrum;
        int first = Engine.AddCharacters( Enumerable.Range( 0, checked(( int ) terminal.Height + 1) ).Select( i => i == 0 ? "*" : "/" ).ToArray( ), default );
        beam = Enumerable.Range( first, checked(( int ) terminal.Height + 1) ).ToArray( );
        for ( int i = 0; i < beam.Length; i++ )
        {
            int id = beam[i];
            terminal.Coordinates[id] = new( i, i );
            Engine.SetLayer( id, 2 );
            terminal.Visible[id] = true;
            Engine.AddScene( id, "laser", new( laser.Select( c => new SceneFrame( VisualBytes( id, terminal.Characters[id].Symbol, c ), 3 ) ), loop: true ) );
            laser = [.. laser[1..], laser[0]];
            Engine.ActivateScene( id, "laser" );
            Engine.Activate( id );
        }
    }

    private void ReclaimSpark( int id ) => sparks.Reclaim( id );

    private void Reposition( Coord target )
    {
        for ( int i = 0; i < beam.Length; i++ )
        {
            Terminal.Coordinates[beam[i]] = new( target.Column + i, target.Row + i );
        }

        sparks.Emit( target, onEmit: ( engine, id ) =>
        {
            long column = Rng.RandInt( target.Column - 20, target.Column + 20 );
            Coord control = new( column, target.Row + Rng.RandInt( -10, 20 ) );
            engine.AddPath( id, "fall", new( [new Waypoint( "0", new( column, 1 ), [control] )], .3, new( EaseKind.OutSine ) ) );
            engine.ActivatePath( id, "fall" );
            engine.ActivateScene( id, "spark" );
        } );
    }

    public override bool NextFrame( )
    {
        if ( pending.Count == 0 && !Engine.HasActive )
        {
            return false;
        }

        if ( delay == 0 )
        {
            for ( long i = 0; i < config.Speed && pending.Count > 0; i++ )
            {
                int id = pending.Dequeue( );
                while ( Terminal.Characters[id].Symbol == " " && Terminal.Characters[id].Foreground is null && Terminal.Characters[id].Background is null && pending.Count > 0 )
                {
                    id = pending.Dequeue( );
                }

                Terminal.Visible[id] = true;
                Engine.Activate( id );
                Reposition( Home[id] );
            }

            delay = config.Delay;
        }
        else
        {
            delay--;
        }

        foreach ( int id in beam )
        {
            if ( pending.Count > 0 )
            {
                Engine.Activate( id );
            }
            else
            {
                Terminal.Visible[id] = false;
            }
        }

        Engine.Update( );
        return true;
    }
}
