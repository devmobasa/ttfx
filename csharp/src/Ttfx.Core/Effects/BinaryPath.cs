using System.Text;

namespace Ttfx.Core;

public sealed record BinaryPathConfig
{
    public Color[] Stops { get; init; } = [Color.FromHex( "00d500" ), Color.FromHex( "007500" )];
    public long[] Steps { get; init; } = [12];
    public GradientDirection GradientDirection { get; init; } = GradientDirection.Radial;
    public Color[] BinaryColors { get; init; } = [Color.FromHex( "044E29" ), Color.FromHex( "157e38" ), Color.FromHex( "45bf55" ), Color.FromHex( "95ed87" )];
    public double MovementSpeed { get; init; } = 1;
    public double ActiveGroups { get; init; } = .08;
}

public sealed class BinaryPath : EffectBase
{
    private sealed class Representation( int character, Coord target, int[] bits )
    {
        internal readonly int Character = character;
        internal readonly Coord Target = target;
        internal readonly int[] Bits = bits;
        internal int Next;
        internal bool Active = true;
    }

    private readonly List<Representation> pending = [], active = [];
    private readonly OrderedSelection<Representation> selections;
    private readonly Queue<int[]> wipe;
    private readonly long maximum;
    private bool wiping, complete, lastProvided;

    public BinaryPath( Terminal terminal, BinaryPathConfig config, EngineRng rng ) : base( terminal, rng )
    {
        wipe = new( terminal.Groups( CharacterGroup.DiagonalTopRightToBottomLeft ) );
        var gradient = new Gradient( config.Stops, config.Steps );
        string[] binaries = InputOrder.Select( id => Convert.ToString( terminal.Characters[id].Symbol.EnumerateRunes( ).First( ).Value, 2 ).PadLeft( 8, '0' ) ).ToArray( );
        int nextBit = Engine.AddCharacters( binaries.SelectMany( binary => binary.Select( digit => digit.ToString( ) ) ).ToArray( ), new( 0, 0 ) );
        for ( int index = 0; index < InputOrder.Length; index++ )
        {
            int id = InputOrder[index];
            int codePoint = terminal.Characters[id].Symbol.EnumerateRunes( ).First( ).Value;
            string binary = Convert.ToString( codePoint, 2 ).PadLeft( 8, '0' );
            int[] bits = Enumerable.Range( nextBit, binary.Length ).ToArray( );
            nextBit += binary.Length;
            pending.Add( new( id, Home[id], bits ) );
        }

        foreach ( var representation in pending )
        {
            var coords = new List<Coord>( ) { RandomCoord( outside: true ) };
            bool column = rng.ChoiceIndex( 2 ) == 0;
            Coord next = coords[0];
            while ( coords[^1] != representation.Target )
            {
                Coord last = coords[^1], target = representation.Target;
                long horizontal = Math.Abs( last.Column - target.Column ), vertical = Math.Abs( last.Row - target.Row );
                if ( column && vertical > 0 )
                {
                    long limit = Math.Min( vertical, Math.Max( 10, ( long ) (terminal.Width * .2) ) );
                    next = new( last.Column, last.Row + rng.RandInt( 1, limit ) * Math.Sign( target.Row - last.Row ) );
                    column = false;
                }
                else if ( !column && horizontal > 0 )
                {
                    next = new( last.Column + rng.RandInt( 1, Math.Min( horizontal, 4 ) ) * Math.Sign( target.Column - last.Column ), last.Row );
                    column = true;
                }
                else
                {
                    next = target;
                }

                coords.Add( next );
            }

            coords.Add( next );
            coords.Add( representation.Target );
            var path = new PathDefinition( coords.Select( ( coord, index ) => new Waypoint( index.ToString( System.Globalization.CultureInfo.InvariantCulture ), coord ) ), config.MovementSpeed );
            foreach ( int bit in representation.Bits )
            {
                Engine.SetCoordinate( bit, coords[0] );
                Engine.AddPath( bit, "digital", path );
                Engine.ActivatePath( bit, "digital" );
                Engine.SetLayer( bit, 1 );
                Color color = config.BinaryColors[rng.ChoiceIndex( config.BinaryColors.Length )];
                Engine.AddScene( bit, "color", new( [new( VisualBytes( bit, terminal.Characters[bit].Symbol, color ), 1 )] ) );
                Engine.ActivateScene( bit, "color" );
            }
        }

        foreach ( int id in InputOrder )
        {
            InputCell cell = terminal.Characters[id];
            Color? fg = terminal.Config.ExistingColors == ExistingColors.Dynamic ? cell.Foreground : FinalColor( id, gradient, config.GradientDirection );
            Color? bg = terminal.Config.ExistingColors == ExistingColors.Dynamic ? cell.Background : null;
            Color? dimFg = fg is { } f ? Color.AdjustBrightness( f, .5 ) : null, dimBg = bg is { } b ? Color.AdjustBrightness( b, .5 ) : null;
            Color white = Color.FromHex( "ffffff" );
            Gradient? collapseFg = dimFg is { } df ? MakeGradient( [white, df], [7] ) : null, collapseBg = dimBg is { } db ? MakeGradient( [white, db], [7] ) : null;
            Gradient? brightenFg = dimFg is { } dff && fg is { } ff ? MakeGradient( [dff, ff], [10] ) : null, brightenBg = dimBg is { } dbb && bg is { } bb ? MakeGradient( [dbb, bb], [10] ) : null;
            Engine.AddScene( id, "collapse", collapseFg is not null || collapseBg is not null ? GradientScene( id, [cell.Symbol], 3, collapseFg, collapseBg, ease: new( EaseKind.InQuad ) ) : new( [new( VisualBytes( id, cell.Symbol ), 3 )], ease: new( EaseKind.InQuad ) ) );
            Engine.AddScene( id, "brighten", brightenFg is not null || brightenBg is not null ? GradientScene( id, [cell.Symbol], 2, brightenFg, brightenBg ) : new( [new( VisualBytes( id, cell.Symbol ), 2 )] ) );
        }

        selections = new( pending );
        maximum = Math.Max( 1, ( long ) (config.ActiveGroups * pending.Count) );
    }

    public override bool NextFrame( )
    {
        if ( !complete || Engine.HasActive )
        {
            if ( !wiping )
            {
                while ( active.Count < maximum && selections.Count != 0 )
                {
                    int index = Rng.ChoiceIndex( selections.Count );
                    active.Add( selections.TakeAt( index ) );
                }

                foreach ( var representation in active )
                {
                    if ( representation.Next < representation.Bits.Length )
                    {
                        int bit = representation.Bits[representation.Next++];
                        Engine.Activate( bit );
                        Terminal.Visible[bit] = true;
                    }
                    else if ( representation.Bits.All( bit => Terminal.Coordinates[bit] == representation.Target ) )
                    {
                        foreach ( int bit in representation.Bits )
                        {
                            Terminal.Visible[bit] = false;
                        }

                        representation.Active = false;
                        Terminal.Visible[representation.Character] = true;
                        Engine.ActivateScene( representation.Character, "collapse" );
                        Engine.Activate( representation.Character );
                    }
                }

                active.RemoveAll( representation => !representation.Active );
                if ( !Engine.HasActive )
                {
                    wiping = true;
                }
            }

            if ( wiping )
            {
                for ( int i = 0; i < 2; i++ )
                {
                    if ( wipe.TryDequeue( out int[]? group ) )
                    {
                        foreach ( int id in group )
                        {
                            Engine.ActivateScene( id, "brighten" );
                            Terminal.Visible[id] = true;
                            Engine.Activate( id );
                        }
                    }
                    else
                    {
                        complete = true;
                    }
                }
            }

            Engine.Update( );
            return true;
        }

        if ( !lastProvided )
        {
            lastProvided = true;
            return true;
        }

        return false;
    }
}
