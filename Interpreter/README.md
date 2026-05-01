# dotnet-brainfuck (Esolang.Brainfuck.Interpreter)

Brainfuck .NET tool.

## Install

```bash
dotnet tool install -g dotnet-brainfuck --prerelease
```

## Update

```bash
dotnet tool update -g dotnet-brainfuck --prerelease
```

## Usage

```bash
dotnet-brainfuck "++++++[>++++++++<-]++++++++++[>.+<-]"
# 0123456789

dotnet-brainfuck parse "++>--"
```

## Commands

| Command | Description |
| --- | --- |
| `dotnet-brainfuck <source>` | Execute source and print output. |
| `dotnet-brainfuck parse <source>` | Print parsed sequence and syntax fragments. |

## Global Syntax Options

| Option | Short | Description |
| --- | --- | --- |
| `--syntax-no-use-default-value` | `-snd` | Disable default Brainfuck syntax assignment. |
| `--syntax-increment-pointer` | `-sip` | Configure token for pointer increment. |
| `--syntax-dencrement-pointer` | `-sdp` | Configure token for pointer decrement. |
| `--syntax-increment-current` | `-sic` | Configure token for current cell increment. |
| `--syntax-decrement-current` | `-sdc` | Configure token for current cell decrement. |
| `--syntax-output` | `-so` | Configure output token. |
| `--syntax-input` | `-si` | Configure input token. |
| `--syntax-begin` | `-sb` | Configure loop-begin token. |
| `--syntax-end` | `-se` | Configure loop-end token. |

## Example with Custom Syntax

```bash
dotnet-brainfuck --syntax-increment-pointer R --syntax-dencrement-pointer L "RRLL"
dotnet-brainfuck parse --syntax-increment-current A --syntax-decrement-current B "AABB"
```
## See also

- [The official Brainfuck page](https://www.muppetlabs.com/~breadbox/bf/)
