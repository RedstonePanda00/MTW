// Decompiled with JetBrains decompiler
// Type: NCLWorm.WormProbe
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using UnityEngine;
using Verse;

#nullable disable
namespace NCLWorm
{
  public class WormProbe : WormThingBase
  {
    public CompProbeBrain Brain => this.GetComp<CompProbeBrain>();

    public virtual void SpawnSetup(Map map, bool respawningAfterLoad)
    {
      base.SpawnSetup(map, respawningAfterLoad);
      if (!respawningAfterLoad && (this.ExactPosition == Vector3.zero))
      {
        IntVec3 position = ((Thing) this).Position;
        this.SetPhysicsState(position.ToVector3Shifted(), Vector3.zero);
      }
      map.GetComponent<WormBossMapComponent>()?.RegisterProbe(this);
    }

    public virtual void Destroy(DestroyMode mode = 0)
    {
      ((Thing) this).Map?.GetComponent<WormBossMapComponent>()?.DeregisterProbe(this);
      base.Destroy(mode);
    }

    protected override void DrawAt(Vector3 drawLoc, bool flip = false)
    {
      base.DrawAt(drawLoc, flip);
    }
  }
}
