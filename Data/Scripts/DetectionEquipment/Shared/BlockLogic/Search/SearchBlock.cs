using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Shared.BlockLogic.GenericControls;
using DetectionEquipment.Shared.Networking;
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

        public SimpleSync<SearchModes> SearchMode = new SimpleSync<SearchModes>(SearchModes.Auto);
        public SimpleSync<bool> InvertAllowControl = new SimpleSync<bool>(false);

        protected override ControlBlockSettingsBase GetSettings => new SearchSettings(this);
        protected override ITerminalControlAdder GetControls => new SearchControls();
        private static readonly Vector2 InvMainAxis = new Vector2(-1, 1), InvOffAxis = new Vector2(1, -1);

        public override void UpdateOnceBeforeFrame()
        {
            if (Block?.CubeGrid?.Physics == null || GlobalData.Killswitch) // ignore projected and other non-physical grids
                return;

            SearchMode.Component = this;
            InvertAllowControl.Component = this;

            base.UpdateOnceBeforeFrame();
        }

        public override void UpdateAfterSimulation()
        {
            if (!MyAPIGateway.Session.IsServer || !Block.IsWorking || GlobalData.Killswitch)
                return;

            foreach (var sensor in ControlledSensors)
            {
                if (!(sensor.AllowMechanicalControl ^ InvertAllowControl.Value))
                    continue;

                var autoMode = sensor.Definition.Movement.AzimuthRate > sensor.Definition.Movement.ElevationRate ? SearchModes.AziFirst : SearchModes.ElevFirst;
                double desiredAzimuth = sensor.DesiredAzimuth;
                double desiredElevation = sensor.DesiredElevation;

                switch (SearchMode.Value == SearchModes.Auto ? autoMode : SearchMode.Value)
                {
                    case SearchModes.AziFirst:
                        SearchRotate(
                            sensor,
                            ref desiredAzimuth,
                            sensor.Azimuth,
                            sensor.MinAzimuth,
                            sensor.MaxAzimuth,
                            ref desiredElevation,
                            sensor.Elevation,
                            sensor.MinElevation,
                            sensor.MaxElevation
                        );
                        break;
                    case SearchModes.ElevFirst:
                        SearchRotate(
                            sensor,
                            ref desiredElevation,
                            sensor.Elevation,
                            sensor.MinElevation,
                            sensor.MaxElevation,
                            ref desiredAzimuth,
                            sensor.Azimuth,
                            sensor.MinAzimuth,
                            sensor.MaxAzimuth
                        );
                        break;
                    case SearchModes.AziOnly:
                        SearchRotate(
                            sensor,
                            ref desiredAzimuth,
                            sensor.Azimuth,
                            sensor.MinAzimuth,
                            sensor.MaxAzimuth
                        );
                        break;
                    case SearchModes.ElevOnly:
                        SearchRotate(
                            sensor,
                            ref desiredElevation,
                            sensor.Elevation,
                            sensor.MinElevation,
                            sensor.MaxElevation
                        );
                        break;
                    case SearchModes.Auto:
                    default:
                        // this should never happen
                        break;
                }

                sensor.DesiredAzimuth = desiredAzimuth;
                sensor.DesiredElevation = desiredElevation;
            }
        }

        private void SearchRotate(BlockSensor sensor, ref double mainAxisDesired, double mainAxisCurrent, double mainAxisMin, double mainAxisMax, ref double offAxisDesired, double offAxisCurrent, double offAxisMin, double offAxisMax)
        {
            bool mainCanRotateFull = mainAxisMax >= Math.PI && mainAxisMin <= -Math.PI;
            bool offCanRotateFull = offAxisMax >= Math.PI && offAxisMin <= -Math.PI;
            
            double mainOffset = (mainCanRotateFull ? 1 : DirectionSigns[sensor].X) * sensor.Sensor.Aperture / 2;
            double offOffset = (offCanRotateFull ? 1 : DirectionSigns[sensor].Y) * sensor.Sensor.Aperture / 2;
            
            mainAxisDesired = MathUtils.Clamp(mainOffset + mainAxisCurrent, mainAxisMin, mainAxisMax);
            // can't rotate full and outside of bounds
            if (!mainCanRotateFull && (mainAxisDesired <= mainAxisMin || mainAxisDesired >= mainAxisMax))
                DirectionSigns[sensor] *= InvMainAxis;

            if (mainCanRotateFull || (mainAxisDesired <= mainAxisMin || mainAxisDesired >= mainAxisMax))
            {
                offAxisDesired = MathUtils.Clamp(offOffset + offAxisCurrent, offAxisMin, offAxisMax);
                // can't rotate full and outside of bounds
                if (!offCanRotateFull && (offAxisDesired <= offAxisMin || offAxisDesired >= offAxisMax))
                    DirectionSigns[sensor] *= InvOffAxis;
            }
        }

        private void SearchRotate(BlockSensor sensor, ref double mainAxisDesired, double mainAxisCurrent, double mainAxisMin, double mainAxisMax)
        {
            bool mainCanRotateFull = mainAxisMax >= Math.PI && mainAxisMin <= -Math.PI;
            
            double mainOffset = (mainCanRotateFull ? 1 : DirectionSigns[sensor].X) * sensor.Sensor.Aperture / 2;
            
            mainAxisDesired = MathUtils.Clamp(mainOffset + mainAxisCurrent, mainAxisMin, mainAxisMax);
            // can't rotate full and outside of bounds
            if (!mainCanRotateFull && (mainAxisDesired <= mainAxisMin || mainAxisDesired >= mainAxisMax))
                DirectionSigns[sensor] *= InvMainAxis;
        }

        public enum SearchModes
        {
            Auto = 0,
            AziFirst = 1,
            ElevFirst = 2,
            AziOnly = 3,
            ElevOnly = 4,
        }
    }
}
