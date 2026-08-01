using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrescentIsleUsefulTool.Pathfinding;

public class PathfinderStepConverter : JsonConverter<PathfinderStep>
{
    public override PathfinderStep? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }

    public override void Write(Utf8JsonWriter writer, PathfinderStep value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        writer.WriteString("Type", value.Type.ToString());

        switch (value.Type)
        {
            case PathfinderStepType.WalkToNode:
                writer.WriteNumber("NodeId", value.NodeId);
                break;
            case PathfinderStepType.TeleportToAethernet:
            case PathfinderStepType.WalkToAethernet:
                writer.WriteString("Aethernet", value.Aethernet.ToString());
                break;
            case PathfinderStepType.RideTurbulence:
                WriteVector(writer, "Position", value.Position);
                WriteVector(writer, "ArrivalPosition", value.ArrivalPosition);
                break;
        }

        writer.WriteEndObject();
    }

    private static void WriteVector(Utf8JsonWriter writer, string name, System.Numerics.Vector3 value)
    {
        writer.WriteStartObject(name);
        writer.WriteNumber("X", value.X);
        writer.WriteNumber("Y", value.Y);
        writer.WriteNumber("Z", value.Z);
        writer.WriteEndObject();
    }
}
