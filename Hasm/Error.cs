namespace Hasm
{
    public enum Error
    {
        Success = 0,
        
        // Compiler.
        SyntaxError = 100,
        OperationNotSupported,
        
        // Processor
        ProgramNotLoaded = 200,
        RequirementsNotMet,
        OperationNotImplemented,
        RegisterOutOfBound,
        DivisionByZero,
        NaN,
        StackOverflow,
        InvalidJump,
        LabelNotFound,
        DeviceOverflow,
        DeviceUnplugged,
        DeviceFailed,
#if HASM_FEATURE_MEMORY
        OutOfMemory,
        MemoryViolation,
        NullPointer,
        DoubleFree,
#endif
        
        AssertFailed = 900,
    }
}