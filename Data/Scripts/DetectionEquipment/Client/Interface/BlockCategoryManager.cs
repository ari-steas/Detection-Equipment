using System.Collections.Generic;
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
            "DetectionAggregatorBlock",
            "IffReflector",
            "DetectionSearchBlock",
            "DetectionTrackerBlock",
        }; // DefinitionManager can load before the BlockCategoryManager on client and cause an exception.

        public static void Init()
        {
            _blockCategory = new GuiBlockCategoryHelper("[Detection Equipment]", "DetectionEquipmentBlockCategory");
            foreach (var item in _bufferBlockSubtypes)
                _blockCategory.AddBlock(item);
            _bufferBlockSubtypes.Clear();
            Log.Info("BlockCategoryManager", "Initialized.");
        }

        public static void RegisterFromDefinition(SensorDefinition definition)
        {
            foreach (var subtype in definition.BlockSubtypes)
                if (_bufferBlockSubtypes.Add(subtype) && _blockCategory != null)
                    _blockCategory.AddBlock(subtype);
        }

        public static void RegisterFromDefinition(CountermeasureEmitterDefinition definition)
        {
            foreach (var subtype in definition.BlockSubtypes)
                if (_bufferBlockSubtypes.Add(subtype) && _blockCategory != null)
                    _blockCategory.AddBlock(subtype);
        }

        public static void Close()
        {
            _blockCategory = null;
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
                if (!_category.ItemIds.Contains(subtypeId))
                    _category.ItemIds.Add(subtypeId);

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
