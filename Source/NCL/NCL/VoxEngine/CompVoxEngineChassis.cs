using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace NCL
{
    public class CompVoxEngineChassis : ThingComp
    {
        private float currentBaseAngle;
        private float targetBaseAngle;
        private float lastMovementWorldAngle;
        private bool angleInitialized;
        private VoxBaseVisualMode visualMode;

        public CompProperties_VoxEngineChassis Props => (CompProperties_VoxEngineChassis)props;

        public float CurrentBaseAngle => currentBaseAngle;

        public float CurrentUserAngle => VoxEngineChassisAngles.RimWorldToUserAngle(currentBaseAngle);

        public VoxBaseVisualMode VisualMode => visualMode;

        public bool UseSideProfileGraphic =>
            visualMode == VoxBaseVisualMode.SideEast || visualMode == VoxBaseVisualMode.SideWest;

        public bool SideMirrorX => visualMode == VoxBaseVisualMode.SideWest;

        public static CompVoxEngineChassis Get(Pawn pawn)
        {
            return pawn?.GetComp<CompVoxEngineChassis>();
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            EnsureAngleInitialized();
            UpdateVisualMode();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref currentBaseAngle, "voxEngineBaseAngle", 0f);
            Scribe_Values.Look(ref targetBaseAngle, "voxEngineTargetBaseAngle", 0f);
            Scribe_Values.Look(ref lastMovementWorldAngle, "voxEngineLastMovementWorldAngle", 0f);
            Scribe_Values.Look(ref angleInitialized, "voxEngineBaseAngleInit", false);
            Scribe_Values.Look(ref visualMode, "voxEngineBaseVisualMode", VoxBaseVisualMode.TopDown);
        }

        public override void CompTick()
        {
            base.CompTick();
            if (parent is not Pawn pawn || !pawn.Spawned)
            {
                return;
            }

            EnsureAngleInitialized();
            UpdateMovementTarget(pawn);
            bool angleChanged = UpdateBaseAngle(1f / 60f);
            bool visualModeChanged = UpdateVisualMode();
            if (angleChanged || visualModeChanged)
            {
                pawn.Drawer?.renderer?.renderTree?.SetDirty();
            }
        }

        public float GetTargetAngle(Pawn pawn)
        {
            return VoxEngineChassisAngles.NormalizeDegrees360(targetBaseAngle);
        }

        private void UpdateMovementTarget(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            if (TryGetPathStepWorldAngle(pawn, out float pathAngle))
            {
                SetTargetAngle(pathAngle);
            }
        }

        private void SetTargetAngle(float worldAngle)
        {
            targetBaseAngle = VoxEngineChassisAngles.NormalizeDegrees360(worldAngle);
            lastMovementWorldAngle = targetBaseAngle;
        }

        private static bool TryGetPathStepWorldAngle(Pawn pawn, out float worldAngle)
        {
            worldAngle = 0f;
            Pawn_PathFollower pather = pawn.pather;
            if (pather == null || !pather.MovingNow)
            {
                return false;
            }

            IntVec3 nextCell = pather.nextCell;
            if (nextCell.IsValid && nextCell != pawn.Position)
            {
                worldAngle = VoxEngineChassisAngles.NormalizeDegrees360(
                    (nextCell.ToVector3Shifted() - pawn.Position.ToVector3Shifted()).AngleFlat());
                return true;
            }

            if (pather.curPath != null && pather.curPath.NodesLeftCount > 0)
            {
                IntVec3 peekCell = pather.curPath.Peek(0);
                if (peekCell.IsValid && peekCell != pawn.Position)
                {
                    worldAngle = VoxEngineChassisAngles.NormalizeDegrees360(
                        (peekCell.ToVector3Shifted() - pawn.Position.ToVector3Shifted()).AngleFlat());
                    return true;
                }
            }

            return false;
        }

        public bool UpdateBaseAngle(float deltaSeconds)
        {
            if (parent is not Pawn pawn)
            {
                return false;
            }

            float targetAngle = VoxEngineChassisAngles.NormalizeDegrees360(GetTargetAngle(pawn));
            float maxDelta = Props.angularVelocityDegPerSec * Mathf.Max(deltaSeconds, 0f);
            float oldAngle = currentBaseAngle;
            currentBaseAngle = VoxEngineChassisAngles.NormalizeDegrees360(
                Mathf.MoveTowardsAngle(currentBaseAngle, targetAngle, maxDelta));
            return Mathf.Abs(Mathf.DeltaAngle(oldAngle, currentBaseAngle)) > 0.001f;
        }

        public bool UpdateVisualMode()
        {
            float userAngle = CurrentUserAngle;
            VoxBaseVisualMode oldMode = visualMode;
            visualMode = VoxEngineChassisAngles.UpdateVisualMode(
                userAngle,
                visualMode,
                Props.sideSwitchHysteresisDeg);
            return visualMode != oldMode;
        }

        public string ActiveBaseTexPath
        {
            get
            {
                VoxChassisLifeStageTextures stage = CurrentLifeStageTextures;
                if (stage != null && !stage.baseTexPath.NullOrEmpty())
                {
                    return stage.baseTexPath;
                }

                return Props.baseTexPath;
            }
        }

        public string ActiveBaseSideTexPath
        {
            get
            {
                VoxChassisLifeStageTextures stage = CurrentLifeStageTextures;
                if (stage != null && !stage.baseSideTexPath.NullOrEmpty())
                {
                    return stage.baseSideTexPath;
                }

                return Props.baseSideTexPath;
            }
        }

        private VoxChassisLifeStageTextures CurrentLifeStageTextures
        {
            get
            {
                if (Props.lifeStageTextures.NullOrEmpty()
                    || parent is not Pawn pawn
                    || pawn.ageTracker == null
                    || pawn.RaceProps == null
                    || pawn.RaceProps.Humanlike)
                {
                    return null;
                }

                int index = Mathf.Clamp(pawn.ageTracker.CurLifeStageIndex, 0, Props.lifeStageTextures.Count - 1);
                return Props.lifeStageTextures[index];
            }
        }

        public string GetActiveBaseTexPath()
        {
            string sidePath = ActiveBaseSideTexPath;
            if (UseSideProfileGraphic && !sidePath.NullOrEmpty())
            {
                return sidePath;
            }

            return ActiveBaseTexPath;
        }

        public float GetDisplayRimAngle()
        {
            return VoxEngineChassisAngles.NormalizeDegrees360(currentBaseAngle);
        }

        public override List<PawnRenderNode> CompRenderNodes()
        {
            if (Props.renderNodeProperties.NullOrEmpty())
            {
                return base.CompRenderNodes();
            }

            if (parent is not Pawn pawn)
            {
                return base.CompRenderNodes();
            }

            List<PawnRenderNode> nodes = new List<PawnRenderNode>();
            PawnRenderTree tree = pawn.Drawer?.renderer?.renderTree;
            foreach (PawnRenderNodeProperties nodeProps in Props.renderNodeProperties)
            {
                if (nodeProps?.nodeClass == null)
                {
                    continue;
                }

                try
                {
                    PawnRenderNode node = (PawnRenderNode)Activator.CreateInstance(
                        nodeProps.nodeClass,
                        pawn,
                        nodeProps,
                        tree);
                    if (node is PawnRenderNode_VoxEngineBase baseNode)
                    {
                        baseNode.BindChassisComp(this);
                    }

                    nodes.Add(node);
                }
                catch (Exception ex)
                {
                    Log.Error($"[NCL VoxEngine] Failed to create render node: {ex}");
                }
            }

            return nodes.Count > 0 ? nodes : base.CompRenderNodes();
        }

        private void EnsureAngleInitialized()
        {
            if (angleInitialized || parent is not Pawn pawn)
            {
                return;
            }

            float spawnAngle = VoxEngineChassisAngles.NormalizeDegrees360(pawn.Rotation.AsAngle);
            currentBaseAngle = spawnAngle;
            targetBaseAngle = spawnAngle;
            lastMovementWorldAngle = spawnAngle;
            angleInitialized = true;
        }
    }
}
