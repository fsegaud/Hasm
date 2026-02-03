namespace GameTest;

// 0 -> KeyCode
public class VirtualKeyboard : Natrium.IDevice
{
    public int KeyCode { get; set; }
    
    public bool TryReadValue(int index, out double value)
    {
        value = KeyCode;
        KeyCode = -1;
        return true;
    }

    public bool TryWriteValue(int index, double value)
    {
        return false;
    }
}