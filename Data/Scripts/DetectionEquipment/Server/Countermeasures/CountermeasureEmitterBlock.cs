using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Utils;
using Sandbox.ModAPI;
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

        public MatrixD MuzzleMatrix => Muzzles[CurrentMuzzleIdx].GetMatrix();

        public CountermeasureEmitterBlock(IMyConveyorSorter block, CountermeasureEmitterDefinition definition)
        {
            Id = CountermeasureManager.HighestCountermeasureEmitterId++;
            CountermeasureManager.CountermeasureEmitterIdMap[Id] = this;

            Definition = definition;
            Block = block;

            SetupMuzzles();
        }

        


        private void SetupMuzzles()
        {
            var muzzles = new Dictionary<string, Muzzle>();
            var bufferDict = new Dictionary<string, IMyModelDummy>();
            foreach (var subpart in SubpartManager.GetAllSubparts(Block))
            {
                ((IMyEntity)subpart).Model?.GetDummies(bufferDict);
                foreach (var dummyKvp in bufferDict)
                    if (Definition.Muzzles.Contains(dummyKvp.Key))
                        muzzles[dummyKvp.Key] = new Muzzle(subpart, dummyKvp.Value);
            }

            Muzzles = new Muzzle[muzzles.Count];
            int latestIdx = 0;
            foreach (var muzzleName in Definition.Muzzles)
            {
                if (!muzzles.ContainsKey(muzzleName))
                    continue;
                Muzzles[latestIdx++] = muzzles[muzzleName];
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
        }
    }
}
