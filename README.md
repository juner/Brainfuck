# Esolang.Brainfuck

[![.NET](https://github.com/Esolang-NET/Brainfuck/actions/workflows/dotnet.yml/badge.svg)](https://github.com/Esolang-NET/Brainfuck/actions/workflows/dotnet.yml)

## Quick Start (Generator)

Write Brainfuck once, call it as a C# method.

```cs
using Esolang.Brainfuck;

Console.WriteLine(BrainfuckSample.SampleMethod1());

partial class BrainfuckSample
{
	[GenerateBrainfuckMethod("++++++[>++++++++<-]++++++++++[>.+<-]")]
	public static partial string? SampleMethod1();
}

// output:
// 0123456789
```

As far as we know, this is the first Brainfuck source generator for .NET.

## Generator Guide

For detailed Generator signatures and patterns, see:

- [Generator README](./Generator/README.md)

### Generator Signatures

| Attribute Argument | `partial` Method Parameters (Input) | `partial` Method Return Types (Output) |
| :--- | :--- | :--- |
| `string` (Source) | `string`, `TextReader`, `PipeReader` | `void`, `string`, `string?`, `int`, `Task`, `ValueTask`, `IEnumerable<byte>`, `IAsyncEnumerable<byte>` |

For runnable examples, see:

- [UseConsole sample](./samples/Generator.UseConsole/Esolang.Brainfuck.Generator.UseConsole.cs)

## Implementation Status

| Area | Status |
|---|---|
| Core Brainfuck instructions | ✅ |
| `[` `]` matching | ✅ |
| Event-based I/O | ✅ |
| `TextReader` / `PipeReader` input (Adapter) | ✅ |
| `TextWriter` / `PipeWriter` output (Adapter) | ✅ |

## Install

```bash
dotnet add package Esolang.Brainfuck.Generator
dotnet add package Esolang.Brainfuck.Parser
dotnet add package Esolang.Brainfuck.Processor
dotnet tool install -g dotnet-brainfuck
```

## Choose Package

| Want to do | Package |
| --- | --- |
| Generate C# methods from Brainfuck at compile time | Esolang.Brainfuck.Generator |
| Parse source into sequence tokens | Esolang.Brainfuck.Parser |
| Execute Brainfuck in-process (Event-based) | Esolang.Brainfuck.Processor |
| Run Brainfuck from CLI | dotnet-brainfuck |

## License

This project is licensed under the MIT License - see the [LICENSE](./LICENSE) file for details.


## NuGet

| Project | NuGet | Summary |
| --- | --- | --- |
| [dotnet-brainfuck](./Interpreter/README.md) | [![NuGet: dotnet-brainfuck](https://img.shields.io/nuget/v/dotnet-brainfuck?logo=nuget&label=2.0.0)](https://www.nuget.org/packages/dotnet-brainfuck/) | brainfuck command line utility dotnet-brainfuck command. |
| [Esolang.Brainfuck.Generator](./Generator/README.md) | [![NuGet: Esolang.Brainfuck.Generator](https://img.shields.io/nuget/v/Esolang.Brainfuck.Generator?logo=nuget&label=2.0.0)](https://www.nuget.org/packages/Esolang.Brainfuck.Generator/) | brainfuck method generator. |
| [Esolang.Brainfuck.Parser](./Parser/README.md) | [![NuGet: Esolang.Brainfuck.Parser](https://img.shields.io/nuget/v/Esolang.Brainfuck.Parser?logo=nuget&label=2.0.0)](https://www.nuget.org/packages/Esolang.Brainfuck.Parser/) | brainfuck source parser. |
| [Esolang.Brainfuck.Processor](./Processor/README.md) | [![NuGet: Esolang.Brainfuck.Processor](https://img.shields.io/nuget/v/Esolang.Brainfuck.Processor?logo=nuget&label=2.0.0)](https://www.nuget.org/packages/Esolang.Brainfuck.Processor/) | brainfuck processor. |

## Framework Support

| Project | Target frameworks |
| --- | --- |
| Esolang.Brainfuck.Generator | netstandard2.0 |
| Esolang.Brainfuck.Parser | net8.0, net9.0, net10.0, netstandard2.1, netstandard2.0 |
| Esolang.Brainfuck.Processor | net8.0, net9.0, net10.0, netstandard2.1, netstandard2.0 |
| dotnet-brainfuck | net8.0, net9.0, net10.0 |

## Changelog

- [CHANGELOG](./CHANGELOG.md)

## See also

- [The official Brainfuck page](https://www.muppetlabs.com/~breadbox/bf/)
