using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Shared.Utils;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.ModAPI;

namespace DetectionEquipment.Shared.Definitions
{
    internal static class DefinitionManager
    {
        internal static DefinitionApi DefinitionApi;

        private static readonly Dictionary<int, SensorDefinition> Definitions = new Dictionary<int, SensorDefinition>();
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

            DefinitionApi.RegisterOnUpdate<SensorDefinition>(OnDefinitionUpdate);
            foreach (string definitionId in DefinitionApi.GetDefinitionsOfType<SensorDefinition>())
                OnDefinitionUpdate(definitionId, 0);
            InternalDefinitions.Register();

            Log.DecreaseIndent();
        }

        public static void Unload()
        {
            DefinitionApi.UnregisterOnUpdate<SensorDefinition>(OnDefinitionUpdate);
            DefinitionApi.UnloadData();
            DefinitionApi = null;
            Definitions.Clear();

            Log.Info("DefinitionManager", "Unloaded.");
        }

        private static void OnDefinitionUpdate(string definitionId, int updateType)
        {
            // We're caching data because getting it from the API is inefficient.
            switch (updateType)
            {
                case 0:
                    var definition = DefinitionApi.GetDefinition<SensorDefinition>(definitionId);
                    definition.Id = definitionId.GetHashCode();
                    Definitions[definition.Id] = definition;
                    if (!MyAPIGateway.Utilities.IsDedicated)
                        Client.Interface.BlockCategoryManager.RegisterFromDefinition(Definitions[definitionId.GetHashCode()]);

                    Log.Info("DefinitionManager", $"Registered new definition {definitionId} (internal ID {definition.Id})"); // TODO spawn new sensors
                    break;
                case 1:
                    Definitions.Remove(definitionId.GetHashCode()); // TODO cleanup existing sensors
                    Log.Info("DefinitionManager", "Unregistered definition " + definitionId);
                    break;
                case 2:
                    // Live methods, ignored.
                    break;
            }
        }

        public static List<BlockSensor> TryCreateSensors(IMyFunctionalBlock block)
        {
            var sensors = new List<BlockSensor>();
            foreach (var definition in Definitions.Values)
            {
                if (!definition.BlockSubtypes.Contains(block.BlockDefinition.SubtypeName))
                    continue;
                sensors.Add(new BlockSensor(block, definition));
            }
            return sensors;
        }

        public static List<SensorDefinition> GetDefinitions(IMyCubeBlock block)
        {
            var defs = new List<SensorDefinition>();
            foreach (var definition in Definitions.Values)
            {
                if (!definition.BlockSubtypes.Contains(block.BlockDefinition.SubtypeName))
                    continue;
                defs.Add(definition);
            }
            return defs;
        }

        public static SensorDefinition GetDefinition(int id)
        {
            return Definitions[id];
        }
    }
}
