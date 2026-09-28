namespace Ttfx.Core;

public sealed record SliceConfig
{
    public string Direction { get; init; } = "vertical";
    public double MovementSpeed { get; init; } = .25;
    public Easing MovementEase { get; init; } = new( EaseKind.InOutExpo );
    public Color[] Stops { get; init; } = [Color.FromHex( "8A008A" ), Color.FromHex( "00D1FF" ), Color.FromHex( "FFFFFF" )];
    public long[] Steps { get; init; } = [12];
    public GradientDirection GradientDirection { get; init; } = GradientDirection.Diagonal;
}

public sealed class Slice : EffectBase
{
    public Slice( Terminal terminal, SliceConfig config, EngineRng rng ) : base( terminal, rng )
    {
        var gradient = new Gradient( config.Stops, config.Steps );
        foreach ( int id in InputOrder )
        {
            InputCell cell = terminal.Characters[id];
            bool dynamic = terminal.Config.ExistingColors == ExistingColors.Dynamic;
            Appearance( id, dynamic ? cell.Foreground : FinalColor( id, gradient, config.GradientDirection ), dynamic ? cell.Background : null );
        }

        void Send( int id, Coord origin )
        {
            terminal.Coordinates[id] = origin;
            Engine.AddPath( id, "home", new( [new Waypoint( "0", Home[id] )], config.MovementSpeed * (config.Direction == "horizontal" ? 2.0 : 1.0), config.MovementEase ) );
            Engine.ActivatePath( id, "home" );
            terminal.Visible[id] = true;
            Engine.Activate( id );
        }

        if ( config.Direction == "diagonal" )
        {
            int[][] groups = terminal.Groups( CharacterGroup.DiagonalBottomLeftToTopRight );
            for ( int index = 0; index < groups.Length; index++ )
            {
                int[] group = groups[index];
                Coord origin = index < groups.Length / 2 ? new( Home[group[0]].Column, 0 ) : new( Home[group[^1]].Column, terminal.Height + 1 );
                foreach ( int id in group )
                {
                    Send( id, origin );
                }
            }
        }
        else
        {
            IEnumerable<int> ids = config.Direction == "horizontal" ? Enumerable.Range( 0, Home.Length ).Where( id => Home[id].Column >= terminal.TextLeft && Home[id].Column <= terminal.TextRight && Home[id].Row >= terminal.TextBottom && Home[id].Row <= terminal.TextTop ) : InputOrder;
            foreach ( int id in ids )
            {
                Coord home = Home[id];
                Send( id, config.Direction == "horizontal" ? new( home.Row <= terminal.TextCenter.Row ? 0 : terminal.Width + 1, home.Row ) : new( home.Column, home.Column <= terminal.TextCenter.Column ? terminal.Height + 1 : 0 ) );
            }
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
