using DetectionEquipment.Server;
using DetectionEquipment.Server.Tracking;
using Sandbox.Game.Entities;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace DetectionEquipment.Shared.Utils
{
    internal static class GlobalObjectPools
    {
        /// <summary>
        /// Please return these cleared!
        /// </summary>
        public static ObjectPool<List<Vector3I>> Vector3IPool;
        /// <summary>
        /// Please return these cleared!
        /// </summary>
        public static ObjectPool<List<MyEntity>> MyEntityPool;
        /// <summary>
        /// Please return these cleared!
        /// </summary>
        public static ObjectPool<List<IHitInfo>> HitInfoPool;
        /// <summary>
        /// Please return these cleared!
        /// </summary>
        public static ObjectPool<List<MyLineSegmentOverlapResult<MyEntity>>> EntityLineOverlapPool;
        /// <summary>
        /// Please return these cleared!
        /// </summary>
        //public static ObjectPool<List<MyLineSegmentOverlapResult<MyVoxelBase>>> VoxelLineOverlapPool;
        public static ObjectPool<HashSet<MyDataBroadcaster>> DataBroadcasterPool;
        public static ObjectPool<Queue<MyDataReceiver>> DataReceiverPool;
        /// <summary>
        /// For use in GridSensorManager.cs
        /// </summary>
        public static AsyncSharedObjectPool<Dictionary<IMyEntity, ITrack>> TrackSharedPool;


        public static void Init()
        {
            Vector3IPool = new ObjectPool<List<Vector3I>>(
                () => new List<Vector3I>(),
                startSize: 100
                );
            MyEntityPool = new ObjectPool<List<MyEntity>>(
                () => new List<MyEntity>(),
                startSize: 100
            );
            HitInfoPool = new ObjectPool<List<IHitInfo>>(
                () => new List<IHitInfo>(),
                startSize: 100
            );
            EntityLineOverlapPool = new ObjectPool<List<MyLineSegmentOverlapResult<MyEntity>>>(
                () => new List<MyLineSegmentOverlapResult<MyEntity>>(),
                startSize: 100
            );
            //VoxelLineOverlapPool = new ObjectPool<List<MyLineSegmentOverlapResult<MyVoxelBase>>>(
            //    () => new List<MyLineSegmentOverlapResult<MyVoxelBase>>(),
            //    startSize: 100
            //);
            DataBroadcasterPool = new ObjectPool<HashSet<MyDataBroadcaster>>(
                () => new HashSet<MyDataBroadcaster>(),
                startSize: 10
            );
            DataReceiverPool = new ObjectPool<Queue<MyDataReceiver>>(
                () => new Queue<MyDataReceiver>(),
                startSize: 10
            );

            if (MyAPIGateway.Session.IsServer)
            {
                TrackSharedPool = new AsyncSharedObjectPool<Dictionary<IMyEntity, ITrack>>(
                    () => new Dictionary<IMyEntity, ITrack>(50),
                    (dict) =>
                    {
                        foreach (var kvp in ServerMain.I.Tracks)
                        {
                            dict.Add(kvp.Key, kvp.Value);
                        }
                    },
                    (dict) => dict.Clear(),
                    3
                );
            }
        }

        public static void Update()
        {
            TrackSharedPool.UpdateTick();
        }

        public static void Unload()
        {
            Vector3IPool = null;
            MyEntityPool = null;
            HitInfoPool = null;
            EntityLineOverlapPool = null;
            //VoxelLineOverlapPool = null;
            DataBroadcasterPool = null;
            DataReceiverPool = null;
        }
    }
}
