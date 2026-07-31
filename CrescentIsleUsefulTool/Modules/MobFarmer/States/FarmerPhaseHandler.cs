using Ocelot.States;

namespace CrescentIsleUsefulTool.Modules.MobFarmer.States;

public abstract class FarmerPhaseHandler(MobFarmerModule module) : StateHandler<FarmerPhase, MobFarmerModule>(module);
