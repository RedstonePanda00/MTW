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
    protected virtual bool TryFindShootingPosition(Pawn pawn, out IntVec3 dest, Verb verbToUse = null)
    {
      Thing enemyTarget = pawn.mindState.enemyTarget;
      bool flag = !pawn.IsColonist && !pawn.IsColonySubhuman;
      Verb verb = verbToUse ?? pawn.TryGetAttackVerb(enemyTarget, flag, this.allowTurrets);
      int num;
      if (verb != null)
      {
        IntVec3 position = ((Thing) pawn).Position;
        num = !((IntVec3) ref position).InHorDistOf(enemyTarget.Position, verb.EffectiveRange) ? 1 : 0;
      }
      else
        num = 1;
      if (num != 0)
      {
        dest = IntVec3.Invalid;
        return false;
      }
      dest = ((Thing) pawn).Position;
      return true;
    }
  }
}
