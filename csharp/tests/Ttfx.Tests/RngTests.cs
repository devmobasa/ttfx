using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ttfx.Core;

namespace Ttfx.Tests;

[TestClass]
public sealed class RngTests
{
    // Independently generated with the existing Python parity shim.
    [TestMethod]
    public void MatchesPythonRawDraws( )
    {
        ulong[] expected = [0xd0764d4f4476689f, 0x519e4174576f3791, 0xfbe07cfb0c24ed8c, 0xb37d9f600cd835b8,
            0xcb231c3874846a73, 0x968d9f004e50de7d, 0x201718ff221a3556, 0x9ae94e070ed8cb46];
        var rng = new EngineRng( 42 );

        foreach ( ulong value in expected )
        {
            Assert.AreEqual( value, rng.NextUInt64( ) );
        }
    }

    [TestMethod]
    public void MatchesPythonRejectionAndShuffle( )
    {
        long[] expected = [-3, -2, 2, 0, 0, 2, -1, -3, -3, -2, 2, -3, 0, -3, -2, -2];
        var rng = new EngineRng( 7 );

        foreach ( long value in expected )
        {
            Assert.AreEqual( value, rng.RandInt( -3, 3 ) );
        }

        int[] values = Enumerable.Range( 0, 12 ).ToArray( );
        new EngineRng( 1337 ).Shuffle<int>( values );
        CollectionAssert.AreEqual( new[] { 5, 11, 6, 0, 4, 1, 3, 8, 10, 9, 2, 7 }, values );
    }

    [TestMethod]
    public void StateTransferContinuesConsumedStream( )
    {
        var rng = new EngineRng( 42 );

        for ( int i = 0; i < 100; i++ )
        {
            _ = rng.NextUInt64( );
        }

        var (a, b, c, d) = rng.State;
        var transferred = new EngineRng( a, b, c, d );

        for ( int i = 0; i < 100; i++ )
        {
            Assert.AreEqual( rng.NextUInt64( ), transferred.NextUInt64( ) );
        }
    }
}
