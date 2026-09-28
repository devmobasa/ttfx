using System.Buffers.Binary;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ttfx.Core;

namespace Ttfx.Tests;

[TestClass]
public sealed class EasingTests
{
    [TestMethod]
    public void SharedEasingGoldens( )
    {
        byte[] data = File.ReadAllBytes( Fixtures.PathFor( "easing_goldens.bin" ) );
        var easings = Enumerable.Range( 0, 31 ).Select( i => new Easing( ( EaseKind ) i ) ).ToList( );
        easings.AddRange( [
            new(EaseKind.CubicBezier, 0.25, 0.1, 0.25, 1),
            new(EaseKind.CubicBezier, 0.42, 0, 0.58, 1),
            new(EaseKind.CubicBezier, 0.68, -0.55, 0.265, 1.55)
        ] );
        Assert.HasCount( easings.Count * 1001 * 8, data );
        int offset = 0;

        foreach ( Easing easing in easings )
        {
            for ( int i = 0; i <= 1000; i++, offset += 8 )
            {
                ulong expected = BinaryPrimitives.ReadUInt64LittleEndian( data.AsSpan( offset, 8 ) );
                ulong actual = BitConverter.DoubleToUInt64Bits( easing.Apply( i / 1000.0 ) );
                ulong difference = actual > expected ? actual - expected : expected - actual;
                Assert.IsLessThanOrEqualTo( easing.Kind == EaseKind.CubicBezier ? 1UL : 0UL, difference,
                    $"{easing.Kind} sample {i}: {expected:x16} != {actual:x16}" );
            }
        }
    }

    [TestMethod]
    [DataRow( "IN_SINE", true )]
    [DataRow( "in_bacK", true )]
    [DataRow( "İn_sine", false )]
    [DataRow( "in_sıne", false )]
    [DataRow( " in_sine", false )]
    public void ParserUsesReferenceCaseRules( string name, bool accepted ) =>
        Assert.AreEqual( accepted, Easing.TryParse( name, out _ ) );
}
