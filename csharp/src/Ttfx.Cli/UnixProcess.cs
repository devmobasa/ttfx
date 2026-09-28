using System.Runtime.InteropServices;

namespace Ttfx.Cli;

internal static partial class UnixProcess
{
    [StructLayout( LayoutKind.Sequential )]
    private struct WindowSize
    {
        internal ushort Rows, Columns, Xpixels, Ypixels;
    }
    [StructLayout( LayoutKind.Sequential )]
    private struct Timespec
    {
        internal long Seconds, Nanoseconds;
    }

    [LibraryImport( "libc", EntryPoint = "ioctl", SetLastError = true )]
    private static partial int GetWindowSize( int fd, nuint request, out WindowSize size );
    [LibraryImport( "libc", EntryPoint = "clock_gettime", SetLastError = true )]
    private static partial int ClockGetTime( int clock, out Timespec time );
    [LibraryImport( "libc", EntryPoint = "clock_nanosleep" )]
    private static partial int ClockNanoSleep( int clock, int flags, in Timespec time, out Timespec remaining );
    [LibraryImport( "libc", EntryPoint = "raise" )]
    internal static partial int Raise( int signal );
    [LibraryImport( "libc", EntryPoint = "signal" )]
    internal static partial nint Signal( int signal, nint handler );

    internal static (long Columns, long Rows) Dimensions( )
    {
        long columns = Options.TryInteger( Environment.GetEnvironmentVariable( "COLUMNS" ) ?? "", out long c ) ? c : 0;
        long rows = Options.TryInteger( Environment.GetEnvironmentVariable( "LINES" ) ?? "", out long r ) ? r : 0;
        bool hasColumns = Options.TryInteger( Environment.GetEnvironmentVariable( "COLUMNS" ) ?? "", out _ );
        bool hasRows = Options.TryInteger( Environment.GetEnvironmentVariable( "LINES" ) ?? "", out _ );
        if ( hasColumns && hasRows )
        {
            return (columns, rows);
        }

        foreach ( int fd in new[] { 1, 2, 0 } )
        {
            if ( UnixIo.IsTty( fd ) != 0 && GetWindowSize( fd, 0x5413, out var size ) == 0 && size.Rows > 0 && size.Columns > 0 )
            {
                return (hasColumns ? columns : size.Columns, hasRows ? rows : size.Rows);
            }
        }

        return (hasColumns ? columns : 80, hasRows ? rows : 24);
    }

    internal static double Now( )
    {
        if ( ClockGetTime( 1, out Timespec time ) != 0 )
        {
            throw new IOException( UnixIo.Error( Marshal.GetLastPInvokeError( ) ) );
        }

        return time.Seconds + time.Nanoseconds / 1_000_000_000.0;
    }

    internal static void Sleep( double seconds )
    {
        if ( seconds <= 0 )
        {
            return;
        }

        var duration = new Timespec { Seconds = ( long ) seconds, Nanoseconds = ( long ) ((seconds - ( long ) seconds) * 1_000_000_000) };

        while ( true )
        {
            int error = ClockNanoSleep( 1, 0, in duration, out Timespec remaining );
            if ( error == 0 )
            {
                return;
            }

            if ( error != 4 )
            {
                throw new IOException( UnixIo.Error( error ) );
            }

            duration = remaining;
        }
    }
}

internal sealed class Signals : IDisposable
{
    private readonly List<PosixSignalRegistration> registrations = [];
    private int interrupted, terminated, resized;
    internal bool Interrupted => Volatile.Read( ref interrupted ) != 0;
    internal bool Terminated => Volatile.Read( ref terminated ) != 0;
    internal bool TakeResize( ) => Interlocked.Exchange( ref resized, 0 ) != 0;

    internal Signals( bool tty )
    {
        if ( !OperatingSystem.IsLinux( ) )
        {
            throw new PlatformNotSupportedException( "Linux is required." );
        }

        registrations.Add( PosixSignalRegistration.Create( PosixSignal.SIGINT, context =>
        {
            context.Cancel = true;
            Volatile.Write( ref interrupted, 1 );
        } ) );
        if ( tty )
        {
            registrations.Add( PosixSignalRegistration.Create( PosixSignal.SIGTERM, context =>
            {
                context.Cancel = true;
                Volatile.Write( ref terminated, 1 );
            } ) );
            registrations.Add( PosixSignalRegistration.Create( PosixSignal.SIGWINCH, context =>
            {
                context.Cancel = true;
                Volatile.Write( ref resized, 1 );
            } ) );
        }
    }

    public void Dispose( )
    {
        foreach ( var registration in registrations )
        {
            registration.Dispose( );
        }
    }
}
