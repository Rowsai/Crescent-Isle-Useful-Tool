using System.Numerics;

namespace CrescentIsleUsefulTool.Modules.Carrots;

public class Carrot(Vector3 position)
{
    public static Vector4 Color { get; } = new(0.2f, 0.8f, 0.2f, 1f);

    // The tracker creates a new managed snapshot every framework update.
    public bool IsValid() => true;

    public Vector3 GetPosition()
    {
        return position;
    }
}
