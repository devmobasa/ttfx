using System.Runtime.CompilerServices;
using Ttfx.Cli;

UnixIo.RestoreSigPipe( );
if ( Environment.GetEnvironmentVariable( "TTFX_CSHARP_SHOW_BUILD" ) == "1" )
{
    UnixIo.WriteText( 2, $"ttfx: C# engine {(RuntimeFeature.IsDynamicCodeSupported ? "managed" : "AOT")} {BuildIdentity.Tier} SDK {BuildIdentity.Sdk} revision {BuildIdentity.Revision}\n" );
}

return Application.Run( args );
