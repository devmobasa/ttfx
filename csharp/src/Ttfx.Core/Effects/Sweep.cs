namespace Ttfx.Core;

public sealed record SweepConfig
{
    public string[] Symbols { get; init; } = ["█", "▓", "▒", "░"];
    public CharacterGroup FirstDirection { get; init; } = CharacterGroup.ColumnRightToLeft;
    public CharacterGroup SecondDirection { get; init; } = CharacterGroup.ColumnLeftToRight;
    public Color[] Stops { get; init; } = [Color.FromHex( "8A008A" ), Color.FromHex( "00D1FF" ), Color.FromHex( "ffffff" )];
    public long[] Steps { get; init; } = [8];
    public GradientDirection GradientDirection { get; init; } = GradientDirection.Vertical;
}

public sealed class Sweep : EffectBase
{
    private int[][] groups;
    private readonly int[][] second;
    private readonly EasingTracker tracker = new( new( EaseKind.InOutCirc ), 100, true );
    private int revealed;
    private bool first = true, complete;

    public Sweep( Terminal terminal, SweepConfig config, EngineRng rng ) : base( terminal, rng )
    {
        var final = new Gradient( config.Stops, config.Steps );
        Color[] grays = [Color.FromHex( "A0A0A0" ), Color.FromHex( "808080" ), Color.FromHex( "404040" ), Color.FromHex( "202020" ), Color.FromHex( "101010" )];
        bool dynamic = terminal.Config.ExistingColors == ExistingColors.Dynamic;
        var palette = new List<Color>( );
        if ( dynamic )
        {
            foreach ( int id in InputOrder )
            {
                InputCell cell = terminal.Characters[id];
                if ( cell.Foreground is { } fg )
                {
                    palette.Add( fg );
                }

                if ( cell.Background is { } bg )
                {
                    palette.Add( bg );
                }
            }
        }

        if ( palette.Count == 0 )
        {
            palette.AddRange( final.Spectrum );
        }

        foreach ( int id in Enumerable.Range( 0, Home.Length ).OrderByDescending( id => Home[id].Row ).ThenBy( id => Home[id].Column ) )
        {
            InputCell cell = terminal.Characters[id];
            var initial = new List<SceneFrame>( );
            foreach ( string symbol in config.Symbols )
            {
                initial.Add( new( VisualBytes( id, symbol, grays[rng.ChoiceIndex( grays.Length )] ), 5 ) );
            }

            initial.Add( new( VisualBytes( id, cell.Symbol, Color.FromHex( "808080" ) ), 1 ) );
            Engine.AddScene( id, "initial", new( initial ) );
            var ending = new List<SceneFrame>( );
            foreach ( string symbol in config.Symbols )
            {
                Color color = dynamic ? palette[rng.ChoiceIndex( palette.Count )] : final.Spectrum[rng.ChoiceIndex( final.Spectrum.Length )];
                ending.Add( new( VisualBytes( id, symbol, color ), 5 ) );
            }

            Color? finalFg = dynamic ? cell.Foreground : id < terminal.InputCount ? FinalColor( id, final, config.GradientDirection ) : Color.FromHex( "000000" );
            Color? finalBg = dynamic ? cell.Background : null;
            ending.Add( new( VisualBytes( id, cell.Symbol, finalFg, finalBg ), 1 ) );
            Engine.AddScene( id, "second", new( ending ) );
        }

        groups = terminal.Groups( config.FirstDirection, includeFill: true );
        second = terminal.Groups( config.SecondDirection, includeFill: true );
    }

    public override bool NextFrame( )
    {
        if ( complete && !Engine.HasActive )
        {
            return false;
        }

        int next = ( int ) (tracker.Step( ) * groups.Length);
        for ( int group = revealed; group < next; group++ )
        {
            foreach ( int id in groups[group] )
            {
                if ( first )
                {
                    Terminal.Visible[id] = true;
                }

                Engine.ActivateScene( id, first ? "initial" : "second" );
                Engine.Activate( id );
            }
        }

        revealed = next;
        if ( tracker.IsComplete && first )
        {
            groups = second;
            tracker.Reset( );
            revealed = 0;
            first = false;
        }
        else if ( tracker.IsComplete )
        {
            complete = true;
        }

        Engine.Update( );
        return true;
    }
}
