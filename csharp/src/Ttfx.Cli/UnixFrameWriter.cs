using System.Runtime.InteropServices;

namespace Ttfx.Cli;

internal sealed partial class UnixFrameWriter : IDisposable
{
    [StructLayout( LayoutKind.Sequential )]
    private unsafe struct IoVector
    {
        internal byte* Base;
        internal nuint Length;
    }

    [LibraryImport( "libc", EntryPoint = "writev", SetLastError = true )]
    private static unsafe partial nint WriteVectors( int fd, IoVector* vectors, int count );

    private byte[]?[] buffers = [];
    private GCHandle[] pins = [];
    private IoVector[] vectors = [];

    internal unsafe void Write( int fd, ReadOnlyMemory<byte>[] rows, byte[]? prefix = null )
    {
        int prefixCount = prefix is null ? 0 : 1;
        int count = rows.Length + prefixCount;
        if ( count != vectors.Length )
        {
            Dispose( );
            buffers = new byte[count][];
            pins = new GCHandle[count];
            vectors = new IoVector[count];
        }

        for ( int index = 0; index < count; index++ )
        {
            if ( !MemoryMarshal.TryGetArray( index < prefixCount ? prefix.AsMemory( ) : rows[index - prefixCount], out ArraySegment<byte> row ) )
            {
                throw new InvalidOperationException( "Terminal rows must use array-backed memory." );
            }

            if ( !ReferenceEquals( buffers[index], row.Array ) )
            {
                if ( pins[index].IsAllocated )
                {
                    pins[index].Free( );
                }

                buffers[index] = row.Array;
                pins[index] = GCHandle.Alloc( row.Array, GCHandleType.Pinned );
            }

            vectors[index].Base = ( byte* ) pins[index].AddrOfPinnedObject( ) + row.Offset;
            vectors[index].Length = ( nuint ) row.Count;
        }

        fixed ( IoVector* first = vectors )
        {
            int index = 0;
            while ( index < vectors.Length )
            {
                while ( index < vectors.Length && vectors[index].Length == 0 )
                {
                    index++;
                }

                if ( index == vectors.Length )
                {
                    break;
                }

                // Linux and macOS accept 1024 iovecs; taller frames are batched.
                nint written = WriteVectors( fd, first + index, Math.Min( 1024, vectors.Length - index ) );
                if ( written < 0 )
                {
                    int error = Marshal.GetLastPInvokeError( );
                    if ( error == 4 )
                    {
                        continue;
                    }

                    throw new UnixIOException( error );
                }

                if ( written == 0 )
                {
                    throw new IOException( "writev returned zero bytes" );
                }

                nuint remaining = ( nuint ) written;
                while ( index < vectors.Length && remaining >= vectors[index].Length )
                {
                    remaining -= vectors[index].Length;
                    index++;
                }

                if ( remaining != 0 )
                {
                    vectors[index].Base += remaining;
                    vectors[index].Length -= remaining;
                }
            }
        }
    }

    public void Dispose( )
    {
        for ( int index = 0; index < pins.Length; index++ )
        {
            if ( pins[index].IsAllocated )
            {
                pins[index].Free( );
            }
        }
    }
}
