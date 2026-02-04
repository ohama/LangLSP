module LangLSP.Server.Protocol

open Ionide.LanguageServerProtocol.Types
open Ast  // FunLang's Span type

/// Convert FunLang Span to LSP Range
/// Note: LexBuffer.FromString creates 0-based positions, matching LSP
/// So no conversion needed (contrary to FsLexYacc documentation)
let spanToLspRange (span: Span) : Range =
    {
        Start = { Line = uint32 span.StartLine; Character = uint32 span.StartColumn }
        End = { Line = uint32 span.EndLine; Character = uint32 span.EndColumn }
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

/// Create TextEdit from span and new text
let createTextEdit (span: Span) (newText: string) : TextEdit =
    {
        Range = spanToLspRange span
        NewText = newText
    }

/// Create WorkspaceEdit for single-file changes
let createWorkspaceEdit (uri: string) (edits: TextEdit[]) : WorkspaceEdit =
    {
        Changes = Some (Map.ofList [ (uri, edits) ])
        DocumentChanges = None
        ChangeAnnotations = None
    }
