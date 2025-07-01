using System.Collections.Generic;
using VRageMath;

namespace DetectionEquipment.Shared.Utils
{
    internal static class GlobalObjectPools
    {
        /// <summary>
        /// Please return these cleared!
        /// </summary>
        public static ObjectPool<List<Vector3I>> Vector3IPool;

        public static void Init()
        {
            Vector3IPool = new ObjectPool<List<Vector3I>>(
                () => new List<Vector3I>(),
                startSize: 100
                );
        }

        public static void Unload()
        {
            Vector3IPool = null;
        }
    }
}
