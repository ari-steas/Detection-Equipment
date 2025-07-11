using System.Collections.Generic;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
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
        public static ObjectPool<List<MyLineSegmentOverlapResult<MyEntity>>> LineSegmentOverlapPool;
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
            LineSegmentOverlapPool = new ObjectPool<List<MyLineSegmentOverlapResult<MyEntity>>>(
                () => new List<MyLineSegmentOverlapResult<MyEntity>>(),
                startSize: 100
            );
        }

        public static void Unload()
        {
            Vector3IPool = null;
            MyEntityPool = null;
            HitInfoPool = null;
        }
    }
}
