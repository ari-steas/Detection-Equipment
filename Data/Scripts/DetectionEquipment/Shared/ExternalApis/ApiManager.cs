using System;
using DetectionEquipment.Shared.Utils;

namespace DetectionEquipment.Shared.ExternalApis
{
    internal static class ApiManager
    {
        public static WcApi.WcApi WcApi;

        public static void Init()
        {
            Log.IncreaseIndent();

            WcApi = new WcApi.WcApi();
            WcApi.Load(() => Log.Info("WcApi", "Ready."));

            Log.DecreaseIndent();
            Log.Info("ApiManager", "Ready.");
        }

        public static void Unload()
        {
            Log.IncreaseIndent();

            WcApi.Unload();
            WcApi = null;

            Log.DecreaseIndent();
            Log.Info("ApiManager", "Unloaded.");
        }
    }
}
