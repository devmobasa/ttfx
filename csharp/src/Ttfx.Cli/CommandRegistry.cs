using System.Text;
using Ttfx.Core;

namespace Ttfx.Cli;

internal sealed record OptionDescriptor( string Name, string? Alias, string? Metavariable, string Description, bool Multiple, bool Hidden )
{
    internal bool AllowNegative => Name is "--canvas-width" or "--canvas-height";
    internal string Display => Metavariable is null ? Name : $"{Name} <{Metavariable}>{(Multiple ? "..." : "")}";
}

internal sealed record EffectDescriptor( string Name, string Description, OptionDescriptor[] Options )
{

    internal IEffect Create( Terminal terminal, Ttfx.Cli.Options options, EngineRng rng ) => Name switch
    {
        "expand" => new Expand( terminal, options.Expand, rng ),
        "randomsequence" => new RandomSequence( terminal, options.RandomSequence, rng ),
        "scattered" => new Scattered( terminal, options.Scattered, rng ),
        "middleout" => new MiddleOut( terminal, options.MiddleOut, rng ),
        "highlight" => new Highlight( terminal, options.Highlight, rng ),
        "rain" => new Rain( terminal, options.Rain, rng ),
        "colorshift" => new ColorShift( terminal, options.ColorShift, rng ),
        "spray" => new Spray( terminal, options.Spray, rng ),
        "pour" => new Pour( terminal, options.Pour, rng ),
        "bouncyballs" => new BouncyBalls( terminal, options.BouncyBalls, rng ),
        "slice" => new Slice( terminal, options.Slice, rng ),
        "waves" => new Waves( terminal, options.Waves, rng ),
        "errorcorrect" => new ErrorCorrect( terminal, options.ErrorCorrect, rng ),
        "sweep" => new Sweep( terminal, options.Sweep, rng ),
        "overflow" => new Overflow( terminal, options.Overflow, rng ),
        "print" => new PrintEffect( terminal, options.PrintEffect, rng ),
        "smoke" => new Smoke( terminal, options.Smoke, rng ),
        "fireworks" => new Fireworks( terminal, options.Fireworks, rng ),
        "orbittingvolley" => new OrbittingVolley( terminal, options.OrbittingVolley, rng ),
        "crumble" => new Crumble( terminal, options.Crumble, rng ),
        "beams" => new Beams( terminal, options.Beams, rng ),
        "unstable" => new Unstable( terminal, options.Unstable, rng ),
        "blackhole" => new Blackhole( terminal, options.Blackhole, rng ),
        "bubbles" => new Bubbles( terminal, options.Bubbles, rng ),
        "swarm" => new Swarm( terminal, options.Swarm, rng ),
        "synthgrid" => new SynthGrid( terminal, options.SynthGrid, rng ),
        "laseretch" => new LaserEtch( terminal, options.LaserEtch, rng ),
        "matrix" => new Matrix( terminal, options.Matrix, rng ),
        "vhstape" => new VhsTape( terminal, options.VhsTape, rng ),
        "thunderstorm" => new Thunderstorm( terminal, options.Thunderstorm, rng ),
        "rings" => new Rings( terminal, options.Rings, rng ),
        "burn" => new Burn( terminal, options.Burn, rng ),
        "binarypath" => new BinaryPath( terminal, options.BinaryPath, rng ),
        "spotlights" => new Spotlights( terminal, options.Spotlights, rng ),
        "wipe" => new Wipe( terminal, options.Wipe ),
        "slide" => new Slide( terminal, options.Slide ),
        "decrypt" => new Decrypt( terminal, options.Decrypt, rng ),
        _ => throw new EngineException( $"No effect registered for '{Name}'." )
    };
}

internal static partial class CommandRegistry
{
    internal static EffectDescriptor? FindEffect( string name ) => Array.Find( Effects, e => e.Name == name );

    internal static OptionDescriptor? FindOption( string? effect, string name ) =>
        Array.Find( effect is null ? RootOptions : FindEffect( effect )!.Options, o => o.Name == name || o.Alias == name );

    internal static string Help( string? effect )
    {
        var output = new StringBuilder( );
        EffectDescriptor? descriptor = effect is null ? null : FindEffect( effect );
        output.Append( descriptor?.Description ?? "Terminal text effects (C# implementation of terminaltexteffects)" );
        output.Append( "\n\nUsage: ttfx" ).Append( effect is null ? " [OPTIONS] [COMMAND]" : $" {effect} [OPTIONS]" ).Append( "\n\n" );
        if ( descriptor is null )
        {
            output.Append( "Commands:\n" );
            foreach ( var entry in Effects )
            {
                output.Append( "  " ).Append( entry.Name.PadRight( 17 ) ).Append( entry.Description ).Append( '\n' );
            }

            output.Append( "  help             Print this message or the help of the given subcommand(s)\n\n" );
        }

        output.Append( "Options:\n" );
        foreach ( var option in descriptor?.Options ?? RootOptions )
        {
            if ( option.Hidden )
            {
                continue;
            }

            output.Append( option.Alias is null ? "      " : $"  {option.Alias}, " ).Append( option.Display ).Append( "\n          " ).Append( option.Description ).Append( '\n' );
        }

        return output.ToString( );
    }

    internal static Options SelectEffect( Options options, EngineRng rng )
    {
        if ( !options.RandomEffect )
        {
            return options;
        }

        var choices = Effects.Where( e => (options.IncludeEffects.Length == 0 || options.IncludeEffects.Contains( e.Name )) && !options.ExcludeEffects.Contains( e.Name ) ).ToArray( );
        if ( choices.Length == 0 )
        {
            throw new EngineException( "No effects available after filtering." );
        }

        return options with
        {
            Effect = choices[rng.ChoiceIndex( choices.Length )].Name,
            Wipe = new( ),
            Slide = new( ),
            Decrypt = new( ),
            Spotlights = new( ),
            Burn = new( ),
            BinaryPath = new( ),
            Expand = new( ),
            RandomSequence = new( ),
            Scattered = new( ),
            MiddleOut = new( ),
            Highlight = new( ),
            Rain = new( ),
            ColorShift = new( ),
            Spray = new( ),
            Pour = new( ),
            BouncyBalls = new( ),
            Slice = new( ),
            Waves = new( ),
            ErrorCorrect = new( ),
            Sweep = new( ),
            Overflow = new( ),
            PrintEffect = new( ),
            Smoke = new( ),
            Fireworks = new( ),
            OrbittingVolley = new( ),
            Crumble = new( ),
            Beams = new( ),
            Unstable = new( ),
            Blackhole = new( ),
            Bubbles = new( ),
            Swarm = new( ),
            SynthGrid = new( ),
            LaserEtch = new( ),
            Matrix = new( ),
            VhsTape = new( ),
            Thunderstorm = new( ),
            Rings = new( )
        };
    }

    internal static string Completion( string shell ) => CompletionGenerator.Generate( shell );
}
