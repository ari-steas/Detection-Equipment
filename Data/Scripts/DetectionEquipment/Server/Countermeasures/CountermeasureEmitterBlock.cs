using System;
using System.Collections.Generic;
using DetectionEquipment.Server.Networking;
using DetectionEquipment.Shared;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Networking;
using DetectionEquipment.Shared.Utils;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

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

        private Countermeasure[] _attachedCountermeasures;

        public MatrixD MuzzleMatrix => Muzzles[CurrentMuzzleIdx].GetMatrix();

        public CountermeasureEmitterBlock(IMyConveyorSorter block, CountermeasureEmitterDefinition definition)
        {
            Id = CountermeasureManager.HighestCountermeasureEmitterId++;
            CountermeasureManager.CountermeasureEmitterIdMap[Id] = this;

            Definition = definition;
            Block = block;

            SetupMuzzles();
        }

        private bool _didCloseAttached = false;
        public void Update()
        {
            if (!Block.IsWorking)
            {
                if (!Definition.IsCountermeasureAttached || _didCloseAttached)
                    return;
                foreach (var counter in _attachedCountermeasures)
                    counter?.Close();
                _didCloseAttached = true;
                return;
            }
            _didCloseAttached = false;

            _shotAggregator += Definition.ShotsPerSecond / 60f;
            int startMuzzleIdx = CurrentMuzzleIdx;
            while (_shotAggregator >= 1)
            {
                FireOnce();
                if (CurrentMuzzleIdx == startMuzzleIdx) // Prevent infinite loop if all muzzles are in use.
                    break;
            }

            if (Block.ShowOnHUD && Definition.IsCountermeasureAttached)
            {
                foreach (var counter in _attachedCountermeasures)
                {
                    var matrix = MatrixD.CreateWorld(counter.Position, counter.Direction,
                        Vector3D.CalculatePerpendicularVector(counter.Direction));
                    var color = new Color((uint) ((50 + counter.Id) * Block.EntityId)).Alpha(0.1f);
                    MySimpleObjectDraw.DrawTransparentCone(ref matrix, (float) Math.Tan(counter.EffectAperture) * counter.Definition.MaxRange, counter.Definition.MaxRange, ref color, 8, DebugDraw.MaterialSquare);
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
            }

            CurrentMuzzleIdx++;
            if (CurrentMuzzleIdx >= Muzzles.Length)
                CurrentMuzzleIdx = 0;
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
