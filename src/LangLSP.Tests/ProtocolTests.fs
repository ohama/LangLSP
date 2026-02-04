module LangLSP.Tests.ProtocolTests

open Expecto
open Expecto.ExpectoFsCheck
open FsCheck
open LangLSP.Server.Protocol

/// Generator for valid Span values
/// Note: LexBuffer.FromString produces 0-based spans (contrary to FsLexYacc docs)
let validSpanGen : Gen<Ast.Span> =
    gen {
        let! startLine = Gen.choose(0, 1000)
        let! startColumn = Gen.choose(0, 200)
        let! endLine = Gen.choose(startLine, 1000)
        // If on same line, endColumn >= startColumn
        // If on different line, endColumn can be anything
        let! endColumn =
            if endLine = startLine then
                Gen.choose(startColumn, 200)
            else
                Gen.choose(0, 200)

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

        testPropertyWithConfig fsCheckConfig "spanToLspRange direct conversion (both 0-based)" <| fun (span: Ast.Span) ->
            let range = spanToLspRange span
            // Both FunLang (from LexBuffer.FromString) and LSP are 0-based
            // No conversion needed
            Expect.equal range.Start.Line (uint32 span.StartLine) "Start line should match"
            Expect.equal range.Start.Character (uint32 span.StartColumn) "Start char should match"
            Expect.equal range.End.Line (uint32 span.EndLine) "End line should match"
            Expect.equal range.End.Character (uint32 span.EndColumn) "End char should match"

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

        testCase "spanToLspRange: (0,0) is valid first position" <| fun _ ->
            let span : Ast.Span = {
                FileName = "test.fun"
                StartLine = 0
                StartColumn = 0
                EndLine = 0
                EndColumn = 5
            }
            let range = spanToLspRange span
            // (0,0) is a valid position in both FunLang and LSP
            Expect.equal range.Start.Line 0u "First line is 0"
            Expect.equal range.Start.Character 0u "First column is 0"
            Expect.equal range.End.Line 0u "End line is 0"
            Expect.equal range.End.Character 5u "End column is 5"

        testCase "spanToLspRange typical span" <| fun _ ->
            let span : Ast.Span = {
                FileName = "test.fun"
                StartLine = 5
                StartColumn = 10
                EndLine = 5
                EndColumn = 20
            }
            let range = spanToLspRange span
            Expect.equal range.Start.Line 5u "Line preserved"
            Expect.equal range.Start.Character 10u "Column preserved"
            Expect.equal range.End.Line 5u "End line preserved"
            Expect.equal range.End.Character 20u "End column preserved"
    ]
