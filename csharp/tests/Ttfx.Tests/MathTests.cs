using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ttfx.Core;

namespace Ttfx.Tests;

[TestClass]
public sealed class MathTests
{
    [TestMethod]
    public void RustNonFiniteAndSignedZeroSemantics( )
    {
        Assert.AreEqual( 1.0, RustMath.Min( double.NaN, 1 ) );
        Assert.AreEqual( 1.0, RustMath.Max( 1, double.NaN ) );
        Assert.AreEqual( long.MinValue, RustMath.RoundHalfEven( double.PositiveInfinity ) );
        Assert.AreEqual( long.MinValue, RustMath.RoundHalfEven( double.NegativeInfinity ) );
        Assert.AreEqual( 0L, RustMath.RoundHalfEven( double.NaN ) );
        Assert.AreEqual( long.MinValue, BitConverter.DoubleToInt64Bits( RustMath.Min( -0.0, 0.0 ) ) );
        Assert.AreEqual( 0L, BitConverter.DoubleToInt64Bits( RustMath.Max( -0.0, 0.0 ) ) );
        Assert.AreEqual( double.PositiveInfinity, RustMath.PowHalf( double.NegativeInfinity ) );
        Assert.IsTrue( double.IsNaN( RustMath.PowHalf( -1 ) ) );
    }

    [TestMethod]
    [DataRow( 7L, -2L, -4L, -1L )]
    [DataRow( -7L, 2L, -4L, 1L )]
    [DataRow( -7L, -2L, 3L, -1L )]
    public void DivisionAndModuloMatchPython( long a, long b, long quotient, long remainder )
    {
        Assert.AreEqual( quotient, RustMath.FloorDiv( a, b ) );
        Assert.AreEqual( remainder, RustMath.Mod( a, b ) );
    }
}
