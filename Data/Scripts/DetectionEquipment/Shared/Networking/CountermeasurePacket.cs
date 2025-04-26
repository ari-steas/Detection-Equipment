using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;
using VRageMath;

namespace DetectionEquipment.Shared.Networking
{
    /// <summary>
    /// Network data used to spawn a countermeasure clientside. Only contains visual data.
    /// </summary>
    [ProtoContract]
    internal class CountermeasurePacket : PacketBase
    {
        [ProtoMember(1)] public int DefinitionId;
        public Vector3D Position => new Vector3D(_posx, _posy, _posz);
        public Vector3 Direction => new Vector3(_dirx, _diry, _dirz);
        public Vector3 Velocity => new Vector3(_velx, _vely, _velz);

        // We save a bit of network load by inlining classes like Vector3 and by using floats where possible.
        [ProtoMember(2)] private double _posx;
        [ProtoMember(3)] private double _posy;
        [ProtoMember(4)] private double _posz;
        [ProtoMember(5)] private float _dirx;
        [ProtoMember(6)] private float _diry;
        [ProtoMember(7)] private float _dirz;
        [ProtoMember(8)] private float _velx;
        [ProtoMember(9)] private float _vely;
        [ProtoMember(10)] private float _velz;

        public CountermeasurePacket(Server.Countermeasures.Countermeasure countermeasure)
        {
            DefinitionId = countermeasure.Definition.Id;

            _posx = countermeasure.Position.X;
            _posy = countermeasure.Position.Y;
            _posz = countermeasure.Position.Z;
            _dirx = (float) countermeasure.Direction.X;
            _diry = (float) countermeasure.Direction.Y;
            _dirz = (float) countermeasure.Direction.Z;
            _velx = (float) countermeasure.Velocity.X;
            _vely = (float) countermeasure.Velocity.Y;
            _velz = (float) countermeasure.Velocity.Z;
        }

        public override void Received(ulong senderSteamId, bool fromServer)
        {
            if (!fromServer)
                return;
            Client.Countermeasures.CountermeasureManager.RegisterNew(new Client.Countermeasures.Countermeasure(this));
        }
    }
}
