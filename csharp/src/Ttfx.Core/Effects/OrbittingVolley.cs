namespace Ttfx.Core;

public sealed record OrbittingVolleyConfig
{
    public string TopSymbol { get; init; } = "█";
    public string RightSymbol { get; init; } = "█";
    public string BottomSymbol { get; init; } = "█";
    public string LeftSymbol { get; init; } = "█";
    public double LauncherSpeed { get; init; } = .8;
    public double CharacterSpeed { get; init; } = 1.5;
    public double Volume { get; init; } = .03;
    public long Delay { get; init; } = 30;
    public Easing Ease { get; init; } = new( EaseKind.OutSine );
    public Color[] Stops { get; init; } = [Color.FromHex( "FFA15C" ), Color.FromHex( "44D492" )];
    public long[] Steps { get; init; } = [12];
    public GradientDirection GradientDirection { get; init; } = GradientDirection.Radial;
}

public sealed class OrbittingVolley : EffectBase
{
    private readonly OrbittingVolleyConfig config;
    private readonly int[] launchers;
    private readonly Coord[] corners;
    private readonly Queue<int>[] magazines = [new( ), new( ), new( ), new( )];
    private readonly Gradient gradient;
    private long delay;
    private bool complete;

    public OrbittingVolley( Terminal terminal, OrbittingVolleyConfig config, EngineRng rng ) : base( terminal, rng )
    {
        this.config = config;
        gradient = new( config.Stops, config.Steps );
        foreach ( int id in InputOrder )
        {
            InputCell cell = terminal.Characters[id];
            bool dynamic = terminal.Config.ExistingColors == ExistingColors.Dynamic;
            Appearance( id, dynamic ? cell.Foreground : FinalColor( id, gradient, config.GradientDirection ), dynamic ? cell.Background : null );
            Engine.AddPath( id, "home", new( [new Waypoint( "0", Home[id] )], config.CharacterSpeed, config.Ease, 1 ) );
            Engine.Register( id, EngineEvent.PathComplete, new( CallerKind.Path, "home" ), static ( engine, character ) => engine.SetLayer( character, 0 ) );
        }

        corners = [new( 1, terminal.Height ), new( terminal.Width, terminal.Height ), new( terminal.Width, 1 ), new( 1, 1 )];
        string[] symbols = [config.TopSymbol, config.RightSymbol, config.BottomSymbol, config.LeftSymbol];
        launchers = new int[4];
        for ( int i = 0; i < 4; i++ )
        {
            int id = Engine.AddCharacter( symbols[i], corners[i] );
            launchers[i] = id;
            Engine.SetLayer( id, 2 );
            terminal.Visible[id] = true;
            Engine.Activate( id );
        }

        int main = launchers[0];
        Appearance( main, gradient.Spectrum[^1] );
        Engine.AddPath( main, "perimeter", new( [new Waypoint( "0", corners[0] ), new Waypoint( "1", corners[1] )], config.LauncherSpeed, layer: 2 ) );
        Engine.ActivatePath( main, "perimeter" );
        int index = 0;
        foreach ( int[] group in terminal.Groups( CharacterGroup.CenterToOutside ) )
        {
            foreach ( int id in group )
            {
                magazines[index++ % 4].Enqueue( id );
            }
        }
    }

    public override bool NextFrame( )
    {
        if ( magazines.Any( magazine => magazine.Count > 0 ) || Engine.ActiveCount > 1 )
        {
            int main = launchers[0];
            if ( !Engine.PathIsActive( main ) )
            {
                Terminal.Coordinates[main] = corners[0];
                Engine.ActivatePath( main, "perimeter" );
                Engine.Activate( main );
            }

            Coord parent = Terminal.Coordinates[main];
            double progress = ( double ) parent.Column / Terminal.Width;
            for ( int i = 0; i < 4; i++ )
            {
                int id = launchers[i];
                if ( i > 0 )
                {
                    Coord corner = corners[i];
                    Terminal.Coordinates[id] = corner == corners[1] ? new( Terminal.Width, Math.Max( 1, Terminal.Height - ( long ) (Terminal.Height * progress) ) ) : corner == corners[2] ? new( Math.Max( 1, Terminal.Width - ( long ) (Terminal.Width * progress) ), 1 ) : new( 1, Math.Min( Terminal.Height, 1 + ( long ) (Terminal.Height * progress) ) );
                }

                Coord coord = Terminal.Coordinates[id];
                Appearance( id, gradient.AtCoordinate( 1, Terminal.Height, 1, Terminal.Width, coord, config.GradientDirection ) );
            }

            if ( delay == 0 )
            {
                long count = Math.Max( ( long ) ((config.Volume * Terminal.InputCount) / 4.0), 1 );
                for ( int launcher = 0; launcher < 4; launcher++ )
                {
                    for ( long i = 0; i < count && magazines[launcher].TryDequeue( out int id ); i++ )
                    {
                        Terminal.Coordinates[id] = Terminal.Coordinates[launchers[launcher]];
                        Engine.ActivatePath( id, "home" );
                        Terminal.Visible[id] = true;
                        Engine.Activate( id );
                    }
                }

                delay = config.Delay;
            }
            else
            {
                delay--;
            }

            Engine.Update( );
            return true;
        }

        if ( !complete )
        {
            complete = true;
            foreach ( int id in launchers )
            {
                Terminal.Visible[id] = false;
            }

            return true;
        }

        return false;
    }
}
