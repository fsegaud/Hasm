namespace Natrium
{
    public interface IDevice
    {
        bool TryReadValue(int index, out double value);
        bool TryWriteValue(int index, double value);
    }
}