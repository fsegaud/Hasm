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
        
        Hasm.Processor processor = new Hasm.Processor(numDevices: 4);
        Screen screen = new Screen();
        processor.PlugDevice(1, screen);
        processor.PlugDevice(0, new Eeprom(32));
        
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
        string b64 = program.ToBase64();
        Console.WriteLine($"[dbg]-----------------------------------------------------------------------------------------");
        Console.WriteLine($"length: {b64.Length}    req_registers: {program.RequiredRegisters}    " +
                          $"req_stack: {program.RequiredStack}    req_devices: {program.RequiredDevices}    " +
#if HASM_FEATURE_MEMORY
                          $"req_memory: {program.RequiredMemory}" +
#endif
                          $"\n{b64}");
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

// 0: index (w)
// 1: value (rw)
// 2: length (r)
// 3: read_only (rw)
public class Eeprom : Hasm.IDevice
{
    private readonly double[] _memory;
    private uint _nextIndex;
    private bool _readOnly;

    public Eeprom(int size)
    {
        _memory = new double[size];
    }

    public Eeprom(double[] memory)
    {
        _memory = memory;
    }

    public bool TryReadValue(int index, out double value)
    {
        value = 0;
        switch (index)
        {
            case 1 :
                if (_nextIndex >= _memory.Length)
                    return false;
                value = _memory[_nextIndex];
                break;
            
            case 2:
                value = _memory.Length;
                break;
            
            case 3:
                value = _readOnly ? 1d : 0d;
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
                if (_nextIndex >= _memory.Length || _readOnly)
                    return false;
                _memory[_nextIndex] = value;
                break;
            
            case 3:
                _readOnly = value > 0d;
                break;
            
            default:
                return false;
        }

        return true;
    }
}