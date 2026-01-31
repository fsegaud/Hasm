namespace GameTest;

// 0 -> Index
// 1 -> Value
public class VirtualScreen(int width, int height) : Hasm.IDevice
{
    private uint _nextIndex;
    
    public char[] Data { get; } = new char[width * height];
    public int Width { get; private set; } = width;
    public int Height { get; private set; } = height;
    
    public bool TryReadValue(int index, out double value)
    {
        value = 0;
        switch (index)
        {
            case 1 :
                if (_nextIndex >= Data.Length)
                    return false;
                value = Data[_nextIndex];
                break;
            
            default:
                return false;
        }

        return true;
    }

    public bool TryWriteValue(int index, double value)
    {
        switch (index)
        {
            case 0 :
                if (value < 0 || value >= Data.Length)
                    return false;
                _nextIndex = (uint)value;
                break;
            
            case 1 :
                if (_nextIndex >= Data.Length)
                    return false;
                Data[_nextIndex] = (char)value;
                break;

            default:
                return false;
        }

        return true;
    }
}
