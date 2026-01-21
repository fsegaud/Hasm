using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Hasm
{
    public class Processor
    {
        private readonly int _frequencyHz;
        private readonly double[] _registers;
        private readonly double[] _stack;
        private readonly IDevice?[] _devices;

        private uint _stackPointer;
        private uint _returnAddress;
        
        public  Result LastError { get; private set; }

        public Processor(int numRegistries = 8, int stackLength = 16, int numDevices = 0, int frequencyHz = 0)
        {
            _registers = new double[numRegistries];
            _stack = new double[stackLength];
            _devices = new IDevice[numDevices];
            _frequencyHz = frequencyHz;
        }

        public bool PlugDevice(uint deviceSlot, IDevice device)
        {
            if (deviceSlot >= _devices.Length)
                return false;
            
            _devices[deviceSlot] = device;
            return true;
        }

        public bool UnplugDevice(uint deviceSlot, IDevice device)
        {
            if (deviceSlot >= _devices.Length)
                return false;
            
            _devices[deviceSlot] = null;
            return true;
        }

        [SuppressMessage("ReSharper", "UnusedMethodReturnValue.Local")]
        private bool TrySetDestination(ref Instruction instruction, double value)
        {
            switch (instruction.DestinationRegistryType)
            {
                case Instruction.OperandType.UserRegister:
                {
                    if (instruction.Destination >= _registers.Length)
                    {
                        LastError = new Result(Error.RegisterOutOfBound, instruction);
                        return false;
                    }
                    
                    _registers[instruction.Destination] = value;
                    break;
                }
                
                case Instruction.OperandType.StackPointer:
                {
                    _stackPointer = (uint)value;
                    break;
                }
                
                case Instruction.OperandType.ReturnAddress:
                    _returnAddress = (uint)value;
                    break;
                
                case Instruction.OperandType.DeviceRegister:
                {
                    int deviceSlot = (int)instruction.Destination >> 16;
                    int deviceRegister = 0xffff & (int)instruction.Destination;
                    
                    if (deviceSlot > _devices.Length)
                    {
                        LastError = new Result(Error.DeviceOverflow, instruction);
                        return false;
                    }
                    
                    IDevice? device = _devices[deviceSlot];
                    if (device != null)
                    {
                        if (!(bool)_devices[deviceSlot]?.TryWriteValue(deviceRegister, value))
                        {
                            LastError = new Result(Error.DeviceFailed, instruction);
                            return false;
                        }
                    }
                    else
                    {
                        LastError = new Result(Error.DeviceUnplugged, instruction);
                        return false;
                    }

                    break;
                }
                
                case Instruction.OperandType.Literal:
                    throw new InvalidOperationException();
                
                default:
                    throw new NotImplementedException();
            }

            return true;
        }

        private bool TryGetDestination(ref Instruction instruction, out double value)
        {
            value = 0d;
            switch (instruction.DestinationRegistryType)
            {
                case Instruction.OperandType.UserRegister:
                {
                    if (instruction.Destination >= _registers.Length)
                    {
                        LastError = new Result(Error.RegisterOutOfBound, instruction);
                        return false;
                    }
                    
                    value = _registers[instruction.Destination];
                    break;
                }

                case Instruction.OperandType.StackPointer: value = _stackPointer; break;
                case Instruction.OperandType.ReturnAddress: value = _returnAddress; break;

                case Instruction.OperandType.DeviceRegister:
                {
                    int deviceSlot = (int)instruction.Destination >> 16;
                    int deviceRegister = 0xffff & (int)instruction.Destination;
                    
                    if (deviceSlot > _devices.Length)
                    {
                        LastError = new Result(Error.DeviceOverflow, instruction);
                        return false;
                    }
                    
                    IDevice? device = _devices[deviceSlot];
                    if (device != null)
                    {
                        if (!(bool)_devices[deviceSlot]?.TryReadValue(deviceRegister, out value))
                        {
                            LastError = new Result(Error.DeviceFailed, instruction);
                            return false;
                        }
                    }
                    else
                    {
                        LastError = new Result(Error.DeviceUnplugged, instruction);
                        return false;
                    }

                    break;
                }
                    
                case Instruction.OperandType.Literal:
                    throw new InvalidOperationException();
                
                default:
                    throw new NotImplementedException();
            }

            return true;
        }

        private bool TryGetOperandValue(ref Instruction instruction, Instruction.OperandType type, ref double value)
        {
            switch (type)
            {
                case Instruction.OperandType.Literal: break;
                case Instruction.OperandType.StackPointer: value = _stackPointer; break;
                case Instruction.OperandType.ReturnAddress: value = _returnAddress; break;
                case Instruction.OperandType.UserRegister:
                {
                    if (value < 0 || value >= _registers.Length)
                    {
                        LastError = new Result(Error.RegisterOutOfBound, instruction);
                        return false;
                    }
                    value = _registers[(int)value]; break;
                }
                
                case Instruction.OperandType.DeviceRegister:
                {
                    int deviceSlot = (int)value >> 16;
                    int deviceRegister = 0xffff & (int)value;
                    
                    if (deviceSlot > _devices.Length)
                    {
                        LastError = new Result(Error.DeviceOverflow, instruction);
                        return false;
                    }
                    
                    IDevice? device = _devices[deviceSlot];
                    if (device != null)
                    {
                        if (!(bool)_devices[deviceSlot]?.TryReadValue(deviceRegister, out value))
                            LastError = new Result(Error.DeviceFailed, instruction);
                    }
                    else
                        LastError = new Result(Error.DeviceUnplugged, instruction);

                    break;
                }
                
                default: throw new ArgumentOutOfRangeException();
            }

            return true;
        }
        
        public bool Run(Program program, Action<string>? debugCallback = null, DebugData debugData = DebugData.None)
        {
            _stackPointer = 0;
            _returnAddress = 0;

            LastError = Result.Success();

            if (_registers.Length < program.RequiredRegisters || _stack.Length < program.RequiredStack || 
                _devices.Length < program.RequiredDevices)
            {
                LastError = new Result(Error.RequirementsNotMet);
                return false;
            }
            
            bool breakLoop = false;
            for (int index = 0; index < program.Instructions.Length && !breakLoop; index++)
            {
                Instruction instruction = program.Instructions[index];

                double destinationValue;
                double leftOperandValue = instruction.LeftOperandValue;
                double rightOperandValue = instruction.RightOperandValue;
                
                if (!TryGetOperandValue(ref instruction, instruction.LeftOperandType, ref leftOperandValue) ||
                    !TryGetOperandValue(ref instruction, instruction.RightOperandType, ref rightOperandValue))
                    return false;
                    
                switch (instruction.Operation)
                {
                    case Operation.Nop:
                        break;
                    
                    case Operation.Move:
                        TrySetDestination(ref instruction, leftOperandValue);
                        break;
                    
                    case Operation.Add:
                        TrySetDestination(ref instruction, leftOperandValue + rightOperandValue);
                        break;
                    
                    case Operation.Subtract:
                        TrySetDestination(ref instruction, leftOperandValue - rightOperandValue);
                        break;
                    
                    case Operation.Multiply:
                        TrySetDestination(ref instruction, leftOperandValue * rightOperandValue);
                        break;
                    
                    case Operation.Divide:
                        if (rightOperandValue == 0)
                        {
                            LastError = new Result(Error.DivisionByZero, instruction);
                            return false;
                        }

                        TrySetDestination(ref instruction, leftOperandValue / rightOperandValue);
                        break;
                    
                    case Operation.SquareRoot:
                        if (leftOperandValue < 0)
                        {
                            LastError = new Result(Error.NaN, instruction);
                            return false;
                        }

                        TrySetDestination(ref instruction, Math.Sqrt(leftOperandValue));
                        break;
                    
                    case Operation.Increment:
                    {
                        if (!TryGetDestination(ref instruction, out destinationValue))
                            return false;
                        TrySetDestination(ref instruction, destinationValue + 1d);
                        break;
                    }
                    
                    case Operation.Decrement:
                    {
                        if (!TryGetDestination(ref instruction, out destinationValue))
                            return false;
                        TrySetDestination(ref instruction, destinationValue - 1d);
                        break;
                    }
                    
                    case Operation.Equal:
                        TrySetDestination(ref instruction, Math.Abs(leftOperandValue - rightOperandValue) < double.Epsilon ? 1d : 0d);
                        break;
                    
                    case Operation.NotEqual:
                        TrySetDestination(ref instruction, Math.Abs(leftOperandValue - rightOperandValue) < double.Epsilon ? 0d : 1d);
                        break;
                    
                    case Operation.GreaterThan:
                        TrySetDestination(ref instruction, leftOperandValue > rightOperandValue ? 1d : 0d);
                        break;
                    
                    case Operation.GreaterThanOrEqual:
                        TrySetDestination(ref instruction, leftOperandValue >= rightOperandValue ? 1d : 0d);
                        break;
                    
                    case Operation.LesserThan:
                        TrySetDestination(ref instruction, leftOperandValue < rightOperandValue ? 1d : 0d);
                        break;
                    
                    case Operation.LesserThanOrEqual:
                        TrySetDestination(ref instruction, leftOperandValue <= rightOperandValue ? 1d : 0d);
                        break;
                    
                    case Operation.Push:
                        if (_stackPointer >= _stack.Length)
                        {
                            LastError = new Result(Error.StackOverflow, instruction);
                            return false;
                        }

                        if (!TryGetDestination(ref instruction, out destinationValue))
                            return false;
                        _stack[_stackPointer++] = destinationValue;
                        break;
                    
                    case Operation.Pop:
                        if (_stackPointer == 0)
                        {
                            LastError = new Result(Error.StackOverflow, instruction);
                            return false;
                        }
                        TrySetDestination(ref instruction, _stack[--_stackPointer]);
                        break;
                    
                    case Operation.Peek:
                        if (_stackPointer == 0)
                        {
                            LastError = new Result(Error.StackOverflow, instruction);
                            return false;
                        }
                        TrySetDestination(ref instruction, _stack[_stackPointer - 1]);
                        break;
                    
                    case Operation.Assert:
                        if (!TryGetDestination(ref instruction, out destinationValue))
                            return false;
                        if (Math.Abs(destinationValue - leftOperandValue) > double.Epsilon)
                        {
                            LastError = new Result(Error.AssertFailed, instruction);
                            return false;
                        }
                        break;

                    case Operation.Jump:
                    {
                        var foundDestination = -1;
                        for (var searchIndex = 0; searchIndex < program.Instructions.Length; searchIndex++)
                        {
                            if (program.Instructions[searchIndex].Line == (int)leftOperandValue)
                            {
                                foundDestination = searchIndex;
                                break;
                            }
                        }

                        if (foundDestination < 0)
                        {
                            LastError = new Result(Error.InvalidJump, instruction);
                            return false;
                        }
                        index = foundDestination - 1;
                        break;
                    }

                    case Operation.JumpReturnAddress:
                    {
                        var foundDestination = -1;
                        for (var searchIndex = 0; searchIndex < program.Instructions.Length; searchIndex++)
                        {
                            if (program.Instructions[searchIndex].Line == (int)leftOperandValue)
                            {
                                foundDestination = searchIndex;
                                break;
                            }
                        }

                        if (foundDestination < 0)
                        {
                            LastError = new Result(Error.InvalidJump, instruction);
                            return false;
                        }
                        index = foundDestination - 1;

                        // Find next instruction (+1 wouldn't ignore blank lines and comments).
                        for (var searchIndex = 0u; searchIndex < program.Instructions.Length; searchIndex++)
                        {
                            if (program.Instructions[searchIndex].Line > instruction.Line + 1)
                            {
                                _returnAddress = program.Instructions[searchIndex].Line;
                                break;
                            }
                        }

                        break;
                    }
                    
                    case Operation.Ret:
                        breakLoop = true;
                        break;

                    default:
                        LastError = new Result(Error.OperationNotImplemented, instruction);
                        return false;
                }

                if (LastError.Error != Error.Success)
                    return false;
                
                if ((debugData & DebugData.RawInstruction) > 0)
                    debugCallback?.Invoke($"processor > Raw[{instruction.Line:d4}]: " + instruction.RawText);
                
                if ((debugData & DebugData.CompiledInstruction) > 0)
                    debugCallback?.Invoke($"processor > Cmp[{instruction.Line:d4}]: " + instruction);
                
                if ((debugData & DebugData.Memory) > 0)
                    debugCallback?.Invoke($"processor > Mem[{instruction.Line:d4}]: " + DumpMemory());
                
                if ((debugData & DebugData.Separator) > 0)
                    debugCallback?.Invoke("-------------------------------------------------------------------------" +
                                          "-----------------------------------------------");
                
                if (_frequencyHz > 0)
                    Thread.Sleep(1000 / _frequencyHz);
            }

            return true;
        }

        public double ReadRegistry(int registry)
        {
            return registry >= 0 && registry < _registers.Length ? _registers[registry] : double.NaN;
        }

        public string DumpMemory()
        {
            return $"Sp: {_stackPointer:d4} Ra: {_returnAddress:d4} " + 
                   $"Registries: {string.Join(" ", _registers)} " +
                   $"Stack: {string.Join(" ", _stack)} ";
        }
    }
}