using System.Globalization;
using Ttfx.Core;

namespace Ttfx.Cli;

internal sealed class UsageException( string message ) : Exception( message );

internal sealed partial record Options
{
    internal TerminalConfig Terminal { get; init; } = new( );
    internal WipeConfig Wipe { get; init; } = new( );
    internal SlideConfig Slide { get; init; } = new( );
    internal DecryptConfig Decrypt { get; init; } = new( );
    internal SpotlightsConfig Spotlights { get; init; } = new( );
    internal BurnConfig Burn { get; init; } = new( );
    internal BinaryPathConfig BinaryPath { get; init; } = new( );
    internal RingsConfig Rings { get; init; } = new( );
    internal ExpandConfig Expand { get; init; } = new( );
    internal RandomSequenceConfig RandomSequence { get; init; } = new( );
    internal ScatteredConfig Scattered { get; init; } = new( );
    internal MiddleOutConfig MiddleOut { get; init; } = new( );
    internal HighlightConfig Highlight { get; init; } = new( );
    internal RainConfig Rain { get; init; } = new( );
    internal ColorShiftConfig ColorShift { get; init; } = new( );
    internal SprayConfig Spray { get; init; } = new( );
    internal PourConfig Pour { get; init; } = new( );
    internal BouncyBallsConfig BouncyBalls { get; init; } = new( );
    internal SliceConfig Slice { get; init; } = new( );
    internal WavesConfig Waves { get; init; } = new( );
    internal ErrorCorrectConfig ErrorCorrect { get; init; } = new( );
    internal SweepConfig Sweep { get; init; } = new( );
    internal OverflowConfig Overflow { get; init; } = new( );
    internal PrintEffectConfig PrintEffect { get; init; } = new( );
    internal SmokeConfig Smoke { get; init; } = new( );
    internal FireworksConfig Fireworks { get; init; } = new( );
    internal OrbittingVolleyConfig OrbittingVolley { get; init; } = new( );
    internal CrumbleConfig Crumble { get; init; } = new( );
    internal BeamsConfig Beams { get; init; } = new( );
    internal UnstableConfig Unstable { get; init; } = new( );
    internal BlackholeConfig Blackhole { get; init; } = new( );
    internal BubblesConfig Bubbles { get; init; } = new( );
    internal SwarmConfig Swarm { get; init; } = new( );
    internal SynthGridConfig SynthGrid { get; init; } = new( );
    internal LaserEtchConfig LaserEtch { get; init; } = new( );
    internal MatrixConfig Matrix { get; init; } = new( );
    internal VhsTapeConfig VhsTape { get; init; } = new( );
    internal ThunderstormConfig Thunderstorm { get; init; } = new( );
    internal string? Effect { get; init; }
    internal string? InputFile { get; init; }
    internal bool M0Dump { get; init; }
    internal bool ParityDump { get; init; }
    internal bool VirtualClock { get; init; }
    internal ulong? MaxFrames { get; init; }
    internal ulong? Seed { get; init; }
    internal bool Help { get; init; }
    internal bool Version { get; init; }
    internal string? Completion { get; init; }
    internal bool RandomEffect { get; init; }
    internal string[] IncludeEffects { get; init; } = [];
    internal string[] ExcludeEffects { get; init; } = [];

    internal static Options Parse( string[] args )
    {
        var options = new Options( );
        var seen = new HashSet<string>( StringComparer.Ordinal );
        var listValues = new Dictionary<string, List<string>>( StringComparer.Ordinal );

        for ( int i = 0; i < args.Length; i++ )
        {
            string token = args[i];
            if ( token is "-h" or "--help" )
            {
                return options with { Help = true };
            }

            if ( options.Effect is null && token is "-v" or "--version" )
            {
                return options with { Version = true };
            }

            if ( options.Effect is null && token == "help" )
            {
                string? target = i + 1 < args.Length ? args[++i] : null;
                if ( target is not null && CommandRegistry.FindEffect( target ) is null )
                {
                    throw UnknownEffect( target );
                }

                return options with { Help = true, Effect = target };
            }

            if ( !token.StartsWith( '-' ) )
            {
                if ( options.Effect is not null )
                {
                    throw Unexpected( token );
                }

                if ( CommandRegistry.FindEffect( token ) is null )
                {
                    throw UnknownEffect( token );
                }

                options = options with { Effect = token };
                continue;
            }

            int equals = token.IndexOf( '=' );
            string name = equals >= 0 ? token[..equals] : token;
            string? attached = equals >= 0 ? token[(equals + 1)..] : null;
            OptionDescriptor? spec = CommandRegistry.FindOption( options.Effect, name );
            if ( spec is null )
            {
                throw Unexpected( name );
            }

            string usage = options.Effect is null ? "ttfx [OPTIONS] [COMMAND]" : $"ttfx {options.Effect} [OPTIONS]";
            string key = $"{options.Effect}:{spec.Name}";
            if ( !seen.Add( key ) && !spec.Multiple )
            {
                throw new UsageException( $"error: the argument '{spec.Display}' cannot be used multiple times\n\nUsage: {usage}\n\nFor more information, try '--help'.\n" );
            }

            var values = new List<string>( );
            if ( spec.Metavariable is not null )
            {
                if ( attached is not null )
                {
                    values.Add( attached );
                }
                else
                {
                    while ( i + 1 < args.Length )
                    {
                        string next = args[i + 1];
                        if ( next.StartsWith( '-' ) && next != "-" && (!spec.AllowNegative || !Numbers.TryFloat( next, out _ )) )
                        {
                            if ( values.Count == 0 && !next.StartsWith( "--", StringComparison.Ordinal ) )
                            {
                                throw new UsageException( $"error: unexpected argument '{(next.Length > 1 ? next[..2] : next)}' found\n\nUsage: {usage}\n\nFor more information, try '--help'.\n" );
                            }

                            break;
                        }

                        values.Add( args[++i] );
                        if ( !spec.Multiple )
                        {
                            break;
                        }
                    }
                }

                if ( values.Count == 0 )
                {
                    if ( spec.Name == "--slice-direction" )
                    {
                        ParseSliceDirection( "" );
                    }

                    if ( spec.Name == "--print-completion" )
                    {
                        ParseShell( "" );
                    }

                    throw new UsageException( $"error: a value is required for '{spec.Display}' but none was supplied\n\nFor more information, try '--help'.\n" );
                }
            }
            else if ( attached is not null )
            {
                throw Unexpected( token );
            }

            try
            {
                if ( spec.Multiple )
                {
                    if ( !listValues.TryGetValue( key, out var previous ) )
                    {
                        previous = [];
                        listValues.Add( key, previous );
                    }

                    previous.AddRange( values );
                    values = previous;
                }

                options = Apply( options, spec, values.ToArray( ) );
            }
            catch ( InvalidOptionValueException error )
            {
                throw InvalidValue( error.Value, spec, error.Message );
            }
            catch ( ArgumentException error )
            {
                throw InvalidValue( values.Count > 0 ? values[0] : "", spec, error.Message );
            }
        }

        if ( options.IncludeEffects.Length != 0 && options.ExcludeEffects.Length != 0 )
        {
            throw new UsageException( "error: the argument '--include-effects <INCLUDE_EFFECTS>...' cannot be used with '--exclude-effects <EXCLUDE_EFFECTS>...'\n\nUsage: ttfx --include-effects <INCLUDE_EFFECTS>...\n\nFor more information, try '--help'.\n" );
        }

        return options;
    }

    private static UsageException Unexpected( string token ) => new( $"error: unexpected argument '{token}' found\n\nFor more information, try '--help'.\n" );
    private static UsageException UnknownEffect( string token ) => new( $"error: unrecognized subcommand '{token}'\n\nUsage: ttfx [OPTIONS] [COMMAND]\n\nFor more information, try '--help'.\n" );
    private static UsageException InvalidValue( string value, OptionDescriptor spec, string message ) => new( $"error: invalid value '{value}' for '{spec.Display}': {message}\n\nFor more information, try '--help'.\n" );

    internal static bool TryInteger( string value, out long result )
    {
        result = 0;

        return value.Length > 0 && value.All( c => c is >= '0' and <= '9' or '+' or '-' ) &&
        long.TryParse( value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out result );
    }

    private static long ParseInteger( string value, long minimum, string? validation )
    {
        if ( !TryInteger( value, out long result ) )
        {
            if ( validation is null )
            {
                if ( value.Length == 0 )
                {
                    throw new ArgumentException( "cannot parse integer from empty string" );
                }

                ReadOnlySpan<char> digits = value.AsSpan( value[0] is '+' or '-' ? 1 : 0 );
                bool overflow = digits.Length > 0 && !digits.ContainsAnyExceptInRange( '0', '9' );
                throw new ArgumentException( overflow ? "number too large to fit in target type" : "invalid digit found in string" );
            }

            throw new ArgumentException( $"invalid int value: '{value}'" );
        }

        if ( result < minimum )
        {
            throw new ArgumentException( validation?.StartsWith( "is ", StringComparison.Ordinal ) == true ? $"{result} {validation}" : $"{result} is not a valid value. {validation}" );
        }

        return result;
    }

    private static ulong ParseUnsigned( string value )
    {
        if ( value.Length == 0 )
        {
            throw new ArgumentException( "cannot parse integer from empty string" );
        }

        ReadOnlySpan<char> digits = value.AsSpan( value[0] == '+' ? 1 : 0 );
        if ( digits.Length == 0 || digits.ContainsAnyExceptInRange( '0', '9' ) )
        {
            throw new ArgumentException( "invalid digit found in string" );
        }

        if ( !ulong.TryParse( value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out ulong result ) )
        {
            throw new ArgumentException( "number too large to fit in target type" );
        }

        return result;
    }

    private static double ParseRatio( string value )
    {
        if ( !Numbers.TryFloat( value, out double result ) )
        {
            throw new ArgumentException( $"invalid float value: '{value}'" );
        }

        if ( !(result >= 0 && result <= 1) )
        {
            throw new ArgumentException( $"{Numbers.FormatFloat( result )} is not a valid value. Argument must be a float 0 <= n <= 1." );
        }

        return result;
    }

    private static double ParsePositiveRatio( string value )
    {
        if ( !Numbers.TryFloat( value, out double result ) )
        {
            throw new ArgumentException( $"invalid float value: '{value}'" );
        }

        if ( !(result > 0 && result <= 1) )
        {
            throw new ArgumentException( $"{Numbers.FormatFloat( result )} is not a valid value. Argument must be a float 0 < n <= 1." );
        }

        return result;
    }

    private static double ParseNonNegativeFloat( string value )
    {
        if ( !Numbers.TryFloat( value, out double result ) )
        {
            throw new ArgumentException( $"invalid float value: '{value}'" );
        }

        if ( !(result >= 0) )
        {
            throw new ArgumentException( $"{Numbers.FormatFloat( result )} is not a valid value. Argument must be a float >= 0." );
        }

        return result;
    }

    private static (double, double) ParseFloatRange( string value )
    {
        int separator = value.IndexOf( '-' );
        if ( separator < 0 || !Numbers.TryFloat( value[..separator], out double low ) || !Numbers.TryFloat( value[(separator + 1)..], out double high ) || !(low > 0 && low <= high) )
        {
            throw new ArgumentException( $"invalid range: '{value}'" );
        }

        return (low, high);
    }

    private static double ParseSpeed( string value )
    {
        if ( !Numbers.TryFloat( value, out double speed ) )
        {
            throw new ArgumentException( $"invalid float value: '{value}'" );
        }

        if ( !(speed > 0) )
        {
            throw new ArgumentException( $"{Numbers.FormatFloat( speed )} is not a valid value. Argument must be a float > 0." );
        }

        return speed;
    }

    private static SlideGrouping ParseSlideGrouping( string value ) => value switch
    {
        "row" => SlideGrouping.Row,
        "column" => SlideGrouping.Column,
        "diagonal" => SlideGrouping.Diagonal,
        _ => throw new ArgumentException( $"invalid choice: '{value}' (choose from 'row', 'column', 'diagonal')" )
    };

    private static Easing ParseEase( string value ) => Easing.TryParse( value, out Easing easing ) ? easing : throw new ArgumentException( $"invalid easing function: '{value}'" );
    private static GradientDirection ParseGradient( string value ) => value switch
    {
        "vertical" => GradientDirection.Vertical,
        "horizontal" => GradientDirection.Horizontal,
        "radial" => GradientDirection.Radial,
        "diagonal" => GradientDirection.Diagonal,
        _ => throw new ArgumentException( $"invalid gradient direction: '{value}'" )
    };
    private static Anchor ParseAnchor( string value ) => value switch
    {
        "n" => Anchor.N,
        "ne" => Anchor.Ne,
        "e" => Anchor.E,
        "se" => Anchor.Se,
        "s" => Anchor.S,
        "sw" => Anchor.Sw,
        "w" => Anchor.W,
        "nw" => Anchor.Nw,
        "c" => Anchor.C,
        _ => throw new ArgumentException( $"invalid anchor: '{value}'" )
    };
    private static CharacterGroup ParseGroup( string value ) => value switch
    {
        "column_left_to_right" => CharacterGroup.ColumnLeftToRight,
        "column_right_to_left" => CharacterGroup.ColumnRightToLeft,
        "row_top_to_bottom" => CharacterGroup.RowTopToBottom,
        "row_bottom_to_top" => CharacterGroup.RowBottomToTop,
        "diagonal_bottom_left_to_top_right" => CharacterGroup.DiagonalBottomLeftToTopRight,
        "diagonal_top_right_to_bottom_left" => CharacterGroup.DiagonalTopRightToBottomLeft,
        "diagonal_top_left_to_bottom_right" => CharacterGroup.DiagonalTopLeftToBottomRight,
        "diagonal_bottom_right_to_top_left" => CharacterGroup.DiagonalBottomRightToTopLeft,
        "center_to_outside" => CharacterGroup.CenterToOutside,
        "outside_to_center" => CharacterGroup.OutsideToCenter,
        _ => throw new ArgumentException( $"invalid character group: '{value}'" )
    };
}
