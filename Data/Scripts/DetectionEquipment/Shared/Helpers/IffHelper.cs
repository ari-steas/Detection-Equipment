using DetectionEquipment.Server.Networking;
using DetectionEquipment.Shared.BlockLogic.IffReflector;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Networking;
using DetectionEquipment.Shared.Utils;
using ProtoBuf;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Text;
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

        public static int GetIffHashCode(string baseCode, int saltA = int.MinValue, int saltB = int.MinValue)
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
            int num1 = saltA < 0  ? _saltA : saltA;
    		int num2 = saltB < 0 ? _saltB : saltB;
    
    		for (int i = 0; i < baseCode.Length; ++i)
    		{
    			int c = baseCode[i];
    
    			num1 ^= c;
    			num1 *= -2048144789; // Multiply by large prime to create chaotic diffusion
    			num1 ^= (num1 >> 13); // Spread high entropy bits to low entropy areas
    			num1 *= -1028477387;
    			num1 ^= (num1 >> 16);
    
    			num2 ^= c + num1;
    			num2 *= 0x27d4eb2d;
    			num2 ^= (num2 >> 15);
    		}
    
    		int result = num1 ^ num2;
    		result ^= (result >> 16);
    		result *= -2048144789;
    		result ^= (result >> 13);
    		result *= -1028477387;
    		result ^= (result >> 16);
            
            return result;
        }

        /// <summary>
        /// Hashes all individual letters [aA-zZ] and some random phrases, outputs results to deteq log.
        /// </summary>
        public static void TestHashing(int numSaltPairs)
        {
            TimeSpan genDuration;
            var primes = MathUtils.GeneratePrimesProfiled(numSaltPairs*2, out genDuration);

            string[] customTestCases = {
                "a", "b", "c", "d", "A", "B", "C", "D", "hi i'm ari", "awawawa", "awawaw"
            };

            int testColumnLength = 0;
            foreach (var test in customTestCases)
            {
                if (test.Length  > testColumnLength)
                    testColumnLength = test.Length;
            }

            StringBuilder sb = new StringBuilder()
                .Append(
                    "\n=========================================\n    IFF HASH TEST RESULTS    \nNumber of salt pairs: ")
                .Append(numSaltPairs)
                .Append("\nGeneration time: ")
                .Append(genDuration.TotalMilliseconds)
                .Append("ms\n\n");

            for (int i = 0; i < primes.Length; i += 2)
            {
                int saltA = primes[i];
                int saltB = primes[i+1];

                sb.AppendLine($"Salt pair {i/2}: <{saltA}, {saltB}>");
                foreach (var test in customTestCases)
                {
                    int hashed = GetIffHashCode(test, saltA, saltB);
                    sb.Append(test.PadRight(testColumnLength)).Append(" | ").Append(hashed.ToString("D10")).Append(" | ").Append(Convert.ToString(hashed, 2));
                    sb.AppendLine();
                }
            }
            

            sb.Append("\n=========================================\n");
            Log.Info("IffHelper", sb.ToString());
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
