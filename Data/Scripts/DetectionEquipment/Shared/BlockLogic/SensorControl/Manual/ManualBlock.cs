using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Shared.BlockLogic.GenericControls;
using DetectionEquipment.Shared.Networking;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Game.ModAPI;
using VRageMath;

namespace DetectionEquipment.Shared.BlockLogic.SensorControl.Manual
{
    internal class ManualBlock : SensorControlBlockBase<IMyConveyorSorter>
    {
        internal override HashSet<BlockSensor> ControlledSensors => ManualControls.ActiveSensors[this];
        public HashSet<IMyShipController> ShipControllers => ManualControls.ShipControllers[this];


        public SimpleSync<bool> ParallaxAccount = new SimpleSync<bool>(false);

        protected override ControlBlockSettingsBase GetSettings => new ManualSettings(this);
        protected override ITerminalControlAdder GetControls => new ManualControls();

        public ManualBlock(IMyFunctionalBlock block) : base(block)
        {
        }

        public override void Init()
        {
            if (Block?.CubeGrid?.Physics == null) // ignore projected and other non-physical grids
                return;
            ParallaxAccount.Component = this;
            base.Init();
        }

        public override void UpdateAfterSimulation()
        {
            IMyShipController thisController;
            if (!Block.IsWorking || !GetController(out thisController))
                return;

            // TODO update frequency
            MyAPIGateway.Utilities.ShowNotification($"{Block.CustomName}: Controlled by {thisController.Pilot.DisplayName}", 1000/60);

            if (!MyAPIGateway.Utilities.IsDedicated)
                UpdateClient(thisController);

            if (MyAPIGateway.Session.IsServer)
                UpdateServer(thisController);
        }

        private bool GetController(out IMyShipController controller)
        {
            foreach (var pContr in ShipControllers)
            {
                if (pContr.Pilot != null && pContr.Pilot.IsPlayer)
                {
                    controller = pContr;
                    return true;
                }
            }

            controller = null;
            return false;
        }

        private void UpdateServer(IMyShipController controller)
        {
            Vector3D aimDir = MyAPIGateway.Session.Camera.WorldMatrix.Forward;
            Vector3D aimFrom = MyAPIGateway.Session.Camera.Position;

            Vector3D aimpoint;

            if (ParallaxAccount.Value)
            {
                // start ray from outside the ship's bounding box
                double dMin, dMax;
                RayD aimLine = new RayD(aimFrom, aimDir);
                controller.CubeGrid.WorldAABB.Intersect(ref aimLine, out dMin, out dMax);

                IHitInfo hitInfo;
                if (MyAPIGateway.Physics.CastLongRay(aimFrom + aimDir * (dMax + 25), aimFrom + aimDir * MyAPIGateway.Session.SessionSettings.ViewDistance, out hitInfo, false))
                    aimpoint = hitInfo.Position;
                else
                    aimpoint = aimFrom + aimDir * 100000;
            }
            else
            {
                aimpoint = aimFrom + aimDir * 100000;
            }

            DebugDraw.AddLine(controller.GetPosition(), aimpoint, Color.HotPink, 1/60f);
            DebugDraw.AddPoint(aimpoint, Color.HotPink, 0);

            foreach (var sensor in ControlledSensors)
            {
                if (!(sensor.AllowMechanicalControl ^ InvertAllowControl.Value) || !sensor.TryTakeControl(this))
                    continue;

                sensor.AimAt(aimpoint);
            }
        }

        private void UpdateClient(IMyShipController controller)
        {

        }
    }
}
