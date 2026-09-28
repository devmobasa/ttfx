namespace Ttfx.Core;

public sealed partial class Engine
{
    private readonly SlotStore<PathPlayback> paths = new( );
    private readonly Dictionary<(int Character, int Name), int> pathSlots = [];
    private int[] activePath;
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<PathDefinition, int[]> waypointCallers = new( );
    private readonly Dictionary<(long X, long Y), double> lineLengths = [];

    private double OriginLength( Coord start, Waypoint end )
    {
        if ( end.Controls is not null )
        {
            return PathDefinition.SegmentLength( start, end );
        }

        var key = (unchecked(end.Coordinate.Column - start.Column), unchecked(end.Coordinate.Row - start.Row));
        if ( !lineLengths.TryGetValue( key, out double length ) )
        {
            length = Geometry.LineLength( start, end.Coordinate, true );
            if ( lineLengths.Count == 8192 )
            {
                lineLengths.Clear( );
            }

            lineLengths.Add( key, length );
        }

        return length;
    }

    public void AddPath( int character, string name, PathDefinition definition )
    {
        var key = (character, NameId( name ));
        if ( pathSlots.ContainsKey( key ) )
        {
            throw new EngineException( $"duplicate path id: {name}" );
        }

        if ( !waypointCallers.TryGetValue( definition, out int[]? ids ) )
        {
            ids = new int[definition.Waypoints.Length];
            for ( int i = 0; i < ids.Length; i++ )
            {
                ids[i] = CallerId( definition.Waypoints[i].Caller );
            }

            waypointCallers.Add( definition, ids );
        }

        int slot = paths.Add( new( character, name, definition )
        {
            NameId = key.Item2,
            Caller = CallerId( new( CallerKind.Path, name ) ),
            WaypointCallers = ids
        } );
        pathSlots.Add( key, slot );
        (ownedPaths[character] ??= []).Add( slot );
    }

    public string? ActivePathName( int character ) => activePath[character] < 0 ? null : paths[activePath[character]].Name;
    public PathDefinition GetPath( int character, string name ) => paths[pathSlots[(character, NameId( name ))]].Definition;

    public void RemovePath( int character, string name )
    {
        var key = (character, NameId( name ));
        if ( !pathSlots.Remove( key, out int slot ) )
        {
            return;
        }

        if ( activePath[character] == slot )
        {
            activePath[character] = -1;
        }

        ownedPaths[character]?.Remove( slot );
        paths.Remove( slot );
    }

    public void ActivatePath( int character, string name )
    {
        int slot = pathSlots[(character, NameId( name ))];
        var path = paths[slot];
        if ( path.Definition.Waypoints.Length == 0 )
        {
            throw new EngineException( $"Path {name} has no waypoints." );
        }

        double distance = OriginLength( Terminal.Coordinates[character], path.Definition.Waypoints[0] );
        activePath[character] = slot;
        path.Origin = Terminal.Coordinates[character];
        path.Distance += distance;
        if ( path.HasOrigin )
        {
            path.Distance -= path.OriginDistance;
        }

        path.OriginDistance = distance;
        path.HasOrigin = true;
        path.Step = 0;
        path.Holding = path.Definition.Hold;
        path.MaxSteps = RustMath.RoundHalfEven( path.Distance / path.Definition.Speed );
        Array.Clear( path.Entered );
        Array.Clear( path.Exited );
        if ( path.Definition.Layer is { } layer )
        {
            Terminal.Layers[character] = layer;
        }

        Emit( character, EngineEvent.PathActivated, path.Caller );
    }

    public void DeactivatePath( int character, string? name = null )
    {
        int slot = activePath[character];
        if ( slot >= 0 && (name is null || paths[slot].Name == name) )
        {
            activePath[character] = -1;
        }
    }

    public bool PathIsActive( int character ) => activePath[character] >= 0;
    public void SetLayer( int character, long layer ) => Terminal.Layers[character] = layer;
    public void SetCoordinate( int character, Coord coordinate ) => Terminal.Coordinates[character] = coordinate;

    private void Move( int character )
    {
        int slot = activePath[character];
        if ( slot < 0 )
        {
            return;
        }

        var path = paths[slot];
        Terminal.Coordinates[character] = StepPath( path );
        slot = activePath[character];
        if ( slot < 0 )
        {
            throw new EngineException( "Active path cleared during movement." );
        }

        path = paths[slot];
        if ( path.Step != path.MaxSteps )
        {
            return;
        }

        if ( path.Definition.Hold != 0 && path.Holding == path.Definition.Hold )
        {
            Emit( character, EngineEvent.PathHolding, path.Caller );
            paths[pathSlots[(character, path.NameId)]].Holding--;

            return;
        }

        if ( path.Holding != 0 )
        {
            path.Holding--;

            return;
        }

        activePath[character] = -1;
        if ( path.Definition.Loop && path.Definition.Waypoints.Length > 1 )
        {
            ActivatePath( character, path.Name );
        }
        else
        {
            Emit( character, EngineEvent.PathComplete, path.Caller );
        }
    }

    private Coord StepPath( PathPlayback path )
    {
        if ( path.MaxSteps == 0 || path.Step >= path.MaxSteps || path.Distance == 0 )
        {
            return path.Definition.Waypoints[^1].Coordinate;
        }

        path.Step++;
        double ratio = ( double ) path.Step / path.MaxSteps;
        double travel = (path.Definition.Ease is { } ease ? ApplyEase( ease, ratio ) : ratio) * path.Distance;
        path.Reached = travel;
        int selected = -1;
        for ( int i = 0; i < path.Definition.Waypoints.Length; i++ )
        {
            double distance = i == 0 ? path.OriginDistance : path.Definition.Distances[i];
            bool entered = path.Entered[i];
            bool exited = path.Exited[i];
            if ( travel <= distance )
            {
                selected = i;
                if ( !entered )
                {
                    path.Entered[i] = true;
                    if ( Emit( path.Character, EngineEvent.SegmentEntered, path.WaypointCallers[i] ) )
                    {
                        path = paths[pathSlots[(path.Character, path.NameId)]];
                    }
                }

                break;
            }

            travel -= distance;
            int caller = path.WaypointCallers[i];
            if ( !entered )
            {
                path.Entered[i] = true;
                if ( Emit( path.Character, EngineEvent.SegmentEntered, caller ) )
                {
                    path = paths[pathSlots[(path.Character, path.NameId)]];
                }
            }

            if ( !exited )
            {
                path.Exited[i] = true;
                if ( Emit( path.Character, EngineEvent.SegmentExited, caller ) )
                {
                    path = paths[pathSlots[(path.Character, path.NameId)]];
                }
            }
        }

        if ( selected < 0 )
        {
            selected = path.Definition.Waypoints.Length - 1;
            travel += selected == 0 ? path.OriginDistance : path.Definition.Distances[selected];
        }

        double segmentDistance = selected == 0 ? path.OriginDistance : path.Definition.Distances[selected];
        double factor = segmentDistance == 0 ? 0 : travel / segmentDistance;
        if ( path.Definition.Ease is null )
        {
            factor = RustMath.Min( factor, 1 );
        }

        Coord start = selected == 0 ? path.Origin : path.Definition.Waypoints[selected - 1].Coordinate;
        Waypoint end = path.Definition.Waypoints[selected];

        return end.Controls is { } controls ? Geometry.OnBezier( start, controls, end.Coordinate, factor ) : Geometry.OnLine( start, end.Coordinate, factor );
    }
}
