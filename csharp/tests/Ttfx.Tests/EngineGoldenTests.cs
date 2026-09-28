using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ttfx.Core;

namespace Ttfx.Tests;

[TestClass]
public sealed class EngineGoldenTests
{
    [TestMethod]
    public void EasedSyncedLoopingBezierPathsMatchPython( )
    {
        using var fixture = JsonDocument.Parse( File.ReadAllText( Fixtures.PathFor( "engine.json" ) ) );
        foreach ( var test in fixture.RootElement.GetProperty( "cases" ).EnumerateArray( ) )
        {
            var terminal = new Terminal( "x", new( ) { NoColor = true }, 8, 5 );
            var engine = new Engine( terminal, createFill: false );
            string? name = test.GetProperty( "ease" ).GetString( );
            Easing? ease = null;
            if ( name is not null )
            {
                Assert.IsTrue( Easing.TryParse( name, out Easing parsed ) );
                ease = parsed;
            }

            SyncMetric? sync = test.GetProperty( "sync" ).GetString( ) switch
            {
                "STEP" => SyncMetric.Step,
                "DISTANCE" => SyncMetric.Distance,
                _ => null
            };
            engine.AddScene( 0, "scene", new( [new( "A"u8.ToArray( ), 2 ), new( "B"u8.ToArray( ), 3 ), new( "C"u8.ToArray( ), 1 )], test.GetProperty( "loop" ).GetBoolean( ), ease, sync ) );
            engine.AddPath( 0, "path", new( [new Waypoint( "one", new( 6, 3 ), [new( 2, 4 ), new( 4, 0 )] ), new Waypoint( "two", new( 1, 2 ) )], .8, ease, hold: 2 ) );
            engine.ActivateScene( 0, "scene" );
            engine.ActivatePath( 0, "path" );
            int frame = 0;
            foreach ( var expected in test.GetProperty( "frames" ).EnumerateArray( ) )
            {
                engine.Tick( 0 );
                string context = $"ease={name}, sync={sync}, loop={test.GetProperty( "loop" )}, frame={frame++}";
                Assert.AreEqual( new Coord( expected[0].GetInt64( ), expected[1].GetInt64( ) ), terminal.Coordinates[0], context );
                Assert.AreEqual( expected[2].GetString( ), Encoding.UTF8.GetString( terminal.VisualBytes[0] ), context );
                Assert.AreEqual( expected[3].GetBoolean( ), engine.IsActive( 0 ), context );
                Assert.AreEqual( expected[4].GetBoolean( ), engine.PathIsActive( 0 ), context );
                Assert.AreEqual( expected[5].GetBoolean( ), engine.SceneIsActive( 0 ), context );
            }
        }
    }
}
