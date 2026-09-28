namespace Ttfx.Core;

public sealed record MatrixConfig
{
    public Color HighlightColor { get; init; } = Color.FromHex( "dbffdb" );
    public Color[] RainColors { get; init; } = [Color.FromHex( "92be92" ), Color.FromHex( "185318" )];
    public string[] Symbols { get; init; } = ["2", "5", "9", "8", "Z", "*", ")", ":", ".", "\"", "=", "+", "-", "¦", "|", "_", "ｦ", "ｱ", "ｳ", "ｴ", "ｵ", "ｶ", "ｷ", "ｹ", "ｺ", "ｻ", "ｼ", "ｽ", "ｾ", "ｿ", "ﾀ", "ﾂ", "ﾃ", "ﾅ", "ﾆ", "ﾇ", "ﾈ", "ﾊ", "ﾋ", "ﾎ", "ﾏ", "ﾐ", "ﾑ", "ﾒ", "ﾓ", "ﾔ", "ﾕ", "ﾗ", "ﾘ", "ﾜ"];
    public (long Min, long Max) FallDelay { get; init; } = (2, 15);
    public (long Min, long Max) ColumnDelay { get; init; } = (3, 9);
    public long RainTime { get; init; } = 15;
    public double SymbolSwapChance { get; init; } = .005;
    public double ColorSwapChance { get; init; } = .001;
    public long ResolveDelay { get; init; } = 3;
    public long Frames { get; init; } = 3;
    public Color[] Stops { get; init; } = [Color.FromHex( "92be92" ), Color.FromHex( "336b33" )];
    public long[] Steps { get; init; } = [12];
    public GradientDirection GradientDirection { get; init; } = GradientDirection.Radial;
}

public sealed class Matrix : EffectBase
{
    private sealed class Column( int[] characters )
    {
        internal readonly int[] Characters = characters;
        internal readonly OrderedQueue<int> Pending = [], Visible = [];
        internal bool Filling, Full;
        internal double DropChance = .08;
        internal long Delay, BaseDelay, Hold;
        internal int Length;
    }

    private readonly MatrixConfig config;
    private readonly ulong symbolThreshold, colorThreshold;
    private readonly OrderedQueue<Column> pending = [];
    private readonly List<Column> active = [], full = [];
    private readonly string[] symbols;
    private readonly Color?[] colors;
    private readonly Color[] rainColors;
    private readonly double rainStart;
    private long columnDelay, resolveDelay;
    private int phase;
    private bool rainComplete, finalFrameShown;

    public Matrix( Terminal terminal, MatrixConfig config, EngineRng rng ) : base( terminal, rng )
    {
        this.config = config;
        symbolThreshold = EngineRng.ChanceThreshold( config.SymbolSwapChance );
        colorThreshold = EngineRng.ChanceThreshold( config.ColorSwapChance );
        resolveDelay = config.ResolveDelay;
        rainColors = new Gradient( config.RainColors, [6] ).Spectrum;
        symbols = terminal.Characters.Take( terminal.Count ).Select( cell => cell.Symbol ).ToArray( );
        colors = terminal.Characters.Take( terminal.Count ).Select( cell => cell.Foreground ).ToArray( );
        var final = new Gradient( config.Stops, config.Steps );
        foreach ( int id in InputOrder )
        {
            Engine.AddScene( id, "resolve", FadeToFinal( id, final, config.GradientDirection, config.HighlightColor, 8, config.Frames ) );
        }

        foreach ( int[] group in terminal.Groups( CharacterGroup.ColumnLeftToRight, true ) )
        {
            var column = new Column( group.Reverse( ).ToArray( ) );
            Setup( column, false );
            pending.Add( column );
        }

        rng.Shuffle( pending.AsSpan( ) );
        rainStart = Engine.Clock.Seconds;
    }

    private void Paint( int id, string symbol, Color? color )
    {
        symbols[id] = symbol;
        colors[id] = Terminal.Config.ExistingColors == ExistingColors.Always && id < Terminal.InputCount ? Terminal.Characters[id].Foreground : color;
        Appearance( id, color, symbol: symbol );
    }

    private void Setup( Column column, bool filling )
    {
        column.Filling = filling;
        column.Pending.Clear( );
        column.Pending.AddRange( column.Characters );
        column.Visible.Clear( );
        foreach ( int id in column.Characters )
        {
            Terminal.Visible[id] = false;
            Terminal.Coordinates[id] = Home[id];
        }

        column.BaseDelay = filling ? Rng.RandInt( Math.Max( config.FallDelay.Min / 3, 1 ), Math.Max( config.FallDelay.Max / 3, 1 ) ) : Rng.RandInt( config.FallDelay.Min, config.FallDelay.Max );
        column.Delay = 0;
        column.Length = filling ? column.Characters.Length : ( int ) Rng.RandInt( Math.Max( 1, ( long ) (column.Characters.Length * .1) ), column.Characters.Length );
        column.Hold = column.Length == column.Characters.Length ? Rng.RandInt( 20, 45 ) : 0;
    }

    private void Trim( Column column )
    {
        if ( column.Visible.Count == 0 )
        {
            return;
        }

        Terminal.Visible[column.Visible[0]] = false;
        column.Visible.RemoveAt( 0 );
        if ( column.Visible.Count > 1 )
        {
            int tail = Math.Max( rainColors.Length - 3, 0 );
            Color darker = Color.AdjustBrightness( rainColors[tail + Rng.ChoiceIndex( rainColors.Length - tail )], .65 );
            int id = column.Visible[0];
            Paint( id, symbols[id], darker );
        }
    }

    private void Tick( Column column )
    {
        if ( column.Delay == 0 )
        {
            if ( column.Pending.Count > 0 )
            {
                int id = column.Pending[0];
                column.Pending.RemoveAt( 0 );
                Paint( id, config.Symbols[Rng.ChoiceIndex( config.Symbols.Length )], config.HighlightColor );
                if ( column.Visible.Count > 0 )
                {
                    int previous = column.Visible[^1];
                    Paint( previous, symbols[previous], rainColors[Rng.ChoiceIndex( rainColors.Length )] );
                }

                Terminal.Visible[id] = true;
                column.Visible.Add( id );
            }
            else if ( column.Visible.Count > 0 )
            {
                int last = column.Visible[^1];
                if ( colors[last] == config.HighlightColor )
                {
                    Paint( last, symbols[last], rainColors[Rng.ChoiceIndex( rainColors.Length )] );
                }

                if ( column.Hold != 0 )
                {
                    column.Hold--;
                }
                else if ( !column.Filling )
                {
                    if ( Rng.Random( ) < column.DropChance )
                    {
                        foreach ( int id in column.Visible )
                        {
                            Coord coord = Terminal.Coordinates[id];
                            Terminal.Coordinates[id] = new( coord.Column, coord.Row - 1 );
                            if ( coord.Row - 1 < 1 )
                            {
                                Terminal.Visible[id] = false;
                            }
                        }

                        column.Visible.RemoveAll( id => Terminal.Coordinates[id].Row < 1 );
                    }

                    Trim( column );
                }
            }

            if ( column.Visible.Count > column.Length )
            {
                Trim( column );
            }

            column.Delay = column.BaseDelay;
        }
        else
        {
            column.Delay--;
        }

        foreach ( int id in column.Visible )
        {
            string? nextSymbol = Rng.Chance( symbolThreshold ) ? config.Symbols[Rng.ChoiceIndex( config.Symbols.Length )] : null;
            Color? nextColor = Rng.Chance( colorThreshold ) ? rainColors[Rng.ChoiceIndex( rainColors.Length )] : null;
            if ( (nextSymbol is not null && nextSymbol != symbols[id]) || (nextColor is not null && nextColor != colors[id]) )
            {
                Paint( id, nextSymbol ?? symbols[id], nextColor ?? colors[id] );
            }
        }
    }

    public override bool NextFrame( )
    {
        if ( phase < 2 )
        {
            if ( columnDelay == 0 )
            {
                long count = phase == 0 ? Rng.RandInt( 1, 3 ) : pending.Count;
                for ( long i = 0; i < count && pending.Count > 0; i++ )
                {
                    active.Add( pending[0] );
                    pending.RemoveAt( 0 );
                }

                columnDelay = phase == 0 ? Rng.RandInt( config.ColumnDelay.Min, config.ColumnDelay.Max ) : 1;
            }
            else
            {
                columnDelay--;
            }

            foreach ( Column column in active )
            {
                Tick( column );
                if ( column.Pending.Count == 0 )
                {
                    if ( column.Filling && !column.Full )
                    {
                        full.Add( column );
                        column.Full = true;
                    }
                    else if ( column.Visible.Count == 0 )
                    {
                        Setup( column, phase == 1 );
                        pending.Add( column );
                    }
                }
            }

            active.RemoveAll( column => column.Visible.Count == 0 );
            if ( phase == 1 && pending.Count == 0 && active.All( column => column.Pending.Count == 0 && column.Filling ) )
            {
                phase = 2;
                active.Clear( );
            }

            if ( phase == 0 && config.RainTime > 0 && Engine.Clock.Seconds - rainStart > config.RainTime )
            {
                rainComplete = true;
                phase = 1;
                foreach ( Column column in active )
                {
                    column.Hold = 0;
                    column.DropChance = 1;
                }

                foreach ( Column column in pending )
                {
                    Setup( column, true );
                }
            }
        }
        else
        {
            foreach ( Column column in full )
            {
                Tick( column );
                if ( column.Visible.Count == 0 )
                {
                    continue;
                }

                if ( resolveDelay == 0 )
                {
                    long count = Rng.RandInt( 1, 4 );
                    for ( long i = 0; i < count && column.Visible.Count > 0; i++ )
                    {
                        int index = Rng.ChoiceIndex( column.Visible.Count ), id = column.Visible[index];
                        column.Visible.RemoveAt( index );
                        InputCell cell = Terminal.Characters[id];
                        if ( cell.Symbol != " " || cell.Foreground is not null || cell.Background is not null )
                        {
                            Engine.ActivateScene( id, "resolve" );
                            Engine.Activate( id );
                        }
                        else
                        {
                            Terminal.Visible[id] = false;
                        }
                    }

                    resolveDelay = config.ResolveDelay;
                }
                else
                {
                    resolveDelay--;
                }
            }

            full.RemoveAll( column => column.Visible.Count == 0 );
        }

        if ( full.Count > 0 || active.Count > 0 || Engine.HasActive || pending.Count > 0 || !rainComplete )
        {
            Engine.Update( );
            return true;
        }

        if ( !finalFrameShown )
        {
            finalFrameShown = true;
            Engine.Update( );
            return true;
        }

        return false;
    }
}
