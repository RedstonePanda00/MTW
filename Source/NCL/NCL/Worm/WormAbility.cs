using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Noise;
using Verse.Sound;
using static UnityEngine.GraphicsBuffer;

namespace NCL.Worm
{


    public class CompProperties_WeaponList : CompProperties
    {
        public List<ThingDefCountClass> weaponList;
        public int checkIntervalTicks = 60; // ????(??60 ticks = 1?)
        public bool randomizeIfMultipleAvailable = false; // ?????????????

        public CompProperties_WeaponList()
        {
            compClass = typeof(CompWeaponList);
        }

    }//???????
    public class CompWeaponList : ThingComp
    {
        public List<ThingDefCountClass> weaponUsed =new List<ThingDefCountClass>();
        private CompProperties_WeaponList Props => (CompProperties_WeaponList)props;

        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            foreach (ThingDefCountClass item in Props.weaponList)
            {
                ThingDefCountClass item1 = item;
                item1.count = -1;
                weaponUsed.Add(item1);
            }
        }

        public override void CompTick()
        {
            base.CompTick();

            if (Find.TickManager.TicksGame % Props.checkIntervalTicks == 0)
            {
                if (!(parent is Pawn pawn) || pawn.equipment?.Primary != null)
                {
                    return;
                }//??????
                List<ThingDef> canuse = new List<ThingDef>();
                foreach (ThingDefCountClass item in weaponUsed)
                {
                    if ((item.count+Props.weaponList.First(t=>t.thingDef==item.thingDef).count)<=Find.TickManager.TicksGame)
                    {
                        canuse.Add(item.thingDef);
                    }
                }
                if (!canuse.NullOrEmpty())
                {
                    GiveWeapon(pawn, canuse.RandomElement());
                }
            }
        }

        private void GiveWeapon(Pawn pawn, ThingDef weaponIndex)
        {
            ThingWithComps weapon = (ThingWithComps)ThingMaker.MakeThing(weaponIndex);
            pawn.equipment.AddEquipment(weapon);
            weaponUsed.First(t=>t.thingDef == weaponIndex).count = Find.TickManager.TicksGame;

        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref weaponUsed, "weaponUsed", LookMode.Deep);
        }
    }



    public class Verb_CastAbilityJumpWithBomb : Verb_CastAbilityJump
    {
        //public override ThingDef JumpFlyerDef => NCLWormDefOf.NCLJumpWithBomb_Flyer;
        protected override bool TryCastShot()
        {
            if (ability.Activate(currentTarget, currentDestination))
            {
                return JumpUtility.DoJump(CasterPawn, currentTarget, base.ReloadableCompSource, verbProps, ability, base.CurrentTarget, DefDatabase<ThingDef>.GetNamed("NCLJumpWithBomb_Flyer"));
            }
            return false;
        }
    }
    public class NCLJumpWithBomb_Flyer : PawnFlyer
    {
        protected override void RespawnPawn()
        {
            Map map = base.Map;
            IntVec3 position = base.Position;
            base.RespawnPawn();
            GenExplosion.DoExplosion(position, map, 4f, DamageDefOf.Bomb, FlyingPawn, 30, 0.45f);

        }
    }

    public class Verb_CastAbilityBurrow : Verb_CastAbilityJump
    {
        //public override ThingDef JumpFlyerDef => NCLWormDefOf.NCLBurrow_Flyer;
        protected override bool TryCastShot()
        {
            if (ability.Activate(currentTarget, currentDestination))
            {
                return JumpUtility.DoJump(CasterPawn, currentTarget, base.ReloadableCompSource, verbProps, ability, base.CurrentTarget, DefDatabase<ThingDef>.GetNamed("NCLBurrow_Flyer"));
            }
            return false;
        }
    }
    public class NCLBurrow_Flyer : PawnFlyer
    {

        private int positionLastComputedTick = -1;

        private Vector3 groundPos;
        protected override void Tick()
        {
            base.Tick();
            GenExplosion.DoExplosion(Position, Map, 1, DamageDefOf.Bomb, null);
        }
        private void RecomputePosition()
        {
            if (positionLastComputedTick != ticksFlying)
            {
                positionLastComputedTick = ticksFlying;
                float t = (float)ticksFlying / (float)ticksFlightTime;
                float t2 = def.pawnFlyer.Worker.AdjustedProgress(t);
                groundPos = Vector3.Lerp(startVec, DestinationPos, t2);
                Position = groundPos.ToIntVec3();
            }
        }
        public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
        {
            RecomputePosition();
            if (phase == DrawPhase.Draw)
            {
                DrawShadow(groundPos);
            }
        }

        public override Vector3 DrawPos
        {
            get
            {
                RecomputePosition();
                return groundPos;
            }
        }
        private void DrawShadow(Vector3 drawLoc)
        {
            Material shadowMaterial = MaterialPool.MatFrom("Things/Skyfaller/SkyfallerShadowCircle", ShaderDatabase.Transparent);
            if (!(shadowMaterial == null))
            {
                Matrix4x4 matrix = default(Matrix4x4);
                matrix.SetTRS(drawLoc, Quaternion.identity, Vector3.one);
                Graphics.DrawMesh(MeshPool.plane10, matrix, shadowMaterial, 0);
            }
        }

    }

    public class Comp_TeleportToRandomAlly : CompAbilityEffect
    {
        // ????
        private const float MinDistance = 5f;
        private const float MaxDistance = 10f;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            if (!(target.Thing is Pawn victim) || victim.Map == null)
                return;

            // ??1: ??????????? (?????)
            Pawn randomAlly = FindRandomEligibleAlly(victim);
            if (randomAlly == null)
            {
                // ?????????????
                ExecuteTeleportSequence(victim, parent.pawn.Position);
                return;
            }

            // ??2: ?????????
            IntVec3 targetCell = FindSafeTeleportSpot(randomAlly.Position, victim.Map);
            if (!targetCell.IsValid)
            {
                // ?????????,??????
                targetCell = randomAlly.Position;
            }

            // ??3: ??????(??????)
            ExecuteTeleportSequence(victim, targetCell);
        }

        private Pawn FindRandomEligibleAlly(Pawn victim)
        {
            Faction faction = parent.pawn.Faction;

            return victim.Map.mapPawns.AllPawnsSpawned
                .Where(p =>
                    p.Faction == faction &&    // ???
                    p != victim &&             // ????
                    p != parent.pawn &&        // ?????
                    !p.Downed &&               // ??????
                    !p.Dead &&                 // ??????
                    p.Spawned                  // ???????
                )
                .RandomElementWithFallback();
        }

        private IntVec3 FindSafeTeleportSpot(IntVec3 center, Map map)
        {
            return CellFinder.RandomClosewalkCellNear(
                center,
                map,
                Mathf.RoundToInt(MaxDistance),
                cell =>
                    cell.DistanceTo(center) >= MinDistance &&  // ??????
                    cell.Standable(map) &&                     // ??????
                    !cell.Fogged(map) &&                       // ?????
                    map.reachability.CanReach(center, cell, PathEndMode.OnCell, TraverseParms.For(TraverseMode.NoPassClosedDoors))
            );
        }

        private void ExecuteTeleportSequence(Pawn victim, IntVec3 targetCell)
        {
            Map map = victim.Map;

            // ????:????(????)
            FleckMaker.Static(victim.Position, map, FleckDefOf.PsycastSkipFlashEntry);

            // ?????(??????)
            victim.DeSpawn(DestroyMode.WillReplace);

            // ??????
            GenSpawn.Spawn(victim, targetCell, map);

            // ????:????(????)
            FleckMaker.Static(targetCell, map, FleckDefOf.PsycastSkipInnerExit);
        }
    }
    public class Comp_NCLBeckon : CompAbilityEffect
    {
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (target.Thing is Pawn pawn)
            {
                Map vec3s = pawn.Map;
                FleckMaker.Static(pawn.Position, vec3s, FleckDefOf.PsycastSkipFlashEntry);
                pawn.DeSpawn(DestroyMode.WillReplace);
                GenSpawn.Spawn(pawn, parent.pawn.Position, vec3s);
                FleckMaker.Static(pawn.Position, vec3s, FleckDefOf.PsycastSkipInnerExit);
            }
        }
    }
    
    public class Comp_NCLPush : CompAbilityEffect
    {

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (target.Thing is Pawn pawn)
            {
                Map map = pawn.Map;

                // 1. ??????
                if (TryFindRandomStandableEdgeCell(map, out IntVec3 validEdgeCell))
                {
                    // ????????????,?????
                    FleckMaker.Static(pawn.Position, map, FleckDefOf.PsycastSkipFlashEntry);
                    pawn.DeSpawn(DestroyMode.WillReplace);
                    GenSpawn.Spawn(pawn, validEdgeCell, map); // ??????????
                    FleckMaker.Static(pawn.Position, map, FleckDefOf.PsycastSkipInnerExit);
                }
                else
                {
                    Log.Message("Could not find valid edge cell.");
                }
            }
        }

        private static bool TryFindRandomStandableEdgeCell(Map map, out IntVec3 result, int maxAttempts = 100)
        {
            int minDistanceFromEdge = 5; // ???????????

            for (int i = 0; i < maxAttempts; i++)
            {
                // 1. ???????????
                IntVec3 edgeCell = CellFinder.RandomEdgeCell(map);

                // 2. ???????????(?? Normalized())
                IntVec3 offset = map.Center - edgeCell;
                float length = Mathf.Sqrt(offset.x * offset.x + offset.z * offset.z);
                IntVec3 direction = length > 0.01f ?
                    new IntVec3(
                        Mathf.RoundToInt(offset.x / length),
                        0,
                        Mathf.RoundToInt(offset.z / length)
                    ) :
                    new IntVec3(0, 0, 0);

                // 3. ?????????5?
                IntVec3 candidate = edgeCell + direction * minDistanceFromEdge;

                // 4. ??????(±2?)??????
                if (Rand.Chance(0.7f)) // 70%??????
                {
                    candidate += new IntVec3(
                        Rand.RangeInclusive(-2, 2),
                        0,
                        Rand.RangeInclusive(-2, 2)
                    );
                }

                // 5. ?????? - ???????????
                if (IsValidTeleportSpot(candidate, map))
                {
                    result = candidate;
                    return true;
                }
            }

            // ????:?????????5-15???????
            Predicate<IntVec3> validator = (IntVec3 c) => IsValidTeleportSpot(c, map);

            // ??????(maxAttempts ? out ????)
            // ??? - ??3????
            if (CellFinder.TryFindRandomCell(map, validator, out result))
            {
                return true;
            }

            result = IntVec3.Invalid;
            Log.Warning("[NCL] Comp_NCLPush could not find a standable edge cell after " + maxAttempts + " attempts.");
            return false;
        }

        // ??????????
        private static int DistanceToEdge(IntVec3 cell, Map map)
        {
            if (!cell.InBounds(map)) return 0;

            int minDist = Mathf.Min(
                cell.x, // ?????
                map.Size.x - cell.x - 1, // ?????
                cell.z, // ?????
                map.Size.z - cell.z - 1 // ?????
            );

            return Mathf.Max(0, minDist);
        }

        // ??:????????(???????,????)
        private static bool IsValidTeleportSpot(IntVec3 candidate, Map map)
        {
            // ????
            if (!candidate.InBounds(map) ||
                !candidate.Standable(map) ||
                DistanceToEdge(candidate, map) < 5)
            {
                return false;
            }

            // ??????
            if (map.fogGrid != null && map.fogGrid.IsFogged(candidate))
            {
                return false;
            }

            // ????(???)
            Building building = candidate.GetEdifice(map);
            if (building != null &&
                (building.def.passability == Traversability.Impassable ||
                 building.def.IsDoor))
            {
                return false;
            }

            // ????(????????)
            if (map.roofGrid.Roofed(candidate) && map.roofGrid.RoofAt(candidate).isThickRoof)
            {
                return false;
            }

            // ?????????


            return true;
        }
    }

    public class Comp_ChaosTeleport : CompAbilityEffect
    {
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            Map map = parent.pawn.Map;
            List<Pawn> pawns = new List<Pawn>();

            // ??????
            foreach (Pawn item in map.mapPawns.AllPawnsSpawned)
            {
                if (item.HostileTo(parent.pawn) ||
                    (parent.pawn.Faction != null &&
                     item.Faction != null &&
                     parent.pawn.Faction.HostileTo(item.Faction)))
                {
                    pawns.Add(item);
                }
            }

            // ?????????????
            for (int i = pawns.Count - 1; i >= 0; i--)
            {
                FleckMaker.Static(pawns[i].Position, map, FleckDefOf.PsycastSkipFlashEntry);
                pawns[i].DeSpawn(DestroyMode.WillReplace);

                // ???????????
                IntVec3 safeSpot = FindMapEdgeSafeSpot(pawns[i], map);
                GenSpawn.Spawn(pawns[i], safeSpot, map);

                FleckMaker.Static(pawns[i].Position, map, FleckDefOf.PsycastSkipInnerExit);
            }
        }

        // ???????????
        private IntVec3 FindMapEdgeSafeSpot(Pawn pawn, Map map)
        {
            // ????????
            Predicate<IntVec3> safeSpotValidator = cell =>
                IsSafeSpot(cell, map) && IsInEdgeBufferZone(cell, map);

            // 1. ????????????????
            for (int i = 0; i < 100; i++)
            {
                // ????????
                IntVec3 edgeCell = CellFinder.RandomEdgeCell(map);

                // ????????(??5-10?)
                int offset = Rand.Range(5, 11);
                IntVec3 candidate = GetOffsetPosition(edgeCell, map, offset);

                if (candidate.InBounds(map) && safeSpotValidator(candidate))
                {
                    return candidate;
                }
            }

            // 2. ???????????(?????????)
            IntVec3 randomEdgeCell = CellFinder.RandomEdgeCell(map);
            if (CellFinder.TryFindRandomCellNear(
                randomEdgeCell,
                map,
                15,
                safeSpotValidator,
                out IntVec3 safeCell))
            {
                return safeCell;
            }

            // 3. ????????????
            List<IntVec3> cornerTargets = new List<IntVec3>
        {
            new IntVec3(5, 0, 5), // ?????5?
            new IntVec3(map.Size.x - 6, 0, 5), // ?????5?
            new IntVec3(5, 0, map.Size.z - 6), // ?????5?
            new IntVec3(map.Size.x - 6, 0, map.Size.z - 6) // ?????5?
        };

            foreach (var corner in cornerTargets)
            {
                if (safeSpotValidator(corner))
                {
                    return corner;
                }

                if (CellFinder.TryFindRandomCellNear(corner, map, 10, safeSpotValidator, out safeCell))
                {
                    return safeCell;
                }
            }

            // 4. ?????????,????????(????????,??????)
            return CellFinder.RandomEdgeCell(map);
        }

        // ????:?????????
        private IntVec3 GetOffsetPosition(IntVec3 edgeCell, Map map, int offset)
        {
            // ????????????
            offset = Mathf.Clamp(offset, 0, Mathf.Max(map.Size.x, map.Size.z) / 2);

            if (edgeCell.x == 0) return new IntVec3(edgeCell.x + offset, 0, edgeCell.z);
            if (edgeCell.x == map.Size.x - 1) return new IntVec3(edgeCell.x - offset, 0, edgeCell.z);
            if (edgeCell.z == 0) return new IntVec3(edgeCell.x, 0, edgeCell.z + offset);
            if (edgeCell.z == map.Size.z - 1) return new IntVec3(edgeCell.x, 0, edgeCell.z - offset);

            return edgeCell; // ????????????,?????
        }

        // ????????????(????5-10?)
        private bool IsInEdgeBufferZone(IntVec3 cell, Map map)
        {
            // ?????????
            int distToWest = cell.x;
            int distToEast = map.Size.x - cell.x - 1;
            int distToSouth = cell.z;
            int distToNorth = map.Size.z - cell.z - 1;

            // ????????
            int minDist = Mathf.Min(distToWest, distToEast, distToSouth, distToNorth);

            // ?????5-10????
            return minDist >= 5 && minDist <= 10;
        }

        // ???????????(???????,????,?????)
        private bool IsSafeSpot(IntVec3 cell, Map map)
        {
            // ??????
            if (!cell.InBounds(map))
                return false;

            // ??????
            if (map.fogGrid?.IsFogged(cell) ?? false)
                return false;

            // ???????????
            Building building = cell.GetEdifice(map);
            if (building != null)
            {
                // ????????
                if (building.def.passability == Traversability.Impassable || building.def.IsDoor)
                    return false;
            }

            // ?????????
            TerrainDef terrain = cell.GetTerrain(map);
            if (terrain == null || terrain.passability == Traversability.Impassable)
                return false;

            // ????(??????)
            if (terrain.IsWater && terrain.extraNonDraftedPerceivedPathCost > 30)
                return false;

            // ?????(????)
            if (map.roofGrid.Roofed(cell) && map.roofGrid.RoofAt(cell).isThickRoof)
                return false;

            // ????:???????????
            return cell.Standable(map);
        }
    }
}
