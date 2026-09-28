namespace Ttfx.Core;

public sealed record PourConfig
{
    public string Direction { get; init; } = "down";
    public long Speed { get; init; } = 2;
    public (double Low, double High) MovementSpeed { get; init; } = (.4, .6);
    public long Gap { get; init; } = 1;
    public Color StartingColor { get; init; } = Color.FromHex( "ffffff" );
    public long Frames { get; init; } = 6;
    public Easing MovementEase { get; init; } = new( EaseKind.InQuad );
    public Color[] Stops { get; init; } = [Color.FromHex( "8A008A" ), Color.FromHex( "00D1FF" ), Color.FromHex( "FFFFFF" )];
    public long[] Steps { get; init; } = [12];
    public GradientDirection GradientDirection { get; init; } = GradientDirection.Vertical;
}

public sealed class Pour : EffectBase
{
    private readonly PourConfig config;
    private readonly Queue<int[]> groups;
    private Queue<int> current;
    private long gap;

    public Pour( Terminal terminal, PourConfig config, EngineRng rng ) : base( terminal, rng )
    {
        this.config = config;
        var gradient = new Gradient( config.Stops, config.Steps );
        CharacterGroup grouping = config.Direction switch
        {
            "up" => CharacterGroup.RowTopToBottom,
            "left" => CharacterGroup.ColumnLeftToRight,
            "right" => CharacterGroup.ColumnRightToLeft,
            _ => CharacterGroup.RowBottomToTop
        };
        int[][] ordered = terminal.Groups( grouping );
        for ( int index = 0; index < ordered.Length; index++ )
        {
            foreach ( int id in ordered[index] )
            {
                Coord home = Home[id];
                terminal.Coordinates[id] = config.Direction switch
                {
                    "up" => new( home.Column, 1 ),
                    "left" => new( terminal.Width, home.Row ),
                    "right" => new( 1, home.Row ),
                    _ => new( home.Column, terminal.Height )
                };
                double speed = rng.Uniform( config.MovementSpeed.Low, config.MovementSpeed.High );
                Engine.AddPath( id, "home", new( [new Waypoint( "0", home )], speed, config.MovementEase ) );
                Engine.ActivatePath( id, "home" );
                SceneDefinition scene = terminal.Config.ExistingColors == ExistingColors.Dynamic ? FadeToFinal( id, gradient, config.GradientDirection, config.StartingColor, 10, config.Frames ) : GradientScene( id, [terminal.Characters[id].Symbol], config.Frames, new( [config.StartingColor, FinalColor( id, gradient, config.GradientDirection )], config.Steps ) );
                Engine.AddScene( id, "pour", scene );
                Engine.ActivateScene( id, "pour" );
                terminal.Visible[id] = false;
            }

            if ( index % 2 != 0 )
            {
                Array.Reverse( ordered[index] );
            }
        }

        groups = new( ordered );
        current = new( groups.Dequeue( ) );
    }

    public override bool NextFrame( )
    {
        if ( groups.Count == 0 && current.Count == 0 && !Engine.HasActive )
        {
            return false;
        }

        if ( current.Count == 0 && groups.TryDequeue( out int[]? group ) )
        {
            current = new( group );
        }

        if ( current.Count > 0 )
        {
            if ( gap == 0 )
            {
                for ( long i = 0; i < config.Speed && current.TryDequeue( out int id ); i++ )
                {
                    Terminal.Visible[id] = true;
                    Engine.Activate( id );
                }

                gap = config.Gap;
            }
            else
            {
                gap--;
            }
        }

        Engine.Update( );
        return true;
    }
}
