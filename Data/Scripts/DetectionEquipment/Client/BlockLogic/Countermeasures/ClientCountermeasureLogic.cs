using DetectionEquipment.Client.Networking;
using System.Collections.Generic;
using DetectionEquipment.Shared;
using DetectionEquipment.Shared.Definitions;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;

namespace DetectionEquipment.Client.BlockLogic.Countermeasures
{
    internal class ClientCountermeasureLogic : IBlockLogic
    {
        public uint Id;
        public CountermeasureEmitterDefinition Definition;
        public IMyTerminalBlock Block { get; set; }
        public bool IsClosed { get; set; }

        private bool _firing = false;
        public bool Firing
        {
            get
            {
                return _firing;
            }
            set
            {
                _firing = value;
                ClientNetwork.SendToServer(new CountermeasureUpdatePacket(Block.EntityId, this));
            }
        }
        public bool Reloading = false; // Client can't update this

        public ClientCountermeasureLogic(List<uint> ids, List<int> defIds)
        {
            Id = ids[0];
            Definition = DefinitionManager.GetCountermeasureEmitterDefinition(defIds[0]);
        }

        public void Register(IMyTerminalBlock block)
        {
            Block = block;

            new CountermeasureControls().DoOnce();

            if (!MyAPIGateway.Session.IsServer && Definition.ActivePowerDraw > 0)
            {
                var resourceSink = (MyResourceSinkComponent)Block.ResourceSink;
                resourceSink.SetRequiredInputFuncByType(GlobalData.ElectricityId,
                    () => ((IMyFunctionalBlock)Block).Enabled ? (float)Definition.ActivePowerDraw / 1000000 : 0);
                resourceSink.SetMaxRequiredInputByType(GlobalData.ElectricityId, (float)Definition.ActivePowerDraw / 1000000);
                resourceSink.Update();
                ((IMyFunctionalBlock)Block).EnabledChanged += b => resourceSink.Update();
            }
        }

        public void Close()
        {
            
        }

        public void UpdateAfterSimulation()
        {
            
        }

        public void UpdateFromNetwork(BlockLogicUpdatePacket updateData)
        {
            
        }
    }
}
