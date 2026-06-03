using Esolang.Brainfuck.Generator.Sequences;
using Esolang.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using static Esolang.Brainfuck.BrainfuckSequence;
using static Esolang.Generator.BindingError;

namespace Esolang.Brainfuck.Generator;

public partial class MethodGenerator
{
    static EmittedMethod? Emit(SourceProductionContext context, GeneratorAttributeSyntaxContext source, LanguageVersion currentLanguageVersion, Compilation compilation, KnownTypes types)
    {
        Status status = new(context, source, currentLanguageVersion, compilation, types);
        if (status.HasError)
            return EmitErrorMethod(status);
        return EmitSuccessMethod(status);
    }

    /// <summary>
    /// Emits the source code for a method with the specified status.
    /// </summary>
    /// <param name="status">The status of the method generation.</param>
    /// <returns>The emitted method.</returns>
    /// <exception cref="ArgumentException"></exception>
    static EmittedMethod EmitSuccessMethod(in Status status)
    {
        if (status.HasError) throw new ArgumentException("invalid status.", nameof(status));
        var format = SymbolDisplayFormat.FullyQualifiedFormat
            .WithMiscellaneousOptions(
                SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
                SymbolDisplayMiscellaneousOptions.UseSpecialTypes |
                SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);
        var methodSymbol = status.MethodSymbol;
        var (openingDefinitionCode, codeForClosingDefinition) = Utils.GenerateOpeningClosingTypeDefinitionCode(methodSymbol);
        var methodModifier = $"{SyntaxFacts.GetText(methodSymbol.DeclaredAccessibility)}{(methodSymbol.IsStatic ? " static" : string.Empty)} partial";
        InternalOptions writeOption = new(
            Space: SPACE,
            VariableStack: STACK_NAME,
            VariableStackIndex: STACK_INDEX,
            VariableCancellationToken: status.ParameterOptions.Value.VariableCancellation,
            VariablePipeWriter: status.ParameterOptions.Value.VariablePipeWriter,
            VariableTextWriter: status.ParameterOptions.Value.VariableTextWriter,
            VariablePipeReader: status.ParameterOptions.Value.VariablePipeReader,
            VariableTextReader: status.ParameterOptions.Value.VariableTextReader,
            VariableInputString: status.ParameterOptions.Value.VariableInputString,
            VariableLogger: status.ParameterOptions.Value.VariableLogger
        );
        var returnTypeSyntax = methodSymbol.ReturnType.ToDisplayString(format);
        var methodBodyCode = GenerateMethodBodyCode(2, status.Sequences, ref writeOption, methodSymbol, status);
        var withAsync = status.IsAsyncMethod && writeOption.UseAwait ? "async" : string.Empty;

        var generatedSourceCode = $$"""
            {{openingDefinitionCode}}
            {{SPACE}}{{methodModifier}} {{withAsync}} {{returnTypeSyntax}} {{methodSymbol.Name}}({{status.ParameterOptions.Value.ParameterSymbols}})
            {{SPACE}}{
            {{methodBodyCode}}
            {{SPACE}}}
            {{codeForClosingDefinition}}
            """;

        var features = RuntimeFacadeFeatures.None;
        if (writeOption.UseListAsMemory) features |= RuntimeFacadeFeatures.UseListAsMemory;
        if (writeOption.HasLoggerParameter) features |= RuntimeFacadeFeatures.UseLogger;
        return new EmittedMethod(generatedSourceCode, features);
    }

    /// <summary>
    /// Emits the source code for a method that reports an error when invoked.
    /// </summary>
    /// <param name="status">The status of the method generation.</param>
    /// <returns>The emitted method.</returns>
    static EmittedMethod EmitErrorMethod(in Status status)
    {
        if (!status.HasError) throw new ArgumentException("status must have error.", nameof(status));
        var (description, message) = status.Error;
        var errorId = description.Id;
        var methodSymbol = status.MethodSymbol;
        var sb = new StringBuilder();
        var (openingDefinitionCode, codeForClosingDefinition) = Utils.GenerateOpeningClosingTypeDefinitionCode(methodSymbol);
        sb.Append($$"""
        {{openingDefinitionCode}}
        """);

        var accessibility = $"{SyntaxFacts.GetText(methodSymbol.DeclaredAccessibility)}{(methodSymbol.IsStatic ? " static" : string.Empty)}";
        var returnType = methodSymbol.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var parameters = string.Join(", ", methodSymbol.Parameters.Select(FormatParameter));

        sb.Append(SPACE).Append(accessibility).Append(" partial ").Append(returnType)
          .Append(' ').Append(methodSymbol.Name).Append('(').Append(parameters).AppendLine(")");
        sb.Append($$"""
        {{SPACE}}{
        {{SPACE}}{{SPACE}}throw new global::System.NotImplementedException("{{errorId}}: {{message}}");
        {{SPACE}}}
        {{codeForClosingDefinition}}

        """);


        return new EmittedMethod(sb.ToString(), RuntimeFacadeFeatures.None);

        static string FormatParameter(IParameterSymbol parameter)
        {
            var modifier = parameter.RefKind switch
            {
                RefKind.In => "in ",
                RefKind.Out => "out ",
                RefKind.Ref => "ref ",
                _ => string.Empty,
            };

            var paramsPrefix = parameter.IsParams ? "params " : string.Empty;
            var typeName = parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return $"{paramsPrefix}{modifier}{typeName} {parameter.Name}";
        }
    }
    const string SPACE = "    ";
    const string STACK_NAME = "stack";
    const string STACK_INDEX = "stackIndex";

    static string GenerateAsyncReturnCode(string space, in Status status, IMethodSymbol methodSymbol)
    {
        var returnType_ = (INamedTypeSymbol)methodSymbol.ReturnType;
        var innerType = returnType_.TypeArguments.First();
        var annoation = returnType_.TypeArgumentNullableAnnotations.First();
        var builder = new StringBuilder();
        var isTask = status.ReturnKind is MethodReturnKind.TaskNullableString or MethodReturnKind.TaskString;
        var isNullable = status.ReturnKind is MethodReturnKind.TaskNullableString or MethodReturnKind.ValueTaskNullableString;

        if (isNullable)
        {
            var bang = annoation is NullableAnnotation.None ? "!" : string.Empty;
            var returnVal = $"default({innerType.ToDisplayString()}){bang}";
            if (isTask)
                builder.AppendLine($"{space}return global::System.Threading.Tasks.Task.FromResult<{innerType.ToDisplayString()}>({returnVal});");
            else
                builder.AppendLine($"{space}return new global::System.Threading.Tasks.ValueTask<{innerType.ToDisplayString()}>({returnVal});");
        }
        else
        {
            if (isTask)
                builder.AppendLine($"{space}return global::System.Threading.Tasks.Task.FromResult(string.Empty);");
            else
                builder.AppendLine($"{space}return new global::System.Threading.Tasks.ValueTask<string>(string.Empty);");
        }
        return builder.ToString();
    }

    static string GenerateStringReturnCode(string space, in Status status)
    {
        var builder = new StringBuilder();
        var returnVal = status.ReturnKind == MethodReturnKind.NullableString ? "null!" : "string.Empty";
        builder.AppendLine($"{space}return {returnVal};");
        return builder.ToString();
    }

    /// <summary>
    /// Generates the method body code for the specified Brainfuck sequence.
    /// </summary>
    /// <param name="indent">Indent level (4 spaces per level).</param>
    /// <param name="sequences">The command sequence.</param>
    /// <param name="options">Internal generation options.</param>
    /// <param name="methodSymbol">The method symbol for which the code is being generated.</param>
    /// <param name="status"></param> 
    /// <returns>The generated method body source code.</returns>
    static string GenerateMethodBodyCode(int indent, BrainfuckSequenceEnumerable sequences, ref InternalOptions options, IMethodSymbol methodSymbol, in Status status)
    {
        var builder = new StringBuilder();
        var SPACE = options.Space;
        var space = string.Join("", Enumerable.Range(0, indent).Select(v => SPACE));
        #region Variable declarations
        builder.AppendLine($$"""
            {{space}}var {{options.VariableStack}} = new global::System.Collections.Generic.List<byte>(){ 0 };
            {{space}}var {{options.VariableStackIndex}} = 0;
            """);

        switch (status, options)
        {
            case ({ IsOutputRequired: true }, { HasTextWriterParameter: true, HasPipeWriterParameter: false }):
                options = options with
                {
                    VariablePipeWriter = "pipeWriter",
                };

                builder.AppendLine($"""
                    {space}var outputPipe = new global::System.IO.Pipelines.Pipe();
                    {space}var {options.VariablePipeWriter} = outputPipe.Writer;
                    """);
                break;
            case ({ IsOutputRequired: true, ReturnKind: MethodReturnKind.String or MethodReturnKind.NullableString or MethodReturnKind.TaskString or MethodReturnKind.TaskNullableString or MethodReturnKind.ValueTaskString or MethodReturnKind.ValueTaskNullableString }, { HasPipeWriterParameter: false }):
                options = options with
                {
                    VariablePipeWriter = "pipeWriter",
                };

                builder.AppendLine($"""
                    {space}var outputPipe = new global::System.IO.Pipelines.Pipe();
                    {space}var {options.VariablePipeWriter} = outputPipe.Writer;
                    """);
                break;
        }

        switch (status, options)
        {
            case ({ IsInputRequired: true }, { HasInputStringParameter: true, HasPipeReaderParameter: false }):
                options = options with
                {
                    VariablePipeReader = "pipeReader",
                };
                builder.AppendLine($$"""
                    {{space}}global::System.IO.Pipelines.PipeReader {{options.VariablePipeReader}};
                    {{space}}{
                    {{space}}{{SPACE}}var inputPipe = new global::System.IO.Pipelines.Pipe();
                    {{space}}{{SPACE}}var bytes = string.IsNullOrEmpty({{options.VariableInputString}}) ? global::System.Array.Empty<byte>() : global::System.Text.Encoding.UTF8.GetBytes({{options.VariableInputString}});
                    {{space}}{{SPACE}}if (bytes.Length > 0)
                    """);
                if (status is { IsAsyncMethod: true })
                {
                    var withCancel = string.IsNullOrEmpty(options.VariableCancellationToken) ? string.Empty : ", " + options.VariableCancellationToken;
                    builder.AppendLine($$"""
                        {{space}}{{SPACE}}{{SPACE}}await inputPipe.Writer.WriteAsync(bytes{{withCancel}});
                        {{space}}{{SPACE}}await inputPipe.Writer.CompleteAsync();
                        """);
                    options = options with
                    {
                        UseAwait = true,
                    };
                }
                else
                {
                    builder.AppendLine($$"""
                        {{space}}{{SPACE}}{{SPACE}}global::System.MemoryExtensions.AsSpan(bytes).CopyTo(inputPipe.Writer.GetSpan(bytes.Length));
                        {{space}}{{SPACE}}inputPipe.Writer.Advance(bytes.Length);
                        {{space}}{{SPACE}}inputPipe.Writer.Complete();
                        """);
                }
                builder.AppendLine($$"""
                    {{space}}{{SPACE}}{{options.VariablePipeReader}} = inputPipe.Reader;
                    {{space}}}
                    """);
                break;
            case ({ IsInputRequired: true }, { HasTextReaderParameter: true, HasPipeReaderParameter: false }):
                options = options with
                {
                    VariablePipeReader = "pipeReader",
                };
                builder.AppendLine($$"""
                    {{space}}global::System.IO.Pipelines.PipeReader {{options.VariablePipeReader}};
                    {{space}}{
                    {{space}}{{SPACE}}var inputPipe = new global::System.IO.Pipelines.Pipe();
                    """);
                if (status is { IsAsyncMethod: true })
                {
                    builder.AppendLine($$"""
                        {{space}}{{SPACE}}var text = await {{options.VariableTextReader}}.ReadToEndAsync();
                        {{space}}{{SPACE}}var bytes = string.IsNullOrEmpty(text) ? global::System.Array.Empty<byte>() : global::System.Text.Encoding.UTF8.GetBytes(text);
                        {{space}}{{SPACE}}if (bytes.Length > 0)
                        {{space}}{{SPACE}}{{SPACE}}await inputPipe.Writer.WriteAsync(bytes);
                        {{space}}{{SPACE}}await inputPipe.Writer.CompleteAsync();
                        """);
                    options = options with
                    {
                        UseAwait = true,
                    };
                }
                else
                {
                    builder.AppendLine($$"""
                        {{space}}{{SPACE}}var text = {{options.VariableTextReader}}.ReadToEnd();
                        {{space}}{{SPACE}}var bytes = string.IsNullOrEmpty(text) ? global::System.Array.Empty<byte>() : global::System.Text.Encoding.UTF8.GetBytes(text);
                        {{space}}{{SPACE}}if (bytes.Length > 0)
                        {{space}}{{SPACE}}{
                        {{space}}{{SPACE}}{{SPACE}}global::System.MemoryExtensions.AsSpan(bytes).CopyTo(inputPipe.Writer.GetSpan(bytes.Length));
                        {{space}}{{SPACE}}{{SPACE}}inputPipe.Writer.Advance(bytes.Length);
                        {{space}}{{SPACE}}}
                        {{space}}{{SPACE}}inputPipe.Writer.Complete();
                        """);
                }
                builder.AppendLine($$"""
                    {{space}}{{SPACE}}{{options.VariablePipeReader}} = inputPipe.Reader;
                    {{space}}}
                    """);
                break;
        }
        #endregion

        var seq = sequences.Select((v, i) => new Sequence(i, v.Sequence, v.Syntax)).ToArray().AsMemory();
        var nest = seq.Nest();
        WriteNest(indent, nest, builder, ref options, status);
        var withCancellation = string.IsNullOrEmpty(options.VariableCancellationToken) ? string.Empty : ", " + options.VariableCancellationToken;

        #region Return statement generation
        switch (status, options)
        {
            case ({ ReturnKind: MethodReturnKind.ValueTaskString or MethodReturnKind.ValueTaskNullableString } and not { IsOutputRequired: true }, { UseAwait: false }):
            case ({ ReturnKind: MethodReturnKind.TaskString or MethodReturnKind.TaskNullableString } and not { IsOutputRequired: true }, { UseAwait: false }):
                {
                    builder.Append(GenerateAsyncReturnCode(space, status, methodSymbol));
                }
                break;

            case ({ ReturnKind: MethodReturnKind.NullableString } and not { IsOutputRequired: true }, _)
                or (
                {
                    ReturnKind: MethodReturnKind.TaskNullableString
                        or MethodReturnKind.ValueTaskNullableString
                } and not { IsOutputRequired: true }, { UseAwait: true }):
                {
                    builder.Append(GenerateStringReturnCode(space, status));
                }
                break;

            case ({ ReturnKind: MethodReturnKind.String } and not { IsOutputRequired: true }, _)
                or ({ ReturnKind: MethodReturnKind.TaskString or MethodReturnKind.ValueTaskString } and not { IsOutputRequired: true }, { UseAwait: true }):
                {
                    builder.Append(GenerateStringReturnCode(space, status));
                }
                break;

            case ({ ReturnKind: MethodReturnKind.TaskString or MethodReturnKind.ValueTaskString, IsOutputRequired: true }, _):
                {
                    builder.AppendLine($$"""
                        {{space}}{
                        {{space}}{{SPACE}}await {{options.VariablePipeWriter}}.CompleteAsync();
                        #if NETCOREAPP3_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
                        {{space}}{{SPACE}}await using var stream = new global::System.IO.MemoryStream();
                        #else
                        {{space}}{{SPACE}}using var stream = new global::System.IO.MemoryStream();
                        #endif
                        {{space}}{{SPACE}}using var reader = new global::System.IO.StreamReader(stream, System.Text.Encoding.UTF8, false, 1024, true);
                        {{space}}{{SPACE}}await outputPipe.Reader.CopyToAsync(stream{{withCancellation}});
                        {{space}}{{SPACE}}stream.Seek(0, global::System.IO.SeekOrigin.Begin);
                        {{space}}{{SPACE}}if (stream.Length == 0) return string.Empty;
                        {{space}}{{SPACE}}var returnString = (await reader.ReadToEndAsync()).TrimEnd('\0');
                        {{space}}{{SPACE}}if (returnString.Length == 0) return string.Empty;
                        {{space}}{{SPACE}}return returnString;
                        {{space}}}

                        """);
                    options = options with
                    {
                        UseAwait = true,
                    };
                }
                break;


            case ({ ReturnKind: MethodReturnKind.TaskNullableString or MethodReturnKind.ValueTaskNullableString, IsOutputRequired: true }, _):
                {
                    builder.AppendLine($$"""
                        {{space}}{
                        {{space}}{{SPACE}}await {{options.VariablePipeWriter}}.CompleteAsync();
                        #if NETCOREAPP3_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
                        {{space}}{{SPACE}}await using var stream = new global::System.IO.MemoryStream();
                        #else
                        {{space}}{{SPACE}}using var stream = new global::System.IO.MemoryStream();
                        #endif
                        {{space}}{{SPACE}}using var reader = new global::System.IO.StreamReader(stream, System.Text.Encoding.UTF8, false, 1024, true);
                        {{space}}{{SPACE}}await outputPipe.Reader.CopyToAsync(stream{{withCancellation}});
                        {{space}}{{SPACE}}stream.Seek(0, global::System.IO.SeekOrigin.Begin);
                        {{space}}{{SPACE}}if (stream.Length == 0) return null!;
                        {{space}}{{SPACE}}var returnString = (await reader.ReadToEndAsync()).TrimEnd('\0');
                        {{space}}{{SPACE}}if (returnString.Length == 0) return null!;
                        {{space}}{{SPACE}}return returnString;
                        {{space}}}

                        """);
                    options = options with
                    {
                        UseAwait = true,
                    };
                }
                break;

            case ({ ReturnKind: MethodReturnKind.String, IsOutputRequired: true }, _):
                {
                    builder.AppendLine($$"""
                        {{space}}{
                        {{space}}{{SPACE}}{{options.VariablePipeWriter}}.Complete();
                        {{space}}{{SPACE}}if (!outputPipe.Reader.TryRead(out var outputResult))
                        {{space}}{{SPACE}}{{SPACE}}return string.Empty;
                        {{space}}{{SPACE}}var resultArray = global::System.Buffers.BuffersExtensions.ToArray(outputResult.Buffer);
                        {{space}}{{SPACE}}outputPipe.Reader.AdvanceTo(outputResult.Buffer.End);
                        {{space}}{{SPACE}}if (resultArray.Length == 0) return string.Empty;
                        {{space}}{{SPACE}}var returnString = global::System.Text.Encoding.UTF8.GetString(resultArray).TrimEnd('\0');
                        {{space}}{{SPACE}}if (returnString.Length == 0) return string.Empty;
                        {{space}}{{SPACE}}return returnString;
                        {{space}}}

                        """);
                }
                break;

            case ({ ReturnKind: MethodReturnKind.NullableString, IsOutputRequired: true }, _):
                {
                    builder.AppendLine($$"""
                        {{space}}{
                        {{space}}{{SPACE}}{{options.VariablePipeWriter}}.Complete();
                        {{space}}{{SPACE}}if (!outputPipe.Reader.TryRead(out var outputResult))
                        {{space}}{{SPACE}}{{SPACE}}return null!;
                        {{space}}{{SPACE}}var resultArray = global::System.Buffers.BuffersExtensions.ToArray(outputResult.Buffer);
                        {{space}}{{SPACE}}outputPipe.Reader.AdvanceTo(outputResult.Buffer.End);
                        {{space}}{{SPACE}}if (resultArray.Length == 0) return null!;
                        {{space}}{{SPACE}}var returnString = global::System.Text.Encoding.UTF8.GetString(resultArray).TrimEnd('\0');
                        {{space}}{{SPACE}}if (returnString.Length == 0) return null!;
                        {{space}}{{SPACE}}return returnString;
                        {{space}}}

                        """);
                }
                break;

            case ({ ReturnKind: MethodReturnKind.IAsyncEnumerableByte }, { UseAwait: false }):
                options = options with
                {
                    UseAwait = true,
                };
                builder.AppendLine($$"""
                    {{space}}await global::System.Threading.Tasks.Task.CompletedTask;

                    """);
                builder.AppendLine($$"""
                    {{space}}yield break;

                    """);
                break;

            case ({ ReturnKind: MethodReturnKind.IEnumerableByte or MethodReturnKind.IAsyncEnumerableByte }, _):
                builder.AppendLine($$"""
                    {{space}}yield break;

                    """);
                break;

            case ({ IsOutputRequired: true, IsAsyncMethod: true }, { HasTextWriterParameter: true }):
                builder.AppendLine($$"""
                    {{space}}{
                    {{space}}{{SPACE}}await {{options.VariablePipeWriter}}.CompleteAsync();
                    {{space}}{{SPACE}}if (await outputPipe.Reader.ReadAsync() is { } outputResult)
                    {{space}}{{SPACE}}{
                    {{space}}{{SPACE}}{{SPACE}}var resultArray = global::System.Buffers.BuffersExtensions.ToArray(outputResult.Buffer);
                    {{space}}{{SPACE}}{{SPACE}}outputPipe.Reader.AdvanceTo(outputResult.Buffer.End);
                    {{space}}{{SPACE}}{{SPACE}}if (resultArray.Length > 0)
                    {{space}}{{SPACE}}{{SPACE}}{
                    {{space}}{{SPACE}}{{SPACE}}{{SPACE}}var outputText = global::System.Text.Encoding.UTF8.GetString(resultArray).TrimEnd('\0');
                    {{space}}{{SPACE}}{{SPACE}}{{SPACE}}if (outputText.Length > 0)
                    {{space}}{{SPACE}}{{SPACE}}{{SPACE}}{{SPACE}}await {{options.VariableTextWriter}}.WriteAsync(outputText);
                    {{space}}{{SPACE}}{{SPACE}}}
                    {{space}}{{SPACE}}}
                    {{space}}}

                    """);
                options = options with
                {
                    UseAwait = true,
                };
                break;

            case ({ IsOutputRequired: true }, { HasTextWriterParameter: true }):
                builder.AppendLine($$"""
                    {{space}}{
                    {{space}}{{SPACE}}{{options.VariablePipeWriter}}.Complete();
                    {{space}}{{SPACE}}if (outputPipe.Reader.TryRead(out var outputResult))
                    {{space}}{{SPACE}}{
                    {{space}}{{SPACE}}{{SPACE}}var resultArray = global::System.Buffers.BuffersExtensions.ToArray(outputResult.Buffer);
                    {{space}}{{SPACE}}{{SPACE}}outputPipe.Reader.AdvanceTo(outputResult.Buffer.End);
                    {{space}}{{SPACE}}{{SPACE}}if (resultArray.Length > 0)
                    {{space}}{{SPACE}}{{SPACE}}{
                    {{space}}{{SPACE}}{{SPACE}}{{SPACE}}var outputText = global::System.Text.Encoding.UTF8.GetString(resultArray).TrimEnd('\0');
                    {{space}}{{SPACE}}{{SPACE}}{{SPACE}}if (outputText.Length > 0)
                    {{space}}{{SPACE}}{{SPACE}}{{SPACE}}{{SPACE}}{{options.VariableTextWriter}}.Write(outputText);
                    {{space}}{{SPACE}}{{SPACE}}}
                    {{space}}{{SPACE}}}
                    {{space}}}

                    """);
                break;

            case ({ ReturnKind: MethodReturnKind.TaskInt32 }, { UseAwait: false }):
                builder.AppendLine($$"""
                    {{space}}return global::System.Threading.Tasks.Task.FromResult(0);

                    """);
                break;

            case ({ ReturnKind: MethodReturnKind.Task }, { UseAwait: false }):
                builder.AppendLine($$"""
                    {{space}}return global::System.Threading.Tasks.Task.CompletedTask;

                    """);
                break;

            case ({ ReturnKind: MethodReturnKind.ValueTaskInt32 }, { UseAwait: false }):
                builder.AppendLine($$"""
                    {{space}}return new global::System.Threading.Tasks.ValueTask<int>(0);

                    """);
                break;

            case ({ ReturnKind: MethodReturnKind.Int32 or MethodReturnKind.TaskInt32 or MethodReturnKind.ValueTaskInt32 }, { UseAwait: false }):
                builder.AppendLine($$"""
                    {{space}}return 0;

                    """);
                break;

            case ({ ReturnKind: MethodReturnKind.ValueTask }, { UseAwait: false }):
                builder.AppendLine($$"""
                    {{space}}return default;

                    """);
                break;

            case ({ ReturnKind: MethodReturnKind.TaskInt32 or MethodReturnKind.ValueTaskInt32 }, { UseAwait: true }):
                builder.AppendLine($$"""
                    {{space}}return 0;

                    """);
                break;
        }
        #endregion

        #region UseListAsMemory generation
        if (options is { UseListAsMemory: true })
        {
            builder.AppendLine($$"""
                {{space}}static global::System.Memory<T> AsMemory<T>(global::System.Collections.Generic.List<T> self)
                {{space}}{
                {{space}}{{SPACE}} return new global::System.Memory<T>(global::System.Runtime.CompilerServices.Unsafe.As<global::Esolang.Brainfuck.__Generated.ListDummyHelper.ListDummy<T>>(self).Items).Slice(0, self.Count);
                {{space}}}
                """);
        }
        #endregion

        return builder.ToString();
    }
    static void WriteNest(int indent, IEnumerable<INestableSequence> sequences, StringBuilder builder, ref InternalOptions options, in Status status)
    {
        foreach (var sequence in sequences)
        {
            if (sequence is Sequence simple)
            {
                if (simple is { Value: Begin or End })
                {
                    WriteComment(indent, simple.Index, Comment, simple.Syntax, builder);
                    continue;
                }
                WriteSequence(indent, simple.Index, simple.Value, simple.Syntax, builder, ref options, status);
                continue;
            }
            if (sequence is NestableSequence nested)
            {
                var begin = nested.Begin;
                WriteSequence(indent, begin.Index, begin.Value, begin.Syntax, builder, ref options, status);
                WriteNest(indent + 1, nested.Nest, builder, ref options, status);
                var end = nested.End;
                WriteSequence(indent, end.Index, end.Value, end.Syntax, builder, ref options, status);
                continue;
            }
        }
    }
    static void WriteSequence(int indent, int id, BrainfuckSequence sequence, ReadOnlyMemory<char> syntax, StringBuilder builder, ref InternalOptions options, in Status status)
    {
        WriteComment(indent, id, sequence, syntax, builder);
        var space = string.Join("", Enumerable.Range(0, indent).Select(v => SPACE));
        var withCancel = options.HasCancellationTokenParameter ? ", " + options.VariableCancellationToken : string.Empty;

        var variableLogger = options.HasLoggerParameter ? options.VariableLogger : null;
        var variableStackIndex = options.VariableStackIndex;
        var variableStack = options.VariableStack;
        Func<BrainfuckSequence, string, string> logCall = variableLogger is not null
            ? (seq, s) =>
                $"{s}global::Esolang.Brainfuck.__Generated.LoggerUtilities.LogInstruction({variableLogger}, {id}, '{seq switch { IncrementPointer => '>', DecrementPointer => '<', IncrementCurrent => '+', DecrementCurrent => '-', Output => '.', Input => ',', Begin => '[', End => ']', _ => '?' }}', {variableStackIndex}, {variableStack}[{variableStackIndex}]);\n"
            : (_1, _2) => string.Empty;

        if (sequence switch
        {
            IncrementPointer => $$"""
                {{space}}{{options.VariableStackIndex}}++;
                {{space}}if ({{options.VariableStack}}.Count <= {{options.VariableStackIndex}}) {{options.VariableStack}}.Add(0);
                {{logCall(IncrementPointer, space)}}
                """,
            DecrementPointer => $$"""
                {{space}}if ({{options.VariableStackIndex}} > 0){{options.VariableStackIndex}}--;
                {{logCall(DecrementPointer, space)}}
                """,
            IncrementCurrent => $$"""
                {{space}}{
                {{space}}{{SPACE}}var value = {{options.VariableStack}}[{{options.VariableStackIndex}}];
                {{space}}{{SPACE}}{{options.VariableStack}}[{{options.VariableStackIndex}}] = unchecked((byte)(value + 1));
                {{logCall(IncrementCurrent, space + SPACE)}}
                {{space}}}
                """,
            DecrementCurrent => $$"""
                {{space}}{
                {{space}}{{SPACE}}var value = {{options.VariableStack}}[{{options.VariableStackIndex}}];
                {{space}}{{SPACE}}{{options.VariableStack}}[{{options.VariableStackIndex}}] = unchecked((byte)(value - 1));
                {{logCall(DecrementCurrent, space + SPACE)}}
                {{space}}}
                """,
            Begin => $$"""
                {{logCall(Begin, space)}}
                {{space}}while({{options.VariableStack}}[{{options.VariableStackIndex}}] is not 0) {
                """,
            End => $$"""
                {{logCall(End, space + SPACE)}}
                {{space}}}
                """,
            Input => $$"""
                {{SimpleInput(ref options, status)}}
                {{logCall(Input, space)}}
                """,
            Output => status switch
            {
                { ReturnKind: MethodReturnKind.IEnumerableByte or MethodReturnKind.IAsyncEnumerableByte } => $$"""
                {{space}}yield return {{options.VariableStack}}[{{options.VariableStackIndex}}];
                {{logCall(Output, space)}}
                """,
                _ => $$"""
                {{SimpleOutput(ref options, status)}}
                {{logCall(Output, space)}}
                """,
            },
            _ => null,
        } is { } text1)
            builder.AppendLine(text1.TrimEnd('\r', '\n'));
        string SimpleInput(ref InternalOptions options, in Status status)
        {
            options = options with
            {
                UseListAsMemory = true,
            };
            if (status is { IsAsyncMethod: true })
            {
                options = options with
                {
                    UseAwait = true,
                };
                return $$"""
                    {{space}}{
                    {{space}}{{SPACE}}if (await {{options.VariablePipeReader}}.ReadAtLeastAsync(1{{withCancel}}) is { } result && result.Buffer.Length >= 0)
                    {{space}}{{SPACE}}{
                    {{space}}{{SPACE}}{{SPACE}}var readableSeq = result.Buffer.Slice(result.Buffer.Start, 1);
                    {{space}}{{SPACE}}{{SPACE}}global::System.Buffers.BuffersExtensions.CopyTo(readableSeq, AsMemory({{options.VariableStack}}).Slice({{options.VariableStackIndex}}, 1).Span);
                    {{space}}{{SPACE}}{{SPACE}}{{options.VariablePipeReader}}.AdvanceTo(readableSeq.End);
                    {{space}}{{SPACE}}}
                    {{space}}}
                    """;
            }
            return $$"""
                {{space}}{
                {{space}}{{SPACE}}if ({{options.VariablePipeReader}}.TryRead(out var result) && result.Buffer.Length >= 0)
                {{space}}{{SPACE}}{
                {{space}}{{SPACE}}{{SPACE}}var readableSeq = result.Buffer.Slice(result.Buffer.Start, 1);
                {{space}}{{SPACE}}{{SPACE}}global::System.Buffers.BuffersExtensions.CopyTo(readableSeq, AsMemory({{options.VariableStack}}).Slice({{options.VariableStackIndex}}, 1).Span);
                {{space}}{{SPACE}}{{SPACE}}{{options.VariablePipeReader}}.AdvanceTo(readableSeq.End);
                {{space}}{{SPACE}}}
                {{space}}}
                """;
        }
        string SimpleOutput(ref InternalOptions options, in Status status)
        {
            options = options with
            {
                UseListAsMemory = true,
            };
            if (status is { IsAsyncMethod: true })
            {
                options = options with
                {
                    UseAwait = true,
                };
                return $"""
                    {space}await {options.VariablePipeWriter}.WriteAsync(AsMemory({options.VariableStack}).Slice({options.VariableStackIndex},1){withCancel});
                    """;
            }
            return $"""
                {space}AsMemory({options.VariableStack}).Slice({options.VariableStackIndex}, 1).Span.CopyTo({options.VariablePipeWriter}.GetSpan(1));
                {space}{options.VariablePipeWriter}.Advance(1);
                """;
        }
    }
    static void WriteComment(int indent, int index, BrainfuckSequence sequence, ReadOnlyMemory<char> syntax, StringBuilder builder)
    {
        var space = string.Join("", Enumerable.Range(0, indent).Select(v => SPACE));
        var comment = syntax.ToString().Replace("\r", "\\r").Replace("\n", "\\n");
        builder.AppendLine($"{space}// {index} {sequence}:{comment}");
    }

    readonly struct Status
    {
        public readonly LanguageVersion CurrentLanguageVersion;
        public readonly bool LanguageVersionTooLow;
        public readonly BrainfuckSequenceEnumerable Sequences = default!;

        [MemberNotNullWhen(false, nameof(Sequences))]
        [MemberNotNullWhen(true, nameof(Error))]
        public readonly bool InvalidValueParameter { get; }

        public readonly MethodReturnKind ReturnKind { get; } = default!;

        [MemberNotNullWhen(false, nameof(ReturnKind))]
        [MemberNotNullWhen(true, nameof(Error))]
        public readonly bool InvalidReturnKind { get; }

        public readonly ParameterOptions? ParameterOptions { get; }

        public readonly (DiagnosticDescriptor Descriptor, string Message) Error { get; } = default!;

        [MemberNotNullWhen(true, nameof(Error))]
        [MemberNotNullWhen(false, nameof(ParameterOptions))]
        public readonly bool InvalidParameterOptions { get; }

        [MemberNotNullWhen(true, nameof(Error))]
        [MemberNotNullWhen(false, nameof(Sequences))]
        [MemberNotNullWhen(false, nameof(ParameterOptions))]
        public readonly bool HasError => InvalidValueParameter || InvalidReturnKind || InvalidParameterOptions;

        public readonly bool RequiredOutputInterface { get; }

        public readonly bool RequiredInputInterface { get; }

        public readonly bool IsOutputRequired => Sequences.RequiredOutput;

        public readonly bool IsInputRequired => Sequences.RequiredInput;

        public readonly bool IsAsyncMethod => ReturnKind
             is MethodReturnKind.Task or MethodReturnKind.ValueTask
             or MethodReturnKind.TaskInt32 or MethodReturnKind.TaskString
             or MethodReturnKind.TaskNullableString or MethodReturnKind.ValueTaskNullableString
             or MethodReturnKind.ValueTaskInt32 or MethodReturnKind.ValueTaskString
             or MethodReturnKind.IAsyncEnumerableByte;

        public readonly IMethodSymbol MethodSymbol { get; }
        public readonly Compilation Compilation { get; }
        public readonly KnownTypes Types { get; }
        public Status(SourceProductionContext context, GeneratorAttributeSyntaxContext source, LanguageVersion currentLanguageVersion, Compilation compilation, KnownTypes types)
        {
            Compilation = compilation;
            Types = types;
            CurrentLanguageVersion = currentLanguageVersion;
            var methodSymbol = (IMethodSymbol)source.TargetSymbol;
            MethodSymbol = methodSymbol;
            var methodDeclarationSyntax = (MethodDeclarationSyntax)source.TargetNode;
            if (!IsLanguageVersionAtLeastCSharp8(currentLanguageVersion))
            {
                LanguageVersionTooLow = true;
                var diagnostic = DiagnosticDescriptors.LanguageVersionTooLow;
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        diagnostic,
                        methodDeclarationSyntax.Identifier.GetLocation(),
                        methodSymbol.Name,
                        currentLanguageVersion.ToString()));
            }
            if (!TryGetSources(methodSymbol, out var sequences))
            {
                InvalidValueParameter = true;
                var diagnostic = DiagnosticDescriptors.InvalidValueParameter;
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        diagnostic,
                        methodDeclarationSyntax.Identifier.GetLocation(),
                        methodSymbol.Name));
                Error = (diagnostic, string.Format(diagnostic.MessageFormat.ToString(), methodSymbol.Name));
                return;
            }
            Sequences = sequences;
            if (!TryGetReturnKind(methodSymbol.ReturnType,
                types,
                out var returnKind))
            {
                InvalidReturnKind = true;
                var diagnostic = DiagnosticDescriptors.InvalidReturnType;
                var displayString = methodSymbol.ReturnType.ToDisplayString();
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        diagnostic,
                        methodDeclarationSyntax.Identifier.GetLocation(),
                        displayString));
                Error = (diagnostic, string.Format(diagnostic.MessageFormat.ToString(), displayString));
                return;
            }
            ReturnKind = returnKind;
            if (!TryGetParameterOptions(methodSymbol, methodSymbol.ReturnType.ToString(), sequences, context, methodDeclarationSyntax, types, out var parameterOptions, out var dest))
            {
                InvalidParameterOptions = true;
                Error = dest.Value;
                return;
            }
            ParameterOptions = parameterOptions;
            ;
            if (sequences.RequiredOutput
                && (returnKind is not (MethodReturnKind.Void or MethodReturnKind.Task or MethodReturnKind.ValueTask
                         or MethodReturnKind.ValueTaskInt32 or MethodReturnKind.TaskInt32 or MethodReturnKind.Int32) ? 1 : 0)
                + (string.IsNullOrEmpty(parameterOptions.VariablePipeWriter) ? 0 : 1)
                + (string.IsNullOrEmpty(parameterOptions.VariableTextWriter) ? 0 : 1) != 1
            )
            {
                RequiredOutputInterface = true;
                // Missing required output interface.
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        DiagnosticDescriptors.RequiredOutputInterface,
                        methodDeclarationSyntax.Identifier.GetLocation())
                );
            }
            if (sequences.RequiredInput
                && string.IsNullOrEmpty(parameterOptions.VariablePipeReader)
                && string.IsNullOrEmpty(parameterOptions.VariableInputString)
                && string.IsNullOrEmpty(parameterOptions.VariableTextReader))
            {
                RequiredInputInterface = true;
                // Missing required input interface.
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        DiagnosticDescriptors.RequiredInputInterface,
                        methodDeclarationSyntax.Identifier.GetLocation())
                );
            }
        }

        static bool IsLanguageVersionAtLeastCSharp8(LanguageVersion languageVersion)
            => languageVersion switch
            {
                LanguageVersion.Default => true,
                LanguageVersion.Latest => true,
                LanguageVersion.Preview => true,
                LanguageVersion.LatestMajor => true,
                _ => languageVersion >= LanguageVersion.CSharp8,
            };

        static bool TryGetSources(
            IMethodSymbol methodSymbol,
            out BrainfuckSequenceEnumerable sequences
        )
        {
            sequences = default!;
            var attributeData = methodSymbol.GetAttributes().Single(
                x => x.AttributeClass?.ToDisplayString() == NameSpaceName + "." + ClassNameBrainfuckAttribution
            );

            if (attributeData.ConstructorArguments is not { Length: > 0 }
                || attributeData.ConstructorArguments[0] is not { IsNull: false, Value: string source }
                || string.IsNullOrEmpty(source))
            {
                return false;
            }
            var incrementPointer = GetNamedArgumentOrDefault(attributeData, nameof(BrainfuckOptions.IncrementPointer), BrainfuckOptionsDefault.IncrementPointer);
            var decrementPointer = GetNamedArgumentOrDefault(attributeData, nameof(BrainfuckOptions.DecrementPointer), BrainfuckOptionsDefault.DecrementPointer);
            var incrementCurrent = GetNamedArgumentOrDefault(attributeData, nameof(BrainfuckOptions.IncrementCurrent), BrainfuckOptionsDefault.IncrementCurrent);
            var decrementCurrent = GetNamedArgumentOrDefault(attributeData, nameof(BrainfuckOptions.DecrementCurrent), BrainfuckOptionsDefault.DecrementCurrent);
            var output = GetNamedArgumentOrDefault(attributeData, nameof(BrainfuckOptions.Output), BrainfuckOptionsDefault.Output);
            var input = GetNamedArgumentOrDefault(attributeData, nameof(BrainfuckOptions.Input), BrainfuckOptionsDefault.Input);
            var begin = GetNamedArgumentOrDefault(attributeData, nameof(BrainfuckOptions.Begin), BrainfuckOptionsDefault.Begin);
            var end = GetNamedArgumentOrDefault(attributeData, nameof(BrainfuckOptions.End), BrainfuckOptionsDefault.End);
            sequences = new BrainfuckSequenceEnumerable(source!.AsMemory(), new BrainfuckOptions(
                IncrementPointer: incrementPointer,
                DecrementPointer: decrementPointer,
                IncrementCurrent: incrementCurrent,
                DecrementCurrent: decrementCurrent,
                Output: output,
                Input: input,
                Begin: begin,
                End: end
            ));
            return true;
            static T GetNamedArgumentOrDefault<T>(AttributeData attributeData, string name, T defaultValue)
            {
                // ImmutbaleArray<T> does not have a Find method...
                foreach (var namedArgument in attributeData.NamedArguments)
                {
                    if (StringComparer.Ordinal.Equals(namedArgument.Key, name))
                    {
                        return (T)namedArgument.Value.Value!;
                    }
                }
                return defaultValue;
            }
        }
        static bool TryGetReturnKind(
            ITypeSymbol returnType,
            KnownTypes types,
            [NotNullWhen(true)] out MethodReturnKind returnKindResult)
        {
            returnKindResult = MethodSignatureBinder.BindReturnKind(returnType, types);
            return returnKindResult is not MethodReturnKind.Invalid;
        }
        static bool TryGetParameterOptions(
            IMethodSymbol methodSymbol,
            string returnTypeName,
            BrainfuckSequenceEnumerable sequences,
            SourceProductionContext context,
            MethodDeclarationSyntax methodDeclarationSyntax,
            KnownTypes types,
            [NotNullWhen(true)] out ParameterOptions parameterOptions,
            [NotNullWhen(false)] out (DiagnosticDescriptor Descriptor, string Message)? dest)
        {
            dest = null;
            parameterOptions = default!;

            var binding = MethodSignatureBinder.Bind(
                methodSymbol,
                types
            );
            if (binding is { IsValid: true, UnhandledParameters.Count: > 0 })
            {
                foreach (var parameter in binding.UnhandledParameters)
                {
                    var descriptor = DiagnosticDescriptors.InvalidParameter;
                    var diagnostic = Diagnostic.Create(descriptor, parameter.Locations[0], parameter.Type.ToDisplayString());
                    context.ReportDiagnostic(diagnostic);
                    dest = (descriptor, string.Format(descriptor.MessageFormat.ToString(), parameter.Type.ToDisplayString()));
                    return false;
                }
            }
            if (!binding.IsValid)
            {
                var (descriptor, location, messageArgs) = binding.Error switch
                {
                    UnsupportedReturnType error => (
                        DiagnosticDescriptors.InvalidReturnType,
                        error.Location,
                        new object[] { error.ReturnType.ToDisplayString() }
                    ),
                    InvalidParameterModifier error => (
                        DiagnosticDescriptors.NotSupportParameterPattern,
                        error.Location,
                        Array.Empty<object>()
                    ),
                    DuplicateInput error => (
                        error.Parameter.Type.ToDisplayString() == error.ExistingKind switch
                        {
                            MethodInputKind.String => "string",
                            MethodInputKind.TextReader => "System.IO.TextReader",
                            MethodInputKind.PipeReader => "System.IO.Pipelines.PipeReader",
                            _ => null
                        } ? DiagnosticDescriptors.DuplicateParameter : DiagnosticDescriptors.NotSupportParameterPattern,
                        error.Location,
                        [error.Parameter.Type.ToDisplayString()]
                    ),
                    DuplicateOutput error => (
                        error.Parameter.Type.ToDisplayString() == error.ExistingKind switch
                        {
                            MethodOutputKind.TextWriter => "System.IO.TextWriter",
                            MethodOutputKind.PipeWriter => "System.IO.Pipelines.PipeWriter",
                            _ => null
                        } ? DiagnosticDescriptors.DuplicateParameter : DiagnosticDescriptors.NotSupportParameterPattern,
                        error.Location,
                        [error.Parameter.Type.ToDisplayString()]
                    ),
                    DuplicateCancellationToken error => (
                        DiagnosticDescriptors.DuplicateParameter,
                        error.Location,
                        new object[] { error.Parameter.Type.ToDisplayString() }
                    ),
                    DuplicateLogger error => (
                        DiagnosticDescriptors.DuplicateParameter,
                        error.Location,
                        new object[] { error.Parameter.Type.ToDisplayString() }
                    ),
                    ReturnOutputConflict error => (
                        DiagnosticDescriptors.NotSupportParameterAndReturnTypePattern,
                        error.Location,
                        new object[] { error.Parameter.Type.ToDisplayString(), returnTypeName }
                    ),
                    { } error => (
                        DiagnosticDescriptors.NotSupportParameterPattern,
                        error.Location,
                        Array.Empty<object>()
                    )
                };

                location ??= methodDeclarationSyntax.Identifier.GetLocation();

                var diagnostic = Diagnostic.Create(descriptor, location, messageArgs);
                context.ReportDiagnostic(diagnostic);
                dest = (descriptor, diagnostic.ToString());
                return false;
            }

            // Preservation of UnusedInputParameter diagnostic
            if (!sequences.RequiredInput && binding.HasExplicitInput)
            {
                var typeName = binding.InputKind switch
                {
                    MethodInputKind.String => "string",
                    MethodInputKind.TextReader => "System.IO.TextReader",
                    MethodInputKind.PipeReader => "System.IO.Pipelines.PipeReader",
                    _ => ""
                };
                context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.UnusedInputParameter, methodDeclarationSyntax.GetLocation(), typeName));
            }

            var variableCancellation = binding.CancellationTokenName ?? string.Empty;
            var variablePipeWriter = binding.OutputKind == MethodOutputKind.PipeWriter ? binding.OutputExpression : string.Empty;
            var variableTextWriter = binding.OutputKind == MethodOutputKind.TextWriter ? binding.OutputExpression : string.Empty;
            var variablePipeReader = binding.InputKind == MethodInputKind.PipeReader ? binding.InputExpression : string.Empty;
            var variableTextReader = binding.InputKind == MethodInputKind.TextReader ? binding.InputExpression : string.Empty;
            var variableInputString = binding.InputKind == MethodInputKind.String ? binding.InputExpression : string.Empty;
            var variableLogger = binding.LoggerExpression ?? string.Empty;

            parameterOptions = new(
                ParameterSymbols: string.Join(", ", methodSymbol.Parameters.Select(p => $"{p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} {p.Name}")),
                VariableCancellation: variableCancellation,
                VariablePipeWriter: variablePipeWriter,
                VariableTextWriter: variableTextWriter,
                VariablePipeReader: variablePipeReader,
                VariableTextReader: variableTextReader,
                VariableInputString: variableInputString,
                VariableLogger: variableLogger,
                IsLoggerFromParameter: binding.IsLoggerFromParameter
            );

            return true;
        }

    }

}
readonly record struct InternalOptions(
    string Space,
    string VariableStack,
    string VariableStackIndex,
    string? VariablePipeWriter,
    string? VariableTextWriter,
    string? VariablePipeReader,
    string? VariableTextReader,
    string? VariableCancellationToken,
    string? VariableInputString,
    string? VariableLogger,
    bool UseListAsMemory = false,
    bool UseAwait = false
)
{
    [MemberNotNullWhen(true, nameof(VariablePipeWriter))]
    public readonly bool HasPipeWriterParameter => !string.IsNullOrEmpty(VariablePipeWriter);
    [MemberNotNullWhen(true, nameof(VariableTextWriter))]
    public readonly bool HasTextWriterParameter => !string.IsNullOrEmpty(VariableTextWriter);
    [MemberNotNullWhen(true, nameof(VariablePipeReader))]
    public readonly bool HasPipeReaderParameter => !string.IsNullOrEmpty(VariablePipeReader);
    [MemberNotNullWhen(true, nameof(VariableTextReader))]
    public readonly bool HasTextReaderParameter => !string.IsNullOrEmpty(VariableTextReader);
    [MemberNotNullWhen(true, nameof(VariableInputString))]
    public readonly bool HasInputStringParameter => !string.IsNullOrEmpty(VariableInputString);
    [MemberNotNullWhen(true, nameof(VariableCancellationToken))]
    public readonly bool HasCancellationTokenParameter => !string.IsNullOrEmpty(VariableCancellationToken);
    [MemberNotNullWhen(true, nameof(VariableLogger))]
    public readonly bool HasLoggerParameter => !string.IsNullOrEmpty(VariableLogger);

}

readonly record struct ParameterOptions(
    string ParameterSymbols,
    string? VariableCancellation,
    string? VariablePipeWriter,
    string? VariableTextWriter,
    string? VariablePipeReader,
    string? VariableTextReader,
    string? VariableInputString,
    string? VariableLogger,
    bool IsLoggerFromParameter
)
{

    [MemberNotNullWhen(true, nameof(VariablePipeWriter))]
    public readonly bool HasPipeWriterParameter => !string.IsNullOrEmpty(VariablePipeWriter);
    [MemberNotNullWhen(true, nameof(VariableTextWriter))]
    public readonly bool HasTextWriterParameter => !string.IsNullOrEmpty(VariableTextWriter);
    [MemberNotNullWhen(true, nameof(VariablePipeReader))]
    public readonly bool HasPipeReaderParameter => !string.IsNullOrEmpty(VariablePipeReader);
    [MemberNotNullWhen(true, nameof(VariableTextReader))]
    public readonly bool HasTextReaderParameter => !string.IsNullOrEmpty(VariableTextReader);
    [MemberNotNullWhen(true, nameof(VariableInputString))]
    public readonly bool HasInputStringParameter => !string.IsNullOrEmpty(VariableInputString);
    [MemberNotNullWhen(true, nameof(VariableLogger))]
    public readonly bool HasLoggerParameter => !string.IsNullOrEmpty(VariableLogger);

}

