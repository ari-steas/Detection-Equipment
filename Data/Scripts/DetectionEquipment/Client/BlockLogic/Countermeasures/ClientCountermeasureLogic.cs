using DetectionEquipment.Client.BlockLogic.Sensors;
using DetectionEquipment.Client.Networking;
using DetectionEquipment.Server.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DetectionEquipment.Shared.Definitions;
using VRage.Game.ModAPI;

namespace DetectionEquipment.Client.BlockLogic.Countermeasures
{
    internal class ClientCountermeasureLogic : IBlockLogic
    {
        public uint Id;
        public CountermeasureEmitterDefinition Definition;
        public IMyCubeBlock Block { get; set; }
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

        public void Register(IMyCubeBlock block)
        {
            Block = block;

            new CountermeasureControls().DoOnce();
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
