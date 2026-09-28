using System.Text;
using Ttfx.Core;

namespace Ttfx.Cli;

internal sealed class InvalidOptionValueException( string value, string message ) : ArgumentException( message )
{
    internal string Value { get; } = value;
}

internal sealed partial record Options
{
    private static class BindingStore
    {
        internal static readonly Dictionary<string, Func<Options, string[], Options>> Values = new( StringComparer.Ordinal )
        {
            [":--input-file"] = ( o, v ) => o with { InputFile = v[0] },
            [":--m0-dump"] = ( o, v ) => o with { M0Dump = true },
            [":--parity-dump"] = ( o, v ) => o with { ParityDump = true },
            [":--virtual-clock"] = ( o, v ) => o with { VirtualClock = true },
            [":--max-frames"] = ( o, v ) => o with { MaxFrames = ParseUnsigned( v[0] ) },
            [":--seed"] = ( o, v ) => o with { Seed = ParseUnsigned( v[0] ) },
            [":--print-completion"] = ( o, v ) => o with { Completion = ParseShell( v[0] ) },
            [":--random-effect"] = ( o, v ) => o with { RandomEffect = true },
            [":--include-effects"] = ( o, v ) => o with { IncludeEffects = v },
            [":--exclude-effects"] = ( o, v ) => o with { ExcludeEffects = v },
            [":--tab-width"] = ( o, v ) => o with { Terminal = o.Terminal with { TabWidth = ParseInteger( v[0], 1, "is not > 0" ) } },
            [":--frame-rate"] = ( o, v ) => o with { Terminal = o.Terminal with { FrameRate = ParseInteger( v[0], 0, "is not >= 0" ) } },
            [":--canvas-width"] = ( o, v ) => o with { Terminal = o.Terminal with { CanvasWidth = ParseInteger( v[0], -1, "is not >= -1" ) } },
            [":--canvas-height"] = ( o, v ) => o with { Terminal = o.Terminal with { CanvasHeight = ParseInteger( v[0], -1, "is not >= -1" ) } },
            [":--anchor-canvas"] = ( o, v ) => o with { Terminal = o.Terminal with { AnchorCanvas = ParseAnchor( v[0] ) } },
            [":--anchor-text"] = ( o, v ) => o with { Terminal = o.Terminal with { AnchorText = ParseAnchor( v[0] ) } },
            [":--existing-color-handling"] = ( o, v ) => o with { Terminal = o.Terminal with { ExistingColors = ParseHandling( v[0] ) } },
            [":--terminal-background-color"] = ( o, v ) => o with { Terminal = o.Terminal with { Background = Color.Parse( v[0] ) } },
            [":--xterm-colors"] = ( o, v ) => o with { Terminal = o.Terminal with { XtermColors = true } },
            [":--no-color"] = ( o, v ) => o with { Terminal = o.Terminal with { NoColor = true } },
            [":--wrap-text"] = ( o, v ) => o with { Terminal = o.Terminal with { WrapText = true } },
            [":--ignore-terminal-dimensions"] = ( o, v ) => o with { Terminal = o.Terminal with { IgnoreTerminalDimensions = true } },
            [":--reuse-canvas"] = ( o, v ) => o with { Terminal = o.Terminal with { ReuseCanvas = true } },
            [":--no-eol"] = ( o, v ) => o with { Terminal = o.Terminal with { NoEol = true } },
            [":--no-restore-cursor"] = ( o, v ) => o with { Terminal = o.Terminal with { NoRestoreCursor = true } },
            ["expand:--expand-easing"] = ( o, v ) => o with { Expand = o.Expand with { ExpandEasing = ParseEase( v[0] ) } },
            ["expand:--movement-speed"] = ( o, v ) => o with { Expand = o.Expand with { MovementSpeed = ParseSpeed( v[0] ) } },
            ["expand:--final-gradient-stops"] = ( o, v ) => o with { Expand = o.Expand with { Stops = ParseValues( v, Color.Parse ) } },
            ["expand:--final-gradient-steps"] = ( o, v ) => o with { Expand = o.Expand with { Steps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["expand:--final-gradient-direction"] = ( o, v ) => o with { Expand = o.Expand with { GradientDirection = ParseGradient( v[0] ) } },
            ["randomsequence:--speed"] = ( o, v ) => o with { RandomSequence = o.RandomSequence with { Speed = ParseSpeed( v[0] ) } },
            ["randomsequence:--final-gradient-frames"] = ( o, v ) => o with { RandomSequence = o.RandomSequence with { Frames = ParseInteger( v[0], long.MinValue, null ) } },
            ["randomsequence:--final-gradient-stops"] = ( o, v ) => o with { RandomSequence = o.RandomSequence with { Stops = ParseValues( v, Color.Parse ) } },
            ["randomsequence:--final-gradient-steps"] = ( o, v ) => o with { RandomSequence = o.RandomSequence with { Steps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["randomsequence:--final-gradient-direction"] = ( o, v ) => o with { RandomSequence = o.RandomSequence with { GradientDirection = ParseGradient( v[0] ) } },
            ["scattered:--movement-speed"] = ( o, v ) => o with { Scattered = o.Scattered with { MovementSpeed = ParseSpeed( v[0] ) } },
            ["scattered:--movement-easing"] = ( o, v ) => o with { Scattered = o.Scattered with { MovementEase = ParseEase( v[0] ) } },
            ["scattered:--final-gradient-frames"] = ( o, v ) => o with { Scattered = o.Scattered with { Frames = ParseInteger( v[0], long.MinValue, null ) } },
            ["scattered:--final-gradient-stops"] = ( o, v ) => o with { Scattered = o.Scattered with { Stops = ParseValues( v, Color.Parse ) } },
            ["scattered:--final-gradient-steps"] = ( o, v ) => o with { Scattered = o.Scattered with { Steps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["scattered:--final-gradient-direction"] = ( o, v ) => o with { Scattered = o.Scattered with { GradientDirection = ParseGradient( v[0] ) } },
            ["middleout:--starting-color"] = ( o, v ) => o with { MiddleOut = o.MiddleOut with { StartingColor = Color.Parse( v[0] ) } },
            ["middleout:--expand-direction"] = ( o, v ) => o with { MiddleOut = o.MiddleOut with { Horizontal = ParseHorizontal( v[0] ) } },
            ["middleout:--center-movement-speed"] = ( o, v ) => o with { MiddleOut = o.MiddleOut with { CenterSpeed = ParseSpeed( v[0] ) } },
            ["middleout:--full-movement-speed"] = ( o, v ) => o with { MiddleOut = o.MiddleOut with { FullSpeed = ParseSpeed( v[0] ) } },
            ["middleout:--center-easing"] = ( o, v ) => o with { MiddleOut = o.MiddleOut with { CenterEase = ParseEase( v[0] ) } },
            ["middleout:--full-easing"] = ( o, v ) => o with { MiddleOut = o.MiddleOut with { FullEase = ParseEase( v[0] ) } },
            ["middleout:--final-gradient-stops"] = ( o, v ) => o with { MiddleOut = o.MiddleOut with { Stops = ParseValues( v, Color.Parse ) } },
            ["middleout:--final-gradient-steps"] = ( o, v ) => o with { MiddleOut = o.MiddleOut with { Steps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["middleout:--final-gradient-direction"] = ( o, v ) => o with { MiddleOut = o.MiddleOut with { GradientDirection = ParseGradient( v[0] ) } },
            ["highlight:--highlight-brightness"] = ( o, v ) => o with { Highlight = o.Highlight with { Brightness = ParseSpeed( v[0] ) } },
            ["highlight:--highlight-direction"] = ( o, v ) => o with { Highlight = o.Highlight with { Direction = ParseGroup( v[0] ) } },
            ["highlight:--highlight-width"] = ( o, v ) => o with { Highlight = o.Highlight with { Width = ParseInteger( v[0], 1, "Argument must be an int > 0." ) } },
            ["highlight:--final-gradient-stops"] = ( o, v ) => o with { Highlight = o.Highlight with { Stops = ParseValues( v, Color.Parse ) } },
            ["highlight:--final-gradient-steps"] = ( o, v ) => o with { Highlight = o.Highlight with { Steps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["highlight:--final-gradient-direction"] = ( o, v ) => o with { Highlight = o.Highlight with { GradientDirection = ParseGradient( v[0] ) } },
            ["rain:--rain-colors"] = ( o, v ) => o with { Rain = o.Rain with { RainColors = ParseValues( v, Color.Parse ) } },
            ["rain:--rain-symbols"] = ( o, v ) => o with { Rain = o.Rain with { Symbols = ParseValues( v, ParseSymbol ) } },
            ["rain:--movement-speed"] = ( o, v ) => o with { Rain = o.Rain with { MovementSpeed = ParseFloatRange( v[0] ) } },
            ["rain:--movement-easing"] = ( o, v ) => o with { Rain = o.Rain with { MovementEase = ParseEase( v[0] ) } },
            ["rain:--final-gradient-stops"] = ( o, v ) => o with { Rain = o.Rain with { Stops = ParseValues( v, Color.Parse ) } },
            ["rain:--final-gradient-steps"] = ( o, v ) => o with { Rain = o.Rain with { Steps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["rain:--final-gradient-direction"] = ( o, v ) => o with { Rain = o.Rain with { GradientDirection = ParseGradient( v[0] ) } },
            ["colorshift:--gradient-stops"] = ( o, v ) => o with { ColorShift = o.ColorShift with { Colors = ParseValues( v, Color.Parse ) } },
            ["colorshift:--gradient-steps"] = ( o, v ) => o with { ColorShift = o.ColorShift with { ColorSteps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["colorshift:--gradient-frames"] = ( o, v ) => o with { ColorShift = o.ColorShift with { Frames = ParseInteger( v[0], 1, "Argument must be an int > 0." ) } },
            ["colorshift:--no-travel"] = ( o, v ) => o with { ColorShift = o.ColorShift with { NoTravel = true } },
            ["colorshift:--travel-direction"] = ( o, v ) => o with { ColorShift = o.ColorShift with { TravelDirection = ParseGradient( v[0] ) } },
            ["colorshift:--reverse-travel-direction"] = ( o, v ) => o with { ColorShift = o.ColorShift with { Reverse = true } },
            ["colorshift:--no-loop"] = ( o, v ) => o with { ColorShift = o.ColorShift with { NoLoop = true } },
            ["colorshift:--cycles"] = ( o, v ) => o with { ColorShift = o.ColorShift with { Cycles = ParseInteger( v[0], 1, "Argument must be an int > 0." ) } },
            ["colorshift:--skip-final-gradient"] = ( o, v ) => o with { ColorShift = o.ColorShift with { SkipFinal = true } },
            ["colorshift:--final-gradient-stops"] = ( o, v ) => o with { ColorShift = o.ColorShift with { Stops = ParseValues( v, Color.Parse ) } },
            ["colorshift:--final-gradient-steps"] = ( o, v ) => o with { ColorShift = o.ColorShift with { Steps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["colorshift:--final-gradient-direction"] = ( o, v ) => o with { ColorShift = o.ColorShift with { GradientDirection = ParseGradient( v[0] ) } },
            ["spray:--spray-position"] = ( o, v ) => o with { Spray = o.Spray with { Position = ParseChoice( v[0], "n", "ne", "e", "se", "s", "sw", "w", "nw", "center" ) } },
            ["spray:--spray-volume"] = ( o, v ) => o with { Spray = o.Spray with { Volume = ParsePositiveRatio( v[0] ) } },
            ["spray:--movement-speed-range"] = ( o, v ) => o with { Spray = o.Spray with { MovementSpeed = ParseFloatRange( v[0] ) } },
            ["spray:--movement-easing"] = ( o, v ) => o with { Spray = o.Spray with { MovementEase = ParseEase( v[0] ) } },
            ["spray:--final-gradient-stops"] = ( o, v ) => o with { Spray = o.Spray with { Stops = ParseValues( v, Color.Parse ) } },
            ["spray:--final-gradient-steps"] = ( o, v ) => o with { Spray = o.Spray with { Steps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["spray:--final-gradient-direction"] = ( o, v ) => o with { Spray = o.Spray with { GradientDirection = ParseGradient( v[0] ) } },
            ["pour:--pour-direction"] = ( o, v ) => o with { Pour = o.Pour with { Direction = ParseChoice( v[0], "up", "down", "left", "right" ) } },
            ["pour:--pour-speed"] = ( o, v ) => o with { Pour = o.Pour with { Speed = ParseInteger( v[0], 1, "Argument must be an int > 0." ) } },
            ["pour:--movement-speed-range"] = ( o, v ) => o with { Pour = o.Pour with { MovementSpeed = ParseFloatRange( v[0] ) } },
            ["pour:--gap"] = ( o, v ) => o with { Pour = o.Pour with { Gap = ParseInteger( v[0], 0, "Argument must be an int >= 0." ) } },
            ["pour:--starting-color"] = ( o, v ) => o with { Pour = o.Pour with { StartingColor = Color.Parse( v[0] ) } },
            ["pour:--final-gradient-frames"] = ( o, v ) => o with { Pour = o.Pour with { Frames = ParseInteger( v[0], long.MinValue, null ) } },
            ["pour:--movement-easing"] = ( o, v ) => o with { Pour = o.Pour with { MovementEase = ParseEase( v[0] ) } },
            ["pour:--final-gradient-stops"] = ( o, v ) => o with { Pour = o.Pour with { Stops = ParseValues( v, Color.Parse ) } },
            ["pour:--final-gradient-steps"] = ( o, v ) => o with { Pour = o.Pour with { Steps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["pour:--final-gradient-direction"] = ( o, v ) => o with { Pour = o.Pour with { GradientDirection = ParseGradient( v[0] ) } },
            ["bouncyballs:--ball-colors"] = ( o, v ) => o with { BouncyBalls = o.BouncyBalls with { BallColors = ParseValues( v, Color.Parse ) } },
            ["bouncyballs:--ball-symbols"] = ( o, v ) => o with { BouncyBalls = o.BouncyBalls with { Symbols = ParseValues( v, ParseSymbol ) } },
            ["bouncyballs:--ball-delay"] = ( o, v ) => o with { BouncyBalls = o.BouncyBalls with { Delay = ParseInteger( v[0], 0, "Argument must be an int >= 0." ) } },
            ["bouncyballs:--movement-speed"] = ( o, v ) => o with { BouncyBalls = o.BouncyBalls with { MovementSpeed = ParseSpeed( v[0] ) } },
            ["bouncyballs:--movement-easing"] = ( o, v ) => o with { BouncyBalls = o.BouncyBalls with { MovementEase = ParseEase( v[0] ) } },
            ["bouncyballs:--final-gradient-stops"] = ( o, v ) => o with { BouncyBalls = o.BouncyBalls with { Stops = ParseValues( v, Color.Parse ) } },
            ["bouncyballs:--final-gradient-steps"] = ( o, v ) => o with { BouncyBalls = o.BouncyBalls with { Steps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["bouncyballs:--final-gradient-direction"] = ( o, v ) => o with { BouncyBalls = o.BouncyBalls with { GradientDirection = ParseGradient( v[0] ) } },
            ["slice:--slice-direction"] = ( o, v ) => o with { Slice = o.Slice with { Direction = ParseSliceDirection( v[0] ) } },
            ["slice:--movement-speed"] = ( o, v ) => o with { Slice = o.Slice with { MovementSpeed = ParseSpeed( v[0] ) } },
            ["slice:--movement-easing"] = ( o, v ) => o with { Slice = o.Slice with { MovementEase = ParseEase( v[0] ) } },
            ["slice:--final-gradient-stops"] = ( o, v ) => o with { Slice = o.Slice with { Stops = ParseValues( v, Color.Parse ) } },
            ["slice:--final-gradient-steps"] = ( o, v ) => o with { Slice = o.Slice with { Steps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["slice:--final-gradient-direction"] = ( o, v ) => o with { Slice = o.Slice with { GradientDirection = ParseGradient( v[0] ) } },
            ["waves:--wave-symbols"] = ( o, v ) => o with { Waves = o.Waves with { Symbols = ParseValues( v, ParseSymbol ) } },
            ["waves:--wave-gradient-stops"] = ( o, v ) => o with { Waves = o.Waves with { WaveColors = ParseValues( v, Color.Parse ) } },
            ["waves:--wave-gradient-steps"] = ( o, v ) => o with { Waves = o.Waves with { WaveSteps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["waves:--wave-count"] = ( o, v ) => o with { Waves = o.Waves with { Count = ParseInteger( v[0], 1, "Argument must be an int > 0." ) } },
            ["waves:--wave-length"] = ( o, v ) => o with { Waves = o.Waves with { Length = ParseInteger( v[0], 1, "Argument must be an int > 0." ) } },
            ["waves:--wave-direction"] = ( o, v ) => o with { Waves = o.Waves with { Direction = ParseWaveDirection( v[0] ) } },
            ["waves:--wave-easing"] = ( o, v ) => o with { Waves = o.Waves with { Ease = ParseEase( v[0] ) } },
            ["waves:--final-gradient-stops"] = ( o, v ) => o with { Waves = o.Waves with { Stops = ParseValues( v, Color.Parse ) } },
            ["waves:--final-gradient-steps"] = ( o, v ) => o with { Waves = o.Waves with { Steps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["waves:--final-gradient-direction"] = ( o, v ) => o with { Waves = o.Waves with { GradientDirection = ParseGradient( v[0] ) } },
            ["errorcorrect:--error-pairs"] = ( o, v ) => o with { ErrorCorrect = o.ErrorCorrect with { Pairs = ParseSpeed( v[0] ) } },
            ["errorcorrect:--swap-delay"] = ( o, v ) => o with { ErrorCorrect = o.ErrorCorrect with { Delay = ParseInteger( v[0], 1, "Argument must be an int > 0." ) } },
            ["errorcorrect:--error-color"] = ( o, v ) => o with { ErrorCorrect = o.ErrorCorrect with { ErrorColor = Color.Parse( v[0] ) } },
            ["errorcorrect:--correct-color"] = ( o, v ) => o with { ErrorCorrect = o.ErrorCorrect with { CorrectColor = Color.Parse( v[0] ) } },
            ["errorcorrect:--movement-speed"] = ( o, v ) => o with { ErrorCorrect = o.ErrorCorrect with { MovementSpeed = ParseSpeed( v[0] ) } },
            ["errorcorrect:--final-gradient-stops"] = ( o, v ) => o with { ErrorCorrect = o.ErrorCorrect with { Stops = ParseValues( v, Color.Parse ) } },
            ["errorcorrect:--final-gradient-steps"] = ( o, v ) => o with { ErrorCorrect = o.ErrorCorrect with { Steps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["errorcorrect:--final-gradient-direction"] = ( o, v ) => o with { ErrorCorrect = o.ErrorCorrect with { GradientDirection = ParseGradient( v[0] ) } },
            ["sweep:--sweep-symbols"] = ( o, v ) => o with { Sweep = o.Sweep with { Symbols = ParseValues( v, ParseSymbol ) } },
            ["sweep:--first-sweep-direction"] = ( o, v ) => o with { Sweep = o.Sweep with { FirstDirection = ParseGroup( v[0] ) } },
            ["sweep:--second-sweep-direction"] = ( o, v ) => o with { Sweep = o.Sweep with { SecondDirection = ParseGroup( v[0] ) } },
            ["sweep:--final-gradient-stops"] = ( o, v ) => o with { Sweep = o.Sweep with { Stops = ParseValues( v, Color.Parse ) } },
            ["sweep:--final-gradient-steps"] = ( o, v ) => o with { Sweep = o.Sweep with { Steps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["sweep:--final-gradient-direction"] = ( o, v ) => o with { Sweep = o.Sweep with { GradientDirection = ParseGradient( v[0] ) } },
            ["overflow:--overflow-gradient-stops"] = ( o, v ) => o with { Overflow = o.Overflow with { OverflowColors = ParseValues( v, Color.Parse ) } },
            ["overflow:--overflow-cycles-range"] = ( o, v ) => o with { Overflow = o.Overflow with { Cycles = ParseIntegerRange( v[0] ) } },
            ["overflow:--overflow-speed"] = ( o, v ) => o with { Overflow = o.Overflow with { Speed = ParseInteger( v[0], 1, "Argument must be an int > 0." ) } },
            ["overflow:--final-gradient-stops"] = ( o, v ) => o with { Overflow = o.Overflow with { Stops = ParseValues( v, Color.Parse ) } },
            ["overflow:--final-gradient-steps"] = ( o, v ) => o with { Overflow = o.Overflow with { Steps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["overflow:--final-gradient-direction"] = ( o, v ) => o with { Overflow = o.Overflow with { GradientDirection = ParseGradient( v[0] ) } },
            ["print:--print-head-return-speed"] = ( o, v ) => o with { PrintEffect = o.PrintEffect with { ReturnSpeed = ParseSpeed( v[0] ) } },
            ["print:--print-speed"] = ( o, v ) => o with { PrintEffect = o.PrintEffect with { Speed = ParseInteger( v[0], 1, "Argument must be an int > 0." ) } },
            ["print:--print-head-easing"] = ( o, v ) => o with { PrintEffect = o.PrintEffect with { Ease = ParseEase( v[0] ) } },
            ["print:--final-gradient-stops"] = ( o, v ) => o with { PrintEffect = o.PrintEffect with { Stops = ParseValues( v, Color.Parse ) } },
            ["print:--final-gradient-steps"] = ( o, v ) => o with { PrintEffect = o.PrintEffect with { Steps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["print:--final-gradient-direction"] = ( o, v ) => o with { PrintEffect = o.PrintEffect with { GradientDirection = ParseGradient( v[0] ) } },
            ["smoke:--starting-color"] = ( o, v ) => o with { Smoke = o.Smoke with { StartingColor = Color.Parse( v[0] ) } },
            ["smoke:--smoke-symbols"] = ( o, v ) => o with { Smoke = o.Smoke with { Symbols = ParseValues( v, ParseSymbol ) } },
            ["smoke:--smoke-gradient-stops"] = ( o, v ) => o with { Smoke = o.Smoke with { SmokeColors = ParseValues( v, Color.Parse ) } },
            ["smoke:--use-whole-canvas"] = ( o, v ) => o with { Smoke = o.Smoke with { WholeCanvas = true } },
            ["smoke:--final-gradient-stops"] = ( o, v ) => o with { Smoke = o.Smoke with { Stops = ParseValues( v, Color.Parse ) } },
            ["smoke:--final-gradient-steps"] = ( o, v ) => o with { Smoke = o.Smoke with { Steps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["smoke:--final-gradient-direction"] = ( o, v ) => o with { Smoke = o.Smoke with { GradientDirection = ParseGradient( v[0] ) } },
            ["fireworks:--explode-anywhere"] = ( o, v ) => o with { Fireworks = o.Fireworks with { Anywhere = true } },
            ["fireworks:--firework-colors"] = ( o, v ) => o with { Fireworks = o.Fireworks with { Colors = ParseValues( v, Color.Parse ) } },
            ["fireworks:--firework-symbol"] = ( o, v ) => o with { Fireworks = o.Fireworks with { Symbol = ParseSymbol( v[0] ) } },
            ["fireworks:--firework-volume"] = ( o, v ) => o with { Fireworks = o.Fireworks with { Volume = ParseRatio( v[0] ) } },
            ["fireworks:--launch-delay"] = ( o, v ) => o with { Fireworks = o.Fireworks with { Delay = ParseInteger( v[0], 0, "Argument must be an int >= 0." ) } },
            ["fireworks:--explode-distance"] = ( o, v ) => o with { Fireworks = o.Fireworks with { Distance = ParseRatio( v[0] ) } },
            ["fireworks:--final-gradient-stops"] = ( o, v ) => o with { Fireworks = o.Fireworks with { Stops = ParseValues( v, Color.Parse ) } },
            ["fireworks:--final-gradient-steps"] = ( o, v ) => o with { Fireworks = o.Fireworks with { Steps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["fireworks:--final-gradient-direction"] = ( o, v ) => o with { Fireworks = o.Fireworks with { GradientDirection = ParseGradient( v[0] ) } },
            ["orbittingvolley:--top-launcher-symbol"] = ( o, v ) => o with { OrbittingVolley = o.OrbittingVolley with { TopSymbol = ParseSymbol( v[0] ) } },
            ["orbittingvolley:--right-launcher-symbol"] = ( o, v ) => o with { OrbittingVolley = o.OrbittingVolley with { RightSymbol = ParseSymbol( v[0] ) } },
            ["orbittingvolley:--bottom-launcher-symbol"] = ( o, v ) => o with { OrbittingVolley = o.OrbittingVolley with { BottomSymbol = ParseSymbol( v[0] ) } },
            ["orbittingvolley:--left-launcher-symbol"] = ( o, v ) => o with { OrbittingVolley = o.OrbittingVolley with { LeftSymbol = ParseSymbol( v[0] ) } },
            ["orbittingvolley:--launcher-movement-speed"] = ( o, v ) => o with { OrbittingVolley = o.OrbittingVolley with { LauncherSpeed = ParseSpeed( v[0] ) } },
            ["orbittingvolley:--character-movement-speed"] = ( o, v ) => o with { OrbittingVolley = o.OrbittingVolley with { CharacterSpeed = ParseSpeed( v[0] ) } },
            ["orbittingvolley:--volley-size"] = ( o, v ) => o with { OrbittingVolley = o.OrbittingVolley with { Volume = ParseRatio( v[0] ) } },
            ["orbittingvolley:--launch-delay"] = ( o, v ) => o with { OrbittingVolley = o.OrbittingVolley with { Delay = ParseInteger( v[0], 0, "Argument must be an int >= 0." ) } },
            ["orbittingvolley:--character-easing"] = ( o, v ) => o with { OrbittingVolley = o.OrbittingVolley with { Ease = ParseEase( v[0] ) } },
            ["orbittingvolley:--final-gradient-stops"] = ( o, v ) => o with { OrbittingVolley = o.OrbittingVolley with { Stops = ParseValues( v, Color.Parse ) } },
            ["orbittingvolley:--final-gradient-steps"] = ( o, v ) => o with { OrbittingVolley = o.OrbittingVolley with { Steps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["orbittingvolley:--final-gradient-direction"] = ( o, v ) => o with { OrbittingVolley = o.OrbittingVolley with { GradientDirection = ParseGradient( v[0] ) } },
            ["crumble:--final-gradient-stops"] = ( o, v ) => o with { Crumble = o.Crumble with { Stops = ParseValues( v, Color.Parse ) } },
            ["crumble:--final-gradient-steps"] = ( o, v ) => o with { Crumble = o.Crumble with { Steps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["crumble:--final-gradient-direction"] = ( o, v ) => o with { Crumble = o.Crumble with { GradientDirection = ParseGradient( v[0] ) } },
            ["beams:--beam-row-symbols"] = ( o, v ) => o with { Beams = o.Beams with { RowSymbols = ParseValues( v, ParseSymbol ) } },
            ["beams:--beam-column-symbols"] = ( o, v ) => o with { Beams = o.Beams with { ColumnSymbols = ParseValues( v, ParseSymbol ) } },
            ["beams:--beam-delay"] = ( o, v ) => o with { Beams = o.Beams with { Delay = ParseInteger( v[0], 1, "Argument must be an int > 0." ) } },
            ["beams:--beam-row-speed-range"] = ( o, v ) => o with { Beams = o.Beams with { RowSpeed = ParseIntegerRange( v[0] ) } },
            ["beams:--beam-column-speed-range"] = ( o, v ) => o with { Beams = o.Beams with { ColumnSpeed = ParseIntegerRange( v[0] ) } },
            ["beams:--beam-gradient-stops"] = ( o, v ) => o with { Beams = o.Beams with { BeamColors = ParseValues( v, Color.Parse ) } },
            ["beams:--beam-gradient-steps"] = ( o, v ) => o with { Beams = o.Beams with { BeamSteps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["beams:--beam-gradient-frames"] = ( o, v ) => o with { Beams = o.Beams with { BeamFrames = ParseInteger( v[0], 1, "Argument must be an int > 0." ) } },
            ["beams:--final-gradient-frames"] = ( o, v ) => o with { Beams = o.Beams with { Frames = ParseInteger( v[0], 1, "Argument must be an int > 0." ) } },
            ["beams:--final-wipe-speed"] = ( o, v ) => o with { Beams = o.Beams with { WipeSpeed = ParseInteger( v[0], 1, "Argument must be an int > 0." ) } },
            ["beams:--final-gradient-stops"] = ( o, v ) => o with { Beams = o.Beams with { Stops = ParseValues( v, Color.Parse ) } },
            ["beams:--final-gradient-steps"] = ( o, v ) => o with { Beams = o.Beams with { Steps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["beams:--final-gradient-direction"] = ( o, v ) => o with { Beams = o.Beams with { GradientDirection = ParseGradient( v[0] ) } },
            ["unstable:--unstable-color"] = ( o, v ) => o with { Unstable = o.Unstable with { UnstableColor = Color.Parse( v[0] ) } },
            ["unstable:--explosion-ease"] = ( o, v ) => o with { Unstable = o.Unstable with { ExplosionEase = ParseEase( v[0] ) } },
            ["unstable:--explosion-speed"] = ( o, v ) => o with { Unstable = o.Unstable with { ExplosionSpeed = ParseSpeed( v[0] ) } },
            ["unstable:--reassembly-ease"] = ( o, v ) => o with { Unstable = o.Unstable with { ReassemblyEase = ParseEase( v[0] ) } },
            ["unstable:--reassembly-speed"] = ( o, v ) => o with { Unstable = o.Unstable with { ReassemblySpeed = ParseSpeed( v[0] ) } },
            ["unstable:--final-gradient-stops"] = ( o, v ) => o with { Unstable = o.Unstable with { Stops = ParseValues( v, Color.Parse ) } },
            ["unstable:--final-gradient-steps"] = ( o, v ) => o with { Unstable = o.Unstable with { Steps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["unstable:--final-gradient-direction"] = ( o, v ) => o with { Unstable = o.Unstable with { GradientDirection = ParseGradient( v[0] ) } },
            ["blackhole:--blackhole-color"] = ( o, v ) => o with { Blackhole = o.Blackhole with { HoleColor = Color.Parse( v[0] ) } },
            ["blackhole:--star-colors"] = ( o, v ) => o with { Blackhole = o.Blackhole with { StarColors = ParseValues( v, Color.Parse ) } },
            ["blackhole:--final-gradient-stops"] = ( o, v ) => o with { Blackhole = o.Blackhole with { Stops = ParseValues( v, Color.Parse ) } },
            ["blackhole:--final-gradient-steps"] = ( o, v ) => o with { Blackhole = o.Blackhole with { Steps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["blackhole:--final-gradient-direction"] = ( o, v ) => o with { Blackhole = o.Blackhole with { GradientDirection = ParseGradient( v[0] ) } },
            ["bubbles:--rainbow"] = ( o, v ) => o with { Bubbles = o.Bubbles with { Rainbow = true } },
            ["bubbles:--bubble-colors"] = ( o, v ) => o with { Bubbles = o.Bubbles with { Colors = ParseValues( v, Color.Parse ) } },
            ["bubbles:--pop-color"] = ( o, v ) => o with { Bubbles = o.Bubbles with { PopColor = Color.Parse( v[0] ) } },
            ["bubbles:--bubble-speed"] = ( o, v ) => o with { Bubbles = o.Bubbles with { Speed = ParseSpeed( v[0] ) } },
            ["bubbles:--bubble-delay"] = ( o, v ) => o with { Bubbles = o.Bubbles with { Delay = ParseInteger( v[0], 1, "Argument must be an int > 0." ) } },
            ["bubbles:--pop-condition"] = ( o, v ) => o with { Bubbles = o.Bubbles with { Condition = ParseChoice( v[0], "row", "bottom", "anywhere" ) } },
            ["bubbles:--movement-easing"] = ( o, v ) => o with { Bubbles = o.Bubbles with { Ease = ParseEase( v[0] ) } },
            ["bubbles:--final-gradient-stops"] = ( o, v ) => o with { Bubbles = o.Bubbles with { Stops = ParseValues( v, Color.Parse ) } },
            ["bubbles:--final-gradient-steps"] = ( o, v ) => o with { Bubbles = o.Bubbles with { Steps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["bubbles:--final-gradient-direction"] = ( o, v ) => o with { Bubbles = o.Bubbles with { GradientDirection = ParseGradient( v[0] ) } },
            ["swarm:--base-color"] = ( o, v ) => o with { Swarm = o.Swarm with { BaseColors = ParseValues( v, Color.Parse ) } },
            ["swarm:--flash-color"] = ( o, v ) => o with { Swarm = o.Swarm with { FlashColor = Color.Parse( v[0] ) } },
            ["swarm:--swarm-size"] = ( o, v ) => o with { Swarm = o.Swarm with { Size = ParseRatio( v[0] ) } },
            ["swarm:--swarm-coordination"] = ( o, v ) => o with { Swarm = o.Swarm with { Coordination = ParseRatio( v[0] ) } },
            ["swarm:--swarm-area-count-range"] = ( o, v ) => o with { Swarm = o.Swarm with { Areas = ParseIntegerRange( v[0] ) } },
            ["swarm:--final-gradient-stops"] = ( o, v ) => o with { Swarm = o.Swarm with { Stops = ParseValues( v, Color.Parse ) } },
            ["swarm:--final-gradient-steps"] = ( o, v ) => o with { Swarm = o.Swarm with { Steps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["swarm:--final-gradient-direction"] = ( o, v ) => o with { Swarm = o.Swarm with { GradientDirection = ParseGradient( v[0] ) } },
            ["synthgrid:--grid-gradient-stops"] = ( o, v ) => o with { SynthGrid = o.SynthGrid with { GridColors = ParseValues( v, Color.Parse ) } },
            ["synthgrid:--grid-gradient-steps"] = ( o, v ) => o with { SynthGrid = o.SynthGrid with { GridSteps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["synthgrid:--grid-gradient-direction"] = ( o, v ) => o with { SynthGrid = o.SynthGrid with { GridDirection = ParseGradient( v[0] ) } },
            ["synthgrid:--grid-row-symbol"] = ( o, v ) => o with { SynthGrid = o.SynthGrid with { RowSymbol = ParseSymbol( v[0] ) } },
            ["synthgrid:--grid-column-symbol"] = ( o, v ) => o with { SynthGrid = o.SynthGrid with { ColumnSymbol = ParseSymbol( v[0] ) } },
            ["synthgrid:--text-generation-symbols"] = ( o, v ) => o with { SynthGrid = o.SynthGrid with { Symbols = ParseValues( v, ParseSymbol ) } },
            ["synthgrid:--max-active-blocks"] = ( o, v ) => o with { SynthGrid = o.SynthGrid with { Maximum = ParsePositiveRatio( v[0] ) } },
            ["synthgrid:--text-gradient-stops"] = ( o, v ) => o with { SynthGrid = o.SynthGrid with { Stops = ParseValues( v, Color.Parse ) } },
            ["synthgrid:--text-gradient-steps"] = ( o, v ) => o with { SynthGrid = o.SynthGrid with { Steps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["synthgrid:--text-gradient-direction"] = ( o, v ) => o with { SynthGrid = o.SynthGrid with { GradientDirection = ParseGradient( v[0] ) } },
            ["laseretch:--etch-pattern"] = ( o, v ) => o with { LaserEtch = o.LaserEtch with { Algorithm = ParseEtchPattern( v[0] ) } },
            ["laseretch:--etch-speed"] = ( o, v ) => o with { LaserEtch = o.LaserEtch with { Speed = ParseInteger( v[0], 1, "Argument must be an int > 0." ) } },
            ["laseretch:--etch-delay"] = ( o, v ) => o with { LaserEtch = o.LaserEtch with { Delay = ParseInteger( v[0], 0, "Argument must be an int >= 0." ) } },
            ["laseretch:--cool-gradient-stops"] = ( o, v ) => o with { LaserEtch = o.LaserEtch with { CoolColors = ParseValues( v, Color.Parse ) } },
            ["laseretch:--laser-gradient-stops"] = ( o, v ) => o with { LaserEtch = o.LaserEtch with { LaserColors = ParseValues( v, Color.Parse ) } },
            ["laseretch:--spark-gradient-stops"] = ( o, v ) => o with { LaserEtch = o.LaserEtch with { SparkColors = ParseValues( v, Color.Parse ) } },
            ["laseretch:--spark-cooling-frames"] = ( o, v ) => o with { LaserEtch = o.LaserEtch with { CoolingFrames = ParseInteger( v[0], 1, "Argument must be an int > 0." ) } },
            ["laseretch:--final-gradient-frames"] = ( o, v ) => o with { LaserEtch = o.LaserEtch with { Frames = ParseInteger( v[0], 1, "Argument must be an int > 0." ) } },
            ["laseretch:--final-gradient-stops"] = ( o, v ) => o with { LaserEtch = o.LaserEtch with { Stops = ParseValues( v, Color.Parse ) } },
            ["laseretch:--final-gradient-steps"] = ( o, v ) => o with { LaserEtch = o.LaserEtch with { Steps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["laseretch:--final-gradient-direction"] = ( o, v ) => o with { LaserEtch = o.LaserEtch with { GradientDirection = ParseGradient( v[0] ) } },
            ["matrix:--highlight-color"] = ( o, v ) => o with { Matrix = o.Matrix with { HighlightColor = Color.Parse( v[0] ) } },
            ["matrix:--rain-color-gradient"] = ( o, v ) => o with { Matrix = o.Matrix with { RainColors = ParseValues( v, Color.Parse ) } },
            ["matrix:--rain-symbols"] = ( o, v ) => o with { Matrix = o.Matrix with { Symbols = ParseValues( v, ParseSymbol ) } },
            ["matrix:--rain-fall-delay-range"] = ( o, v ) => o with { Matrix = o.Matrix with { FallDelay = ParseIntegerRange( v[0] ) } },
            ["matrix:--rain-column-delay-range"] = ( o, v ) => o with { Matrix = o.Matrix with { ColumnDelay = ParseIntegerRange( v[0] ) } },
            ["matrix:--rain-time"] = ( o, v ) => o with { Matrix = o.Matrix with { RainTime = ParseInteger( v[0], 1, "Argument must be an int > 0." ) } },
            ["matrix:--symbol-swap-chance"] = ( o, v ) => o with { Matrix = o.Matrix with { SymbolSwapChance = ParseSpeed( v[0] ) } },
            ["matrix:--color-swap-chance"] = ( o, v ) => o with { Matrix = o.Matrix with { ColorSwapChance = ParseSpeed( v[0] ) } },
            ["matrix:--resolve-delay"] = ( o, v ) => o with { Matrix = o.Matrix with { ResolveDelay = ParseInteger( v[0], 1, "Argument must be an int > 0." ) } },
            ["matrix:--final-gradient-frames"] = ( o, v ) => o with { Matrix = o.Matrix with { Frames = ParseInteger( v[0], 1, "Argument must be an int > 0." ) } },
            ["matrix:--final-gradient-stops"] = ( o, v ) => o with { Matrix = o.Matrix with { Stops = ParseValues( v, Color.Parse ) } },
            ["matrix:--final-gradient-steps"] = ( o, v ) => o with { Matrix = o.Matrix with { Steps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["matrix:--final-gradient-direction"] = ( o, v ) => o with { Matrix = o.Matrix with { GradientDirection = ParseGradient( v[0] ) } },
            ["vhstape:--glitch-line-colors"] = ( o, v ) => o with { VhsTape = o.VhsTape with { LineColors = ParseValues( v, Color.Parse ) } },
            ["vhstape:--glitch-wave-colors"] = ( o, v ) => o with { VhsTape = o.VhsTape with { WaveColors = ParseValues( v, Color.Parse ) } },
            ["vhstape:--noise-colors"] = ( o, v ) => o with { VhsTape = o.VhsTape with { NoiseColors = ParseValues( v, Color.Parse ) } },
            ["vhstape:--glitch-line-chance"] = ( o, v ) => o with { VhsTape = o.VhsTape with { LineChance = ParseRatio( v[0] ) } },
            ["vhstape:--noise-chance"] = ( o, v ) => o with { VhsTape = o.VhsTape with { NoiseChance = ParseRatio( v[0] ) } },
            ["vhstape:--total-glitch-time"] = ( o, v ) => o with { VhsTape = o.VhsTape with { GlitchTime = ParseInteger( v[0], 1, "Argument must be an int > 0." ) } },
            ["vhstape:--final-gradient-stops"] = ( o, v ) => o with { VhsTape = o.VhsTape with { Stops = ParseValues( v, Color.Parse ) } },
            ["vhstape:--final-gradient-steps"] = ( o, v ) => o with { VhsTape = o.VhsTape with { Steps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["vhstape:--final-gradient-direction"] = ( o, v ) => o with { VhsTape = o.VhsTape with { GradientDirection = ParseGradient( v[0] ) } },
            ["thunderstorm:--lightning-color"] = ( o, v ) => o with { Thunderstorm = o.Thunderstorm with { LightningColor = Color.Parse( v[0] ) } },
            ["thunderstorm:--glowing-text-color"] = ( o, v ) => o with { Thunderstorm = o.Thunderstorm with { TextColor = Color.Parse( v[0] ) } },
            ["thunderstorm:--text-glow-time"] = ( o, v ) => o with { Thunderstorm = o.Thunderstorm with { TextGlowTime = ParseInteger( v[0], 1, "Argument must be an int > 0." ) } },
            ["thunderstorm:--raindrop-symbols"] = ( o, v ) => o with { Thunderstorm = o.Thunderstorm with { RainSymbols = ParseValues( v, ParseSymbol ) } },
            ["thunderstorm:--spark-symbols"] = ( o, v ) => o with { Thunderstorm = o.Thunderstorm with { SparkSymbols = ParseValues( v, ParseSymbol ) } },
            ["thunderstorm:--spark-glow-color"] = ( o, v ) => o with { Thunderstorm = o.Thunderstorm with { SparkColor = Color.Parse( v[0] ) } },
            ["thunderstorm:--spark-glow-time"] = ( o, v ) => o with { Thunderstorm = o.Thunderstorm with { SparkGlowTime = ParseInteger( v[0], 1, "Argument must be an int > 0." ) } },
            ["thunderstorm:--storm-time"] = ( o, v ) => o with { Thunderstorm = o.Thunderstorm with { StormTime = ParseInteger( v[0], 1, "Argument must be an int > 0." ) } },
            ["thunderstorm:--final-gradient-frames"] = ( o, v ) => o with { Thunderstorm = o.Thunderstorm with { Frames = ParseInteger( v[0], 1, "Argument must be an int > 0." ) } },
            ["thunderstorm:--final-gradient-stops"] = ( o, v ) => o with { Thunderstorm = o.Thunderstorm with { Stops = ParseValues( v, Color.Parse ) } },
            ["thunderstorm:--final-gradient-steps"] = ( o, v ) => o with { Thunderstorm = o.Thunderstorm with { Steps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["thunderstorm:--final-gradient-direction"] = ( o, v ) => o with { Thunderstorm = o.Thunderstorm with { GradientDirection = ParseGradient( v[0] ) } },
            ["rings:--ring-colors"] = ( o, v ) => o with { Rings = o.Rings with { RingColors = ParseValues( v, Color.Parse ) } },
            ["rings:--ring-gap"] = ( o, v ) => o with { Rings = o.Rings with { RingGap = ParseSpeed( v[0] ) } },
            ["rings:--spin-duration"] = ( o, v ) => o with { Rings = o.Rings with { SpinDuration = ParseInteger( v[0], 1, "Argument must be an int > 0." ) } },
            ["rings:--spin-speed"] = ( o, v ) => o with { Rings = o.Rings with { SpinSpeed = ParseFloatRange( v[0] ) } },
            ["rings:--disperse-duration"] = ( o, v ) => o with { Rings = o.Rings with { DisperseDuration = ParseInteger( v[0], 1, "Argument must be an int > 0." ) } },
            ["rings:--spin-disperse-cycles"] = ( o, v ) => o with { Rings = o.Rings with { Cycles = ParseInteger( v[0], 1, "Argument must be an int > 0." ) } },
            ["rings:--final-gradient-stops"] = ( o, v ) => o with { Rings = o.Rings with { Stops = ParseValues( v, Color.Parse ) } },
            ["rings:--final-gradient-steps"] = ( o, v ) => o with { Rings = o.Rings with { Steps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["rings:--final-gradient-direction"] = ( o, v ) => o with { Rings = o.Rings with { GradientDirection = ParseGradient( v[0] ) } },
            ["burn:--starting-color"] = ( o, v ) => o with { Burn = o.Burn with { StartingColor = Color.Parse( v[0] ) } },
            ["burn:--burn-colors"] = ( o, v ) => o with { Burn = o.Burn with { BurnColors = ParseValues( v, Color.Parse ) } },
            ["burn:--smoke-chance"] = ( o, v ) => o with { Burn = o.Burn with { SmokeChance = ParseRatio( v[0] ) } },
            ["burn:--final-gradient-stops"] = ( o, v ) => o with { Burn = o.Burn with { Stops = ParseValues( v, Color.Parse ) } },
            ["burn:--final-gradient-steps"] = ( o, v ) => o with { Burn = o.Burn with { Steps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["burn:--final-gradient-direction"] = ( o, v ) => o with { Burn = o.Burn with { GradientDirection = ParseGradient( v[0] ) } },
            ["binarypath:--binary-colors"] = ( o, v ) => o with { BinaryPath = o.BinaryPath with { BinaryColors = ParseValues( v, Color.Parse ) } },
            ["binarypath:--movement-speed"] = ( o, v ) => o with { BinaryPath = o.BinaryPath with { MovementSpeed = ParseSpeed( v[0] ) } },
            ["binarypath:--active-binary-groups"] = ( o, v ) => o with { BinaryPath = o.BinaryPath with { ActiveGroups = ParseRatio( v[0] ) } },
            ["binarypath:--final-gradient-stops"] = ( o, v ) => o with { BinaryPath = o.BinaryPath with { Stops = ParseValues( v, Color.Parse ) } },
            ["binarypath:--final-gradient-steps"] = ( o, v ) => o with { BinaryPath = o.BinaryPath with { Steps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["binarypath:--final-gradient-direction"] = ( o, v ) => o with { BinaryPath = o.BinaryPath with { GradientDirection = ParseGradient( v[0] ) } },
            ["spotlights:--beam-width-ratio"] = ( o, v ) => o with { Spotlights = o.Spotlights with { BeamWidthRatio = ParseSpeed( v[0] ) } },
            ["spotlights:--beam-falloff"] = ( o, v ) => o with { Spotlights = o.Spotlights with { BeamFalloff = ParseNonNegativeFloat( v[0] ) } },
            ["spotlights:--search-duration"] = ( o, v ) => o with { Spotlights = o.Spotlights with { SearchDuration = ParseInteger( v[0], 1, "Argument must be an int > 0." ) } },
            ["spotlights:--search-speed-range"] = ( o, v ) => o with { Spotlights = o.Spotlights with { SearchSpeedRange = ParseFloatRange( v[0] ) } },
            ["spotlights:--spotlight-count"] = ( o, v ) => o with { Spotlights = o.Spotlights with { SpotlightCount = ParseInteger( v[0], 1, "Argument must be an int > 0." ) } },
            ["spotlights:--final-gradient-stops"] = ( o, v ) => o with { Spotlights = o.Spotlights with { Stops = ParseValues( v, Color.Parse ) } },
            ["spotlights:--final-gradient-steps"] = ( o, v ) => o with { Spotlights = o.Spotlights with { Steps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["spotlights:--final-gradient-direction"] = ( o, v ) => o with { Spotlights = o.Spotlights with { GradientDirection = ParseGradient( v[0] ) } },
            ["decrypt:--typing-speed"] = ( o, v ) => o with { Decrypt = o.Decrypt with { TypingSpeed = ParseInteger( v[0], 1, "Argument must be an int > 0." ) } },
            ["decrypt:--ciphertext-colors"] = ( o, v ) => o with { Decrypt = o.Decrypt with { CipherColors = ParseValues( v, Color.Parse ) } },
            ["decrypt:--final-gradient-stops"] = ( o, v ) => o with { Decrypt = o.Decrypt with { Stops = ParseValues( v, Color.Parse ) } },
            ["decrypt:--final-gradient-steps"] = ( o, v ) => o with { Decrypt = o.Decrypt with { Steps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["decrypt:--final-gradient-direction"] = ( o, v ) => o with { Decrypt = o.Decrypt with { GradientDirection = ParseGradient( v[0] ) } },
            ["slide:--movement-speed"] = ( o, v ) => o with { Slide = o.Slide with { MovementSpeed = ParseSpeed( v[0] ) } },
            ["slide:--grouping"] = ( o, v ) => o with { Slide = o.Slide with { Grouping = ParseSlideGrouping( v[0] ) } },
            ["slide:--gap"] = ( o, v ) => o with { Slide = o.Slide with { Gap = ParseInteger( v[0], 0, "Argument must be an int >= 0." ) } },
            ["slide:--reverse-direction"] = ( o, v ) => o with { Slide = o.Slide with { ReverseDirection = true } },
            ["slide:--merge"] = ( o, v ) => o with { Slide = o.Slide with { Merge = true } },
            ["slide:--movement-easing"] = ( o, v ) => o with { Slide = o.Slide with { MovementEase = ParseEase( v[0] ) } },
            ["slide:--final-gradient-stops"] = ( o, v ) => o with { Slide = o.Slide with { Stops = ParseValues( v, Color.Parse ) } },
            ["slide:--final-gradient-steps"] = ( o, v ) => o with { Slide = o.Slide with { Steps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["slide:--final-gradient-frames"] = ( o, v ) => o with { Slide = o.Slide with { Frames = ParseInteger( v[0], long.MinValue, null ) } },
            ["slide:--final-gradient-direction"] = ( o, v ) => o with { Slide = o.Slide with { GradientDirection = ParseGradient( v[0] ) } },
            ["wipe:--wipe-direction"] = ( o, v ) => o with { Wipe = o.Wipe with { Direction = ParseGroup( v[0] ) } },
            ["wipe:--wipe-delay"] = ( o, v ) => o with { Wipe = o.Wipe with { Delay = ParseInteger( v[0], 0, "Argument must be an int >= 0." ) } },
            ["wipe:--wipe-ease"] = ( o, v ) => o with { Wipe = o.Wipe with { Ease = ParseEase( v[0] ) } },
            ["wipe:--final-gradient-stops"] = ( o, v ) => o with { Wipe = o.Wipe with { Stops = ParseValues( v, Color.Parse ) } },
            ["wipe:--final-gradient-steps"] = ( o, v ) => o with { Wipe = o.Wipe with { Steps = ParseValues( v, x => ParseInteger( x, 1, "Argument must be an int > 0." ) ) } },
            ["wipe:--final-gradient-frames"] = ( o, v ) => o with { Wipe = o.Wipe with { Frames = ParseInteger( v[0], long.MinValue, null ) } },
            ["wipe:--final-gradient-direction"] = ( o, v ) => o with { Wipe = o.Wipe with { GradientDirection = ParseGradient( v[0] ) } },
        };

    }

    private static Options Apply( Options options, OptionDescriptor spec, string[] values )
    {
        string key = $"{options.Effect}:{spec.Name}";
        if ( !BindingStore.Values.TryGetValue( key, out var assign ) )
        {
            throw new EngineException( $"No option binding registered for {key}." );
        }

        return assign( options, values );
    }

    private static T[] ParseValues<T>( string[] values, Func<string, T> parse )
    {
        var result = new T[values.Length];
        for ( int i = 0; i < values.Length; i++ )
        {
            try
            {
                result[i] = parse( values[i] );
            }
            catch ( ArgumentException error )
            {
                throw new InvalidOptionValueException( values[i], error.Message );
            }
        }

        return result;
    }

    private static bool ParseEtchPattern( string value )
    {
        if ( value == "algorithm" )
        {
            return true;
        }

        ParseGroup( value );
        return false;
    }

    private static (long Low, long High) ParseIntegerRange( string value )
    {
        int dash = value.IndexOf( '-' );
        if ( dash < 0 || !TryInteger( value[..dash], out long low ) || !TryInteger( value[(dash + 1)..], out long high ) )
        {
            throw new ArgumentException( $"invalid range: '{value}'" );
        }

        if ( low <= 0 || high <= 0 || low > high )
        {
            throw new ArgumentException( $"invalid range: '{value}'" );
        }

        return (low, high);
    }

    private static CharacterGroup ParseWaveDirection( string value )
    {
        if ( value is "column_left_to_right" or "column_right_to_left" or "row_top_to_bottom" or "row_bottom_to_top" or "center_to_outside" or "outside_to_center" )
        {
            return ParseGroup( value );
        }

        throw new ArgumentException( $"invalid wave direction: '{value}'" );
    }

    private static string ParseSliceDirection( string value )
    {
        if ( value is "vertical" or "horizontal" or "diagonal" )
        {
            return value;
        }

        string problem = value.Length == 0 ? "a value is required for '--slice-direction <SLICE_DIRECTION>' but none was supplied" : $"invalid value '{value}' for '--slice-direction <SLICE_DIRECTION>'";
        throw new UsageException( $"error: {problem}\n  [possible values: vertical, horizontal, diagonal]\n\nFor more information, try '--help'.\n" );
    }

    private static string ParseChoice( string value, params string[] choices )
    {
        if ( choices.Contains( value ) )
        {
            return value;
        }

        throw new ArgumentException( $"invalid choice: '{value}' (choose from '{string.Join( "', '", choices )}')" );
    }

    private static string ParseSymbol( string value )
    {
        if ( value.EnumerateRunes( ).Count( ) != 1 )
        {
            throw new ArgumentException( $"invalid symbol: '{value}' argument must be a single character" );
        }

        return value;
    }

    private static bool ParseHorizontal( string value ) => value switch
    {
        "horizontal" => true,
        "vertical" => false,
        _ => throw new ArgumentException( $"invalid choice: '{value}' (choose from 'vertical', 'horizontal')" )
    };

    private static ExistingColors ParseHandling( string value ) => value switch
    {
        "ignore" => ExistingColors.Ignore,
        "always" => ExistingColors.Always,
        "dynamic" => ExistingColors.Dynamic,
        _ => throw new ArgumentException( $"invalid choice: '{value}' (choose from 'always', 'dynamic', 'ignore')" )
    };

    private static string ParseShell( string value )
    {
        if ( value is "bash" or "zsh" )
        {
            return value;
        }

        string message = value.Length == 0 ?
            "error: a value is required for '--print-completion <SHELL>' but none was supplied" :
            $"error: invalid value '{value}' for '--print-completion <SHELL>'";
        message += "\n  [possible values: bash, zsh]\n\n";
        string? suggestion = new[] { "bash", "zsh" }.Select( shell => (Shell: shell, Score: Similarity( value, shell )) )
            .Where( item => item.Score > 0.7 ).OrderBy( item => item.Score ).Select( item => item.Shell ).LastOrDefault( );

        if ( suggestion is not null )
        {
            message += $"  tip: a similar value exists: '{suggestion}'\n\n";
        }

        throw new UsageException( message + "For more information, try '--help'.\n" );
    }

    // clap's suggestion threshold uses Jaro similarity over Unicode scalar values.
    private static double Similarity( string first, string second )
    {
        Rune[] a = first.EnumerateRunes( ).ToArray( ), b = second.EnumerateRunes( ).ToArray( );
        if ( a.Length == 0 || b.Length == 0 )
        {
            return 0;
        }

        int range = Math.Max( 0, Math.Max( a.Length, b.Length ) / 2 - 1 );
        var aMatched = new bool[a.Length];
        var bMatched = new bool[b.Length];
        int matches = 0;

        for ( int i = 0; i < a.Length; i++ )
        {
            for ( int j = Math.Max( 0, i - range ); j < Math.Min( b.Length, i + range + 1 ); j++ )
            {
                if ( !bMatched[j] && a[i] == b[j] )
                {
                    aMatched[i] = bMatched[j] = true;
                    matches++;
                    break;
                }
            }
        }

        if ( matches == 0 )
        {
            return 0;
        }

        int position = 0, transpositions = 0;
        for ( int i = 0; i < a.Length; i++ )
        {
            if ( !aMatched[i] )
            {
                continue;
            }

            while ( !bMatched[position] )
            {
                position++;
            }

            if ( a[i] != b[position++] )
            {
                transpositions++;
            }
        }

        return (( double ) matches / a.Length + ( double ) matches / b.Length + (matches - transpositions / 2.0) / matches) / 3;
    }
}
