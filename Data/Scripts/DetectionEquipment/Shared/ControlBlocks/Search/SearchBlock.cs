using DetectionEquipment.Server;
using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Shared.ControlBlocks.Tracker;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.Components;
using VRage.Game.ModAPI.Network;
using VRage.Sync;
using VRageMath;

namespace DetectionEquipment.Shared.ControlBlocks.Search
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ConveyorSorter), false, "DetectionSearchBlock")]
    internal class SearchBlock : ControlBlockBase
    {
        public MySync<long[], SyncDirection.BothWays> ActiveSensors;
        internal List<BlockSensor> ControlledSensors = new List<BlockSensor>();
        internal Dictionary<BlockSensor, Vector2> DirectionSigns = new Dictionary<BlockSensor, Vector2>();

        private static readonly Vector2 InvAzi = new Vector2(-1, 1), InvElev = new Vector2(1, -1);

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            if (Block?.CubeGrid?.Physics == null) // ignore projected and other non-physical grids
                return;

            ActiveSensors.Value = Array.Empty<long>();
            ActiveSensors.ValueChanged += sync =>
            {
                ControlledSensors.Clear();
                DirectionSigns.Clear();
                foreach (var sensor in GridSensors.Sensors)
                {
                    for (int i = 0; i < sync.Value.Length; i++)
                    {
                        if (sensor.Block.EntityId != sync.Value[i])
                            continue;
                        ControlledSensors.Add(sensor);
                        DirectionSigns[sensor] = Vector2I.One;
                        break;
                    }
                };
            };

            new SearchControls().DoOnce();
        }

        public override void UpdateAfterSimulation()
        {
            foreach (var sensor in ControlledSensors)
            {
                if (sensor?.Definition.Movement == null)
                    continue;

                var moveDef = sensor.Definition.Movement;

                bool aziFaster = moveDef.AzimuthRate > moveDef.ElevationRate;
                float aziDirection = moveDef.CanRotateFull ? 1 : DirectionSigns[sensor].X;
                float elevDirection = moveDef.CanElevateFull ? 1 : DirectionSigns[sensor].Y;

                double maxAziRate = moveDef.AzimuthRate * ((moveDef.MaxElevation - moveDef.MinElevation) / moveDef.ElevationRate);
                double maxElevRate = moveDef.ElevationRate * ((moveDef.MaxAzimuth - moveDef.MinAzimuth) / moveDef.AzimuthRate);

                if (aziFaster)
                {
                    if (Math.Abs(sensor.Azimuth - sensor.DesiredAzimuth) <= moveDef.AzimuthRate / 60)
                    {
                        sensor.DesiredAzimuth += MathUtils.ClampAbs(aziDirection * sensor.Sensor.Aperture / 2, moveDef.AzimuthRate);
                        if (!moveDef.CanRotateFull && (sensor.DesiredAzimuth <= moveDef.MinAzimuth || sensor.DesiredAzimuth >= moveDef.MaxAzimuth))
                        {
                            DirectionSigns[sensor] *= InvAzi;

                            sensor.DesiredElevation += MathUtils.ClampAbs(elevDirection * sensor.Sensor.Aperture / 2, maxElevRate);
                            if (!moveDef.CanElevateFull && (sensor.DesiredElevation <= moveDef.MinElevation || sensor.DesiredElevation >= moveDef.MaxElevation))
                                DirectionSigns[sensor] *= InvElev;
                        }
                    }
                }
                else
                {
                    if (Math.Abs(sensor.Elevation - sensor.DesiredElevation) <= moveDef.ElevationRate / 60)
                    {
                        sensor.DesiredElevation += MathUtils.ClampAbs(elevDirection * sensor.Sensor.Aperture / 2, moveDef.ElevationRate);
                        if (!moveDef.CanElevateFull && (sensor.DesiredElevation <= moveDef.MinElevation || sensor.DesiredElevation >= moveDef.MaxElevation))
                        {
                            DirectionSigns[sensor] *= InvElev;

                            sensor.DesiredAzimuth += MathUtils.ClampAbs(aziDirection * sensor.Sensor.Aperture / 2, maxAziRate);
                            if (!moveDef.CanRotateFull && (sensor.DesiredAzimuth <= moveDef.MinAzimuth || sensor.DesiredAzimuth >= moveDef.MaxAzimuth))
                                DirectionSigns[sensor] *= InvAzi;
                        }
                    }
                }
            }
        }
    }
}
