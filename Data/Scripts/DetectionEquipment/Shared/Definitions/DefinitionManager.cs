using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Shared.Utils;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using DetectionEquipment.Server.Countermeasures;
using VRage.Game.ModAPI;

namespace DetectionEquipment.Shared.Definitions
{
    internal static class DefinitionManager
    {
        internal static DefinitionApi DefinitionApi;

        private static readonly Dictionary<int, SensorDefinition> SensorDefinitions = new Dictionary<int, SensorDefinition>();
        private static readonly Dictionary<int, CountermeasureDefinition> CountermeasureDefinitions = new Dictionary<int, CountermeasureDefinition>();
        private static readonly Dictionary<int, CountermeasureEmitterDefinition> CountermeasureEmitterDefinitions = new Dictionary<int, CountermeasureEmitterDefinition>();

        private static bool HasShownApiFailMsg = false;
        private static int ApiFailCheckCount = 0;

        public static void Load()
        {
            HasShownApiFailMsg = false;
            ApiFailCheckCount = 0;
            DefinitionApi = new DefinitionApi();
            DefinitionApi.Init(GlobalData.ModContext, OnApiReady);
            Log.Info("DefinitionManager", "Initialized.");
        }

        public static void Update()
        {
            if (DefinitionApi.IsReady || HasShownApiFailMsg || ApiFailCheckCount++ < 1)
                return;

            MyAPIGateway.Utilities.ShowNotification("Detection Equipment - DefinitionApi isn't loaded!", int.MaxValue, "Red");
            MyAPIGateway.Utilities.ShowNotification("Check logs (%AppData%\\Roaming\\Space Engineers\\Storage\\DetectionEquipment.log) for more info.", int.MaxValue, "Red");

            Log.Info("DefinitionManager", "DefinitionApi failed to load!\n" +
                "==================================\n" +
                "\n" +
                "Detection Equipment *requires* the Definition Helper mod to load sensor definitions.\n" +
                "For whatever reason, the API was unable to register within 10 ticks. This is most likely because the mod wasn't included in the world.\n" +
                "The Definition Helper library can be found at https://steamcommunity.com/sharedfiles/filedetails/?id=3407764326.\n" +
                "If you included the mod in the world, or there's an exception log above this, please reach out to @aristeas. on discord.\n" +
                "\n" +
                "Best of luck,\n" +
                "Aristeas\n" +
                "\n" +
                "==================================");

            HasShownApiFailMsg = true;
        }

        private static void OnApiReady()
        {
            Log.IncreaseIndent();

            DefinitionApi.RegisterOnUpdate<SensorDefinition>(OnSensorDefinitionUpdate);
            foreach (string definitionId in DefinitionApi.GetDefinitionsOfType<SensorDefinition>())
                OnSensorDefinitionUpdate(definitionId, 0);

            DefinitionApi.RegisterOnUpdate<CountermeasureDefinition>(OnCountermeasureDefinitionUpdate);
            foreach (string definitionId in DefinitionApi.GetDefinitionsOfType<CountermeasureDefinition>())
                OnCountermeasureDefinitionUpdate(definitionId, 0);

            DefinitionApi.RegisterOnUpdate<CountermeasureEmitterDefinition>(OnCountermeasureEmitterDefinitionUpdate);
            foreach (string definitionId in DefinitionApi.GetDefinitionsOfType<CountermeasureEmitterDefinition>())
                OnCountermeasureEmitterDefinitionUpdate(definitionId, 0);

            InternalDefinitions.Register();

            Log.DecreaseIndent();
        }

        public static void Unload()
        {
            DefinitionApi.UnregisterOnUpdate<SensorDefinition>(OnSensorDefinitionUpdate);
            DefinitionApi.UnregisterOnUpdate<CountermeasureDefinition>(OnCountermeasureDefinitionUpdate);
            DefinitionApi.UnregisterOnUpdate<CountermeasureEmitterDefinition>(OnCountermeasureEmitterDefinitionUpdate);

            DefinitionApi.UnloadData();
            DefinitionApi = null;
            SensorDefinitions.Clear();

            Log.Info("DefinitionManager", "Unloaded.");
        }

        private static void OnSensorDefinitionUpdate(string definitionId, int updateType)
        {
            // We're caching data because getting it from the API is inefficient.
            switch (updateType)
            {
                case 0:
                    var definition = DefinitionApi.GetDefinition<SensorDefinition>(definitionId);
                    definition.Id = definitionId.GetHashCode();
                    SensorDefinitions[definition.Id] = definition;
                    if (!MyAPIGateway.Utilities.IsDedicated)
                        Client.Interface.BlockCategoryManager.RegisterFromDefinition(SensorDefinitions[definitionId.GetHashCode()]);

                    Log.Info("DefinitionManager", $"Registered new sensor definition {definitionId} (internal ID {definition.Id})"); // TODO spawn new sensors
                    break;
                case 1:
                    SensorDefinitions.Remove(definitionId.GetHashCode()); // TODO cleanup existing sensors
                    Log.Info("DefinitionManager", "Unregistered sensor definition " + definitionId);
                    break;
                case 2:
                    // Live methods, ignored.
                    break;
            }
        }

        private static void OnCountermeasureDefinitionUpdate(string definitionId, int updateType)
        {
            // We're caching data because getting it from the API is inefficient.
            switch (updateType)
            {
                case 0:
                    var definition = DefinitionApi.GetDefinition<CountermeasureDefinition>(definitionId);

                    Log.IncreaseIndent();
                    bool valid = CountermeasureDefinition.Verify(definition);
                    Log.DecreaseIndent();
                    if (!valid)
                    {
                        Log.Info("DefinitionManager", $"Did not register definition {definitionId}.");
                        return;
                    }

                    definition.Id = definitionId.GetHashCode();
                    CountermeasureDefinitions[definition.Id] = definition;

                    Log.Info("DefinitionManager", $"Registered new CM definition {definitionId} (internal ID {definition.Id})"); // TODO spawn new sensors
                    break;
                case 1:
                    SensorDefinitions.Remove(definitionId.GetHashCode()); // TODO cleanup existing sensors
                    Log.Info("DefinitionManager", "Unregistered CM definition " + definitionId);
                    break;
                case 2:
                    // Live methods, ignored.
                    break;
            }
        }

        private static void OnCountermeasureEmitterDefinitionUpdate(string definitionId, int updateType)
        {
            // We're caching data because getting it from the API is inefficient.
            switch (updateType)
            {
                case 0:
                    var definition = DefinitionApi.GetDefinition<CountermeasureEmitterDefinition>(definitionId);

                    Log.IncreaseIndent();
                    bool valid = CountermeasureEmitterDefinition.Verify(definition);
                    Log.DecreaseIndent();
                    if (!valid)
                    {
                        Log.Info("DefinitionManager", $"Did not register definition {definitionId}.");
                        return;
                    }

                    definition.Id = definitionId.GetHashCode();
                    CountermeasureEmitterDefinitions[definition.Id] = definition;
                    if (!MyAPIGateway.Utilities.IsDedicated)
                        Client.Interface.BlockCategoryManager.RegisterFromDefinition(CountermeasureEmitterDefinitions[definitionId.GetHashCode()]);

                    Log.Info("DefinitionManager", $"Registered new CM Emitter definition {definitionId} (internal ID {definition.Id})"); // TODO spawn new
                    break;
                case 1:
                    CountermeasureEmitterDefinitions.Remove(definitionId.GetHashCode()); // TODO cleanup existing
                    Log.Info("DefinitionManager", "Unregistered CM Emitter definition " + definitionId);
                    break;
                case 2:
                    // Live methods, ignored.
                    break;
            }
        }

        public static List<BlockSensor> TryCreateSensors(IMyFunctionalBlock block)
        {
            var sensors = new List<BlockSensor>();
            foreach (var definition in SensorDefinitions.Values)
            {
                if (!definition.BlockSubtypes.Contains(block.BlockDefinition.SubtypeName))
                    continue;
                sensors.Add(new BlockSensor(block, definition));
            }
            return sensors;
        }

        public static List<CountermeasureEmitterBlock> TryCreateCountermeasureEmitters(IMyConveyorSorter block)
        {
            var emitters = new List<CountermeasureEmitterBlock>();
            foreach (var definition in CountermeasureEmitterDefinitions.Values)
            {
                if (!definition.BlockSubtypes.Contains(block.BlockDefinition.SubtypeName))
                    continue;
                emitters.Add(new CountermeasureEmitterBlock(block, definition));
            }
            return emitters;
        }

        public static List<SensorDefinition> GetSensorDefinitions(IMyCubeBlock block)
        {
            var defs = new List<SensorDefinition>();
            foreach (var definition in SensorDefinitions.Values)
            {
                if (!definition.BlockSubtypes.Contains(block.BlockDefinition.SubtypeName))
                    continue;
                defs.Add(definition);
            }
            return defs;
        }

        public static List<CountermeasureEmitterDefinition> GetCountermeasureEmitterDefinitions(IMyCubeBlock block)
        {
            var defs = new List<CountermeasureEmitterDefinition>();
            foreach (var definition in CountermeasureEmitterDefinitions.Values)
            {
                if (!definition.BlockSubtypes.Contains(block.BlockDefinition.SubtypeName))
                    continue;
                defs.Add(definition);
            }
            return defs;
        }

        public static SensorDefinition GetSensorDefinition(int id)
        {
            return SensorDefinitions[id];
        }

        public static CountermeasureDefinition GetCountermeasureDefinition(int id)
        {
            return CountermeasureDefinitions[id];
        }

        public static CountermeasureEmitterDefinition GetCountermeasureEmitterDefinition(int id)
        {
            return CountermeasureEmitterDefinitions[id];
        }
    }
}
