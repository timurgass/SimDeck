namespace SimDeck.Core;

public sealed record TruckGpsNode(string Uid,float X,float Z,float DistanceToEnd,float SecondsToEnd);
public sealed record TruckGpsWaypoint(string Uid,float X,float Z,float Y=0);
