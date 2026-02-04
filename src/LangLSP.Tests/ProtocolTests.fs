module LangLSP.Tests.ProtocolTests

open Expecto
open Expecto.ExpectoFsCheck
open FsCheck
open LangLSP.Server.Protocol

/// Generator for valid Span values
/// FunLang Span is 1-based, positive, and start <= end
let validSpanGen : Gen<Ast.Span> =
    gen {
        let! startLine = Gen.choose(1, 1000)
        let! startColumn = Gen.choose(1, 200)
        let! endLine = Gen.choose(startLine, 1000)
        // If on same line, endColumn >= startColumn
        // If on different line, endColumn can be anything
        let! endColumn =
            if endLine = startLine then
                Gen.choose(startColumn, 200)
            else
                Gen.choose(1, 200)

        let span : Ast.Span = {
            FileName = "test.fun"
            StartLine = startLine
            StartColumn = startColumn
            EndLine = endLine
            EndColumn = endColumn
        }
        return span
    }

/// Arbitrary instance for valid Spans
type ValidSpanArbitrary =
    static member Span() = Arb.fromGen validSpanGen

[<Tests>]
let protocolTests =
    // Configure FsCheck to run 500 test cases
    let fsCheckConfig = { FsCheckConfig.defaultConfig with maxTest = 500; arbitrary = [typeof<ValidSpanArbitrary>] }

    testList "Protocol" [

        testPropertyWithConfig fsCheckConfig "spanToLspRange converts to 0-based" <| fun (span: Ast.Span) ->
            let range = spanToLspRange span
            // LSP is 0-based, FunLang is 1-based
            // Line numbers should be decremented by 1
            Expect.equal range.Start.Line (uint32 (max 0 (span.StartLine - 1))) "Start line should be 0-based"
            Expect.equal range.Start.Character (uint32 (max 0 (span.StartColumn - 1))) "Start char should be 0-based"
            Expect.equal range.End.Line (uint32 (max 0 (span.EndLine - 1))) "End line should be 0-based"
            Expect.equal range.End.Character (uint32 (max 0 (span.EndColumn - 1))) "End char should be 0-based"

        testPropertyWithConfig fsCheckConfig "spanToLspRange preserves line ordering" <| fun (span: Ast.Span) ->
            let range = spanToLspRange span
            // If start line <= end line in source, it should remain so in LSP
            Expect.isLessThanOrEqual range.Start.Line range.End.Line "Start line should be <= end line"

        testPropertyWithConfig fsCheckConfig "spanToLspRange same line preserves character ordering" <| fun (span: Ast.Span) ->
            let range = spanToLspRange span
            // If on same line, start character <= end character
            if range.Start.Line = range.End.Line then
                Expect.isLessThanOrEqual range.Start.Character range.End.Character
                    "On same line, start char should be <= end char"

        testCase "spanToLspRange edge case: first line first column" <| fun _ ->
            let span : Ast.Span = {
                FileName = "test.fun"
                StartLine = 1
                StartColumn = 1
                EndLine = 1
                EndColumn = 1
            }
            let range = spanToLspRange span
            // First line, first column in FunLang (1,1) should become (0,0) in LSP
            Expect.equal range.Start.Line 0u "First line should be 0"
            Expect.equal range.Start.Character 0u "First column should be 0"
            Expect.equal range.End.Line 0u "End line should be 0"
            Expect.equal range.End.Character 0u "End column should be 0"

        testCase "spanToLspRange edge case: invalid (0,0) span clamped" <| fun _ ->
            let span : Ast.Span = {
                FileName = "test.fun"
                StartLine = 0
                StartColumn = 0
                EndLine = 0
                EndColumn = 0
            }
            let range = spanToLspRange span
            // Invalid (0,0) span should be clamped to (0,0) instead of wrapping to uint.MaxValue
            Expect.equal range.Start.Line 0u "Invalid span start line should clamp to 0"
            Expect.equal range.Start.Character 0u "Invalid span start char should clamp to 0"
            Expect.equal range.End.Line 0u "Invalid span end line should clamp to 0"
            Expect.equal range.End.Character 0u "Invalid span end char should clamp to 0"
    ]
