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

            Vector3D targetBearing = track.Position - Position;
            double targetRange = targetBearing.Normalize();

            double targetAngle = 0;
            if (visibilitySet.BoundingBox.Intersects(new RayD(Position, Direction)) == null)
                targetAngle = Math.Acos(Vector3D.Dot(Direction, targetBearing));

            if (targetAngle > Aperture)
                return null;

            double receiverAreaAtAngle = Aperture <= Math.PI && Definition.RadarProperties.AccountForRadarAngle ? Definition.RadarProperties.ReceiverArea * targetAngle : Definition.RadarProperties.ReceiverArea;

            const double inherentNoise = 3;
            var sensorSignal = MathUtils.ToDecibels(
                4 * Math.PI * receiverAreaAtAngle * track.CommsVisibility(Position) // antenna range is linearly proportional to power draw and that's silly.
                /
                targetRange * (inherentNoise + CountermeasureNoise)
                );

            if (double.IsNegativeInfinity(sensorSignal) || sensorSignal < 15)
                return null;

            double trackCrossSection = sensorSignal;
            double trackRange = targetRange;
            double maxRangeError = targetRange * Definition.RangeErrorModifier * (1 - MathHelper.Clamp(sensorSignal / Definition.DetectionThreshold, 0, 1));
            Vector3D trackBearing = targetBearing;
            double maxBearingError = Definition.BearingErrorModifier * (1 - MathHelper.Clamp(sensorSignal / Definition.DetectionThreshold, 0, 1));
            var iffCodes = IffHelper.GetIffCodes(track.Grid, SensorDefinition.SensorType.Antenna);

            CountermeasureManager.ApplyDrfm(this, track, ref trackCrossSection, ref trackRange, ref maxRangeError, ref trackBearing, ref maxBearingError, ref iffCodes);

            trackBearing = MathUtils.RandomCone(trackBearing, maxBearingError);
            trackRange += (2 * MathUtils.Random.NextDouble() - 1) * maxRangeError;

            var data = new DetectionInfo
            (
                track,
                this,
                trackCrossSection,
                trackRange,
                maxRangeError,
                trackBearing,
                maxBearingError,
                iffCodes
            );

            OnDetection?.Invoke(ObjectPackager.Package(data));

            return data;
        }
    }
}
