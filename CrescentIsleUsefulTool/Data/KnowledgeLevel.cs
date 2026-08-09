using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace CrescentIsleUsefulTool.Data;

/// <summary>
/// Resolves the foray-specific level displayed on enemies in the Occult
/// Crescent. <see cref="ICharacter.Level"/> is the regular battle-job level
/// and is commonly 100 here, so it must not be used for route safety.
/// </summary>
public static class KnowledgeLevel
{
    public static unsafe byte? TryGet(ICharacter character)
    {
        if (character.Address == nint.Zero)
        {
            return null;
        }

        var level = ((BattleChara*)character.Address)->ForayInfo.Level;
        // Zero means that foray information has not been supplied for this
        // object. Treat it as unknown instead of falling back to job Lv.100,
        // otherwise harmless field enemies incorrectly block every route.
        return level > 0 ? level : null;
    }
}
