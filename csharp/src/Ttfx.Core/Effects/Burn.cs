namespace Ttfx.Core;

public sealed record BurnConfig
{
    public Color StartingColor { get; init; } = Color.FromHex( "837373" );
    public Color[] BurnColors { get; init; } = [Color.FromHex( "ffffff" ), Color.FromHex( "fff75d" ), Color.FromHex( "fe650d" ), Color.FromHex( "8A003C" ), Color.FromHex( "510100" )];
    public double SmokeChance { get; init; } = .5;
    public Color[] Stops { get; init; } = [Color.FromHex( "00c3ff" ), Color.FromHex( "ffff1c" )];
    public long[] Steps { get; init; } = [12];
    public GradientDirection GradientDirection { get; init; } = GradientDirection.Vertical;
}

public sealed class Burn : EffectBase
{
    private readonly BurnConfig config;
    private readonly Queue<int> order;
    private readonly ParticlePool smoke;

    public Burn( Terminal terminal, BurnConfig config, EngineRng rng ) : base( terminal, rng )
    {
        this.config = config;
        Coord starting = RandomCoord( text: true );
        int start = Array.IndexOf( Home, starting );
        var tree = new PrimsSimple( terminal, rng, Home, start );
        var smokeGradient = new Gradient( [Color.FromHex( "504F4F" ), Color.FromHex( "C7C7C7" )], [9] );
        smoke = new( Engine, rng, [".", ",", "'", "`", "#", "*"], initialCount: 2000, maximum: 2000, initialize: ( engine, id ) =>
        {
            engine.AddScene( id, "smoke", GradientScene( id, [terminal.Characters[id].Symbol], 10, smokeGradient ) );
            engine.SetLayer( id, 2 );
        } );
        var finalGradient = new Gradient( config.Stops, config.Steps );
        var fire = new Gradient( config.BurnColors, [10] );
        while ( !tree.Complete )
        {
            tree.Step( );
        }

        string[] symbols = ["'", ".", "▖", "▙", "█", "▜", "▀", "▝", "."];
        foreach ( int id in InputOrder )
        {
            InputCell cell = terminal.Characters[id];
            terminal.Visible[id] = true;
            Appearance( id, config.StartingColor );
            Engine.AddScene( id, "burn", GradientScene( id, symbols, 4, fire ) );
            if ( terminal.Config.ExistingColors == ExistingColors.Dynamic )
            {
                Gradient? fg = cell.Foreground is { } f ? MakeGradient( [fire.Spectrum[^1], f], [8] ) : null, bg = cell.Background is { } b ? MakeGradient( [fire.Spectrum[^1], b], [8] ) : null;
                Engine.AddScene( id, "final", fg is not null || bg is not null ? GradientScene( id, [cell.Symbol], 4, fg, bg ) : new( [new( VisualBytes( id, cell.Symbol ), 4 )] ) );
            }
            else
            {
                var gradient = new Gradient( [fire.Spectrum[^1], FinalColor( id, finalGradient, config.GradientDirection )], [8] );
                Engine.AddScene( id, "final", GradientScene( id, [cell.Symbol], 4, gradient ) );
            }

            Engine.Register( id, EngineEvent.SceneComplete, new( CallerKind.Scene, "burn" ), static ( engine, character ) => engine.ActivateScene( character, "final" ) );
            Engine.Register( id, EngineEvent.SceneComplete, new( CallerKind.Scene, "burn" ), ( _, character ) => EmitSmoke( Home[character] ) );
        }

        order = new( tree.Order );
    }

    private void EmitSmoke( Coord origin )
    {
        if ( Rng.Random( ) > config.SmokeChance )
        {
            return;
        }

        smoke.Emit( origin, onEmit: ( engine, id ) =>
        {
            engine.ResetScene( id, "smoke" );
            Coord target = new( Rng.RandInt( origin.Column - 4, origin.Column + 4 ), Terminal.Height + 1 );
            engine.AddPath( id, "rise", new( [new Waypoint( "0", target )], .5 ) );
            engine.ActivatePath( id, "rise" );
            engine.ActivateScene( id, "smoke" );
            engine.Register( id, EngineEvent.SceneComplete, new( CallerKind.Scene, "smoke" ), ( _, character ) => smoke.Reclaim( character ) );
        } );
    }

    public override bool NextFrame( )
    {
        if ( order.Count == 0 && !Engine.HasActive )
        {
            return false;
        }

        long count = Rng.RandInt( 2, 4 );
        for ( long i = 0; i < count; i++ )
        {
            if ( !order.TryDequeue( out int id ) )
            {
                continue;
            }

            InputCell cell = Terminal.Characters[id];
            if ( cell.Symbol == " " && (Terminal.Config.ExistingColors == ExistingColors.Ignore || (cell.Foreground is null && cell.Background is null)) )
            {
                continue;
            }

            Engine.ActivateScene( id, "burn" );
            Engine.Activate( id );
        }

        Engine.Update( );
        return true;
    }
}
