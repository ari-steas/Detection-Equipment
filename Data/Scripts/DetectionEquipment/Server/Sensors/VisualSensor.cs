using DetectionEquipment.Server.Tracking;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Structs;
using DetectionEquipment.Shared.Utils;
using Sandbox.ModAPI;
using System;
using DetectionEquipment.Shared;
using VRage;
using VRage.Game.ModAPI;
using VRageMath;
using static DetectionEquipment.Server.SensorBlocks.GridSensorManager;

namespace DetectionEquipment.Server.Sensors
{
    internal class VisualSensor : ISensor
    {
        public uint Id { get; private set; }
        public SensorDefinition Definition { get; private set; }
        public Action<MyTuple<double, double, double, double, Vector3D, string[]>> OnDetection { get; set; } = null;

        public Vector3D Position { get; set; } = Vector3D.Zero;
        public Vector3D Direction { get; set; } = Vector3D.Forward;
        public double Aperture { get; set; } = MathHelper.ToRadians(15);

        public bool IsInfrared = false;
        public double BearingErrorModifier { get; set; } = 0.1;
        public double RangeErrorModifier { get; set; } = 5;
        public double MinVisibility = 0.001;
        public double CountermeasureNoise { get; set; } = 0;

        public VisualSensor(SensorDefinition definition)
        {
            Id = ServerMain.I.HighestSensorId++;
            IsInfrared = definition.Type == SensorDefinition.SensorType.Infrared;
            Definition = definition;
            
            ServerMain.I.SensorIdMap[Id] = this;
        }

        private VisualSensor() { }

        public void Close()
        {
            ServerMain.I.SensorIdMap.Remove(Id);
        }

        public DetectionInfo? GetDetectionInfo(VisibilitySet visibilitySet)
        {
            var visibility = IsInfrared ? visibilitySet.InfraredVisibility : visibilitySet.OpticalVisibility;

            double targetAngle = 0;
            if (visibilitySet.BoundingBox.Intersects(new RayD(Position, Direction)) == null)
                targetAngle = Vector3D.Angle(Direction, visibilitySet.ClosestCorner - Position);
            if (targetAngle > Aperture)
                return null;

            Vector3D bearing = visibilitySet.Position - Position;
            double range = bearing.Normalize();
            double targetSizeRatio = Math.Tan(Math.Sqrt(visibility/Math.PI) / range) / Aperture;

            //MyAPIGateway.Utilities.ShowNotification($"{targetSizeRatio*100:F1}% ({MathHelper.ToDegrees(Aperture):N0}° aperture)", 1000/60);
            if (targetSizeRatio < MinVisibility)
                return null;

            double errorScalar = 1 - MathHelper.Clamp(targetSizeRatio, 0, 1);

            double maxBearingError = Aperture/2 * BearingErrorModifier * errorScalar + CountermeasureNoise/100;
            bearing = MathUtils.RandomCone(bearing, maxBearingError);

            double maxRangeError = Math.Sqrt(range) * RangeErrorModifier * errorScalar + CountermeasureNoise/100;
            range += (2 * MathUtils.Random.NextDouble() - 1) * maxRangeError;

            OnDetection?.Invoke(new MyTuple<double, double, double, double, Vector3D, string[]>(visibility, range, maxRangeError, maxBearingError, bearing, Array.Empty<string>()));

            return new DetectionInfo
            {
                Track = visibilitySet.Track,
                Sensor = this,
                CrossSection = visibility,
                Bearing = bearing,
                BearingError = maxBearingError,
                Range = range,
                RangeError = maxRangeError,
                IffCodes = Array.Empty<string>(),
            };
        }
    }
}
