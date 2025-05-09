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

            try
            {
                WcApi = new WcApi.WcApi();
                WcApi.Load(() => Log.Info("WcApi", "Ready."));
            }
            catch (Exception ex)
            {
                Log.Exception("ApiManager", new Exception("Failed to load WcApi!", ex));
            }

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

        /// <summary>
        /// Registers an action to invoke when the API is ready, or calls it immediately if ready.
        /// </summary>
        /// <param name="action"></param>
        public static void WcOnLoadRegisterOrInvoke(Action action)
        {
            if (WcApi.IsReady)
                action.Invoke();
            else
                WcApi.ReadyCallback += action;
        }
    }
}
