using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

// TODO: Stack
// TODO: Memory
// TODO: Jumps

namespace Hasm
{
    public enum Error
    {
        Success = 0,
        
        // Compiler.
        SyntaxError = 100,
        OperationNotSupported,
        
        // Processor
        OperationNotImplemented = 200,
        RegistryOutOfBound,
        DivisionByZero,
        NaN,
        
        AssertFailed = 300,
    }

    public struct Result
    {
        public readonly Error Error;
        public readonly string? RawInstruction;
        public readonly int Line;

        internal static Result Success()
        {
            return new Result(Error.Success);
        }
        
        internal Result(Error error, Instruction instruction)
        {
            Error = error;
            RawInstruction = instruction.RawText;
            Line = instruction.Line;
        }
        
        internal Result(Error error, int line = 0, string? rawInstruction = null)
        {
            Error = error;
            RawInstruction = rawInstruction;
            Line = line;
        }
    }
    
    public class Processor
    {
        private readonly float[] _registries;

        public Processor(int numRegistries = 4)
        {
            _registries = new float[numRegistries];
        }
        
        public Result Run(Program program, Action<string>? debugCallback = null)
        {
            for (int i = 0; i < program.Instructions.Length; i++)
            {
                Instruction instruction = program.Instructions[i];

                if (instruction.DestinationRegistry < 0 || instruction.DestinationRegistry >= _registries.Length)
                    return new Result(Error.RegistryOutOfBound, instruction);
                    
                if (instruction.LeftOperandType == OperandType.Registry 
                    && (instruction.LeftOperandValue < 0 || instruction.LeftOperandValue >= _registries.Length))
                    return new Result(Error.RegistryOutOfBound, instruction);
                    
                if (instruction.RightOperandType == OperandType.Registry 
                    && (instruction.RightOperandValue < 0 || instruction.RightOperandValue >= _registries.Length))
                    return new Result(Error.RegistryOutOfBound, instruction);
                    
                float leftOperandValue = instruction.LeftOperandType == OperandType.Registry 
                    ? _registries[(int)instruction.LeftOperandValue] 
                    : instruction.LeftOperandValue;
                float rightOperandValue = instruction.RightOperandType == OperandType.Registry 
                    ? _registries[(int)instruction.RightOperandValue] 
                    : instruction.RightOperandValue;
                
                switch (instruction.Operation)
                {
                    case Operation.Nop:
                        break;
                    
                    case Operation.Move:
                        _registries[instruction.DestinationRegistry] = leftOperandValue;
                        break;
                    
                    case Operation.Add:
                        _registries[instruction.DestinationRegistry] = leftOperandValue + rightOperandValue;
                        break;
                    
                    case Operation.Subtract:
                        _registries[instruction.DestinationRegistry] = leftOperandValue - rightOperandValue;
                        break;
                    
                    case Operation.Multiply:
                        _registries[instruction.DestinationRegistry] = leftOperandValue * rightOperandValue;
                        break;
                    
                    case Operation.Divide:
                        if (rightOperandValue == 0)
                            return new Result(Error.DivisionByZero, instruction);
                        _registries[instruction.DestinationRegistry] = leftOperandValue / rightOperandValue;
                        break;
                    
                    case Operation.SquareRoot:
                        if (leftOperandValue < 0 )
                            return new Result(Error.NaN, instruction);
                        _registries[instruction.DestinationRegistry] = (float)Math.Sqrt(leftOperandValue);
                        break;
                    
                    case Operation.Assert:
                        if (Math.Abs(_registries[instruction.DestinationRegistry] - leftOperandValue) > float.Epsilon)
                            return new Result(Error.AssertFailed, instruction);
                        break;
                    
                    default:
                        return new Result(Error.OperationNotImplemented, instruction);
                }
                
                debugCallback?.Invoke($"processor > Exe[{instruction.Line}]: " + instruction);
                debugCallback?.Invoke($"processor > Reg[{instruction.Line}]: [ " + string.Join(" ", _registries) + " ]");
            }

            return Result.Success();
        }

        public float ReadRegistry(int registry)
        {
            return registry >= 0 && registry < _registries.Length ? _registries[registry] : float.NaN;
        }

        public string DumpMemory()
        {
            return $"Registries: {string.Join(" ", _registries)}";
        }
    }

    public class Compiler
    {
        public Result Compile(string input, ref Program program, Action<string>? debugCallback = null)
        {
            List<Instruction> instructions = new List<Instruction>();
            
            string[] lines = input.Split('\n');
            for (var index = 0; index < lines.Length; index++)
            {
                // Pre-parse.
                lines[index] =  lines[index].Trim();
                
                if (string.IsNullOrEmpty(lines[index]))
                    continue;
                
                //  Empty.

                Regex regex = new Regex(@"^[\s\t]*$");
                Match match = regex.Match(lines[index]);

                if (match.Success)
                    continue;
                
                // Comments.

                regex = new Regex(@".*(?<com>;.*)"); // TODO: One Regex object per expression.
                match = regex.Match(lines[index]);

                if (match.Success)
                {
                    lines[index] = lines[index].Replace(match.Groups["com"].Value, string.Empty).TrimEnd();

                    if (string.IsNullOrEmpty(lines[index]))
                        continue;
                }
                
                // Self operations.

                regex = new Regex(@"^(?<opt>nop)$");
                match = regex.Match(lines[index]);

                if (match.Success)
                {
                    string opt = match.Groups["opt"].Value;

                    Instruction instruction = default;
                    instruction.RawText = lines[index];
                    instruction.Line = index + 1;
                    
                    switch (opt)
                    {
                        case "nop": instruction.Operation = Operation.Nop; break;
                        default: return new Result(Error.OperationNotSupported, instruction);
                    }

                    instructions.Add(instruction);
                    debugCallback?.Invoke("compiler > " + instruction);

                    continue;
                }

                // Unary operations.

                // regex = new Regex(@"(?<opt>mov|sqrt)\s+(?<opd>r\d+)\s+(?<opl>r?\d+[.]?\d*)");
                regex = new Regex(@"^(?<opt>mov|sqrt|assert)\s+(?<opd>r\d+\b)\s+(?<opl>-?\d+[.]?\d*|r\d+\b)$");
                match = regex.Match(lines[index]);

                if (match.Success)
                {
                    string opt = match.Groups["opt"].Value;
                    string opd = match.Groups["opd"].Value;
                    string opl = match.Groups["opl"].Value;
                    
                    Instruction instruction = default;
                    instruction.RawText = lines[index];
                    instruction.Line = index + 1;
                    
                    switch (opt)
                    {
                        case "mov": instruction.Operation = Operation.Move; break;
                        case "sqrt": instruction.Operation = Operation.SquareRoot; break;
                        case "assert": instruction.Operation = Operation.Assert; break;
                        default: return new Result(Error.OperationNotSupported, instruction);
                    }
                    
                    instruction.DestinationRegistry = int.Parse(opd.Substring(1));
                    
                    if (opl[0] == 'r')
                    {
                        instruction.LeftOperandType = OperandType.Registry;
                        instruction.LeftOperandValue = int.Parse(opl.Substring(1));
                    }
                    else
                    {
                        instruction.LeftOperandType = OperandType.Literal;
                        instruction.LeftOperandValue = float.Parse(opl, CultureInfo.InvariantCulture);
                    }
                    
                    instructions.Add(instruction);
                    debugCallback?.Invoke("compiler > " + instruction);

                    continue;
                }

                // Binary operations.

                // regex = new Regex(
                //     @"^(?<opt>add|sub|mul|div)\s+(?<opd>r\d+)\s+(?<opl>r?\d+[.]?\d*)\s+(?<opr>r?\d+[.]?\d*)$");
                regex = new Regex(
                    @"^(?<opt>add|sub|mul|div)\s+(?<opd>r\d+\b)\s+(?<opl>-?\d+[.]?\d*|r\d+\b)\s+(?<opr>-?\d+[.]?\d*|r\d+\b)$");
                match = regex.Match(lines[index]);

                if (match.Success)
                {
                    string opt = match.Groups["opt"].Value;
                    string opd = match.Groups["opd"].Value;
                    string opl = match.Groups["opl"].Value;
                    string opr = match.Groups["opr"].Value;

                    Instruction instruction = default;
                    instruction.RawText = lines[index];
                    instruction.Line = index + 1;
                    
                    switch (opt)
                    {
                        case "add": instruction.Operation = Operation.Add; break;
                        case "sub": instruction.Operation = Operation.Subtract; break;
                        case "mul": instruction.Operation = Operation.Multiply; break;
                        case "div": instruction.Operation = Operation.Divide; break;
                        default: return new Result(Error.OperationNotSupported, instruction);
                    }

                    instruction.DestinationRegistry = int.Parse(opd.Substring(1));

                    if (opl[0] == 'r')
                    {
                        instruction.LeftOperandType = OperandType.Registry;
                        instruction.LeftOperandValue = int.Parse(opl.Substring(1));
                    }
                    else
                    {
                        instruction.LeftOperandType = OperandType.Literal;
                        instruction.LeftOperandValue = float.Parse(opl, CultureInfo.InvariantCulture);
                    }

                    if (opr[0] == 'r')
                    {
                        instruction.RightOperandType = OperandType.Registry;
                        instruction.RightOperandValue = int.Parse(opr.Substring(1));
                    }
                    else
                    {
                        instruction.RightOperandType = OperandType.Literal;
                        instruction.RightOperandValue = float.Parse(opr, CultureInfo.InvariantCulture);
                    }

                    instructions.Add(instruction);
                    debugCallback?.Invoke("compiler > " + instruction);

                    continue;
                }

                return new Result(Error.SyntaxError, index + 1, lines[index]);
            }

            program.Instructions = instructions.ToArray();

            return Result.Success();
        }
    }
    
    public class Program
    {
        internal Instruction[] Instructions = Array.Empty<Instruction>();
    }

    internal enum Operation
    {
        Nop = 0,
        Move,
        Add,
        Subtract,
        Multiply,
        Divide,
        SquareRoot,
        
        Assert = 100,
    }

    internal enum OperandType
    {
        Literal,
        Registry
    }
    
    internal struct Instruction
    {
        internal Operation Operation;

        internal int DestinationRegistry;
        
        internal OperandType LeftOperandType;
        internal float LeftOperandValue;
        
        internal OperandType RightOperandType;
        internal float RightOperandValue;

        internal int Line;
        internal string RawText;

        public override string ToString()
        {
            return $"{Operation} {DestinationRegistry} {LeftOperandType} {LeftOperandValue} " +
                   $"{RightOperandType} {RightOperandValue}";
        }
    }
}
