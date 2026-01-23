using System.Diagnostics.CodeAnalysis;

namespace HasmTest;

class Program
{
    [SuppressMessage("ReSharper", "UnusedParameter.Local")]
    static void Main(string[] args)
    {
        string sourceFile = "../../../test.hasm";
        
        Hasm.Compiler compiler = new Hasm.Compiler();
        Hasm.Program? program = compiler.Compile(File.ReadAllText(sourceFile));
        if (program == null)
        {
            Hasm.Result error = compiler.LastError;
            Console.Error.WriteLine($"Compilation Error: {error.Error} ({error.Error:D}) '{error.RawInstruction}' at line {error.Line}");
            return;
        }

        PrintProgramInfo(program);
        
        Hasm.Processor processor = new Hasm.Processor(numDevices: 3);
        Screen screen = new Screen();
        processor.PlugDevice(2, screen);
        processor.PlugDevice(1, new Eeprom(32));
        processor.PlugDevice(0, new Rom(['0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
                                                    'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 
                                                    'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 
                                                    'u', 'v', 'w', 'x', 'y', 'z']));
        
        if (!processor.Run(program, DebugCallback))
        {
            Hasm.Result error = processor.LastError;
            Console.Error.WriteLine( $"Runtime Error: {error.Error} ({error.Error:D}) '{error.RawInstruction}' at line {error.Line}");
            return;
        }
        
        Console.WriteLine(screen.Display);
    }
    
    static void DebugCallback(Hasm.DebugData data)
    {
        Console.WriteLine($"[dbg]    ln: {data.Line:d4} > {data.RawInstruction}");
        Console.WriteLine($"[dbg]    ra: {data.ReturnAddress:d4}   registers: {string.Join(' ',  data.Registers)}");
        Console.WriteLine($"[dbg]    sp: {data.StackPointer:d4}       stack: {string.Join(' ', data.Stack)}");
#if HASM_FEATURE_MEMORY
        Console.WriteLine($"[dbg]                  memory: {string.Join(' ',  data.Memory)}");
        Console.WriteLine($"[dbg]               memblocks: {string.Join(' ',  data.MemoryBlocks)}");
#endif
        Console.WriteLine($"[dbg]-----------------------------------------------------------------------------------------");
    }

    static void PrintProgramInfo(Hasm.Program program)
    {
        Console.WriteLine($"[dbg]-----------------------------------------------------------------------------------------");
        Console.WriteLine(program.ToBase64());
        Console.WriteLine($"[dbg]-----------------------------------------------------------------------------------------");
    }
}

public class Screen : Hasm.IDevice
{
    public string Display { get; private set; } = string.Empty;
    
    public bool TryReadValue(int index, out double value)
    {
        value = 0d;
        return false;
    }

    public bool TryWriteValue(int index, double value)
    {
        switch (index)
        {
            case 0 :
                // Appends a character to the display.
                Display += (char)value;
                break;
            
            case 1 :
                // Reset the display.
                Display = string.Empty;
                break;
            
            default:
                return false;
        }

        return true;
    }
}

// Mode 0: Read char at index.
// Mode 1: Write index at 0, read value.
public class Rom(double[] memory) : Hasm.IDevice
{
    private double? _nextRead = null;

    public bool TryReadValue(int index, out double value)
    {
        if (_nextRead != null)
        {
            value = _nextRead.Value;
            _nextRead = null;
            return true;
        }
        
        value = 0;
        if (index < 0 || index >= memory.Length)
            return false;

        value = memory[index];
        return true;
    }

    public bool TryWriteValue(int index, double value)
    {
        if (index == 0)
        {
            int readIndex = (int)value;
            if (readIndex < 0 || readIndex >= memory.Length)
                return false;
            
            _nextRead = memory[readIndex];
            return true;
        }

        return false;
    }
}

public class Eeprom(int size) : Hasm.IDevice
{
    private readonly double[] _memory = new double[size];

    private uint _nextIndex;

    public bool TryReadValue(int index, out double value)
    {
        value = 0;
        switch (index)
        {
            case 0:
                return false;
            
            case 1 :
                if (_nextIndex >= _memory.Length)
                    return false;
                value = _memory[_nextIndex];
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
                if (value < 0 || value >= _memory.Length)
                    return false;
                _nextIndex = (uint)value;
                break;
            
            case 1 :
                if (_nextIndex >= _memory.Length)
                    return false;
                _memory[_nextIndex] = value;
                break;
            
            default:
                return false;
        }

        return true;
    }
}