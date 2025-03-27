using DetectionEquipment.Shared.BlockLogic.Aggregator;
using DetectionEquipment.Shared.Utils;
using ProtoBuf;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using System;
using VRage.Game.Components;

namespace DetectionEquipment.Shared.BlockLogic
{
    [ProtoContract]
    internal abstract class ControlBlockSettingsBase
    {
        [ProtoIgnore] public IControlBlockBase AttachedLogic { get; private set;}

        public ControlBlockSettingsBase(IControlBlockBase logic)
        {
            AttachedLogic = logic;
        }

        protected ControlBlockSettingsBase()
        {

        }

        /// <summary>
        /// Populate a given logic's data.
        /// </summary>
        /// <param name="logic"></param>
        protected abstract void AssignData();

        /// <summary>
        /// Populate this object from a given logic's data.
        /// </summary>
        protected abstract void RetrieveData();

        protected abstract ControlBlockSettingsBase Deserialize(byte[] rawData);

        public void LoadSettings()
        {
            if (AttachedLogic.CubeBlock.Storage == null)
            {
                AssignData();
                //Log.Info("ControlBlockSettingsBase", $"Failed to load {AttachedLogic.CubeBlock.EntityId} {AttachedLogic.CubeBlock.DefinitionDisplayNameText} data because storage is null.");
                SaveBlockSettings();
                return;
            }

            string rawData;
            if (!AttachedLogic.CubeBlock.Storage.TryGetValue(GlobalData.SettingsGuid, out rawData))
            {
                AssignData();
                //Log.Info("ControlBlockSettingsBase", $"Failed to load {AttachedLogic.CubeBlock.EntityId} {AttachedLogic.CubeBlock.DefinitionDisplayNameText} data because storage does not contain relevant data.");
                SaveBlockSettings();
                return;
            }

            try
            {
                var loadedSettings = Deserialize(Convert.FromBase64String(rawData));

                if (loadedSettings == null)
                {
                    //Log.Info("ControlBlockSettingsBase", $"Failed to load {AttachedLogic.CubeBlock.EntityId} {AttachedLogic.CubeBlock.DefinitionDisplayNameText} data because stored data was invalid.");
                    loadedSettings = this;
                }
                else
                {
                    loadedSettings.AttachedLogic = AttachedLogic;
                }

                loadedSettings.AssignData();

                //Log.Info("ControlBlockSettingsBase", $"Loaded {AttachedLogic.CubeBlock.DefinitionDisplayNameText} data for block {AttachedLogic.CubeBlock.EntityId}.");
                SaveBlockSettings();
            }
            catch (Exception e)
            {
                Log.Exception("ControlBlockSettingsBase", e);
            }
        }

        public void SaveBlockSettings()
        {
            if (AttachedLogic?.CubeBlock == null)
                return; // called too soon or after it was already closed, ignore

            if (AttachedLogic.CubeBlock.Storage == null)
            {
                AttachedLogic.CubeBlock.Storage = new MyModStorageComponent();
                //Log.Info("BlockSensorSettings", $"Created new storage component for block {block.EntityId}.");
            }

            RetrieveData();
            AttachedLogic.CubeBlock.Storage.SetValue(GlobalData.SettingsGuid, Convert.ToBase64String(MyAPIGateway.Utilities.SerializeToBinary(this)));
            //Log.Info("ControlBlockSettingsBase", $"Saved {AttachedLogic.CubeBlock.DefinitionDisplayNameText} data for block {AttachedLogic.CubeBlock.EntityId}.");
        }
    }
}
