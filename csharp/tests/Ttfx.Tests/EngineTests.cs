using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Ttfx.Core;

namespace Ttfx.Tests;

[TestClass]
public sealed class EngineTests
{
    private static Terminal Canvas( string text = "x", long width = 1 ) => new( text, new( )
    {
        CanvasWidth = width,
        CanvasHeight = 1,
        IgnoreTerminalDimensions = true,
        NoColor = true
    }, width, 1 );

    [TestMethod]
    public void CompletionActionsRunInlineAndCanAppendAnotherAction( )
    {
        var terminal = Canvas( );
        var engine = new Engine( terminal );
        engine.AddScene( 0, "first", new( [new( "a"u8.ToArray( ), 1 )] ) );
        engine.AddScene( 0, "second", new( [new( "b"u8.ToArray( ), 2 )] ) );
        var trace = new List<string>( );
        EventCaller caller = new( CallerKind.Scene, "first" );
        engine.Register( 0, EngineEvent.SceneActivated, new( CallerKind.Scene, "second" ), ( _, _ ) => trace.Add( "activated" ) );
        engine.Register( 0, EngineEvent.SceneComplete, caller, ( context, id ) =>
        {
            trace.Add( "complete" );
            context.ActivateScene( id, "second" );
            context.Register( id, EngineEvent.SceneComplete, caller, ( _, _ ) => trace.Add( "appended" ) );
        } );
        engine.ActivateScene( 0, "first" );
        engine.Activate( 0 );

        engine.Update( );

        CollectionAssert.AreEqual( new[] { "complete", "activated", "appended" }, trace );
        Assert.AreEqual( "b", Encoding.UTF8.GetString( terminal.VisualBytes[0] ) );
        Assert.IsTrue( engine.HasActive );
        engine.Update( );
        Assert.IsTrue( engine.HasActive );
        engine.Update( );
        Assert.IsFalse( engine.HasActive );
    }

    [TestMethod]
    public void SceneActivationResumesAndExplicitResetRestarts( )
    {
        var terminal = Canvas( );
        var engine = new Engine( terminal );
        engine.AddScene( 0, "resume", new( [new( "a"u8.ToArray( ), 2 ), new( "b"u8.ToArray( ), 2 )] ) );
        engine.ActivateScene( 0, "resume" );
        engine.Activate( 0 );
        engine.Update( );
        engine.DeactivateScene( 0 );
        engine.ActivateScene( 0, "resume" );
        engine.Update( );
        engine.Update( );

        Assert.AreEqual( "b", Encoding.UTF8.GetString( terminal.VisualBytes[0] ) );
        engine.ResetScene( 0, "resume" );
        engine.Update( );
        Assert.AreEqual( "a", Encoding.UTF8.GetString( terminal.VisualBytes[0] ) );
    }

    [TestMethod]
    public void NewActiveCharactersWaitForTheNextUpdateSnapshot( )
    {
        var terminal = Canvas( "ab", 2 );
        var engine = new Engine( terminal );
        engine.AddScene( 0, "first", new( [new( "a"u8.ToArray( ), 1 )] ) );
        engine.AddScene( 1, "next", new( [new( "b"u8.ToArray( ), 1 )] ) );
        var completed = new List<int>( );
        engine.Register( 0, EngineEvent.SceneComplete, new( CallerKind.Scene, "first" ), ( context, _ ) =>
        {
            completed.Add( 0 );
            context.ActivateScene( 1, "next" );
            context.Activate( 1 );
        } );
        engine.Register( 1, EngineEvent.SceneComplete, new( CallerKind.Scene, "next" ), ( _, id ) => completed.Add( id ) );
        engine.ActivateScene( 0, "first" );
        engine.Activate( 0 );

        engine.Update( );
        CollectionAssert.AreEqual( new[] { 0 }, completed );
        engine.Update( );
        CollectionAssert.AreEqual( new[] { 0, 1 }, completed );
    }

    [TestMethod]
    public void CachedRowsRepairLayerAndVisibilityChanges( )
    {
        var terminal = Canvas( "a" );
        int added = terminal.AddCharacter( "b", new( 1, 1 ) );
        terminal.Visible[0] = terminal.Visible[added] = true;
        terminal.Layers[0] = 2;
        Assert.AreEqual( "a", Encoding.UTF8.GetString( terminal.Render( ) ) );
        terminal.Visible[0] = false;
        Assert.AreEqual( "b", Encoding.UTF8.GetString( terminal.Render( ) ) );
        terminal.Visible[0] = true;
        terminal.Layers[added] = 3;
        Assert.AreEqual( "b", Encoding.UTF8.GetString( terminal.Render( ) ) );
        terminal.VisualBytes[added] = "c"u8.ToArray( );
        Assert.AreEqual( "c", Encoding.UTF8.GetString( terminal.Render( ) ) );
    }

    [TestMethod]
    public void CharactersAddedDuringCallbacksWaitAndRetainIdentityOrder( )
    {
        var terminal = Canvas( "ab", 2 );
        var engine = new Engine( terminal );
        var completed = new List<int>( );
        foreach ( int id in new[] { 1, 0 } )
        {
            engine.AddScene( id, "once", new( [new( "x"u8.ToArray( ), 1 )] ) );
            engine.Register( id, EngineEvent.SceneComplete, new( CallerKind.Scene, "once" ), ( context, character ) =>
            {
                completed.Add( character );
                if ( character == 0 )
                {
                    int first = context.AddCharacters( Enumerable.Repeat( "p", 70 ).ToArray( ), new( 1, 1 ) );
                    for ( int particle = first + 69; particle >= first; particle-- )
                    {
                        context.AddScene( particle, "once", new( [new( "p"u8.ToArray( ), 1 )] ) );
                        context.Register( particle, EngineEvent.SceneComplete, new( CallerKind.Scene, "once" ), ( _, done ) => completed.Add( done ) );
                        context.ActivateScene( particle, "once" );
                        context.Activate( particle );
                    }
                }
            } );
            engine.ActivateScene( id, "once" );
            engine.Activate( id );
        }

        engine.Update( );
        CollectionAssert.AreEqual( new[] { 0, 1 }, completed );
        Assert.AreEqual( 70, engine.ActiveCount );
        engine.Update( );
        CollectionAssert.AreEqual( Enumerable.Range( 0, 72 ).ToArray( ), completed );
        Assert.IsFalse( engine.HasActive );
    }

    [TestMethod]
    public void VirtualClockAdvancesByOneFrameEvenWithPacingDisabled( )
    {
        var clock = new EngineClock( true, 0 );
        for ( int frame = 0; frame < 120; frame++ )
        {
            clock.AdvanceFrame( );
        }

        Assert.AreEqual( 2.0, clock.Seconds, 1e-12 );
    }

    [TestMethod]
    public void PathsHoldBeforeCompletionAndTickMotionBeforeScenes( )
    {
        var terminal = Canvas( "x", 5 );
        var engine = new Engine( terminal );
        engine.AddPath( 0, "move", new( [new Waypoint( "end", new( 5, 1 ) )], 1, hold: 2 ) );
        engine.AddScene( 0, "landed", new( [new( "z"u8.ToArray( ), 1 )] ) );
        var trace = new List<string>( );
        engine.Register( 0, EngineEvent.PathHolding, new( CallerKind.Path, "move" ), ( _, _ ) => trace.Add( "hold" ) );
        engine.Register( 0, EngineEvent.PathComplete, new( CallerKind.Path, "move" ), ( context, id ) =>
        {
            trace.Add( "path" );
            context.ActivateScene( id, "landed" );
        } );
        engine.Register( 0, EngineEvent.SceneComplete, new( CallerKind.Scene, "landed" ), ( _, _ ) => trace.Add( "scene" ) );
        engine.ActivatePath( 0, "move" );
        engine.Activate( 0 );
        for ( int frame = 0; frame < 4; frame++ )
        {
            engine.Update( );
        }

        Assert.AreEqual( new Coord( 5, 1 ), terminal.Coordinates[0] );
        CollectionAssert.AreEqual( new[] { "hold" }, trace );
        engine.Update( );
        engine.Update( );
        CollectionAssert.AreEqual( new[] { "hold", "path", "scene" }, trace );
        Assert.IsFalse( engine.HasActive );
    }
    [TestMethod]
    public void RecycledParticlesDiscardOldScenesAndCallbacks( )
    {
        var terminal = Canvas( );
        var engine = new Engine( terminal );
        var pool = new ParticlePool( engine, new EngineRng( 1 ), ["p"], maximum: 1 );
        var trace = new List<int>( );
        ParticleReset reset = new( ) { ClearScenes = true, ClearEvents = true };
        int? original = null;
        for ( int emission = 0; emission < 8; emission++ )
        {
            int token = emission;
            int? id = pool.Emit( new( 1, 1 ), reset: reset, onEmit: ( context, character ) =>
            {
                context.AddScene( character, "once", new( [new( "p"u8.ToArray( ), 1 )] ) );
                context.Register( character, EngineEvent.SceneComplete, new( CallerKind.Scene, "once" ), ( _, done ) =>
                {
                    trace.Add( token );
                    pool.Reclaim( done );
                } );
                context.ActivateScene( character, "once" );
            } );
            Assert.IsNotNull( id );
            original ??= id;
            Assert.AreEqual( original, id );
            Assert.IsNull( pool.Acquire( ) );
            engine.Update( );
            Assert.IsFalse( engine.HasActive );
        }

        Assert.AreEqual( 1, pool.Count );
        CollectionAssert.AreEqual( Enumerable.Range( 0, 8 ).ToArray( ), trace );
    }

}
