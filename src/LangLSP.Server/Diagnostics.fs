module LangLSP.Server.Diagnostics

open System
open FSharp.Text.Lexing
open Ionide.LanguageServerProtocol
open Ionide.LanguageServerProtocol.Types
open LangLSP.Server.Protocol

/// Parse FunLang source code and catch syntax errors
/// Returns Ok(ast) on success, Error(diagnostic) on parse error
let parseFunLang (source: string) (uri: string) : Result<Ast.Expr, Diagnostic> =
    try
        let lexbuf = LexBuffer<char>.FromString(source)
        let ast = Parser.start Lexer.tokenize lexbuf
        Ok ast
    with
    | ex ->
        // Parse error - create a diagnostic
        // fsyacc exceptions contain position info in the message
        let message =
            if ex.Message.Contains("parse error") then
                "Syntax error: " + ex.Message
            else
                "Parse error: " + ex.Message

        // Create a span for the error location
        // If we can't extract position, use (1,1)-(1,1)
        let span : Ast.Span = {
            FileName = uri
            StartLine = 1
            StartColumn = 1
            EndLine = 1
            EndColumn = 1
        }

        let diag: Diagnostic = {
            Range = spanToLspRange span
            Severity = Some DiagnosticSeverity.Error
            Code = None
            CodeDescription = None
            Source = Some "funlang"
            Message = message
            Tags = None
            RelatedInformation = None
            Data = None
        }
        Error diag

/// Type check AST using FunLang's type checker
/// Returns Ok(type) on success, Error(diagnostic) on type error
let typecheckAst (ast: Ast.Expr) : Result<Type.Type, Diagnostic> =
    match TypeCheck.typecheckWithDiagnostic ast with
    | Ok ty -> Ok ty
    | Error funlangDiag ->
        // Convert FunLang diagnostic to LSP diagnostic
        let lspDiag = diagnosticToLsp funlangDiag
        Error lspDiag

/// Analyze document and return all diagnostics
/// Returns list of diagnostics (empty if no errors)
let analyze (uri: string) (source: string) : Diagnostic list =
    match parseFunLang source uri with
    | Error parseDiag ->
        // Parse error - stop here, don't try to typecheck
        [parseDiag]
    | Ok ast ->
        // Parse succeeded, now typecheck
        match typecheckAst ast with
        | Ok _ ->
            // No errors
            []
        | Error typeDiag ->
            // Type error
            [typeDiag]

/// Publish diagnostics to client
let publishDiagnostics (lspClient: ILspClient) (uri: string) (diagnostics: Diagnostic list) : Async<unit> =
    async {
        let publishParams: PublishDiagnosticsParams = {
            Uri = uri
            Diagnostics = Array.ofList diagnostics
            Version = None
        }
        do! lspClient.TextDocumentPublishDiagnostics publishParams
    }

/// Clear diagnostics for a document
let clearDiagnostics (lspClient: ILspClient) (uri: string) : Async<unit> =
    publishDiagnostics lspClient uri []
