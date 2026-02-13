using System;
using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Shared.BlockLogic.GenericControls;
using DetectionEquipment.Shared.Networking;
using Sandbox.ModAPI;
using System.Collections.Generic;
using DetectionEquipment.Shared.BlockLogic.Aggregator;
using DetectionEquipment.Shared.Structs;
using DetectionEquipment.Shared.Utils;
using VRage.Game.ModAPI;
using VRageMath;

namespace DetectionEquipment.Shared.BlockLogic.SensorControl.Manual
{
    internal class ManualBlock : SensorControlBlockBase<IMyConveyorSorter>
    {
        private const int UpdateInterval = 2; // update 30fps

        internal override HashSet<BlockSensor> ControlledSensors => ManualControls.ActiveSensors[this];
        public HashSet<IMyShipController> ShipControllers => ManualControls.ShipControllers[this];
        public AggregatorBlock Aggregator => ManualControls.ActiveAggregators[this];


        public SimpleSync<bool> ParallaxAccount = new SimpleSync<bool>(false);
        public SimpleSync<Vector3D> RelativeAimpoint = new SimpleSync<Vector3D>(Vector3D.Zero);
        public SimpleSync<long> LockedTarget = new SimpleSync<long>(long.MinValue);
        public SimpleSync<long> Controller = new SimpleSync<long>(long.MinValue);

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
            RelativeAimpoint.Component = this;
            LockedTarget.Component = this;
            if (MyAPIGateway.Session.IsServer)
                LockedTarget.Validate = OnLockedTargetChanged;
            Controller.Component = this;
            base.Init();
        }

        public override void UpdateAfterSimulation()
        {
            IMyShipController thisController;
            if (!Block.IsWorking || !GetController(out thisController))
                return;

            if (!MyAPIGateway.Utilities.IsDedicated && MyAPIGateway.Session.GameplayFrameCounter % UpdateInterval == 0)
                UpdateClient(thisController);

            if (MyAPIGateway.Session.IsServer)
                UpdateServer(thisController);
        }

        public bool GetController(out IMyShipController controller)
        {
            if (MyAPIGateway.Session.IsServer)
            {
                foreach (var pContr in ShipControllers)
                {
                    if (pContr.Pilot != null && pContr.Pilot.IsPlayer)
                    {
                        controller = pContr;
                        Controller.Value = controller.EntityId;
                        return true;
                    }
                }

                controller = null;
                Controller.Value = long.MinValue;
                return false;
            }
            else // client can't read pilot
            {
                if (Controller.Value != long.MinValue)
                {
                    controller = MyAPIGateway.Entities.GetEntityById(Controller.Value) as IMyShipController;
                    return controller != null;
                }

                controller = null;
                return false;
            }
        }

        public void TryLockTarget()
        {
            IMyShipController controller;
            if (!Block.IsWorking || Aggregator == null || !GetController(out controller))
                return;

            if (!MyAPIGateway.Session.IsServer) // client sends update trigger, server triggers trylocktarget
            {
                UpdateClient(controller);
                LockedTarget.Value = long.MaxValue;
                return;
            }

            Vector3D aimFrom = controller.GetPosition();
            Vector3D aimDir = Vector3D.Transform(RelativeAimpoint.Value, controller.CubeGrid.WorldMatrix) - aimFrom;

            if (GlobalData.DebugLevel >= 2)
            {
                DebugDraw.AddLine(aimFrom, aimDir + aimFrom, Color.HotPink, 10);
            }

            WorldDetectionInfo? closest = null;
            double closestAngle = double.MaxValue;

            var detSet = Aggregator.DetectionSet;
            lock (detSet)
            {
                foreach (var target in detSet)
                {
                    double angle = Vector3D.Angle(aimDir, target.Position - aimFrom);
                    if (angle > Math.PI/8 || target.EntityId == LockedTarget.Value) // 22.5deg
                        continue;
                    if (angle < closestAngle)
                    {
                        closest = target;
                        closestAngle = angle;
                    }
                }
            }

            if (closest == null)
            {
                UnlockTarget();
                return;
            }

            LockedTarget.Value = closest.Value.EntityId;
        }

        public void UnlockTarget()
        {
            LockedTarget.Value = long.MinValue;
        }

        private long OnLockedTargetChanged(long newTargetId)
        {
            if (newTargetId == long.MaxValue)
            {
                TryLockTarget();
                return LockedTarget.Value;
            }

            return newTargetId;
        }

        private void UpdateServer(IMyShipController controller)
        {
            if (LockedTarget.Value == long.MinValue)
            {
                // no target locked
                if (RelativeAimpoint.Value == Vector3D.Zero)
                    return;

                Vector3D globalAimpoint = Vector3D.Transform(RelativeAimpoint.Value, controller.CubeGrid.WorldMatrix);

                if (GlobalData.DebugLevel >= 2)
                {
                    DebugDraw.AddLine(controller.GetPosition(), globalAimpoint, Color.HotPink, 1/60f);
                    DebugDraw.AddPoint(globalAimpoint, Color.HotPink, 0);
                }

                foreach (var sensor in ControlledSensors)
                {
                    if (!(sensor.AllowMechanicalControl ^ InvertAllowControl.Value) || !sensor.TryTakeControl(this))
                        continue;

                    sensor.AimAt(globalAimpoint);
                }
            }
            else
            {
                Vector3D globalAimpoint = Vector3D.Zero;
                bool anySet = false;
                foreach (var target in Aggregator.DetectionSet)
                {
                    if (target.EntityId != LockedTarget.Value)
                        continue;
                    globalAimpoint = target.Position;
                    anySet = true;
                    break;
                }

                if (!anySet)
                    return;

                foreach (var sensor in ControlledSensors)
                {
                    if (!(sensor.AllowMechanicalControl ^ InvertAllowControl.Value) || !sensor.TryTakeControl(this))
                        continue;

                    sensor.AimAt(globalAimpoint);
                }
            }
        }

        private void UpdateClient(IMyShipController controller)
        {
            Log.Info("ManualBlock", $"Pilot: {controller.Pilot != MyAPIGateway.Session.Player.Character} {controller.Pilot.DisplayName}, {MyAPIGateway.Session.Player.Character.DisplayName}\n{LockedTarget.Value}");
            if (controller.Pilot != MyAPIGateway.Session.Player.Character || LockedTarget.Value != long.MinValue)
                return;

            Vector3D aimDir = MyAPIGateway.Session.Camera.WorldMatrix.Forward;
            Vector3D aimFrom = MyAPIGateway.Session.Camera.Position;

            if (ParallaxAccount.Value)
            {
                // start ray from outside the ship's bounding box
                double dMin, dMax;
                RayD aimLine = new RayD(aimFrom, aimDir);
                controller.CubeGrid.WorldAABB.Intersect(ref aimLine, out dMin, out dMax);

                IHitInfo hitInfo;
                if (MyAPIGateway.Physics.CastLongRay(aimFrom + aimDir * (dMax + 25), aimFrom + aimDir * MyAPIGateway.Session.SessionSettings.ViewDistance, out hitInfo, false))
                    RelativeAimpoint.Value = Vector3D.Transform(hitInfo.Position, MatrixD.Invert(controller.CubeGrid.WorldMatrix));
                else
                    RelativeAimpoint.Value = Vector3D.Transform(aimFrom + aimDir * 100000, MatrixD.Invert(controller.CubeGrid.WorldMatrix));
            }
            else
            {
                RelativeAimpoint.Value = Vector3D.Transform(aimFrom + aimDir * 100000, MatrixD.Invert(controller.CubeGrid.WorldMatrix));
            }


            if (GlobalData.DebugLevel >= 2 && !MyAPIGateway.Session.IsServer)
            {
                DebugDraw.AddLine(controller.GetPosition(), RelativeAimpoint.Value, Color.HotPink, UpdateInterval/60f);
                DebugDraw.AddPoint(RelativeAimpoint.Value, Color.HotPink, 0);
            }
        }
    }
}
