using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using System.Collections.Generic;
using static ECommons.UIHelpers.AddonMasterImplementations.AddonMaster;

namespace ICE.Scheduler.Tasks
{
    internal static class TaskFishing
    {
        public static HashSet<uint> SupportedMissions { get; set; } = [490, 542, 543, 544];
        public static void TryEnqueueFishing()
        {
            if (CosmicHelper.CurrentLunarMission != 0)
            {
                Job targetClass;
                if (((Job)CosmicHelper.CurrentMissionInfo.JobId).IsDol())
                    targetClass = (Job)CosmicHelper.CurrentMissionInfo.JobId;
                else if (((Job)CosmicHelper.CurrentMissionInfo.JobId2).IsDol())
                    targetClass = (Job)CosmicHelper.CurrentMissionInfo.JobId2;
                else
                    return;
                if ((Job)PlayerHelper.GetClassJobId() != targetClass)
                    GearsetHandler.TaskClassChange(targetClass);
                else
                    MakeFishingTask();
            }
        }

        internal static void MakeFishingTask()
        {
            var (currentScore, bronzeScore, silverScore, goldScore) = MissionHandler.GetCurrentScores();

            if (currentScore == 0 && silverScore == 0 && goldScore == 0)
            {
                IceLogging.Debug("Failed to get scores, aborting");
                return;
            }

            if (MissionHandler.IsMissionTimedOut())
            {
                SchedulerMain.State |= IceState.AbortInProgress;
                return;
            }

            if (currentScore >= goldScore)
            {
                IceLogging.Error("[TaskFishing | Current Score] We shouldn't be here, stopping and progressing");
                SchedulerMain.State |= IceState.ScoringMission;
                return;
            }

            if (!P.TaskManager.IsBusy)
            {
                int currentIndex = SchedulerMain.CurrentIndex;

                CosmicHelper.OpenStellarMission();
                var currentMission = CosmicHelper.CurrentLunarMission;

                List<uint> MissionNodes = new List<uint>();

                foreach (var entry in SchedulerMain.CurrentNodeSet)
                {
                    if (CosmicHelper.MissionInfoDict[currentMission].NodeSet == entry.NodeSet)
                    {
                        MissionNodes.Add(entry.NodeId);
                    }
                }
                uint nodeId = MissionNodes[currentIndex];

                // Checking to make sure that you're not currently gathering
                if (!Svc.Condition[ConditionFlag.Gathering])
                {
                    Vector3 nodeLoc = GatheringUtil.MoonNodeInfoList.Where(x => x.NodeId == nodeId).FirstOrDefault().LandZone;
                    if (PlayerHelper.GetDistanceToPlayer(nodeLoc) > 0.5f)
                    {
                        // Seen that the distance between you and the node is greater than 2, pathfinding
                        P.TaskManager.Enqueue(() => PathToNode(nodeLoc), "Pathing to node");
                        return;
                    }
                    else
                    {
                       P.TaskManager.Enqueue(() => StartAutoHook(), "Start AutoHook");
                       P.TaskManager.Enqueue(() => Svc.Condition[ConditionFlag.Fishing], "Making sure fishing is started");
                    }
                }
                // Check the score
                P.TaskManager.Enqueue(() => !Svc.Condition[ConditionFlag.Fishing]);
                P.TaskManager.Enqueue(() => SchedulerMain.State |= IceState.ScoringMission, "Checking score");
            }
        }

        /// <summary>
        /// Checks to see distance to the node. If you're to far away, will pathfind to it.
        /// </summary>
        /// <param name="id"></param>
        internal static bool? PathToNode(Vector3 nodeLoc)
        {
            if (PlayerHelper.GetDistanceToPlayer(nodeLoc) > 0.3f && !P.Navmesh.IsRunning())
            {
                if (EzThrottler.Throttle("Throttling pathfind"))
                {
                    P.Navmesh.SetTolerance(0.1f);
                    P.Navmesh.PathfindAndMoveTo(nodeLoc, false);
                }
            }
            else if (PlayerHelper.GetDistanceToPlayer(nodeLoc) < 0.3f)
            {
                if (P.Navmesh.IsRunning())
                {
                    P.Navmesh.Stop();
                }

                return true;
            }

            return false;
        }

        internal static bool? StartAutoHook()
        {
            if (!Svc.Condition[ConditionFlag.Fishing])
            {
                if (EzThrottler.Throttle("Throttling AutoHook start"))
                {
                    MacroManager.Execute("/ahstart");
                }
                return false;
            }
            return true;
        }
    }
}
