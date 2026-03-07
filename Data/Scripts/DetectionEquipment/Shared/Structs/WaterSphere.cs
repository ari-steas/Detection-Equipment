using DetectionEquipment.Shared.ExternalApis.WaterMod;
using Sandbox.Game.Entities;
using VRageMath;

namespace DetectionEquipment.Shared.Structs
{
    public struct WaterSphere
    {
        public readonly MyPlanet Planet;
        public readonly BoundingSphereD BaseSphere;
        public readonly float TideHeight;

        public BoundingSphereD CurrentSphere { get; private set; }


        public WaterSphere(MyPlanet p)
        {
            Planet = p;
            
            var waterData = WaterModApi.GetPhysical(Planet);
            BaseSphere = new BoundingSphereD(waterData.Item1, waterData.Item2);
            CurrentSphere = BaseSphere;

            var tideData = WaterModApi.GetTideData(Planet);
            TideHeight = tideData.Item1;

            UpdateCurrentSphere();
        }

        public void UpdateCurrentSphere()
        {
            if (TideHeight == 0)
                return;

            Vector3D tideDirection = WaterModApi.GetTideDirection(Planet);

            CurrentSphere = new BoundingSphereD(BaseSphere.Center + tideDirection * TideHeight, BaseSphere.Radius);
        }
    }
}
