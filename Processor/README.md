# Esolang.Brainfuck.Processor

Brainfuck processor.

## Install

```bash
dotnet add package Esolang.Brainfuck.Processor
```

## Usage

```cs
using System;
using Esolang.Brainfuck.Processor;

var source = "+++++++++[>++++++++>+++++++++++>+++++<<<-]>.>++.+++++++..+++.>-.------------.<++++++++.--------.+++.------.--------.>+.";
var processor = new BrainfuckProcessor(source);
var result = await processor.RunAndOutputStringAsync();

Console.WriteLine(result);
// output:
// Hello, world!
```

## Execution Model

| Item | Behavior |
| --- | --- |
| Memory cell type | `byte` (0-255), increment/decrement wraps around. |
| Initial tape | One cell initialized to `0`. |
| `>` (pointer increment) | Moves right and grows tape with a new `0` cell when needed. |
| `<` (pointer decrement) | At index `0`, it becomes a no-op. |
| `[` and `]` | Standard loop jump when matching bracket exists; if not found, the command advances as no-op. |
| Input | Reads one byte from `System.IO.Pipelines.PipeReader` into current cell. |
| Output | Writes one byte from current cell to `System.IO.Pipelines.PipeWriter`. |

## Advanced API

- `Run` / `RunAsync`: execute and return `BrainfuckContext`.
- `RunAndOutputString` / `RunAndOutputStringAsync`: execute and collect UTF-8 output.
- `StepCommands`: iterate command-by-command execution.

## See also

- [The official Brainfuck page](https://www.muppetlabs.com/~breadbox/bf/)
