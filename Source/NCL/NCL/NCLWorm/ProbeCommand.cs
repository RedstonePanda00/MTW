// Decompiled with JetBrains decompiler
// Type: NCLWorm.ProbeCommand
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  public class ProbeCommand : IExposable
  {
    public ProbeCommandType Type;
    public Vector3 TargetPos;
    public Thing TargetThing;

    public bool IsValid => this.Type != 0;

    public static ProbeCommand Move(Vector3 pos)
    {
      return new ProbeCommand()
      {
        Type = ProbeCommandType.MoveToPosition,
        TargetPos = pos
      };
    }

    public static ProbeCommand Attack(Thing target)
    {
      return new ProbeCommand()
      {
        Type = ProbeCommandType.FocusFire,
        TargetThing = target
      };
    }

    public static ProbeCommand Suicide(Thing target)
    {
      return new ProbeCommand()
      {
        Type = ProbeCommandType.SelfDestruct,
        TargetThing = target
      };
    }

    public void ExposeData()
    {
      Scribe_Values.Look<ProbeCommandType>(ref this.Type, "Type", ProbeCommandType.None, false);
      Scribe_Values.Look<Vector3>(ref this.TargetPos, "TargetPos", new Vector3(), false);
      Scribe_References.Look<Thing>(ref this.TargetThing, "TargetThing", false);
    }
  }
}
