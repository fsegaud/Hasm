namespace HasmTest;

class Program
{
    static void Main(string[] args)
    {
        string sourceFile = "../../../test.hasm";
        
        Hasm.Compiler compiler = new Hasm.Compiler();
        Hasm.Program program = new Hasm.Program();
        var result = compiler.Compile(File.ReadAllText(sourceFile), ref program);
        if (result.Error != Hasm.Error.Success)
        {
            Console.Error.WriteLine($"Compilation Error: {result.Error} ({result.Error:D}) at line {result.Line}");
        }
        
        Hasm.Processor processor = new Hasm.Processor(8);
        result = processor.Run(program, DebugCallback);
        if (result.Error != Hasm.Error.Success)
        {
            Console.Error.WriteLine(
                $"Runtime Error: {result.Error} ({result.Error:D}) '{result.RawInstruction}' at line {result.Line}");
            Console.WriteLine(processor.DumpMemory());
        }
    }
    
    static void DebugCallback(string msg)
    {
        Console.WriteLine("[dbg] " + msg);
    }
}
