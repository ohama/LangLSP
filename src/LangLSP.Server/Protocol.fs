module LangLSP.Server.Protocol

open Ionide.LanguageServerProtocol.Types
open Ast  // FunLang's Span type

/// Convert FunLang Span (1-based) to LSP Range (0-based)
/// FunLang: StartLine=1 means first line
/// LSP: line=0 means first line
let spanToLspRange (span: Span) : Range =
    {
        Start = { Line = uint32 (span.StartLine - 1); Character = uint32 (span.StartColumn - 1) }
        End = { Line = uint32 (span.EndLine - 1); Character = uint32 (span.EndColumn - 1) }
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
