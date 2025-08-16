using DetectionEquipment.Server.Tracking;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Helpers;
using DetectionEquipment.Shared.Structs;
using DetectionEquipment.Shared.Utils;
using System;
using DetectionEquipment.Server.Countermeasures;
using VRageMath;
using static DetectionEquipment.Server.SensorBlocks.GridSensorManager;

namespace DetectionEquipment.Server.Sensors
{
    internal class VisualSensor : ISensor
    {
        public bool Enabled { get; set; } = true;
        public uint Id { get; private set; }
        public SensorDefinition Definition { get; private set; }
        public Action<object[]> OnDetection { get; set; } = null;

        public Vector3D Position { get; set; } = Vector3D.Zero;
        public Vector3D Direction { get; set; } = Vector3D.Forward;
        public double Aperture { get; set; }

        public bool IsInfrared = false;
        public double CountermeasureNoise { get; set; } = 0;

        public VisualSensor(SensorDefinition definition)
        {
            Id = ServerMain.I.HighestSensorId++;
            IsInfrared = definition.Type == SensorDefinition.SensorType.Infrared;
            Definition = definition;
            Aperture = definition.MaxAperture;
            
            ServerMain.I.SensorIdMap[Id] = this;
        }

        public void Close()
        {
            ServerMain.I.SensorIdMap.Remove(Id);
        }

        public DetectionInfo? GetDetectionInfo(VisibilitySet visibilitySet)
        {
            if (!Enabled)
                return null;

            double targetAngle = 0;
            if (visibilitySet.BoundingBox.Intersects(new RayD(Position, Direction)) == null)
                targetAngle = Vector3D.Angle(Direction, visibilitySet.ClosestCorner - Position);
            if (targetAngle > Aperture)
                return null;

            Vector3D targetBearing = visibilitySet.Track.Position - Position;
            double targetRange = targetBearing.Normalize();
            var visibility = IsInfrared ? visibilitySet.InfraredVisibility : visibilitySet.OpticalVisibility;
            double targetSizeRatio = Math.Tan(Math.Sqrt(visibility/Math.PI) / targetRange) / Aperture;

            //MyAPIGateway.Utilities.ShowNotification($"{targetSizeRatio*100:F1}% ({MathHelper.ToDegrees(Aperture):N0}° aperture)", 1000/60);
            if (targetSizeRatio < Definition.DetectionThreshold)
                return null;

            double errorScalar = 1 - MathHelper.Clamp(targetSizeRatio, 0, 1);

            var gT = visibilitySet.Track as GridTrack;

            double trackCrossSection = visibility;
            double trackRange = targetRange;
            double maxRangeError = Math.Sqrt(targetRange) * Definition.RangeErrorModifier * errorScalar + CountermeasureNoise/100;
            Vector3D trackBearing = targetBearing;
            double maxBearingError = Aperture/2 * Definition.BearingErrorModifier * errorScalar + CountermeasureNoise/100;
            var iffCodes = gT != null ? IffHelper.GetIffCodes(gT.Grid, IsInfrared ? SensorDefinition.SensorType.Infrared : SensorDefinition.SensorType.Optical) : Array.Empty<string>();

            CountermeasureManager.ApplyDrfm(this, visibilitySet.Track, ref trackCrossSection, ref trackRange, ref maxRangeError, ref trackBearing, ref maxBearingError, ref iffCodes);

            trackBearing = MathUtils.RandomCone(trackBearing, maxBearingError);
            trackRange += (2 * MathUtils.Random.NextDouble() - 1) * maxRangeError;

            var detection = new DetectionInfo
            (
                visibilitySet.Track,
                this,
                trackCrossSection,
                trackRange,
                maxRangeError,
                trackBearing,
                maxBearingError,
                iffCodes
            );

            OnDetection?.Invoke(ObjectPackager.Package(detection));

            return detection;
        }
    }
}
