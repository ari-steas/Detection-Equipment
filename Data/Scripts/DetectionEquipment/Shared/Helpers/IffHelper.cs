using DetectionEquipment.Server.Networking;
using DetectionEquipment.Shared.BlockLogic.IffReflector;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Networking;
using DetectionEquipment.Shared.Utils;
using ProtoBuf;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage.Game.ModAPI;

namespace DetectionEquipment.Shared.Helpers
{
    internal static class IffHelper
    {
        private static Dictionary<IMyCubeGrid, IffInfo> _iffMap;
        private static Stack<int> _saltQueue = null;
        private static int _saltA = 5381, _saltB = 1566083941;

        public static void Load()
        {
            _iffMap = new Dictionary<IMyCubeGrid, IffInfo>();
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
                _iffMap.Add(grid, new IffInfo(new HashSet<IffReflectorBlock> { component }));
                return;
            }
            _iffMap[grid].Blocks.Add(component);
        }

        public static void RemoveComponent(IMyCubeGrid grid, IffReflectorBlock component)
        {
            IffInfo compSet;
            if (!_iffMap.TryGetValue(grid, out compSet))
                return;
            compSet.Blocks.Remove(component);
            if (compSet.Blocks.Count == 0 || grid.Closed)
                _iffMap.Remove(grid);
        }

        public static string[] GetIffCodes(IMyCubeGrid grid, SensorDefinition.SensorType sensorType)
        {
            IffInfo info;
            return _iffMap.TryGetValue(grid, out info) ? info.GetCodes(sensorType) : Array.Empty<string>();
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
            foreach (var info in _iffMap.Values)
            {
                info.LastUpdate = -1;
                foreach (var iff in info.Blocks)
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
            // courtesy of @its.taco.time
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
                "a", "b", "c", "d", "A", "B", "C", "D", "ari says hi", $"today is {DateTime.Now.DayOfWeek}", ""
            };

            int testColumnLength = 0;
            foreach (var test in customTestCases)
            {
                if (test.Length  > testColumnLength)
                    testColumnLength = test.Length;
            }

            StringBuilder sb = new StringBuilder()
                .Append(
                    "\n          IFF HASH TEST RESULTS\n=========================================\nNumber of salt pairs: ")
                .Append(numSaltPairs)
                .Append("\nGeneration time: ")
                .Append(genDuration.TotalMilliseconds)
                .Append("ms\n");

            for (int i = 0; i < primes.Length; i += 2)
            {
                int saltA = primes[i];
                int saltB = primes[i+1];

                sb.AppendLine($"\nSalt pair {i/2}: <{saltA}, {saltB}>");
                foreach (var test in customTestCases)
                {
                    int hashed = GetIffHashCode(test, saltA, saltB);
                    sb.Append((test == "" ? "(empty string)" : test).PadRight(testColumnLength)).Append(" | ").Append(hashed.ToString("N0").PadLeft(13)).Append(" | ").Append(Convert.ToString(hashed, 2).PadLeft(32, '0'));
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

                foreach (var gridInfo in _iffMap.Values)
                {
                    foreach (var iff in gridInfo.Blocks)
                    {
                        iff.ForceUpdateHash();
                    }
                }
            }

            public override PacketInfo GetInfo()
            {
                return PacketInfo.FromPacket(this,
                    new PacketInfo
                    {
                        PacketTypeName = nameof(_saltA),
                        PacketSize = sizeof(int)
                    },
                    new PacketInfo
                    {
                        PacketTypeName = nameof(_saltB),
                        PacketSize = sizeof(int)
                    }
                );
            }
        }

        private class IffInfo
        {
            public HashSet<IffReflectorBlock> Blocks;
            public int LastUpdate;
            private string[][] _codeCache;

            private static readonly int IffCodeCacheSize = Enum.GetValues(typeof(SensorDefinition.SensorType)).Cast<int>().Max();

            public IffInfo(HashSet<IffReflectorBlock> blocks)
            {
                Blocks = blocks;
                LastUpdate = -1;
                _codeCache = new string[IffCodeCacheSize][];
                for (int i = 0; i < IffCodeCacheSize; i++)
                    _codeCache[i] = Array.Empty<string>();
            }

            public string[] GetCodes(SensorDefinition.SensorType sensorType)
            {
                if (sensorType == SensorDefinition.SensorType.None)
                    return Array.Empty<string>();

                // don't pass in "None" otherwise it crashes )))
                int sType = (int) sensorType - 1;
                if (MyAPIGateway.Session.GameplayFrameCounter == LastUpdate)
                    return _codeCache[sType];

                var codes = new HashSet<string>();
                foreach (var reflector in Blocks)
                    if (reflector.Enabled && reflector.SensorType == sensorType)
                        codes.Add(reflector.IffCodeCache);

                string[] array;
                if (_codeCache[sType].Length == codes.Count)
                    array = _codeCache[sType];
                else
                    array = new string[codes.Count];

                int i = 0;
                foreach (var code in codes)
                {
                    array[i] = code;
                    i++;
                }

                _codeCache[sType] = array;
                LastUpdate = MyAPIGateway.Session.GameplayFrameCounter;

                return array;
            }
        }
    }
}
