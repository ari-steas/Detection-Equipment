using DetectionEquipment.Server.Tracking;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Helpers;
using DetectionEquipment.Shared.Structs;
using DetectionEquipment.Shared.Utils;
using System;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;
using static DetectionEquipment.Server.SensorBlocks.GridSensorManager;

namespace DetectionEquipment.Server.Sensors
{
    internal class AntennaSensor : ISensor
    {
        public bool Enabled { get; set; } = true;
        public uint Id { get; private set; }
        public SensorDefinition Definition { get; private set; }
        public Action<object[]> OnDetection { get; set; } = null;
        public Vector3D Position { get; set; } = Vector3D.Zero;
        public Vector3D Direction { get; set; } = Vector3D.Forward;
        public double Aperture { get; set; }
        public double CountermeasureNoise { get; set; } = 0;

        public AntennaSensor(SensorDefinition definition)
        {
            Id = ServerMain.I.HighestSensorId++;
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

            var track = visibilitySet.Track as GridTrack;
            if (track == null)
                return null;

            double targetAngle = 0;
            if (visibilitySet.BoundingBox.Intersects(new RayD(Position, Direction)) == null)
                targetAngle = Vector3D.Angle(Direction, visibilitySet.ClosestCorner - Position);
            if (targetAngle > Aperture)
                return null;

            Vector3D bearing = track.Position - Position;
            double range = bearing.Normalize();
            double receiverAreaAtAngle = Aperture <= Math.PI && Definition.RadarProperties.AccountForRadarAngle ? Definition.RadarProperties.ReceiverArea * Math.Cos(targetAngle) : Definition.RadarProperties.ReceiverArea;

            const double inherentNoise = 3;
            var sensorSignal = MathUtils.ToDecibels(
                4 * Math.PI * receiverAreaAtAngle * track.CommsVisibility(Position) // antenna range is linearly proportional to power draw and that's silly.
                /
                range * (inherentNoise + CountermeasureNoise)
                );

            if (double.IsNegativeInfinity(sensorSignal) || sensorSignal < 15)
                return null;


            double maxBearingError = Definition.BearingErrorModifier * (1 - MathHelper.Clamp(sensorSignal / Definition.DetectionThreshold, 0, 1));
            bearing = MathUtils.RandomCone(bearing, maxBearingError);

            double maxRangeError = range * Definition.RangeErrorModifier * (1 - MathHelper.Clamp(sensorSignal / Definition.DetectionThreshold, 0, 1));
            range += (2 * MathUtils.Random.NextDouble() - 1) * maxRangeError;

            var iffCodes = IffHelper.GetIffCodes(track.Grid, SensorDefinition.SensorType.Antenna);

            var data = new DetectionInfo
            (
                track,
                this,
                sensorSignal,
                range,
                maxRangeError,
                bearing,
                maxBearingError,
                iffCodes
            );

            OnDetection?.Invoke(ObjectPackager.Package(data));

            return data;
        }
    }
}
