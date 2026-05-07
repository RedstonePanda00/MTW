using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;

public class HediffComp_AddAbility : HediffComp
{
    public HediffCompProperties_AddAbility Props => (HediffCompProperties_AddAbility)props;

    public override void CompPostMake()
    {
        base.CompPostMake();
        AddAbility();
    }

    public override void CompPostPostRemoved()
    {
        base.CompPostPostRemoved();
        RemoveAbility();
    }

    private void AddAbility()
    {
        if (parent.pawn.abilities != null && Props.abilityDef != null)
        {
            parent.pawn.abilities.GainAbility(Props.abilityDef);
        }
    }

    private void RemoveAbility()
    {
        if (parent.pawn.abilities != null && Props.abilityDef != null)
        {
            parent.pawn.abilities.RemoveAbility(Props.abilityDef);
        }
    }
}

// 自定义属性类
public class HediffCompProperties_AddAbility : HediffCompProperties
{
    public AbilityDef abilityDef;

    public HediffCompProperties_AddAbility()
    {
        compClass = typeof(HediffComp_AddAbility);
    }
}