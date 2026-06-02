// Decompiled with JetBrains decompiler
// Type: NyarsModPackOne.JobGiver_AIFightEnemiesInPlace
// Assembly: NyearsModPackOne, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: FDB00AC3-5462-4449-9639-A371FA2E00F3
// Assembly location: C:\Users\15858\OneDrive\Documents\Sundry\RwModCoop\MTW\1.6\Assemblies\NyearsModPackOne.dll

using RimWorld;
using Verse;

#nullable disable
namespace NyarsModPackOne
{
  public class JobGiver_AIFightEnemiesInPlace : JobGiver_AIFightEnemy
  {
    protected override bool TryFindShootingPosition(Pawn pawn, out IntVec3 dest, Verb verbToUse = null)
    {
      Thing enemyTarget = pawn.mindState.enemyTarget;
      bool allowManualCastWeapons = !pawn.IsColonist && !pawn.IsColonySubhuman;
      Verb verb = verbToUse ?? pawn.TryGetAttackVerb(enemyTarget, allowManualCastWeapons, this.allowTurrets);
      if (verb == null || enemyTarget == null)
      {
        dest = IntVec3.Invalid;
        return false;
      }
      if (verb.CanHitTargetFrom(pawn.Position, enemyTarget))
      {
        dest = pawn.Position;
        return true;
      }
      dest = IntVec3.Invalid;
      return false;
    }
  }
}
