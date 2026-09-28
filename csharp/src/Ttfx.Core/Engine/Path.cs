using System.Globalization;

namespace Ttfx.Core;

public sealed record Waypoint( string Name, Coord Coordinate, Coord[]? Controls = null )
{
    internal EventCaller Caller { get; } = new( CallerKind.Waypoint, Name, Coordinate,
        Controls is null ? null : string.Join( ';', Controls.Select( c => $"{c.Column.ToString( CultureInfo.InvariantCulture )},{c.Row.ToString( CultureInfo.InvariantCulture )}" ) ) );
}

public sealed class PathDefinition
{
    public Waypoint[] Waypoints { get; }
    public double Speed { get; set; }
    public Easing? Ease { get; }
    public long? Layer { get; }
    public long Hold { get; set; }
    public bool Loop { get; }
    internal double[] Distances { get; }
    internal double Distance { get; }

    public PathDefinition( IEnumerable<Waypoint> waypoints, double speed, Easing? ease = null, long? layer = null, long hold = 0, bool loop = false )
    {
        if ( speed <= 0 )
        {
            throw new EngineException( $"Path speed must be greater than 0. Received: {Numbers.FormatFloat( speed )}" );
        }

        Waypoints = waypoints.ToArray( );
        var names = new HashSet<string>( StringComparer.Ordinal );
        foreach ( var waypoint in Waypoints )
        {
            if ( !names.Add( waypoint.Name ) )
            {
                throw new EngineException( $"duplicate waypoint id: {waypoint.Name}" );
            }
        }

        Speed = speed;
        Ease = ease;
        Layer = layer;
        Hold = hold;
        Loop = loop;
        Distances = new double[Waypoints.Length];
        for ( int i = 1; i < Waypoints.Length; i++ )
        {
            Distances[i] = SegmentLength( Waypoints[i - 1].Coordinate, Waypoints[i] );
            Distance += Distances[i];
        }
    }

    internal static double SegmentLength( Coord start, Waypoint end ) => end.Controls is { } controls ?
        Geometry.BezierLength( start, controls, end.Coordinate ) : Geometry.LineLength( start, end.Coordinate, true );
}

internal sealed class PathPlayback( int character, string name, PathDefinition definition )
{
    internal readonly int Character = character;
    internal readonly string Name = name;
    internal readonly PathDefinition Definition = definition;
    internal readonly bool[] Entered = new bool[definition.Waypoints.Length];
    internal readonly bool[] Exited = new bool[definition.Waypoints.Length];
    internal int NameId, Caller;
    internal int[] WaypointCallers = [];
    internal Coord Origin;
    internal double OriginDistance;
    internal bool HasOrigin;
    internal double Distance = definition.Distance;
    internal double Reached;
    internal long Step, MaxSteps, Holding;
}
