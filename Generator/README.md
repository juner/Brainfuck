# Esolang.Brainfuck.Generator

Brainfuck source generator for .NET.

## Install

```bash
dotnet add package Esolang.Brainfuck.Generator
```

## Usage

Use `GenerateBrainfuckMethodAttribute` on a `partial` method.

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

## Features

- Determines whether input/output interfaces are required from the source.
- Supports synchronous and asynchronous method signatures.
- Supports string, `System.IO.TextReader`/`System.IO.TextWriter`, and `System.IO.Pipelines` based input/output patterns.

## Supported Method Signatures

| Category | Supported types |
| --- | --- |
| Input parameter | `string`, `System.IO.Pipelines.PipeReader`, `System.IO.TextReader` |
| Output parameter | `System.IO.Pipelines.PipeWriter`, `System.IO.TextWriter` |
| Return type | `void`, `string`, `System.Threading.Tasks.Task<string>`, `System.Threading.Tasks.ValueTask<string>`, `System.Collections.Generic.IEnumerable<byte>`, `System.Collections.Generic.IAsyncEnumerable<byte>` |
| Other parameter | `System.Threading.CancellationToken` |

Output-related signatures are allowed even when the source does not contain `.`. In that case string returns produce `null`, byte-sequence returns complete without values, and output parameters (`PipeWriter`/`TextWriter`) are simply left unused.

## Signature Patterns

The generator accepts one input source, one output destination, and an optional `CancellationToken`.

### 1. Return string directly

```cs
using Esolang.Brainfuck;

partial class BrainfuckSample
{
    [GenerateBrainfuckMethod("++++++[>++++++++<-]++++++++++[>.+<-]")]
    public static partial string? Digits();
}
```

Use when the Brainfuck source outputs bytes and you want the result as a string.

### 2. Async return (`Task<string?>` / `ValueTask<string?>`)

```cs
using Esolang.Brainfuck;

partial class BrainfuckSample
{
    [GenerateBrainfuckMethod("1+++++++++[>++++++++>+++++++++++>+++++<<<-]>.>++.+++++++..+++.>-.------------.<++++++++.--------.+++.------.--------.>+.")]
    public static partial Task<string?> HelloWorldAsync();

    [GenerateBrainfuckMethod("++++++[>++++++++<-]++++++++++[>.+<-]")]
    public static partial ValueTask<string?> DigitsValueTaskAsync();
}
```

Use when the caller wants async-compatible signatures.

### 3. Input from `string`

```cs
using Esolang.Brainfuck;

partial class BrainfuckSample
{
    [GenerateBrainfuckMethod(",.")]
    public static partial string? ReadOneChar(string input);
}
```

Use when input is already in memory as text.

### 4. Input from `PipeReader`

```cs
using Esolang.Brainfuck;
using System.IO.Pipelines;

partial class BrainfuckSample
{
    [GenerateBrainfuckMethod(",.")]
    public static partial Task<string?> ReadOneCharFromPipeAsync(PipeReader input, CancellationToken cancellationToken = default);
}
```

Use when input comes from a stream/pipeline (network, process, etc.).

### 5. Output to `PipeWriter`

```cs
using Esolang.Brainfuck;
using System.IO.Pipelines;

partial class BrainfuckSample
{
    [GenerateBrainfuckMethod("++++++[>++++++++<-]++++++++++[>.+<-]")]
    public static partial Task WriteDigitsToPipeAsync(PipeWriter output, CancellationToken cancellationToken = default);
}
```

Use when you want to push output bytes to a pipeline sink instead of returning a string.

### 6. Input from `TextReader` / Output to `TextWriter`

```cs
using Esolang.Brainfuck;
using System.IO;

partial class BrainfuckSample
{
    [GenerateBrainfuckMethod(",.")]
    public static partial Task<string?> ReadOneCharFromTextReaderAsync(TextReader input, CancellationToken cancellationToken = default);

    [GenerateBrainfuckMethod("++++++[>++++++++<-]++++++++++[>.+<-]")]
    public static partial Task WriteDigitsToTextWriterAsync(TextWriter output, CancellationToken cancellationToken = default);
}
```

Use when your caller is already based on text-oriented reader/writer abstractions.

### 7. Byte sequence return

```cs
using Esolang.Brainfuck;

partial class BrainfuckSample
{
    [GenerateBrainfuckMethod("+++++.")]
    public static partial System.Collections.Generic.IEnumerable<byte> GetBytes();

    [GenerateBrainfuckMethod("+++++.")]
    public static partial System.Collections.Generic.IAsyncEnumerable<byte> GetBytesAsync();
}
```

Use when the consumer needs raw bytes and controls text decoding.

## Combination Rules

- Do not combine `string` return (`string` / `Task<string>` / `ValueTask<string>`) with an output parameter (`PipeWriter` or `TextWriter`).
- Do not combine `IEnumerable<byte>` / `IAsyncEnumerable<byte>` returns with an output parameter (`PipeWriter` or `TextWriter`).
- Do not combine input sources in the same method. Choose one of: `string`, `PipeReader`, `TextReader`.
- Use at most one output sink parameter. Choose one of: `PipeWriter`, `TextWriter`.
- Use at most one `CancellationToken`.
- If source contains `,`, one input parameter (`string`, `PipeReader`, or `TextReader`) is required.
- If source contains `.`, one output target is required: `string`, `Task<string>`, `ValueTask<string>`, `IEnumerable<byte>`, `IAsyncEnumerable<byte>`, `PipeWriter`, or `TextWriter`.
- If source does not contain `.`, output-related signatures are still valid and produce no output.
- If source does not contain `,`, input parameters are still accepted but produce BF0009 (Hidden).

## UseConsole Sample Run

You can run the sample project that includes all main signature patterns:

```bash
dotnet run --project samples/Generator.UseConsole/Esolang.Brainfuck.Generator.UseConsole.csproj --framework net8.0
```

Current sample methods in `samples/Generator.UseConsole/Esolang.Brainfuck.Generator.UseConsole.cs` include:

- `Task<string?>` return pattern.
- `ValueTask<string?>` return pattern.
- `string?` return pattern.
- `IEnumerable<byte>` / `IAsyncEnumerable<byte>` return pattern.
- `string` input pattern.
- `PipeReader` input pattern.
- `TextReader` input pattern.
- `PipeWriter` output pattern.
- `TextWriter` output pattern.
- Custom Brainfuck token mapping pattern.

Example output:

```text
SampleMethod1: 0123456789
SampleMethod2: Hello, world!
SampleMethod6: 0123456789
SampleMethod7: 0123456789
SampleMethod8: 0123456789
SampleMethod9: 0123456789
SampleMethod3: A
SampleMethod10: B
SampleMethod4: Z
SampleMethod11: 0123456789
SampleMethod5: 0123456789
```

## See also

- [The official Brainfuck page](https://www.muppetlabs.com/~breadbox/bf/)
## Diagnostics

| ID | Meaning |
| --- | --- |
| BF0001 | Invalid value parameter on attribute. |
| BF0002 | Unsupported return type. |
| BF0003 | Unsupported parameter type. |
| BF0004 | Duplicate unsupported parameter pattern. |
| BF0005 | Unsupported input parameter combination (`string` / `PipeReader` / `TextReader` mixed use). |
| BF0006 | Unsupported parameter and return type combination. |
| BF0007 | Source requires output interface (`string`/`Task<string>`/`ValueTask<string>`/byte sequence return or `PipeWriter`/`TextWriter` parameter). |
| BF0008 | Source requires input interface (`string`/`PipeReader`/`TextReader` parameter). |
| BF0009 | Input parameter provided but source does not contain the input command (Hidden). |
