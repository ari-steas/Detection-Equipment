using System;
using DetectionEquipment.Shared.Utils;
using RichHudFramework.Client;
using RichHudFramework.Internal;

namespace DetectionEquipment.Shared.ExternalApis
{
    internal static class ApiManager
    {
        public static WcApi.WcApi WcApi;
        private static Action _onWcReady = () => Log.Info("WcApi", "Ready.");
        private static Action _onRichHudReady = () => Log.Info("RichHud", "Ready.");

        public static void Init()
        {
            Log.IncreaseIndent();

            try
            {
                WcApi = new WcApi.WcApi();
                WcApi.Load(_onWcReady);
            }
            catch (Exception ex)
            {
                Log.Exception("ApiManager", new Exception("Failed to load WcApi!", ex));
            }

            try
            {
                RichHudClient.Init("Detection Equipment", _onRichHudReady, null);
            }
            catch (Exception ex)
            {
                Log.Exception("ApiManager", new Exception("Failed to load RichHudClient!", ex));
            }

            Log.DecreaseIndent();
            Log.Info("ApiManager", "Ready.");
        }

        public static void Unload()
        {
            Log.IncreaseIndent();

            WcApi.Unload();
            WcApi = null;
            _onWcReady = null;
            _onRichHudReady = null;

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
                _onWcReady += action;
        }

        /// <summary>
        /// Registers an action to invoke when the API is ready, or calls it immediately if ready.
        /// </summary>
        /// <param name="action"></param>
        public static void RichHudOnLoadRegisterOrInvoke(Action action)
        {
            if (RichHudClient.Registered)
                action.Invoke();
            else
                _onRichHudReady += action;
        }
    }
}
