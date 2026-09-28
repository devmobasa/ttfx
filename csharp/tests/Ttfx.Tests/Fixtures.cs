namespace Ttfx.Tests;

internal static class Fixtures
{
    internal static string PathFor( string name )
    {
        var directory = new DirectoryInfo( AppContext.BaseDirectory );

        while ( directory is not null )
        {
            string candidate = Path.Combine( directory.FullName, "tests", "fixtures", name );
            if ( File.Exists( candidate ) )
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException( $"Root fixture {name} not found." );
    }
}
