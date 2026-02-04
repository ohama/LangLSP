module LangLSP.Server.Protocol

open Ionide.LanguageServerProtocol.Types
open Ast  // FunLang's Span type

/// Convert FunLang Span (1-based) to LSP Range (0-based)
/// FunLang: StartLine=1 means first line
/// LSP: line=0 means first line
/// Edge case: if span is (0,0), clamp to (0,0) instead of wrapping to uint.MaxValue
let spanToLspRange (span: Span) : Range =
    let clamp x = max 0 (x - 1)
    {
        Start = { Line = uint32 (clamp span.StartLine); Character = uint32 (clamp span.StartColumn) }
        End = { Line = uint32 (clamp span.EndLine); Character = uint32 (clamp span.EndColumn) }
    }

/// Convert FunLang Diagnostic to LSP Diagnostic
let diagnosticToLsp (diag: Diagnostic.Diagnostic) : Diagnostic =
    {
        Range = spanToLspRange diag.PrimarySpan
        Severity = Some DiagnosticSeverity.Error
        Code = None  // Will be enhanced later with error codes
        CodeDescription = None
        Source = Some "funlang"
        Message = diag.Message
        Tags = None
        RelatedInformation =
            if List.isEmpty diag.SecondarySpans then None
            else
                diag.SecondarySpans
                |> List.map (fun (span, label) ->
                    {
                        Location = {
                            Uri = span.FileName  // Will need URI conversion later
                            Range = spanToLspRange span
                        }
                        Message = label
                    })
                |> Array.ofList
                |> Some
        Data = None
    }
