namespace Ttfx.Core;

public sealed record WavesConfig
{
    public string[] Symbols { get; init; } = ["▁", "▂", "▃", "▄", "▅", "▆", "▇", "█", "▇", "▆", "▅", "▄", "▃", "▂", "▁"];
    public Color[] WaveColors { get; init; } = [Color.FromHex( "f0ff65" ), Color.FromHex( "ffb102" ), Color.FromHex( "31a0d4" ), Color.FromHex( "ffb102" ), Color.FromHex( "f0ff65" )];
    public long[] WaveSteps { get; init; } = [6];
    public long Count { get; init; } = 7;
    public long Length { get; init; } = 2;
    public CharacterGroup Direction { get; init; } = CharacterGroup.ColumnLeftToRight;
    public Easing Ease { get; init; } = new( EaseKind.InOutSine );
    public Color[] Stops { get; init; } = [Color.FromHex( "ffb102" ), Color.FromHex( "31a0d4" ), Color.FromHex( "f0ff65" )];
    public long[] Steps { get; init; } = [12];
    public GradientDirection GradientDirection { get; init; } = GradientDirection.Diagonal;
}

public sealed class Waves : EffectBase
{
    private readonly Queue<int[]> groups;

    public Waves( Terminal terminal, WavesConfig config, EngineRng rng ) : base( terminal, rng )
    {
        var final = new Gradient( config.Stops, config.Steps );
        var wave = new Gradient( config.WaveColors, config.WaveSteps );
        foreach ( int id in InputOrder )
        {
            SceneDefinition one = GradientScene( id, config.Symbols, config.Length, wave );
            var frames = new List<SceneFrame>( );
            for ( long count = 0; count < config.Count; count++ )
            {
                frames.AddRange( one.Frames );
            }

            Engine.AddScene( id, "wave", new( frames, ease: config.Ease ) );
            SceneDefinition ending = FadeToFinal( id, final, config.GradientDirection, wave.Spectrum[^1], config.Steps, 10 );
            InputCell cell = terminal.Characters[id];
            bool dynamic = terminal.Config.ExistingColors == ExistingColors.Dynamic;
            if ( dynamic && cell.Foreground is null && cell.Background is not null )
            {
                ending = new( [.. ending.Frames, new( VisualBytes( id, cell.Symbol, null, cell.Background ), 10 )] );
            }

            Engine.AddScene( id, "final", ending );
            Engine.Register( id, EngineEvent.SceneComplete, new( CallerKind.Scene, "wave" ), static ( engine, character ) => engine.ActivateScene( character, "final" ) );
            Engine.ActivateScene( id, "wave" );
            if ( dynamic )
            {
                Appearance( id, cell.Foreground, cell.Background );
            }
        }

        groups = new( terminal.Groups( config.Direction ) );
    }

    public override bool NextFrame( )
    {
        if ( groups.Count == 0 && !Engine.HasActive )
        {
            return false;
        }

        if ( groups.TryDequeue( out int[]? group ) )
        {
            foreach ( int id in group )
            {
                Terminal.Visible[id] = true;
                Engine.Activate( id );
            }
        }

        Engine.Update( );
        return true;
    }
}
