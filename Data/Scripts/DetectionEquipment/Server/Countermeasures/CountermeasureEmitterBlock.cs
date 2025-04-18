﻿using System;
using System.Collections.Generic;
using DetectionEquipment.Shared;
using DetectionEquipment.Shared.Definitions;
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
        private int CurrentSequenceIdx = 0;
        private float ShotAggregator = 0;

        private Countermeasure[] AttachedCountermeasures;

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
                foreach (var counter in AttachedCountermeasures)
                    counter?.Close();
                _didCloseAttached = true;
                return;
            }
            _didCloseAttached = false;

            ShotAggregator += Definition.ShotsPerSecond / 60f;
            int startMuzzleIdx = CurrentMuzzleIdx;
            while (ShotAggregator >= 1)
            {
                FireOnce();
                if (CurrentMuzzleIdx == startMuzzleIdx) // Prevent infinite loop if all muzzles are in use.
                    break;
            }

            if (Block.ShowOnHUD && Definition.IsCountermeasureAttached)
            {
                foreach (var counter in AttachedCountermeasures)
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
            if (!Definition.IsCountermeasureAttached || !(AttachedCountermeasures[CurrentMuzzleIdx]?.IsActive ?? false)) // Only spawn a new countermeasure if needed.
            {
                var counterDef =
                    DefinitionManager.GetCountermeasureDefinition(Definition.CountermeasureIds[CurrentSequenceIdx++]
                        .GetHashCode());
                if (CurrentSequenceIdx >= Definition.CountermeasureIds.Length)
                    CurrentSequenceIdx = 0;

                var counter = new Countermeasure(counterDef, this);
                if (Definition.IsCountermeasureAttached)
                    AttachedCountermeasures[CurrentMuzzleIdx] = counter;

                if (!string.IsNullOrEmpty(Definition.FireParticle)) // TODO: this doesn't go in server
                {
                    MyParticleEffect discard;

                    uint renderId = Muzzles[CurrentMuzzleIdx].Parent?.Render?.GetRenderObjectID() ?? uint.MaxValue;
                    var matrix = (MatrixD) Muzzles[CurrentMuzzleIdx].Dummy.Matrix;
                    var pos = MuzzleMatrix.Translation;
                    if (!MyParticlesManager.TryCreateParticleEffect(Definition.FireParticle, ref matrix, ref pos, renderId, out discard)) // TODO this goes in client
                    {
                        Log.Exception("CountermeasureEmitterBlock", new Exception($"Failed to create new projectile particle \"{Definition.FireParticle}\"!"));
                    }
                }

                ShotAggregator -= 1;
            }

            CurrentMuzzleIdx++;
            if (CurrentMuzzleIdx >= Muzzles.Length)
                CurrentMuzzleIdx = 0;
        }


        private void SetupMuzzles()
        {
            var muzzles = new Dictionary<string, Muzzle>();
            var bufferDict = new Dictionary<string, IMyModelDummy>();

            CheckPartForMuzzles((MyEntity) Block, ref muzzles, ref bufferDict);
            foreach (var subpart in SubpartManager.GetAllSubparts(Block))
                CheckPartForMuzzles(subpart, ref muzzles, ref bufferDict);

            Muzzles = new Muzzle[muzzles.Count];
            AttachedCountermeasures = new Countermeasure[muzzles.Count];
            int latestIdx = 0;
            foreach (var muzzleName in Definition.Muzzles)
            {
                if (!muzzles.ContainsKey(muzzleName))
                    continue;
                Muzzles[latestIdx] = muzzles[muzzleName];
                AttachedCountermeasures[latestIdx] = null;
                latestIdx++;
            }
        }

        private void CheckPartForMuzzles(MyEntity entity, ref Dictionary<string, Muzzle> muzzles,
            ref Dictionary<string, IMyModelDummy> bufferDict)
        {
            ((IMyEntity)entity).Model?.GetDummies(bufferDict);
            foreach (var dummyKvp in bufferDict)
                if (Definition.Muzzles.Contains(dummyKvp.Key))
                    muzzles[dummyKvp.Key] = new Muzzle(entity, dummyKvp.Value);
            bufferDict.Clear();
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
        }
    }
}
