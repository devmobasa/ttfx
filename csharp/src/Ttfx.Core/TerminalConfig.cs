namespace Ttfx.Core;

public enum Anchor
{
    N, Ne, E, Se, S, Sw, W, Nw, C
}
public enum ExistingColors
{
    Ignore, Always, Dynamic
}
public enum CharacterGroup
{
    ColumnLeftToRight, ColumnRightToLeft, RowTopToBottom, RowBottomToTop,
    DiagonalBottomLeftToTopRight, DiagonalTopRightToBottomLeft,
    DiagonalTopLeftToBottomRight, DiagonalBottomRightToTopLeft, CenterToOutside, OutsideToCenter
}

public sealed record TerminalConfig
{
    public bool UseVirtualClock { get; init; }
    public long TabWidth { get; init; } = 4;
    public bool XtermColors { get; init; }
    public bool NoColor { get; init; }
    public Color Background { get; init; } = Color.FromHex( "000000" );
    public ExistingColors ExistingColors { get; init; }
    public bool WrapText { get; init; }
    public long FrameRate { get; init; } = 60;
    public long CanvasWidth { get; init; } = -1;
    public long CanvasHeight { get; init; } = -1;
    public Anchor AnchorCanvas { get; init; } = Anchor.Sw;
    public Anchor AnchorText { get; init; } = Anchor.Sw;
    public bool IgnoreTerminalDimensions { get; init; }
    public bool ReuseCanvas { get; init; }
    public bool NoEol { get; init; }
    public bool NoRestoreCursor { get; init; }
}

public sealed class EngineException( string message ) : Exception( message );
public sealed class UnsupportedAnsiException( string sequence ) : Exception
{
    public string Sequence { get; } = sequence;
}
