using Sandbox.ModAPI;
using System;
using VRage.Game.ModAPI;
using VRage.Utils;
using System.Collections.Generic;
using VRage;
using DetectionEquipment.Shared.Utils;

namespace DetectionEquipment.Shared.Definitions
{
    /// <summary>
    ///     Class used to communicate with the DefinitionHelper mod. <br /><br />
    ///     This is based off of my (Aristeas) Modular Assemblies mod's ModularDefinitionApi.
    /// </summary>
    internal class DefinitionApi
    {
        /// <summary>
        ///     The expected API version. Don't touch this.
        /// </summary>
        public const int ApiVersion = 2;

        /// <summary>
        ///     Triggered whenever the API is ready - added to by the constructor or manually.
        /// </summary>
        public Action OnReady;

        /// <summary>
        ///     The currently loaded Definition Framework version.
        ///     <remarks>
        ///         Not the API version; see <see cref="ApiVersion" />
        ///     </remarks>
        /// </summary>
        public int FrameworkVersion { get; private set; } = -1;

        /// <summary>
        ///     Displays whether endpoints are loaded and the API is ready for use.
        /// </summary>
        public bool IsReady { get; private set; }

        /// <summary>
        ///     Call this to initialize the Modular API.<br />
        ///     <remarks>
        ///         API methods will be unusable until the endpoints are populated. Check <see cref="IsReady" /> or utilize
        ///         <see cref="OnReady" /> for safety.
        ///     </remarks>
        /// </summary>
        /// <param name="modContext"></param>
        /// <param name="onLoad">Method to be triggered when the API is ready.</param>
        /// <exception cref="Exception"></exception>
        public void Init(IMyModContext modContext, Action onLoad = null)
        {
            if (_isRegistered)
                throw new Exception($"{GetType().Name}.Load() should not be called multiple times!");

            _modContext = modContext;
            OnReady = onLoad;
            _isRegistered = true;
            MyAPIGateway.Utilities.RegisterMessageHandler(ApiChannel, HandleMessage);
            MyAPIGateway.Utilities.SendModMessage(ApiChannel, "ApiEndpointRequest");
            MyLog.Default.WriteLineAndConsole(
                $"{_modContext.ModName}: DefinitionAPI listening for API methods...");
            Log.Info("DefinitionApi", "Listening for API methods...");
        }

        /// <summary>
        ///     Call this to unload the Modular API; i.e. in case of instantiating a new API or for freeing up resources.
        ///     <remarks>
        ///         This method will also be called automatically when the Modular Assemblies Framework is
        ///         closed.
        ///     </remarks>
        /// </summary>
        public void UnloadData()
        {
            MyAPIGateway.Utilities.UnregisterMessageHandler(ApiChannel, HandleMessage);

            if (_apiInit)
                ApiAssign(); // Clear API methods if the API is currently inited.

            _isRegistered = false;
            _apiInit = false;
            IsReady = false;
            OnReady = null;
            MyLog.Default.WriteLineAndConsole($"{_modContext.ModName}: DefinitionAPI unloaded.");
            Log.Info("DefinitionApi", "Unloaded.");
        }

        // These sections are what the user can actually see when referencing the API, and can be used freely. //
        // Note the null checks. //

        #region Definitions

        /// <summary>
        /// Register a definition.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="definitionId"></param>
        /// <param name="definition"></param>
        public void RegisterDefinition<T>(string definitionId, T definition) where T : class
        {
            byte[] data = MyAPIGateway.Utilities.SerializeToBinary(definition);
            _registerDefinition?.Invoke(definitionId, typeof(T), data);
        }

        /// <summary>
        /// Retrieve a definition of a given type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="definitionId"></param>
        /// <returns></returns>
        public T GetDefinition<T>(string definitionId) where T : class
        {
            byte[] data = _getDefinition?.Invoke(definitionId, typeof(T));
            return data == null ? null : MyAPIGateway.Utilities.SerializeFromBinary<T>(data);
        }

        /// <summary>
        /// Returns an array of all definitionIds of a given type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public string[] GetDefinitionsOfType<T>() where T : class => _getDefinitionsOfType?.Invoke(typeof(T));

        /// <summary>
        /// Remove a definition from the list.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="definitionId"></param>
        public void RemoveDefinition<T>(string definitionId) where T : class => _removeDefinition?.Invoke(definitionId, typeof(T));

        #endregion

        #region Delegates

        /// <summary>
        /// Register delegates attached to a function.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="definitionId"></param>
        /// <param name="delegates"></param>
        public void RegisterDelegates<T>(string definitionId, Dictionary<string, Delegate> delegates) =>
            _registerDelegates?.Invoke(definitionId, typeof(T), delegates);

        /// <summary>
        /// Retrieve delegates attached to a definition.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="definitionId"></param>
        /// <returns></returns>
        public Dictionary<string, Delegate> GetDelegates<T>(string definitionId) =>
            _getDelegates?.Invoke(definitionId, typeof(T));

        #endregion

        #region Actions

        /// <summary>
        /// Register an action triggered on update. Arg1 is definitionId, Arg2 is updateType (0 = update, 1 = removal, 2 = delegateUpdate)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="action"></param>
        public void RegisterOnUpdate<T>(Action<string, int> action) => _registerOnUpdate?.Invoke(typeof(T), action);

        /// <summary>
        /// Remove an update action.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="action"></param>
        public void UnregisterOnUpdate<T>(Action<string, int> action) => _unregisterOnUpdate?.Invoke(typeof(T), action);

        #endregion

        #region Logging

        /// <summary>
        /// Write debug information to the DefinitionHelper logs.
        /// </summary>
        /// <param name="text"></param>
        public void LogDebug(string text) => _logDebug?.Invoke($"[{_modContext.ModName}] " + text);

        /// <summary>
        /// Write information to the DefinitionHelper logs.
        /// </summary>
        /// <param name="text"></param>
        public void LogInfo(string text) => _logInfo?.Invoke($"[{_modContext.ModName}] " + text);

        #endregion

        // This section lists all the delegates that will be assigned and utilized below. //

        #region Delegates

        // Definitions
        private Action<string, Type, byte[]> _registerDefinition;
        private Func<string, Type, byte[]> _getDefinition;
        private Func<Type, string[]> _getDefinitionsOfType;
        private Action<string, Type> _removeDefinition;

        // Delegates
        private Action<string, Type, Dictionary<string, Delegate>> _registerDelegates;
        private Func<string, Type, Dictionary<string, Delegate>> _getDelegates;

        // Actions
        private Action<Type, Action<string, int>> _registerOnUpdate;
        private Action<Type, Action<string, int>> _unregisterOnUpdate;

        // Logging
        private Action<string> _logDebug;
        private Action<string> _logInfo;

        #endregion

        #region API Initialization

        private bool _isRegistered;
        private bool _apiInit;
        private const long ApiChannel = 8754;
        private IReadOnlyDictionary<string, Delegate> _methodMap;
        private IMyModContext _modContext;

        /// <summary>
        ///     Assigns all API methods. Internal function, avoid editing.
        /// </summary>
        /// <returns></returns>
        private bool ApiAssign()
        {
            _apiInit = _methodMap != null;

            // Definitions
            SetApiMethod("RegisterDefinition", ref _registerDefinition);
            SetApiMethod("GetDefinition", ref _getDefinition);
            SetApiMethod("GetDefinitionsOfType", ref _getDefinitionsOfType);
            SetApiMethod("RemoveDefinition", ref _removeDefinition);

            // Delegates
            SetApiMethod("RegisterDelegates", ref _registerDelegates);
            SetApiMethod("GetDelegates", ref _getDelegates);

            // Actions
            SetApiMethod("RegisterOnUpdate", ref _registerOnUpdate);
            SetApiMethod("UnregisterOnUpdate", ref _unregisterOnUpdate);

            // Logging
            SetApiMethod("LogDebug", ref _logDebug);
            SetApiMethod("LogInfo", ref _logInfo);

            // Unload data if told to by the framework, otherwise notify that the API is ready.
            if (_methodMap == null)
            {
                UnloadData();
                return false;
            }

            _methodMap = null;
            return true;
        }

        /// <summary>
        ///     Assigns a single API endpoint.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name">Shared endpoint name; matches with the framework mod.</param>
        /// <param name="method">Method to assign.</param>
        /// <exception cref="Exception"></exception>
        private void SetApiMethod<T>(string name, ref T method) where T : class
        {
            if (_methodMap == null)
            {
                method = null;
                return;
            }

            if (!_methodMap.ContainsKey(name))
                throw new Exception("Method Map does not contain method " + name);
            var del = _methodMap[name];
            if (del.GetType() != typeof(T))
                throw new Exception(
                    $"Method {name} type mismatch! [MapMethod: {del.GetType().Name} | ApiMethod: {typeof(T).Name}]");
            method = _methodMap[name] as T;
        }

        /// <summary>
        ///     Triggered whenever the API receives a message from the framework mod.
        /// </summary>
        /// <param name="obj"></param>
        private void HandleMessage(object obj)
        {
            try
            {
                if (_apiInit || obj is string || obj == null) // the "ApiEndpointRequest" message will also be received here, we're ignoring that
                    return;

                var tuple = (MyTuple<int, IReadOnlyDictionary<string, Delegate>>)obj;
                var receivedVersion = tuple.Item1;
                var dict = tuple.Item2;

                if (receivedVersion != ApiVersion)
                    LogInfo($"Expected API version ({ApiVersion}) differs from received API version {receivedVersion}; errors may occur.");

                _methodMap = dict;

                if (!ApiAssign()) // If we're unassigning the API, don't notify when ready
                    return;

                IsReady = true;
                LogInfo($"Definition API v{ApiVersion} loaded!");
                Log.Info("DefinitionApi", $"Definition API v{ApiVersion} loaded!");
                OnReady?.Invoke();
            }
            catch (Exception ex)
            {
                // We really really want to notify the player if something goes wrong here.
                MyLog.Default.WriteLineAndConsole($"{_modContext.ModName}: Exception in DefinitionApi! " + ex);
                MyAPIGateway.Utilities.ShowMessage(_modContext.ModName, "Exception in DefinitionApi!\n" + ex);
                if (_logInfo != null)
                    LogInfo(ex.ToString());
            }
        }

        #endregion
    }
}
