namespace Hasm
{
    public enum Error
    {
        Success = 0,
        
        // Compiler.
        SyntaxError = 100,
        OperationNotSupported,
        
        // Processor
        RequirementsNotMet = 200,
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
        
        AssertFailed = 900,
    }
}