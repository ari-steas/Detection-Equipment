using System.Collections.Generic;
using DetectionEquipment.Shared;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Utils;
using Sandbox.Definitions;
using VRage.Game;

namespace DetectionEquipment.Client.Interface
{
    public static class BlockCategoryManager
    {
        private static GuiBlockCategoryHelper _blockCategory;
        private static HashSet<string> _bufferBlockSubtypes = new HashSet<string>
        {
            // Everything relevant should be automatically added, but if not, put its subtype here.
        }; // DefinitionManager can load before the BlockCategoryManager on client and cause an exception.

        private static Dictionary<string, string> _subtypeToTypePairing;

        public static void Init()
        {
            _subtypeToTypePairing = new Dictionary<string, string>();
            foreach (var def in MyDefinitionManager.Static.GetAllDefinitions())
            {
                if (string.IsNullOrEmpty(def.Id.SubtypeName)) continue;
                _subtypeToTypePairing[def.Id.SubtypeName] = def.Id.TypeId.ToString().Replace("MyObjectBuilder_", "");
                if (def.Context?.ModPath == GlobalData.ModContext.ModPath && (def is MyCubeBlockDefinition || def is MyPhysicalItemDefinition)) // Adds all blocks from this mod automatically
                    RegisterFromSubtype(def.Id.SubtypeName);
            }

            _blockCategory = new GuiBlockCategoryHelper("[Detection Equipment]", "DetectionEquipmentBlockCategory");
            foreach (var item in _bufferBlockSubtypes)
                _blockCategory.AddBlock(item);

            Log.Info("BlockCategoryManager", "Initialized.");
        }

        public static void RegisterFromDefinition(SensorDefinition definition)
        {
            foreach (var subtype in definition.BlockSubtypes)
                RegisterFromSubtype(subtype);
        }

        public static void RegisterFromDefinition(CountermeasureEmitterDefinition definition)
        {
            foreach (var subtype in definition.BlockSubtypes)
                RegisterFromSubtype(subtype);
        }

        public static void RegisterFromSubtype(string subtypeId)
        {
            if (_bufferBlockSubtypes.Add(subtypeId) && _blockCategory != null)
                _blockCategory.AddBlock(subtypeId);
        }

        public static void Close()
        {
            _subtypeToTypePairing = null;
            _blockCategory = null;
            _bufferBlockSubtypes = null;
            Log.Info("BlockCategoryManager", "Unloaded.");
        }

        private class GuiBlockCategoryHelper
        {
            private readonly MyGuiBlockCategoryDefinition _category;

            public GuiBlockCategoryHelper(string name, string id)
            {
                _category = new MyGuiBlockCategoryDefinition
                {
                    Id = new MyDefinitionId(typeof(MyObjectBuilder_GuiBlockCategoryDefinition), id),
                    Name = name,
                    DisplayNameString = name,
                    ItemIds = new HashSet<string>(),
                    IsBlockCategory = true,
                };
                MyDefinitionManager.Static.GetCategories().Add(name, _category);
            }

            public void AddBlock(string subtypeId)
            {
                Log.IncreaseIndent();
                string typeId;
                if (_subtypeToTypePairing.TryGetValue(subtypeId, out typeId)) // keen broke block category items with just subtypeid
                {
                    _category.ItemIds.Add(typeId + "/" + subtypeId);
                    Log.Info("GuiBlockCategoryHelper", $"Added {typeId + "/" + subtypeId}");
                }
                else
                {
                    _category.ItemIds.Add(subtypeId + "/(null)");
                    Log.Info("GuiBlockCategoryHelper", $"Added {subtypeId + "/(null)"}");
                }
                Log.DecreaseIndent();

                //foreach (var _cat in MyDefinitionManager.Static.GetCategories().Values)
                //{
                //    HeartData.I.Log.Log("Category " + _cat.Name);
                //    foreach (var _id in _cat.ItemIds)
                //        HeartData.I.Log.Log($"   \"{_id}\"");
                //}
            }
        }
    }
}
