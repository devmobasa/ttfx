namespace Ttfx.Core;

public sealed record ColorShiftConfig
{
    public Color[] Colors { get; init; } = [Color.FromHex( "e81416" ), Color.FromHex( "ffa500" ), Color.FromHex( "faeb36" ), Color.FromHex( "79c314" ), Color.FromHex( "487de7" ), Color.FromHex( "4b369d" ), Color.FromHex( "70369d" )];
    public long[] ColorSteps { get; init; } = [12];
    public long Frames { get; init; } = 2;
    public bool NoTravel { get; init; }
    public GradientDirection TravelDirection { get; init; } = GradientDirection.Radial;
    public bool Reverse { get; init; }
    public bool NoLoop { get; init; }
    public long Cycles { get; init; } = 3;
    public bool SkipFinal { get; init; }
    public Color[] Stops { get; init; } = [Color.FromHex( "e81416" ), Color.FromHex( "ffa500" ), Color.FromHex( "faeb36" ), Color.FromHex( "79c314" ), Color.FromHex( "487de7" ), Color.FromHex( "4b369d" ), Color.FromHex( "70369d" )];
    public long[] Steps { get; init; } = [12];
    public GradientDirection GradientDirection { get; init; } = GradientDirection.Vertical;
}

public sealed class ColorShift : EffectBase
{
    public ColorShift( Terminal terminal, ColorShiftConfig config, EngineRng rng ) : base( terminal, rng )
    {
        var final = new Gradient( config.Stops, config.Steps );
        var gradient = new Gradient( config.Colors, config.ColorSteps, !config.NoLoop );
        var cycles = new long[terminal.InputCount];
        foreach ( int id in InputOrder )
        {
            Coord coord = Home[id];
            double position = config.TravelDirection switch
            {
                GradientDirection.Horizontal => ( double ) coord.Column / terminal.Width,
                GradientDirection.Vertical => ( double ) coord.Row / terminal.Height,
                GradientDirection.Diagonal => ( double ) (coord.Row + coord.Column) / (terminal.Width + terminal.Height),
                _ => Geometry.NormalizedDistance( terminal.TextBottom, terminal.TextTop, terminal.TextLeft, terminal.TextRight, coord )
            };
            long shift = config.NoTravel ? 0 : ( long ) (gradient.Spectrum.Length * position);
            if ( config.Reverse )
            {
                shift = -shift;
            }

            int length = gradient.Spectrum.Length;
            int k = ( int ) (shift < 0 ? Math.Max( length + shift, 0 ) : Math.Min( shift, length ));
            Color[] colors = [.. gradient.Spectrum[k..], .. gradient.Spectrum[..k]];
            Engine.AddScene( id, "gradient", new( colors.Select( color => new SceneFrame( VisualBytes( id, terminal.Characters[id].Symbol, color ), config.Frames ) ) ) );
            Engine.AddScene( id, "final", FadeToFinal( id, final, config.GradientDirection, colors[^1], 8, config.Frames ) );
            Engine.ActivateScene( id, "gradient" );
            Engine.Activate( id );
            terminal.Visible[id] = true;
            Engine.Register( id, EngineEvent.SceneComplete, new( CallerKind.Scene, "gradient" ), ( engine, character ) =>
            {
                cycles[character]++;
                if ( config.Cycles == 0 || cycles[character] < config.Cycles )
                {
                    engine.ActivateScene( character, "gradient" );
                }
                else if ( !config.SkipFinal )
                {
                    engine.ActivateScene( character, "final" );
                }
            } );
        }
    }

    public override bool NextFrame( )
    {
        if ( !Engine.HasActive )
        {
            return false;
        }

        Engine.Update( );
        return true;
    }
}
