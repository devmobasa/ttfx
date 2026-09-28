using System.Diagnostics;
using System.Text.Json;
using Ttfx.Core;

if ( args.Length != 1 )
{
    return 2;
}

string input = string.Join( '\n', Enumerable.Repeat( new string( 'x', 190 ), 46 ) );
var terminal = new Terminal( input, new( ) { CanvasWidth = 200, CanvasHeight = 50, IgnoreTerminalDimensions = true }, 200, 50 );
var wipe = new Wipe( terminal, new( ) { Frames = 1_000_000 } );
for ( int warmup = 0; warmup < 500; warmup++ )
{
    wipe.NextFrame( );
    terminal.Render( );
}

long allocatedBefore = GC.GetAllocatedBytesForCurrentThread( );
long started = Stopwatch.GetTimestamp( );
int outputBytes = 0;
for ( int iteration = 0; iteration < 1000; iteration++ )
{
    wipe.NextFrame( );
    outputBytes = terminal.Render( ).Length;
}

TimeSpan elapsed = Stopwatch.GetElapsedTime( started );
long allocated = GC.GetAllocatedBytesForCurrentThread( ) - allocatedBefore;
using var file = new FileStream( args[0], FileMode.Create, FileAccess.Write );
using var output = new Utf8JsonWriter( file, new( ) { Indented = true } );
output.WriteStartObject( );
output.WriteString( "kernel", "fixed-state wipe update plus full-frame render" );
output.WriteNumber( "iterations", 1000 );
output.WriteNumber( "milliseconds", elapsed.TotalMilliseconds );
output.WriteNumber( "allocated_bytes", allocated );
output.WriteNumber( "bytes_per_frame", outputBytes );
output.WriteEndObject( );
output.Flush( );
return 0;
