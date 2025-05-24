using DetectionEquipment.Shared.BlockLogic.GenericControls;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System.Linq;
using VRage.Utils;
using VRageMath;

namespace DetectionEquipment.Client.Sensors
{
    internal class SensorControls : TerminalControlAdder<ClientBlockSensor, IMyCameraBlock>
    {
        private IMyTerminalControlSlider _apeSlider, _aziSlider, _eleSlider;

        protected override void CreateTerminalActions()
        {
            var currentSensorSet = CreateListbox(
                "CurrentSensor",
                "Current Sensor",
                "Sensor to display controls for",
                false,
                (block, content, selected) =>
                {
                    var logic = block.GameLogic.GetAs<ClientBlockSensor>();
                    if (logic == null)
                        return;

                    foreach (var sensor in logic.Sensors.Values)
                    {
                        var item = new VRage.ModAPI.MyTerminalControlListBoxItem(MyStringId.GetOrCompute(sensor.Definition.Type.ToString()), MyStringId.GetOrCompute(""), sensor.Id);
                        content.Add(item);
                        if (sensor.Id == logic.CurrentSensorId)
                            selected.Add(item);
                    }
                },
                (block, selected) =>
                {
                    var logic = block.GameLogic.GetAs<ClientBlockSensor>();
                    if (logic == null)
                        return;
                    uint selectedId = (uint) selected[0].UserData;
                    if (logic.Sensors.Values.Any(b => b.Id == selectedId))
                        logic.CurrentSensorId = selectedId;
                    _apeSlider.UpdateVisual();
                    _aziSlider.UpdateVisual();
                    _eleSlider.UpdateVisual();
                }
                );
            currentSensorSet.VisibleRowsCount = 4;
            currentSensorSet.Visible = b => (b.GameLogic.GetAs<ClientBlockSensor>()?.Sensors.Count ?? 0) > 1;
            currentSensorSet.SupportsMultipleBlocks = false;

            _apeSlider = CreateSlider(
                "Aperture",
                "Aperture",
                "Sensor aperture in degrees",
                0,
                360,
                b => MathHelper.ToDegrees(b.GameLogic.GetAs<ClientBlockSensor>().CurrentAperture),
                (b, v) =>
                {
                    var logic = b.GameLogic.GetAs<ClientBlockSensor>();
                    logic.CurrentAperture = (float)MathHelper.Clamp(MathHelper.ToRadians(v), logic.CurrentDefinition.MinAperture, logic.CurrentDefinition.MaxAperture);
                },
                (b, sb) => sb.Append(MathHelper.ToDegrees(b.GameLogic.GetAs<ClientBlockSensor>().CurrentAperture).ToString("F1") + "°")
                );
            _apeSlider.SetLimits(
                    b => (float)MathHelper.ToDegrees(b.GameLogic.GetAs<ClientBlockSensor>().CurrentDefinition.MinAperture),
                    b => (float)MathHelper.ToDegrees(b.GameLogic.GetAs<ClientBlockSensor>().CurrentDefinition.MaxAperture)
                );

            _aziSlider = CreateSlider(
                "Azimuth",
                "Desired Azimuth",
                "Sensor azimuth in degrees",
                0,
                360,
                b => MathHelper.ToDegrees(b.GameLogic.GetAs<ClientBlockSensor>().CurrentDesiredAzimuth),
                (b, v) =>
                {
                    var logic = b.GameLogic.GetAs<ClientBlockSensor>();
                    if (logic.CurrentDefinition.Movement == null)
                        return;
                    logic.CurrentDesiredAzimuth = (float)MathHelper.Clamp(MathHelper.ToRadians(v), logic.CurrentDefinition.Movement.MinAzimuth, logic.CurrentDefinition.Movement.MaxAzimuth);
                },
                (b, sb) => sb.Append(MathHelper.ToDegrees(b.GameLogic.GetAs<ClientBlockSensor>().CurrentDesiredAzimuth).ToString("F1") + "°")
                );
            _aziSlider.SetLimits(
                    b => (float)MathHelper.ToDegrees(b.GameLogic.GetAs<ClientBlockSensor>().CurrentDefinition.Movement?.MinAzimuth ?? 0),
                    b => (float)MathHelper.ToDegrees(b.GameLogic.GetAs<ClientBlockSensor>().CurrentDefinition.Movement?.MaxAzimuth ?? 0)
                );
            _aziSlider.Enabled = b => b.GameLogic.GetAs<ClientBlockSensor>().CurrentDefinition.Movement != null;

            _eleSlider = CreateSlider(
                "Elevation",
                "Desired Elevation",
                "Sensor elevation in degrees",
                0,
                360,
                b => MathHelper.ToDegrees(b.GameLogic.GetAs<ClientBlockSensor>().CurrentDesiredElevation),
                (b, v) =>
                {
                    var logic = b.GameLogic.GetAs<ClientBlockSensor>();
                    if (logic.CurrentDefinition.Movement == null)
                        return;
                    logic.CurrentDesiredElevation = (float)MathHelper.Clamp(MathHelper.ToRadians(v), logic.CurrentDefinition.Movement.MinElevation, logic.CurrentDefinition.Movement.MaxElevation);
                },
                (b, sb) => sb.Append(MathHelper.ToDegrees(b.GameLogic.GetAs<ClientBlockSensor>().CurrentDesiredElevation).ToString("F1") + "°")
                );
            _eleSlider.SetLimits(
                    b => (float)MathHelper.ToDegrees(b.GameLogic.GetAs<ClientBlockSensor>().CurrentDefinition.Movement?.MinElevation ?? 0),
                    b => (float)MathHelper.ToDegrees(b.GameLogic.GetAs<ClientBlockSensor>().CurrentDefinition.Movement?.MaxElevation ?? 0)
                );
            _eleSlider.Enabled = b => b.GameLogic.GetAs<ClientBlockSensor>().CurrentDefinition.Movement != null;
        }

        protected override void CreateTerminalProperties()
        {

        }
    }
}
