namespace DetectionEquipment.Shared.Structs
{
    internal interface IPackageable
    {
        int FieldCount { get; }
        void Package(object[] fieldArray);
    }
}
