using System.Runtime.InteropServices;
using System.Text;

namespace Ttfx.Cli;

internal static partial class UnixIo
{
    private const int Interrupted = 4;
    [LibraryImport( "libc", EntryPoint = "read", SetLastError = true )]
    private static unsafe partial nint Read( int fd, byte* buffer, nuint count );
    [LibraryImport( "libc", EntryPoint = "write", SetLastError = true )]
    private static unsafe partial nint Write( int fd, byte* buffer, nuint count );
    [LibraryImport( "libc", EntryPoint = "isatty" )]
    internal static partial int IsTty( int fd );
    [LibraryImport( "libc", EntryPoint = "signal" )]
    private static partial nint Signal( int signal, nint handler );
    [LibraryImport( "libc", EntryPoint = "strerror" )]
    private static partial nint StrError( int error );

    [LibraryImport( "libc", EntryPoint = "open", StringMarshalling = StringMarshalling.Utf8, SetLastError = true )]
    private static partial int Open( string path, int flags );
    [LibraryImport( "libc", EntryPoint = "close", SetLastError = true )]
    private static partial int Close( int fd );

    internal static byte[] ReadFile( string path )
    {
        int fd = Open( path, 0x80000 );
        if ( fd < 0 )
        {
            throw new UnixIOException( Marshal.GetLastPInvokeError( ) );
        }

        try
        {
            return ReadAll( fd );
        }
        finally { Close( fd ); }
    }

    internal static void RestoreSigPipe( ) => Signal( 13, 0 );
    internal static string Error( int error ) => $"{Marshal.PtrToStringUTF8( StrError( error ) )} (os error {error})";

    internal static unsafe byte[] ReadAll( int fd )
    {
        using var bytes = new MemoryStream( );
        byte[] buffer = new byte[8192];

        while ( true )
        {
            nint count;
            fixed ( byte* p = buffer )
            {
                count = Read( fd, p, ( nuint ) buffer.Length );
            }

            int error = Marshal.GetLastPInvokeError( );
            if ( count == 0 )
            {
                return bytes.ToArray( );
            }

            if ( count < 0 )
            {
                if ( error == Interrupted )
                {
                    continue;
                }

                throw new UnixIOException( error );
            }

            bytes.Write( buffer.AsSpan( 0, checked(( int ) count) ) );
        }
    }

    internal static unsafe void WriteAll( int fd, ReadOnlySpan<byte> bytes )
    {
        while ( !bytes.IsEmpty )
        {
            nint count;
            fixed ( byte* p = bytes )
            {
                count = Write( fd, p, ( nuint ) bytes.Length );
            }

            int error = Marshal.GetLastPInvokeError( );
            if ( count < 0 )
            {
                if ( error == Interrupted )
                {
                    continue;
                }

                throw new UnixIOException( error );
            }

            if ( count == 0 )
            {
                throw new IOException( "write returned zero bytes" );
            }

            bytes = bytes[checked(( int ) count)..];
        }
    }

    internal static void WriteText( int fd, string value ) => WriteAll( fd, Encoding.UTF8.GetBytes( value ) );
}

internal sealed class UnixIOException( int error ) : IOException( UnixIo.Error( error ) )
{
    internal int ErrorCode { get; } = error;
}
