using Ocelot.States;

namespace CrescentIsleUsefulTool.Modules.StateManager.States;

[StateAttribute<State>(State.InCriticalEncounter)]
public class InCriticalEncounterHandler(StateManagerModule module) : BaseHandler(module)
{
    public override State? Handle()
    {
        if (!IsInCriticalEncounter())
        {
            // The CE has ended even if the player remains in combat for a few
            // frames. Staying in InCriticalEncounter suppresses the exit event
            // and leaves automation waiting forever.
            return IsInCombat() ? State.InCombat : State.Idle;
        }

        return null;
    }
}
