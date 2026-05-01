# Esolang.Brainfuck.Parser

Brainfuck source parser.

## Install

```bash
dotnet add package Esolang.Brainfuck.Parser
```

## Usage

```cs
using System;
using Esolang.Brainfuck;

var source = "[,+++++.]";
var sequences = new BrainfuckSequenceEnumerable(source);
foreach (var (sequence, syntax) in sequences)
{
    Console.WriteLine($"syntax:{syntax} sequence:{sequence}");
}
// output:
// syntax:[ sequence:Begin
// syntax:, sequence:Input
// syntax:+ sequence:IncrementCurrent
// syntax:+ sequence:IncrementCurrent
// syntax:+ sequence:IncrementCurrent
// syntax:+ sequence:IncrementCurrent
// syntax:+ sequence:IncrementCurrent
// syntax:. sequence:Output
// syntax:] sequence:End
```

## Default Syntax Options

| Sequence | Default syntax |
| --- | --- |
| IncrementPointer | `>` |
| DecrementPointer | `<` |
| IncrementCurrent | `+` |
| DecrementCurrent | `-` |
| Output | `.` |
| Input | `,` |
| Begin | `[` |
| End | `]` |

## Behavior Notes

- Parsing is tokenization-focused. Non-command text is returned as `BrainfuckSequence.Comment`.
- `BrainfuckSequenceEnumerable.RequiredInput` and `RequiredOutput` indicate whether the source contains input/output commands.
- Custom syntaxes are matched by longest token first.

## See also

- [The official Brainfuck page](https://www.muppetlabs.com/~breadbox/bf/)
