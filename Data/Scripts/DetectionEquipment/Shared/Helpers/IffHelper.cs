using DetectionEquipment.Server.Networking;
using DetectionEquipment.Shared.BlockLogic.IffReflector;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Networking;
using DetectionEquipment.Shared.Utils;
using ProtoBuf;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.ModAPI;

namespace DetectionEquipment.Shared.Helpers
{
    internal static class IffHelper
    {
        private static Dictionary<IMyCubeGrid, HashSet<IffReflectorBlock>> _iffMap;
        private static Stack<int> _saltQueue = null;
        private static int _saltA = 5381, _saltB = 1566083941;

        public static void Load()
        {
            _iffMap = new Dictionary<IMyCubeGrid, HashSet<IffReflectorBlock>>();
            RefreshSalts();
        }

        public static void Update()
        {
            // hours to ticks
            if ((int)(MyAPIGateway.Session.GameplayFrameCounter % (GlobalData.IffResaltInterval.Value * 216000)) == 0)
                RefreshSalts();
        }

        public static void Unload()
        {
            _iffMap = null;
            _saltQueue = null;
        }

        public static void RegisterComponent(IMyCubeGrid grid, IffReflectorBlock component)
        {
            if (!_iffMap.ContainsKey(grid))
            {
                _iffMap.Add(grid, new HashSet<IffReflectorBlock> { component });
                return;
            }
            _iffMap[grid].Add(component);
        }

        public static void RemoveComponent(IMyCubeGrid grid, IffReflectorBlock component)
        {
            HashSet<IffReflectorBlock> compSet;
            if (!_iffMap.TryGetValue(grid, out compSet))
                return;
            compSet.Remove(component);
            if (compSet.Count == 0 || grid.Closed)
                _iffMap.Remove(grid);
        }

        public static string[] GetIffCodes(IMyCubeGrid grid, SensorDefinition.SensorType sensorType)
        {
            HashSet<IffReflectorBlock> map;
            if (!_iffMap.TryGetValue(grid, out map))
                return Array.Empty<string>();
            var codes = new HashSet<string>();
            foreach (var reflector in map)
                if (reflector.Enabled && (sensorType == SensorDefinition.SensorType.None || reflector.SensorType == sensorType))
                    codes.Add(reflector.IffCodeCache);

            var array = new string[codes.Count];
            int i = 0;
            foreach (var code in codes)
            {
                array[i] = code;
                i++;
            }

            return array;
        }

        private static void RefreshSalts()
        {
            if (!MyAPIGateway.Session.IsServer)
                return; // why would this ever get called???

            // populate saltQueue
            if (_saltQueue == null || _saltQueue.Count < 2)
            {
                int saltCount = (int)Math.Ceiling(24 / GlobalData.IffResaltInterval.Value) * 2;
                _saltQueue = new Stack<int>(saltCount);
                TimeSpan genTime;
                int[] primes = MathUtils.GeneratePrimesProfiled(saltCount, out genTime);
                foreach (int prime in primes)
                    _saltQueue.Push(prime);
                Log.Info("IffHelper", $"Generated {saltCount} prime salts in {genTime.TotalMilliseconds}ms.");
            }

            _saltA = _saltQueue.Pop();
            _saltB = _saltQueue.Pop();
            ServerNetwork.SendToEveryone(new IffSaltPacket(_saltA, _saltB));
            foreach (var gridMap in _iffMap.Values)
            {
                foreach (var iff in gridMap)
                {
                    iff.ForceUpdateHash();
                }
            }
            
            Log.Info("IffHelper", $"Cycled salts!\nA: {_saltA}\nB: {_saltB}");
        }

        public static int GetIffHashCode(string baseCode)
        {
            // honestly this was kind of a waste of time on my part; getting the codes directly from the enemy grid is a lot faster & more reliable than brute forcing.
            // *however*, between you and me - actually cracking the hashcode sounds a lot more interesting, so maybe give that a try? not that there's anything I can really do to stop you, haha
            //
            // do remember that we play games for fun.
            //
            // either way, best of luck!
            // - ari

            // also if you make me add a config option to restrict IFF codes to faction ID or whatever I am GOING to crack some skulls. probably my own first.

            // custom hashing algorithm featuring a strong avalanche effect to prevent reverse-engineering
            // uses large prime numbers, bit shifts and XORs to cause massive changes in the output when input is changed
            int num1 = _saltA;
            int num2 = _saltB;
            
            for (int i = 0; i < str.Length; ++i)
    		{
    			int c = str[i];
    
    			num1 ^= c;
    			num1 *= unchecked((int)0x85ebca6b);
    			num1 ^= (num1 >> 13);
    			num1 *= unchecked((int)0xc2b2ae35);
    			num1 ^= (num1 >> 16);
    
    			num2 ^= c + num1;
    			num2 *= 0x27d4eb2d;
    			num2 ^= (num2 >> 15);
    		}
    
    		int result = num1 ^ num2;
    		result ^= (result >> 16);
    		result *= unchecked((int)0x85ebca6b);
    		result ^= (result >> 13);
    		result *= unchecked((int)0xc2b2ae35);
    		result ^= (result >> 16);
            
            return result;
        }

        [ProtoContract]
        internal class IffSaltPacket : PacketBase
        {
            [ProtoMember(1)] private int _saltA;
            [ProtoMember(2)] private int _saltB;

            public IffSaltPacket(int saltA, int saltB)
            {
                _saltA = saltA;
                _saltB = saltB;
            }

            private IffSaltPacket() { }

            public override void Received(ulong senderSteamId, bool fromServer)
            {
                if (!fromServer)
                {
                    // there's way better methods than this, silly
                    Log.Info("IffSaltPacket", $"Player {senderSteamId} is trying to cheat! BOO!");
                    return;
                }

                IffHelper._saltA = _saltA;
                IffHelper._saltB = _saltB;

                foreach (var gridMap in _iffMap.Values)
                {
                    foreach (var iff in gridMap)
                    {
                        iff.ForceUpdateHash();
                    }
                }
            }
        }
    }
}
