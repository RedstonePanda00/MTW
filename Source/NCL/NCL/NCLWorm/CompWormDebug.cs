// Decompiled with JetBrains decompiler
// Type: NCLWorm.CompWormDebug
// Assembly: NCL_WormBoss, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B627DDDD-3AC9-4D6A-89F6-EA72F95FB570
// Assembly location: C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3555799437\1.6\Assemblies\NCL_WormBoss (3).dll

using RimWorld;
using System.Text;
using Verse;
using Verse.AI;

#nullable disable
namespace NCLWorm
{
  public class CompWormDebug : ThingComp
  {
    public virtual string CompInspectStringExtra()
    {
      if (!Prefs.DevMode)
        return (string) null;
      StringBuilder stringBuilder = new StringBuilder();
      stringBuilder.AppendLine("=== WORM BOSS DEBUG ===");
      stringBuilder.AppendLine("Faction: " + (((Thing) this.parent).Faction?.Name ?? "NULL"));
      if (((Thing) this.parent).Faction != null)
      {
        bool flag = FactionUtility.HostileTo(((Thing) this.parent).Faction, Faction.OfPlayer);
        stringBuilder.AppendLine(string.Format("HostileToPlayer: {0}", (object) flag));
      }
      AttackTargetsCache attackTargetsCache = ((Thing) this.parent).Map.attackTargetsCache;
      bool flag1 = GenHostility.IsPotentialThreat(this.parent as IAttackTarget);
      stringBuilder.AppendLine(string.Format("IsPotentialThreat: {0}", (object) flag1));
      if (!flag1)
        ;
      stringBuilder.AppendLine(string.Format("FillPercent: {0}", (object) ((Thing) this.parent).def.fillPercent));
      return stringBuilder.ToString();
    }
  }
}
