using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ttfx.Core;

namespace Ttfx.Tests;

[TestClass]
public sealed class AllocationTests
{
    [TestMethod]
    public void FixedRendererAndWipeUpdateAllocateNothingAfterWarmup( )
    {
        var terminal = new Terminal( "ab\nc😀", new( ), 80, 24 );
        var wipe = new Wipe( terminal, new( )
        {
            Frames = 1_000_000
        } );
        // Grow buffers and complete lazy runtime initialization outside measurement.
        for ( int i = 0; i < 100; i++ )
        {
            _ = wipe.NextFrame( );
            _ = terminal.Render( ).Length;
        }

        long before = GC.GetAllocatedBytesForCurrentThread( );

        for ( int i = 0; i < 1000; i++ )
        {
            _ = wipe.NextFrame( );
            _ = terminal.Render( ).Length;
        }

        long after = GC.GetAllocatedBytesForCurrentThread( );
        Assert.AreEqual( 0L, after - before );
    }
}
