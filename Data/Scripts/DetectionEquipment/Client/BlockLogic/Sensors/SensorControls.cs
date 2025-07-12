using System;
using DetectionEquipment.Shared.BlockLogic.GenericControls;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System.Linq;
using VRage.Utils;
using VRageMath;

namespace DetectionEquipment.Client.BlockLogic.Sensors
{
    internal class SensorControls : TerminalControlAdder<IMyCameraBlock>
    {
        private IMyTerminalControlSlider _apeSlider, _aziSlider, _eleSlider, _minAziSlider, _maxAziSlider, _minEleSlider, _maxEleSlider;

        protected override Func<IMyTerminalBlock, bool> VisibleFunc => block => block.GetLogic<ClientSensorLogic>() != null;
        public override string IdPrefix => "SensorControls_";

        private static bool HasMovement(IMyTerminalBlock block) =>
            block.GetLogic<ClientSensorLogic>().CurrentDefinition.Movement != null;

        protected override void CreateTerminalActions()
        {
            CreateAction(
                "IncAzimuth",
                "Increase Azimuth",
                b =>
                {
                    var logic = b.GetLogic<ClientSensorLogic>();
                    logic.CurrentDesiredAzimuth = (float) MathHelper.Clamp(logic.CurrentDesiredAzimuth + 0.0175, logic.CurrentDefinition.Movement.MinAzimuth, logic.CurrentDefinition.Movement.MaxAzimuth);
                },
                (b, sb) => sb.Append($"AZ  {MathHelper.ToDegrees(b.GetLogic<ClientSensorLogic>().CurrentDesiredAzimuth):N0}°"),
                @"Textures\GUI\Icons\Actions\Increase.dds"
                ).Enabled = HasMovement;
            CreateAction(
                "DecAzimuth",
                "Decrease Azimuth",
                b =>
                {
                    var logic = b.GetLogic<ClientSensorLogic>();
                    logic.CurrentDesiredAzimuth = (float) MathHelper.Clamp(logic.CurrentDesiredAzimuth - 0.0175, logic.CurrentDefinition.Movement.MinAzimuth, logic.CurrentDefinition.Movement.MaxAzimuth);
                },
                (b, sb) => sb.Append($"AZ  {MathHelper.ToDegrees(b.GetLogic<ClientSensorLogic>().CurrentDesiredAzimuth):N0}°"),
                @"Textures\GUI\Icons\Actions\Decrease.dds"
            ).Enabled = HasMovement;

            CreateAction(
                "IncElevation",
                "Increase Elevation",
                b =>
                {
                    var logic = b.GetLogic<ClientSensorLogic>();
                    logic.CurrentDesiredElevation = (float) MathHelper.Clamp(logic.CurrentDesiredElevation + 0.0175, logic.CurrentDefinition.Movement.MinElevation, logic.CurrentDefinition.Movement.MaxElevation);
                },
                (b, sb) => sb.Append($"EV  {MathHelper.ToDegrees(b.GetLogic<ClientSensorLogic>().CurrentDesiredElevation):N0}°"),
                @"Textures\GUI\Icons\Actions\Increase.dds"
            ).Enabled = HasMovement;
            CreateAction(
                "DecElevation",
                "Decrease Elevation",
                b =>
                {
                    var logic = b.GetLogic<ClientSensorLogic>();
                    logic.CurrentDesiredElevation = (float) MathHelper.Clamp(logic.CurrentDesiredElevation - 0.0175, logic.CurrentDefinition.Movement.MinElevation, logic.CurrentDefinition.Movement.MaxElevation);
                },
                (b, sb) => sb.Append($"EV  {MathHelper.ToDegrees(b.GetLogic<ClientSensorLogic>().CurrentDesiredElevation):N0}°"),
                @"Textures\GUI\Icons\Actions\Decrease.dds"
            ).Enabled = HasMovement;

            CreateAction(
                "IncAperture",
                "Increase Aperture",
                b =>
                {
                    var logic = b.GetLogic<ClientSensorLogic>();
                    logic.CurrentAperture = (float) MathHelper.Clamp(logic.CurrentAperture + 0.0175, logic.CurrentDefinition.MinAperture, logic.CurrentDefinition.MaxAperture);
                },
                (b, sb) => sb.Append($"AP  {MathHelper.ToDegrees(b.GetLogic<ClientSensorLogic>().CurrentAperture):N0}°"),
                @"Textures\GUI\Icons\Actions\Increase.dds"
            ).Enabled = b => b.GetLogic<ClientSensorLogic>()?.CurrentDefinition != null;
            CreateAction(
                "DecAperture",
                "Decrease Aperture",
                b =>
                {
                    var logic = b.GetLogic<ClientSensorLogic>();
                    logic.CurrentAperture = (float) MathHelper.Clamp(logic.CurrentAperture - 0.0175, logic.CurrentDefinition.MinAperture, logic.CurrentDefinition.MaxAperture);
                },
                (b, sb) => sb.Append($"AP  {MathHelper.ToDegrees(b.GetLogic<ClientSensorLogic>().CurrentAperture):N0}°"),
                @"Textures\GUI\Icons\Actions\Decrease.dds"
            ).Enabled = b => b.GetLogic<ClientSensorLogic>()?.CurrentDefinition != null;
        }

        protected override void CreateTerminalProperties()
        {
            var currentSensorSet = CreateListbox(
                "CurrentSensor",
                "Current Sensor",
                "Sensor to display controls for",
                false,
                (block, content, selected) =>
                {
                    var logic = block.GetLogic<ClientSensorLogic>();
                    if (logic == null)
                        return;

                    foreach (var sensor in logic.Sensors.Values)
                    {
                        var item = new VRage.ModAPI.MyTerminalControlListBoxItem(
                            MyStringId.GetOrCompute(
                                string.IsNullOrEmpty(sensor.Definition.TerminalName) ?
                                sensor.Definition.Type.ToString() :
                                sensor.Definition.TerminalName
                            ), MyStringId.GetOrCompute(""), sensor.Id);
                        content.Add(item);
                        if (sensor.Id == logic.CurrentSensorId)
                            selected.Add(item);
                    }
                },
                (block, selected) =>
                {
                    var logic = block.GetLogic<ClientSensorLogic>();
                    if (logic == null)
                        return;
                    uint selectedId = (uint)selected[0].UserData;
                    if (logic.Sensors.Values.Any(b => b.Id == selectedId))
                        logic.CurrentSensorId = selectedId;
                    _apeSlider.UpdateVisual();
                    _aziSlider.UpdateVisual();
                    _eleSlider.UpdateVisual();
                }
                );
            currentSensorSet.VisibleRowsCount = 4;
            currentSensorSet.Visible = b => (b.GetLogic<ClientSensorLogic>()?.Sensors.Count ?? 0) > 1;
            currentSensorSet.SupportsMultipleBlocks = false;

            _apeSlider = CreateSlider(
                "Aperture",
                "Aperture",
                "Sensor aperture in degrees",
                0,
                360,
                b => MathHelper.ToDegrees(b.GetLogic<ClientSensorLogic>().CurrentAperture),
                (b, v) =>
                {
                    var logic = b.GetLogic<ClientSensorLogic>();
                    logic.CurrentAperture = (float)MathHelper.Clamp(MathHelper.ToRadians(v), logic.CurrentDefinition.MinAperture, logic.CurrentDefinition.MaxAperture);
                },
                (b, sb) => sb.Append(MathHelper.ToDegrees(b.GetLogic<ClientSensorLogic>().CurrentAperture).ToString("F1") + "°")
                );
            _apeSlider.SetLimits(
                    b => (float)MathHelper.ToDegrees(b.GetLogic<ClientSensorLogic>().CurrentDefinition.MinAperture),
                    b => (float)MathHelper.ToDegrees(b.GetLogic<ClientSensorLogic>().CurrentDefinition.MaxAperture)
                );
            _apeSlider.Enabled = b =>
            {
                var def = b.GetLogic<ClientSensorLogic>().CurrentDefinition;
                if (def == null)
                    return false;
                return def.MinAperture != def.MaxAperture;
            };

            CreateToggle(
                "AllowMechanicalControl",
                "Allow Automatic Control",
                "If disabled, prevents tracker and searcher blocks from controlling this sensor.",
                b => b.GetLogic<ClientSensorLogic>().CurrentAllowMechanicalControl,
                (b, v) => b.GetLogic<ClientSensorLogic>().CurrentAllowMechanicalControl = v
            ).Enabled = HasMovement;

            #region Azimuth

            _aziSlider = CreateSlider(
                "Azimuth",
                "Desired Azimuth",
                "Sensor azimuth in degrees",
                0,
                360,
                b => MathHelper.ToDegrees(b.GetLogic<ClientSensorLogic>().CurrentDesiredAzimuth),
                (b, v) =>
                {
                    var logic = b.GetLogic<ClientSensorLogic>();
                    if (logic.CurrentDefinition.Movement == null)
                        return;
                    logic.CurrentDesiredAzimuth = (float)MathHelper.Clamp(MathHelper.ToRadians(v), logic.CurrentDefinition.Movement.MinAzimuth, logic.CurrentDefinition.Movement.MaxAzimuth);
                },
                (b, sb) => sb.Append(MathHelper.ToDegrees(b.GetLogic<ClientSensorLogic>().CurrentDesiredAzimuth).ToString("F1") + "°")
                );
            _aziSlider.SetLimits(
                    b => (float)MathHelper.ToDegrees(b.GetLogic<ClientSensorLogic>().CurrentDefinition.Movement?.MinAzimuth ?? 0),
                    b => (float)MathHelper.ToDegrees(b.GetLogic<ClientSensorLogic>().CurrentDefinition.Movement?.MaxAzimuth ?? 0)
                );
            _aziSlider.Enabled = HasMovement;

            _minAziSlider = CreateSlider(
                "MinAzimuth",
                "Minimum Azimuth",
                "Minimum sensor azimuth in degrees",
                0,
                360,
                b => MathHelper.ToDegrees(b.GetLogic<ClientSensorLogic>().CurrentMinAzimuth),
                (b, v) =>
                {
                    var logic = b.GetLogic<ClientSensorLogic>();
                    if (logic.CurrentDefinition.Movement == null)
                        return;
                    logic.CurrentMinAzimuth = (float)MathHelper.Clamp(MathHelper.ToRadians(v), logic.CurrentDefinition.Movement.MinAzimuth, logic.CurrentDefinition.Movement.MaxAzimuth);
                    _aziSlider.UpdateVisual();
                    _maxAziSlider.UpdateVisual();
                },
                (b, sb) => sb.Append(MathHelper.ToDegrees(b.GetLogic<ClientSensorLogic>().CurrentMinAzimuth).ToString("F1") + "°")
            );
            _minAziSlider.SetLimits(
                b => (float)MathHelper.ToDegrees(b.GetLogic<ClientSensorLogic>().CurrentDefinition.Movement?.MinAzimuth ?? 0),
                b => (float)MathHelper.ToDegrees(b.GetLogic<ClientSensorLogic>().CurrentDefinition.Movement?.MaxAzimuth ?? 0)
            );
            _minAziSlider.Enabled = HasMovement;

            _maxAziSlider = CreateSlider(
                "MaxAzimuth",
                "Maximum Azimuth",
                "Maximum sensor azimuth in degrees",
                0,
                360,
                b => MathHelper.ToDegrees(b.GetLogic<ClientSensorLogic>().CurrentMaxAzimuth),
                (b, v) =>
                {
                    var logic = b.GetLogic<ClientSensorLogic>();
                    if (logic.CurrentDefinition.Movement == null)
                        return;
                    logic.CurrentMaxAzimuth = (float)MathHelper.Clamp(MathHelper.ToRadians(v), logic.CurrentDefinition.Movement.MinAzimuth, logic.CurrentDefinition.Movement.MaxAzimuth);
                    _aziSlider.UpdateVisual();
                    _minAziSlider.UpdateVisual();
                },
                (b, sb) => sb.Append(MathHelper.ToDegrees(b.GetLogic<ClientSensorLogic>().CurrentMaxAzimuth).ToString("F1") + "°")
            );
            _maxAziSlider.SetLimits(
                b => (float)MathHelper.ToDegrees(b.GetLogic<ClientSensorLogic>().CurrentDefinition.Movement?.MinAzimuth ?? 0),
                b => (float)MathHelper.ToDegrees(b.GetLogic<ClientSensorLogic>().CurrentDefinition.Movement?.MaxAzimuth ?? 0)
            );
            _maxAziSlider.Enabled = HasMovement;

            #endregion

            #region Elevation

            _eleSlider = CreateSlider(
                "Elevation",
                "Desired Elevation",
                "Sensor elevation in degrees",
                0,
                360,
                b => MathHelper.ToDegrees(b.GetLogic<ClientSensorLogic>().CurrentDesiredElevation),
                (b, v) =>
                {
                    var logic = b.GetLogic<ClientSensorLogic>();
                    if (logic.CurrentDefinition.Movement == null)
                        return;
                    logic.CurrentDesiredElevation = (float)MathHelper.Clamp(MathHelper.ToRadians(v), logic.CurrentDefinition.Movement.MinElevation, logic.CurrentDefinition.Movement.MaxElevation);
                },
                (b, sb) => sb.Append(MathHelper.ToDegrees(b.GetLogic<ClientSensorLogic>().CurrentDesiredElevation).ToString("F1") + "°")
                );
            _eleSlider.SetLimits(
                    b => (float)MathHelper.ToDegrees(b.GetLogic<ClientSensorLogic>().CurrentDefinition.Movement?.MinElevation ?? 0),
                    b => (float)MathHelper.ToDegrees(b.GetLogic<ClientSensorLogic>().CurrentDefinition.Movement?.MaxElevation ?? 0)
                );
            _eleSlider.Enabled = HasMovement;

            _minEleSlider = CreateSlider(
                "MinElevation",
                "Minimum Elevation",
                "Minimum sensor elevation in degrees",
                0,
                360,
                b => MathHelper.ToDegrees(b.GetLogic<ClientSensorLogic>().CurrentMinElevation),
                (b, v) =>
                {
                    var logic = b.GetLogic<ClientSensorLogic>();
                    if (logic.CurrentDefinition.Movement == null)
                        return;
                    logic.CurrentMinElevation = (float)MathHelper.Clamp(MathHelper.ToRadians(v), logic.CurrentDefinition.Movement.MinElevation, logic.CurrentDefinition.Movement.MaxElevation);
                    _eleSlider.UpdateVisual();
                    _maxEleSlider.UpdateVisual();
                },
                (b, sb) => sb.Append(MathHelper.ToDegrees(b.GetLogic<ClientSensorLogic>().CurrentMinElevation).ToString("F1") + "°")
            );
            _minEleSlider.SetLimits(
                b => (float)MathHelper.ToDegrees(b.GetLogic<ClientSensorLogic>().CurrentDefinition.Movement?.MinElevation ?? 0),
                b => (float)MathHelper.ToDegrees(b.GetLogic<ClientSensorLogic>().CurrentDefinition.Movement?.MaxElevation ?? 0)
            );
            _minEleSlider.Enabled = HasMovement;

            _maxEleSlider = CreateSlider(
                "MaxElevation",
                "Maximum Elevation",
                "Maximum sensor elevation in degrees",
                0,
                360,
                b => MathHelper.ToDegrees(b.GetLogic<ClientSensorLogic>().CurrentMaxElevation),
                (b, v) =>
                {
                    var logic = b.GetLogic<ClientSensorLogic>();
                    if (logic.CurrentDefinition.Movement == null)
                        return;
                    logic.CurrentMaxElevation = (float)MathHelper.Clamp(MathHelper.ToRadians(v), logic.CurrentDefinition.Movement.MinElevation, logic.CurrentDefinition.Movement.MaxElevation);
                    _eleSlider.UpdateVisual();
                    _minEleSlider.UpdateVisual();
                },
                (b, sb) => sb.Append(MathHelper.ToDegrees(b.GetLogic<ClientSensorLogic>().CurrentMaxElevation).ToString("F1") + "°")
            );
            _maxEleSlider.SetLimits(
                b => (float)MathHelper.ToDegrees(b.GetLogic<ClientSensorLogic>().CurrentDefinition.Movement?.MinElevation ?? 0),
                b => (float)MathHelper.ToDegrees(b.GetLogic<ClientSensorLogic>().CurrentDefinition.Movement?.MaxElevation ?? 0)
            );
            _maxEleSlider.Enabled = HasMovement;

            #endregion
        }
    }
}
