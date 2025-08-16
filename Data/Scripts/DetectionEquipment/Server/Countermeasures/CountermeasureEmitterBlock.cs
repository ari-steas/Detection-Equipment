using System;
using System.Collections.Generic;
using DetectionEquipment.Server.Networking;
using DetectionEquipment.Shared;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Networking;
using DetectionEquipment.Shared.Utils;
using Sandbox.Game;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame;
using VRageMath;
using IMyCubeBlock = VRage.Game.ModAPI.IMyCubeBlock;
using IMyEntity = VRage.ModAPI.IMyEntity;

namespace DetectionEquipment.Server.Countermeasures
{
    internal class CountermeasureEmitterBlock
    {
        public uint Id;
        public IMyConveyorSorter Block;
        public CountermeasureEmitterDefinition Definition;

        public Muzzle[] Muzzles;
        public int CurrentMuzzleIdx = 0;
        private int _currentSequenceIdx = 0;
        private float _shotAggregator = 0;

        private int _magazineShots = 0;
        private int _reloadTicks = 0;
        private bool _hadReload = true;

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
                _resourceSink.Update();
                ServerNetwork.SendToEveryoneInSync(new Client.BlockLogic.Countermeasures.CountermeasureUpdatePacket(this), Block.GetPosition());
            }
        }

        private bool _reloading = false;

        public bool Reloading
        {
            get
            {
                return _reloading;
            }
            set
            {
                _reloading = value;
                ServerNetwork.SendToEveryoneInSync(new Client.BlockLogic.Countermeasures.CountermeasureUpdatePacket(this), Block.GetPosition());
            }
        }

        private Countermeasure[] _attachedCountermeasures;

        public MatrixD MuzzleMatrix => Muzzles[CurrentMuzzleIdx].GetMatrix();

        public CountermeasureEmitterBlock(IMyConveyorSorter block, CountermeasureEmitterDefinition definition)
        {
            Id = CountermeasureManager.HighestCountermeasureEmitterId++;
            CountermeasureManager.CountermeasureEmitterIdMap[Id] = this;

            Definition = definition;
            Block = block;

            _magazineShots = definition.MagazineSize;

            SetupMuzzles();

            _resourceSink = (MyResourceSinkComponent)Block.ResourceSink;
            if (Definition.ActivePowerDraw > 0)
            {
                _resourceSink.SetRequiredInputFuncByType(GlobalData.ElectricityId, () => Block.Enabled && Firing ? Definition.ActivePowerDraw : 0);
                _resourceSink.SetMaxRequiredInputByType(GlobalData.ElectricityId, Definition.ActivePowerDraw);
                _resourceSink.Update();
            }
            Block.EnabledChanged += b => _resourceSink.Update();

            if (Block.HasInventory)
            {
                var constraint = new MyInventoryConstraint("Countermeasure Ammo", icon: @"Textures\GUI\Icons\FilterAmmo25mm.dds");
                if (!string.IsNullOrEmpty(Definition.MagazineItem))
                    constraint.Add(Definition.MagazineItemDefinition);

                var inventory = (MyInventory) Block.GetInventory();
                inventory.Constraint = constraint;
            }
        }

        public void UpdateFromPacket(Client.BlockLogic.Countermeasures.CountermeasureUpdatePacket packet)
        {
            _firing = packet.Firing;
            _reloading = packet.Reloading;
            _resourceSink.Update();
        }

        private bool _didCloseAttached = false;
        private MyResourceSinkComponent _resourceSink;

        public void Update()
        {
            if (_hadReload)
            {
                if (Reloading)
                {
                    if (_reloadTicks <= 0)
                    {
                        Reloading = false;
                        _magazineShots = Definition.MagazineSize;
                    }
                    else
                        _reloadTicks -= 1;
                }
            }
            else if (_reloadTicks > 0 && MyAPIGateway.Session.GameplayFrameCounter % 9 == 0)
            {
                _hadReload = TryConsumeReload();
            }

            bool canFire = (Block.IsWorking || (Block.IsFunctional && Block.Enabled && Definition.ActivePowerDraw <= 0)) && Firing && !Reloading && _hadReload;
            if (!canFire)
            {
                if (!Definition.IsCountermeasureAttached || _didCloseAttached)
                    return;
                for (var i = 0; i < _attachedCountermeasures.Length; i++)
                {
                    _attachedCountermeasures[i]?.Close();
                    _attachedCountermeasures[i] = null;
                }

                _didCloseAttached = true;
            }
            _didCloseAttached = false;

            

            if (canFire)
            {
                _shotAggregator += Definition.ShotsPerSecond / 60f;
                int startMuzzleIdx = CurrentMuzzleIdx;
                while (_shotAggregator >= 1 && (_magazineShots > 0 || Definition.MagazineSize <= 0))
                {
                    FireOnce();
                    if (CurrentMuzzleIdx == startMuzzleIdx) // Prevent infinite loop if all muzzles are in use.
                        break;
                }
                _shotAggregator -= (float) Math.Floor(_shotAggregator); // aggregator shouldn't finish a cycle with shots available

                if (Definition.MagazineSize > 0 && _magazineShots <= 0 && Definition.ReloadTime > 1/60f)
                {
                    Reloading = true;
                    _hadReload = TryConsumeReload();
                    _reloadTicks = (int)(Definition.ReloadTime * 60);
                }
            }

            if (!MyAPIGateway.Utilities.IsDedicated && Block.ShowOnHUD && Definition.IsCountermeasureAttached && Block.HasLocalPlayerAccess())
            {
                foreach (var counter in _attachedCountermeasures)
                {
                    if (counter == null)
                        continue;
                    
                    var matrix = MatrixD.CreateWorld(counter.Position, counter.Direction,
                        Vector3D.CalculatePerpendicularVector(counter.Direction));
                    var color = new Color((uint) ((50 + counter.Id) * Block.EntityId)).Alpha(0.1f);

                    if (counter.EffectAperture < (float)Math.PI)
                    {
                        MySimpleObjectDraw.DrawTransparentCone(ref matrix, (float) Math.Tan(counter.EffectAperture) * counter.Definition.MaxRange, counter.Definition.MaxRange, ref color, 8, DebugDraw.MaterialSquare);
                    }
                    else
                    {
                        DebugDraw.AddLine(counter.Position, counter.Position + counter.Direction * counter.Definition.MaxRange, color, 0);
                        MySimpleObjectDraw.DrawTransparentSphere(ref matrix, counter.Definition.MaxRange, ref color, MySimpleObjectRasterizer.Wireframe, 20);
                    }
                }
            }
        }

        private void FireOnce()
        {
            if (!Definition.IsCountermeasureAttached || !(_attachedCountermeasures[CurrentMuzzleIdx]?.IsActive ?? false)) // Only spawn a new countermeasure if needed.
            {
                var counterDefName = Definition.CountermeasureIds[_currentSequenceIdx++];
                var counterDef =
                    DefinitionManager.GetCountermeasureDefinition(counterDefName.GetHashCode());
                if (counterDef == null)
                    throw new Exception($"Invalid countermeasure definition {counterDefName}");
                if (_currentSequenceIdx >= Definition.CountermeasureIds.Length)
                    _currentSequenceIdx = 0;

                var counter = new Countermeasure(counterDef, this);
                if (Definition.IsCountermeasureAttached)
                    _attachedCountermeasures[CurrentMuzzleIdx] = counter;

                if (!string.IsNullOrEmpty(Definition.FireParticle))
                    ServerNetwork.SendToEveryoneInSync(new CountermeasureEmitterPacket(this), Block.WorldMatrix.Translation);

                _shotAggregator -= 1;
                _magazineShots -= 1;
            }

            CurrentMuzzleIdx++;
            if (CurrentMuzzleIdx >= Muzzles.Length)
                CurrentMuzzleIdx = 0;
        }

        private bool TryConsumeReload()
        {
            if (MyAPIGateway.Session.CreativeMode || string.IsNullOrEmpty(Definition.MagazineItem))
                return true;

            var inventory = Block.GetInventory();
            if (inventory == null)
                throw new Exception($"Missing inventory on block with subtype {Block.BlockDefinition.SubtypeName}.");

            var items = CountermeasureManager.InventoryItemPool.Pop();
            inventory.GetItems(items);

            bool hadItem = false;
            foreach (var item in items)
            {
                if (item.Type.SubtypeId != Definition.MagazineItem || item.Amount < 1)
                    continue;
                inventory.RemoveItems(item.ItemId, 1);
                hadItem = true;
                break;
            }

            CountermeasureManager.InventoryItemPool.Push(items);
            return hadItem;
        }


        private void SetupMuzzles()
        {
            var muzzles = new Dictionary<string, Muzzle>();
            
            Muzzle.CheckBlockForMuzzles(Block, Definition, ref muzzles);

            Muzzles = new Muzzle[muzzles.Count];
            _attachedCountermeasures = new Countermeasure[muzzles.Count];
            int latestIdx = 0;
            foreach (var muzzleName in Definition.Muzzles)
            {
                if (!muzzles.ContainsKey(muzzleName))
                    continue;
                Muzzles[latestIdx] = muzzles[muzzleName];
                _attachedCountermeasures[latestIdx] = null;
                latestIdx++;
            }
        }

        public class Muzzle
        {
            public IMyModelDummy Dummy;
            public MyEntity Parent;

            public Muzzle(MyEntity parent, IMyModelDummy dummy)
            {
                Parent = parent;
                Dummy = dummy;
            }

            public MatrixD GetMatrix()
            {
                return Dummy.Matrix * Parent.WorldMatrix;
            }

            public static void CheckBlockForMuzzles(IMyCubeBlock block, CountermeasureEmitterDefinition definition, ref Dictionary<string, Muzzle> muzzles)
            {
                var bufferDict = new Dictionary<string, IMyModelDummy>();
                CheckPartForMuzzles((MyEntity) block, definition, ref muzzles, ref bufferDict);
                foreach (var subpart in SubpartManager.GetAllSubparts(block))
                    CheckPartForMuzzles(subpart, definition, ref muzzles, ref bufferDict);
            }

            private static void CheckPartForMuzzles(MyEntity entity, CountermeasureEmitterDefinition definition, ref Dictionary<string, Muzzle> muzzles,
                ref Dictionary<string, IMyModelDummy> bufferDict)
            {
                ((IMyEntity)entity).Model?.GetDummies(bufferDict);
                foreach (var dummyKvp in bufferDict)
                    if (definition.Muzzles.Contains(dummyKvp.Key))
                        muzzles[dummyKvp.Key] = new Muzzle(entity, dummyKvp.Value);
                bufferDict.Clear();
            }
        }
    }
}
