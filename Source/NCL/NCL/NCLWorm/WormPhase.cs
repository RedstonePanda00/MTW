// Decompiled with JetBrains decompiler
// Type: NCLWorm.WormPhase
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using Verse;

#nullable disable
namespace NCLWorm
{
  public abstract class WormPhase : IExposable
  {
    protected TC_WormDecisionController brain;
    protected CompWormCoordinator coordinator;

    public virtual float? DesiredStiffness => new float?();

    public bool IsFinished { get; protected set; } = false;

    public virtual void OnEnter(TC_WormDecisionController _brain)
    {
      this.brain = _brain;
      this.coordinator = this.brain.Coordinator;
      this.IsFinished = false;
    }

    public virtual void OnExit()
    {
    }

    public virtual string GetStateString() => "Base";

    public virtual void Update()
    {
    }

    public abstract MovementIntent GetMovementIntent();

    public abstract WeaponIntent GetWeaponIntent();

    public virtual void UpdateSegmentBehavior(WormBody seg, int index, int totalCount)
    {
      WormActions.SetVentState(seg, 0.0f);
      WormActions.OrderCeaseFire(seg);
    }

    public virtual void ExposeData()
    {
      bool isFinished = this.IsFinished;
      Scribe_Values.Look<bool>(ref isFinished, "IsFinished", false, false);
      this.IsFinished = isFinished;
    }

    protected void Finish() => this.IsFinished = true;
  }
}
