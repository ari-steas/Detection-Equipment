using System.Collections.Generic;
using DetectionEquipment.Shared.Utils;

namespace DetectionEquipment.Shared.Structs
{
    internal static class ObjectPackager
    {
        // yes, there is a reference exploit for the PbApi related to this class.
        // if I catch wind of someone abusing it, everyone will suffer
        // pretty please don't do that
        // :(
        private static Dictionary<int, ObjectPool<object[]>> _objectBuffers;

        public static void Load()
        {
            _objectBuffers = new Dictionary<int, ObjectPool<object[]>>();
        }

        public static void Unload()
        {
            _objectBuffers = null;
        }

        public static object[] Package<TObject>(TObject toPackage) where TObject : IPackageable
        {
            if (toPackage == null)
                return null;

            ObjectPool<object[]> buffer;
            if (!_objectBuffers.TryGetValue(toPackage.FieldCount, out buffer))
            {
                buffer = new ObjectPool<object[]>(
                    () => new object[toPackage.FieldCount],
                    null,
                    CleanArray
                );
                _objectBuffers[toPackage.FieldCount] = buffer;
            }

            var fieldArray = buffer.Pop();
            toPackage.Package(fieldArray);
            return fieldArray;
        }

        public static void Return(object[] fieldArray)
        {
            ObjectPool<object[]> buffer;
            if (!_objectBuffers.TryGetValue(fieldArray.Length, out buffer))
                return;
            buffer.Push(fieldArray);
        }

        private static void CleanArray(object[] array)
        {
            for (int i = 0; i < array.Length; i++)
                array[i] = null;
        }
    }
}
