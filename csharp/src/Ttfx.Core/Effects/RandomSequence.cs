namespace Ttfx.Core;

public sealed record RandomSequenceConfig
{
    public double Speed { get; init; } = .007;
    public long Frames { get; init; } = 8;
    public Color[] Stops { get; init; } = [Color.FromHex( "8A008A" ), Color.FromHex( "00D1FF" ), Color.FromHex( "FFFFFF" )];
    public long[] Steps { get; init; } = [12];
    public GradientDirection GradientDirection { get; init; } = GradientDirection.Vertical;
}

public sealed class RandomSequence : EffectBase
{
    private readonly List<int> pending;
    private readonly long perTick;

    public RandomSequence( Terminal terminal, RandomSequenceConfig config, EngineRng rng ) : base( terminal, rng )
    {
        var gradient = new Gradient( config.Stops, config.Steps );
        perTick = Math.Max( ( long ) (config.Speed * terminal.InputCount), 1 );
        foreach ( int id in InputOrder )
        {
            InputCell cell = terminal.Characters[id];
            bool dynamic = terminal.Config.ExistingColors == ExistingColors.Dynamic;
            Color? finalFg = dynamic ? cell.Foreground : FinalColor( id, gradient, config.GradientDirection );
            Color? finalBg = dynamic ? cell.Background : null;
            Gradient? fg = finalFg is { } f ? MakeGradient( [terminal.Config.Background, f], [7] ) : null;
            Gradient? bg = finalBg is { } b ? MakeGradient( [terminal.Config.Background, b], [7] ) : null;
            SceneDefinition scene;
            if ( fg is not null || bg is not null )
            {
                scene = GradientScene( id, [cell.Symbol], config.Frames, fg, bg );
            }
            else
            {
                var neutral = GradientScene( id, [cell.Symbol], config.Frames, MakeGradient( [terminal.Config.Background, Color.FromHex( "808080" )], [7] ) );
                scene = new( [.. neutral.Frames, new( VisualBytes( id, cell.Symbol ), config.Frames )] );
            }

            Engine.AddScene( id, "reveal", scene );
            Engine.ActivateScene( id, "reveal" );
            terminal.Visible[id] = false;
        }

        pending = [.. InputOrder];
        rng.Shuffle( System.Runtime.InteropServices.CollectionsMarshal.AsSpan( pending ) );
    }

    public override bool NextFrame( )
    {
        if ( pending.Count == 0 && !Engine.HasActive )
        {
            return false;
        }

        for ( long i = 0; i < perTick && pending.Count > 0; i++ )
        {
            int id = pending[^1];
            pending.RemoveAt( pending.Count - 1 );
            Terminal.Visible[id] = true;
            Engine.Activate( id );
        }

        Engine.Update( );
        return true;
    }
}
