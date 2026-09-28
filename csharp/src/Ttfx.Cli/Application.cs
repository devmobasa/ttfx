using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Ttfx.Core;

namespace Ttfx.Cli;

internal static class Application
{
    internal static int Run( string[] args )
    {
        Options options;

        try
        {
            options = Options.Parse( args );
        }
        catch ( UsageException error )
        {
            UnixIo.WriteText( 2, error.Message );

            return 2;
        }

        if ( options.Version )
        {
            UnixIo.WriteText( 1, "ttfx 0.4.0\n" );

            return 0;
        }

        if ( options.Help )
        {
            UnixIo.WriteText( 1, CommandRegistry.Help( options.Effect ) );

            return 0;
        }

        if ( options.Completion is { } shell )
        {
            UnixIo.WriteText( 1, CommandRegistry.Completion( shell ) );

            return 0;
        }

        options = options with { Terminal = options.Terminal with { UseVirtualClock = options.VirtualClock || options.ParityDump } };

        byte[] data;

        try
        {
            data = options.InputFile is { } path ? UnixIo.ReadFile( path ) : UnixIo.IsTty( 0 ) == 0 ? UnixIo.ReadAll( 0 ) : [];
        }
        catch ( IOException error )
        {
            if ( options.InputFile is not null )
            {
                UnixIo.WriteText( 1, $"Error reading input file: {error.Message}\n" );
            }
            else
            {
                UnixIo.WriteText( 1, "NO INPUT.\n" );
            }

            return 1;
        }

        string input;

        try
        {
            input = Decode( data );
        }
        catch ( EngineException error )
        {
            UnixIo.WriteText( 1, $"{(options.InputFile is null ? "Error decoding input" : "Error reading input file")}: {error.Message}\n" );

            return 1;
        }

        if ( string.IsNullOrWhiteSpace( input ) )
        {
            UnixIo.WriteText( 1, "NO INPUT.\n" );

            return 1;
        }

        if ( !options.M0Dump && !options.RandomEffect && options.Effect is null )
        {
            UnixIo.WriteText( 2, "Error: No effect specified.\n" );

            return 1;
        }

        try
        {
            var dimensions = UnixProcess.Dimensions( );
            var terminal = new Terminal( input, options.Terminal, dimensions.Columns, dimensions.Rows );
            double lastPrinted = UnixProcess.Now( );
            if ( options.M0Dump )
            {
                Array.Fill( terminal.Visible, true );
                UnixIo.WriteAll( 1, terminal.Render( ) );
                UnixIo.WriteAll( 1, "\n"u8 );

                return 0;
            }

            var rng = new EngineRng( options.Seed ?? BitConverter.ToUInt64( RandomNumberGenerator.GetBytes( 8 ) ) );
            options = CommandRegistry.SelectEffect( options, rng );
            EffectDescriptor descriptor = CommandRegistry.FindEffect( options.Effect! )!;
            bool tty = !options.ParityDump && UnixIo.IsTty( 1 ) != 0;
            using Signals? signals = options.ParityDump ? null : new Signals( tty );

            while ( true )
            {
                IEffect effect = descriptor.Create( terminal, options, rng );
                bool resized = Stream( terminal, effect, options, signals, tty, lastPrinted );
                if ( !resized )
                {
                    break;
                }

                dimensions = UnixProcess.Dimensions( );
                terminal = new Terminal( input, options.Terminal with
                {
                    ReuseCanvas = false
                }, dimensions.Columns, dimensions.Rows );
                lastPrinted = UnixProcess.Now( );
            }

            if ( signals?.Terminated == true )
            {
                signals.Dispose( );
                UnixProcess.Signal( 15, 0 );
                UnixProcess.Raise( 15 );
            }

            return signals?.Interrupted == true ? 1 : 0;
        }
        catch ( UnsupportedAnsiException error )
        {
            UnixIo.WriteText( 2, $"Error: Unsupported ANSI sequence in input data: {DebugString( error.Sequence )}\n" );

            return 1;
        }
        catch ( EngineException error )
        {
            UnixIo.WriteText( 2, $"Error: {error.Message}\n" );

            return 1;
        }
        catch ( ArgumentException error )
        {
            UnixIo.WriteText( 2, $"Error: {error.Message}\n" );

            return 1;
        }
        catch ( UnixIOException error ) when ( !options.ParityDump && UnixIo.IsTty( 1 ) != 0 && error.ErrorCode is 5 or 32 )
        {
            return 0;
        }
        catch ( IOException error )
        {
            UnixIo.WriteText( 2, $"Error: io error: {error.Message}\n" );

            return 1;
        }
    }

    private static bool Stream( Terminal terminal, IEffect effect, Options options, Signals? signals, bool tty, double lastFrame )
    {
        ulong frames = 0;
        using var writer = new UnixFrameWriter( );
        string move = $"\u001b8\u001b7\u001b[{Math.Max( 0, terminal.VisibleTop )}A";
        byte[] moveBytes = Encoding.UTF8.GetBytes( move );
        double? resizeAt = null;
        bool resized = false;
        if ( !options.ParityDump )
        {
            UnixIo.WriteAll( 1, "\u001b[?25l"u8 );
            if ( terminal.Config.ReuseCanvas )
            {
                UnixIo.WriteAll( 1, moveBytes );
            }

            byte[] blank = Encoding.UTF8.GetBytes( new string( ' ', checked(( int ) Math.Max( 0, terminal.VisibleRight )) ) + "\n" );

            for ( long r = 0; r < terminal.VisibleTop; r++ )
            {
                UnixIo.WriteAll( 1, blank );
            }

            UnixIo.WriteAll( 1, "\u001b7"u8 );
        }

        try
        {
            while ( true )
            {
                if ( signals?.Interrupted == true || signals?.Terminated == true )
                {
                    break;
                }

                if ( tty && signals?.TakeResize( ) == true )
                {
                    resizeAt = UnixProcess.Now( );
                }

                if ( resizeAt is { } time && UnixProcess.Now( ) - time >= 0.05 )
                {
                    resizeAt = null;
                    if ( !terminal.Config.IgnoreTerminalDimensions )
                    {
                        var dimensions = UnixProcess.Dimensions( );
                        if ( terminal.LayoutChanged( dimensions.Columns, dimensions.Rows ) )
                        {
                            resized = true;
                            break;
                        }
                    }
                }

                if ( !effect.NextFrame( ) )
                {
                    break;
                }

                if ( !options.ParityDump && !options.VirtualClock && terminal.Config.FrameRate > 0 )
                {
                    double delay = 1.0 / terminal.Config.FrameRate - (UnixProcess.Now( ) - lastFrame);
                    UnixProcess.Sleep( delay );
                    lastFrame = UnixProcess.Now( );
                }

                if ( signals?.Interrupted == true || signals?.Terminated == true )
                {
                    break;
                }

                terminal.Clock.AdvanceFrame( );
                ReadOnlyMemory<byte>[] rows = terminal.RenderRows( );
                if ( options.ParityDump )
                {
                    UnixIo.WriteText( 1, terminal.RenderedLength.ToString( CultureInfo.InvariantCulture ) + "\n" );
                    writer.Write( 1, rows );
                    UnixIo.WriteAll( 1, "\n"u8 );
                }
                else
                {
                    writer.Write( 1, rows, moveBytes );
                }

                frames++;
                if ( options.ParityDump && options.MaxFrames is { } max && frames >= max )
                {
                    break;
                }
            }
        }
        finally
        {
            if ( !options.ParityDump )
            {
                if ( resized )
                {
                    UnixIo.WriteAll( 1, "\u001b8"u8 );
                    if ( terminal.VisibleTop > 0 )
                    {
                        UnixIo.WriteText( 1, $"\u001b[{terminal.VisibleTop}A" );
                    }

                    UnixIo.WriteAll( 1, "\u001b[0J"u8 );
                }
                else
                {
                    if ( !terminal.Config.NoRestoreCursor )
                    {
                        UnixIo.WriteAll( 1, "\u001b[?25h"u8 );
                    }

                    if ( !terminal.Config.NoEol )
                    {
                        UnixIo.WriteAll( 1, "\n"u8 );
                    }
                }
            }
        }

        if ( options.ParityDump )
        {
            UnixIo.WriteText( 2, $"frames={frames}\n" );
        }

        return resized;
    }

    private static string Decode( byte[] data )
    {
        int offset = 0;

        while ( offset < data.Length )
        {
            OperationStatus status = Rune.DecodeFromUtf8( data.AsSpan( offset ), out _, out int consumed );
            if ( status == OperationStatus.NeedMoreData )
            {
                throw new EngineException( $"incomplete utf-8 byte sequence from index {offset}" );
            }

            if ( status == OperationStatus.InvalidData )
            {
                int length = 1;
                byte lead = data[offset];
                int expected = lead is >= 0xc2 and <= 0xdf ? 2 : lead is >= 0xe0 and <= 0xef ? 3 : lead is >= 0xf0 and <= 0xf4 ? 4 : 1;

                for ( int i = 1; i < expected && offset + i < data.Length; i++ )
                {
                    byte next = data[offset + i];
                    if ( next is < 0x80 or > 0xbf || i == 1 && (lead == 0xe0 && next < 0xa0 || lead == 0xed && next >= 0xa0 || lead == 0xf0 && next < 0x90 || lead == 0xf4 && next >= 0x90) )
                    {
                        break;
                    }

                    length++;
                }

                throw new EngineException( $"invalid utf-8 sequence of {length} bytes from index {offset}" );
            }

            offset += consumed;
        }

        return Encoding.UTF8.GetString( data );
    }

    private static string DebugString( string value )
    {
        var result = new StringBuilder( "\"" );

        foreach ( Rune rune in value.EnumerateRunes( ) )
        {
            result.Append( rune.Value switch
            {
                27 => "\\u{1b}",
                7 => "\\u{7}",
                0 => "\\0",
                9 => "\\t",
                10 => "\\n",
                13 => "\\r",
                34 => "\\\"",
                92 => "\\\\",
                _ => rune.ToString( )
            } );
        }

        return result.Append( '"' ).ToString( );
    }
}
