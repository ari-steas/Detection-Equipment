using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Shared.BlockLogic.GenericControls;
using DetectionEquipment.Shared.Utils;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.Components;
using VRageMath;

namespace DetectionEquipment.Shared.BlockLogic.Search
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ConveyorSorter), false, "DetectionSearchBlock", "DetectionSearchBlock_Small")]
    internal class SearchBlock : ControlBlockBase<IMyConveyorSorter>
    {
        internal HashSet<BlockSensor> ControlledSensors => SearchControls.ActiveSensors[this];
        internal Dictionary<BlockSensor, Vector2> DirectionSigns = new Dictionary<BlockSensor, Vector2>();
        protected override ControlBlockSettingsBase GetSettings => new SearchSettings(this);
        protected override ITerminalControlAdder GetControls => new SearchControls();
        private static readonly Vector2 InvMainAxis = new Vector2(-1, 1), InvOffAxis = new Vector2(1, -1);

        public override void UpdateOnceBeforeFrame()
        {
            if (Block?.CubeGrid?.Physics == null || GlobalData.Killswitch) // ignore projected and other non-physical grids
                return;
            base.UpdateOnceBeforeFrame();
        }

        public override void UpdateAfterSimulation()
        {
            if (!MyAPIGateway.Session.IsServer || !Block.IsWorking || GlobalData.Killswitch)
                return;

            foreach (var sensor in ControlledSensors)
            {
                bool aziFaster = sensor.Definition.Movement.AzimuthRate > sensor.Definition.Movement.ElevationRate;
                double desiredAzimuth = sensor.DesiredAzimuth;
                double desiredElevation = sensor.DesiredElevation;

                if (aziFaster)
                {
                    SearchRotate(
                        sensor,
                        ref desiredAzimuth,
                        sensor.Definition.Movement.MinAzimuth,
                        sensor.Definition.Movement.MaxAzimuth,
                        ref desiredElevation,
                        sensor.Definition.Movement.MinElevation,
                        sensor.Definition.Movement.MaxElevation
                    );
                }
                else
                {
                    SearchRotate(
                        sensor,
                        ref desiredElevation,
                        sensor.Definition.Movement.MinElevation,
                        sensor.Definition.Movement.MaxElevation,
                        ref desiredAzimuth,
                        sensor.Definition.Movement.MinAzimuth,
                        sensor.Definition.Movement.MaxAzimuth
                    );
                }

                sensor.DesiredAzimuth = desiredAzimuth;
                sensor.DesiredElevation = desiredElevation;
            }
        }

        private void SearchRotate(BlockSensor sensor, ref double mainAxisDesired, double mainAxisMin, double mainAxisMax, ref double offAxisDesired, double offAxisMin, double offAxisMax)
        {
            bool mainCanRotateFull = mainAxisDesired >= Math.PI && mainAxisDesired <= -Math.PI;
            bool offCanRotateFull = offAxisDesired >= Math.PI && offAxisDesired <= -Math.PI;
            
            double mainOffset = (mainCanRotateFull ? 1 : DirectionSigns[sensor].X) * sensor.Sensor.Aperture / 2;
            double offOffset = (offCanRotateFull ? 1 : DirectionSigns[sensor].Y) * sensor.Sensor.Aperture / 2;
            
            mainAxisDesired = MathUtils.Clamp(mainOffset + mainAxisDesired, mainAxisMin, mainAxisMax);
            // can't rotate full and outside of bounds
            if (!mainCanRotateFull && (mainAxisDesired <= mainAxisMin || mainAxisDesired >= mainAxisMax))
                DirectionSigns[sensor] *= InvMainAxis;

            if (mainCanRotateFull || (mainAxisDesired <= mainAxisMin || mainAxisDesired >= mainAxisMax))
            {
                offAxisDesired = MathUtils.Clamp(offOffset + offAxisDesired, offAxisMin, offAxisMax);
                // can't rotate full and outside of bounds
                if (!offCanRotateFull && (offAxisDesired <= offAxisMin || offAxisDesired >= offAxisMax))
                    DirectionSigns[sensor] *= InvOffAxis;
            }
        }
    }
}
