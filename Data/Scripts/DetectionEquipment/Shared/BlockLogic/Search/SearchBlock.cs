using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Shared.Utils;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.Components;
using VRageMath;

namespace DetectionEquipment.Shared.BlockLogic.Search
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ConveyorSorter), false, "DetectionSearchBlock")]
    internal class SearchBlock : ControlBlockBase<IMyConveyorSorter>
    {
        internal HashSet<BlockSensor> ControlledSensors => SearchControls.ActiveSensors[this];
        internal Dictionary<BlockSensor, Vector2> DirectionSigns = new Dictionary<BlockSensor, Vector2>();
        protected override ControlBlockSettingsBase GetSettings => new SearchSettings(this);
        private static readonly Vector2 InvAzi = new Vector2(-1, 1), InvElev = new Vector2(1, -1);

        public override void UpdateOnceBeforeFrame()
        {
            if (Block?.CubeGrid?.Physics == null) // ignore projected and other non-physical grids
                return;
            new SearchControls().DoOnce(this);
            base.UpdateOnceBeforeFrame();
        }

        public override void UpdateAfterSimulation()
        {
            if (!MyAPIGateway.Session.IsServer)
                return;

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
