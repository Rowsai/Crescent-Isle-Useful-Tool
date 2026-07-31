using FFXIVClientStructs.FFXIV.Client.Game;

namespace Ocelot.Gameplay;

public partial class Actions
{
    public readonly static Action AutoAttack = new(ActionType.GeneralAction, 1);

    public readonly static Action Jump = new(ActionType.GeneralAction, 2);

    public readonly static Action LimitBreak = new(ActionType.GeneralAction, 3);

    public readonly static Action Sprint = new(ActionType.GeneralAction, 4);

    public readonly static Action Desynthesis = new(ActionType.GeneralAction, 5);

    public readonly static Action Repair = new(ActionType.GeneralAction, 6);

    public readonly static Action Teleport = new(ActionType.GeneralAction, 7);

    public readonly static Action Return = new(ActionType.GeneralAction, 8);

    public readonly static Action MountRoulette = new(ActionType.GeneralAction, 9);

    public readonly static Action MinionRoulette = new(ActionType.GeneralAction, 10);

    public readonly static Action MateriaMelding = new(ActionType.GeneralAction, 12);

    public readonly static Action AdvancedMateriaMelding = new(ActionType.GeneralAction, 13);

    public readonly static Action MateriaExtraction = new(ActionType.GeneralAction, 14);

    public readonly static Action Dye = new(ActionType.GeneralAction, 15);

    public readonly static Action TargetForward = new(ActionType.GeneralAction, 16);

    public readonly static Action TargetBack = new(ActionType.GeneralAction, 17);

    public readonly static Action SetDown = new(ActionType.GeneralAction, 18);

    public readonly static Action Decipher = new(ActionType.GeneralAction, 19);

    public readonly static Action Dig = new(ActionType.GeneralAction, 20);

    public readonly static Action AetherialReduction = new(ActionType.GeneralAction, 21);

    public readonly static Action CastGlamour = new(ActionType.GeneralAction, 22);

    public readonly static Action Dismount = new(ActionType.GeneralAction, 23);

    public readonly static Action FlyingMountRoulette = new(ActionType.GeneralAction, 24);

    public readonly static Action GlamourPlate = new(ActionType.GeneralAction, 25);

    public readonly static Action DutyActionI = new(ActionType.GeneralAction, 26);

    public readonly static Action DutyActionII = new(ActionType.GeneralAction, 27);

    public readonly static Action PutAway = new(ActionType.GeneralAction, 28);

    public readonly static Action SortPetHotbar = new(ActionType.GeneralAction, 29);

    public readonly static Action PhantomActionI = new(ActionType.GeneralAction, 31);

    public readonly static Action PhantomActionII = new(ActionType.GeneralAction, 32);

    public readonly static Action PhantomActionIII = new(ActionType.GeneralAction, 33);

    public readonly static Action PhantomActionIV = new(ActionType.GeneralAction, 34);

    public readonly static Action PhantomActionV = new(ActionType.GeneralAction, 35);

    public readonly static Action AttackTarget = new(ActionType.GeneralAction, 36);

    public readonly static Action BindTarget = new(ActionType.GeneralAction, 37);

    public readonly static Action IgnoreTarget = new(ActionType.GeneralAction, 38);

    public readonly static Action SquareTarget = new(ActionType.GeneralAction, 39);

    public readonly static Action CircleTarget = new(ActionType.GeneralAction, 40);

    public readonly static Action PlusTarget = new(ActionType.GeneralAction, 41);

    public readonly static Action TriangleTarget = new(ActionType.GeneralAction, 42);
}
