using System.Linq;
using Dalamud.Game.ClientState.Statuses;

namespace Ocelot.Extensions;

public static class StatusListEx
{
    public static bool HasStatus(this StatusList list, uint id)
    {
        return list.Any(status => status.StatusId == id);
    }
}
