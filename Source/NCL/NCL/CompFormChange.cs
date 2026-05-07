using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace NCL
{
    public class CompFormChange : ThingComp
    {
        public int revertTickCounter;
        public int cooldownNow;
        public int cooldownMax;

        public ThingComp FindCompSameType(Type t)
        {
            for (int i = 0; i < parent.AllComps.Count; i++)
            {
                if (parent.AllComps[i].GetType() == t)
                    return parent.AllComps[i];
            }
            return null;
        }

        public void TryTransformInto(Pawn pawn, TransformData tsd)
        {
            ThingWithComps newThing = !tsd.thingDef.MadeFromStuff
                ? (ThingWithComps)ThingMaker.MakeThing(tsd.thingDef, null)
                : (ThingWithComps)ThingMaker.MakeThing(tsd.thingDef, parent.Stuff);
            newThing.HitPoints = parent.HitPoints;

            List<ThingComp> scratch = new List<ThingComp>();
            scratch.AddRange(newThing.AllComps);
            foreach (ThingComp newComp in scratch)
            {
                if (Props.SharedCompsResolved.Contains(newComp.GetType()))
                {
                    ThingComp oldComp = FindCompSameType(newComp.GetType());
                    newThing.AllComps.Remove(newComp);
                    newThing.AllComps.Add(oldComp);
                    parent.AllComps.Remove(oldComp);
                    parent.AllComps.Add(newComp);
                }
            }

            foreach (ThingComp c in newThing.AllComps)
                c.parent = newThing;

            CompFormChange newFormComp = newThing.TryGetComp<CompFormChange>();
            if (newFormComp != null)
            {
                newFormComp.cooldownNow = tsd.transformCooldown;
                newFormComp.cooldownMax = tsd.transformCooldown;
            }

            IThingHolder parentHolder = ParentHolder;
            Map map = parent.Map;
            Vector3 drawPos = parent.DrawPos;
            parent.Destroy(DestroyMode.Vanish);

            if (pawn == null)
            {
                if (map != null)
                {
                    GenSpawn.Spawn(newThing, IntVec3Utility.ToIntVec3(drawPos), map, WipeMode.Vanish);
                    if (tsd.moteOnTransform != null)
                        MoteMaker.MakeStaticMote(drawPos, map, tsd.moteOnTransform, 1f);
                }
                else
                    parentHolder.GetDirectlyHeldThings().TryAdd(newThing, true);
            }
            else
            {
                pawn.equipment.AddEquipment(newThing);
                drawPos = pawn.DrawPos;
                map = pawn.Map;
                if (tsd.moteOnTransform != null)
                    MoteMaker.MakeStaticMote(drawPos, map, tsd.moteOnTransform, 1f);
            }

            if (map != null && tsd.soundOnTransform != null)
                tsd.soundOnTransform.PlayOneShot(SoundInfo.InMap(new TargetInfo(IntVec3Utility.ToIntVec3(drawPos), map)));
        }

        public CompPropertiesWeaponSwitch Props => (CompPropertiesWeaponSwitch)props;

        public Pawn GetEquipper()
        {
            if (ParentHolder is Pawn_EquipmentTracker tracker)
                return tracker.pawn;
            return null;
        }

        public void CooldownTick()
        {
            cooldownNow = Mathf.Max(cooldownNow - 1, 0);
            revertTickCounter++;
            if (Props.revertData == null || Props.revertData.revertAfterTicks > revertTickCounter)
                return;
            TryTransformInto(GetEquipper(), Props.revertData);
        }

        public IEnumerable<Gizmo> HeldGizmos(Pawn pawn)
        {
            foreach (TransformData transformData in Props.transformData)
            {
                bool ready = true;
                if (transformData.needApparel != null)
                {
                    ready = false;
                    foreach (Thing thing in pawn.apparel.WornApparel)
                    {
                        if (thing.def == transformData.needApparel)
                        {
                            ready = true;
                            break;
                        }
                    }
                }

                TransformData tsdP = transformData;
                Texture2D icon = transformData.thingDef.uiIcon;
                float iconDrawScale = transformData.thingDef.uiIconScale;
                if (!GenText.NullOrEmpty(transformData.iconPath))
                {
                    icon = ContentFinder<Texture2D>.Get(transformData.iconPath, true);
                    iconDrawScale = transformData.iconSize;
                }

                Command_Transform_Action cmd = new Command_Transform_Action
                {
                    defaultLabel = transformData.label,
                    defaultDesc = transformData.description,
                    compFormChange = this,
                    transformData = tsdP,
                    icon = icon,
                    iconDrawScale = iconDrawScale,
                    Disabled = cooldownNow > 0 || !ready,
                    action = () => TryTransformInto(pawn, tsdP)
                };
                cmd.disabledReason = ready ? string.Empty : "appActive".Translate(transformData.needApparel.label).ToString();
                yield return cmd;
            }

            if (Props.revertData != null)
            {
                TransformData revertData = Props.revertData;
                Texture2D icon2 = revertData.thingDef.uiIcon;
                float iconDrawScale2 = revertData.thingDef.uiIconScale;
                if (!GenText.NullOrEmpty(revertData.iconPath))
                {
                    icon2 = ContentFinder<Texture2D>.Get(revertData.iconPath, true);
                    iconDrawScale2 = revertData.iconSize;
                }

                yield return new Command_AutoReversion_Action
                {
                    defaultLabel = revertData.label,
                    defaultDesc = revertData.description,
                    compFormChange = this,
                    transformData = revertData,
                    icon = icon2,
                    iconDrawScale = iconDrawScale2,
                    Disabled = true
                };
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref cooldownNow, "cooldownNow", 0);
            Scribe_Values.Look(ref cooldownMax, "cooldownMax", 0);
            Scribe_Values.Look(ref revertTickCounter, "revertTickCounter", 0);
        }
    }
}
