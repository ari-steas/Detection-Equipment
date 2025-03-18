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
        protected override void CreateTerminalActions()
        {
            IMyTerminalControlSlider apeSlider = null, aziSlider = null, eleSlider = null;

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
                    apeSlider.UpdateVisual();
                    aziSlider.UpdateVisual();
                    eleSlider.UpdateVisual();
                }
                );
            currentSensorSet.VisibleRowsCount = 4;
            currentSensorSet.Visible = b => b.GameLogic.GetAs<ClientBlockSensor>().Sensors.Count > 1;
            currentSensorSet.SupportsMultipleBlocks = false;

            apeSlider = CreateSlider(
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
            apeSlider.SetLimits(
                    b => (float)MathHelper.ToDegrees(b.GameLogic.GetAs<ClientBlockSensor>().CurrentDefinition.MinAperture),
                    b => (float)MathHelper.ToDegrees(b.GameLogic.GetAs<ClientBlockSensor>().CurrentDefinition.MaxAperture)
                );

            aziSlider = CreateSlider(
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
            aziSlider.SetLimits(
                    b => (float)MathHelper.ToDegrees(b.GameLogic.GetAs<ClientBlockSensor>().CurrentDefinition.Movement?.MinAzimuth ?? 0),
                    b => (float)MathHelper.ToDegrees(b.GameLogic.GetAs<ClientBlockSensor>().CurrentDefinition.Movement?.MaxAzimuth ?? 0)
                );
            aziSlider.Enabled = b => b.GameLogic.GetAs<ClientBlockSensor>().CurrentDefinition.Movement != null;

            eleSlider = CreateSlider(
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
            eleSlider.SetLimits(
                    b => (float)MathHelper.ToDegrees(b.GameLogic.GetAs<ClientBlockSensor>().CurrentDefinition.Movement?.MinElevation ?? 0),
                    b => (float)MathHelper.ToDegrees(b.GameLogic.GetAs<ClientBlockSensor>().CurrentDefinition.Movement?.MaxElevation ?? 0)
                );
            eleSlider.Enabled = b => b.GameLogic.GetAs<ClientBlockSensor>().CurrentDefinition.Movement != null;
        }

        protected override void CreateTerminalProperties()
        {

        }
    }
}
