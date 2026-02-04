module LangLSP.Server.Diagnostics

open System
open FSharp.Text.Lexing
open Serilog
open Ionide.LanguageServerProtocol
open Ionide.LanguageServerProtocol.Types
open LangLSP.Server.Protocol
open LangLSP.Server.References

/// Parse FunLang source code and catch syntax errors
/// Returns Ok(ast) on success, Error(diagnostic) on parse error
let parseFunLang (source: string) (uri: string) : Result<Ast.Expr, Diagnostic> =
    let lexbuf = LexBuffer<char>.FromString(source)
    try
        let ast = Parser.start Lexer.tokenize lexbuf
        Ok ast
    with
    | ex ->
        // Parse error - extract position from lexbuf
        let message =
            if ex.Message.Contains("parse error") then
                "Syntax error: " + ex.Message
            else
                "Parse error: " + ex.Message

        // Use lexbuf position (1-based line, 0-based column in FsLexYacc)
        let startPos = lexbuf.StartPos
        let endPos = lexbuf.EndPos
        Log.Debug("Parse error at StartPos: Line={StartLine}, Col={StartCol}; EndPos: Line={EndLine}, Col={EndCol}",
                  startPos.Line, startPos.Column, endPos.Line, endPos.Column)
        let span : Ast.Span = {
            FileName = uri
            StartLine = startPos.Line + 1
            StartColumn = startPos.Column
            EndLine = endPos.Line + 1
            EndColumn = endPos.Column
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

/// Find unused let-bound variables in the AST
/// Returns list of (name, span) for unused variables
let findUnusedVariables (ast: Ast.Expr) : (string * Ast.Span) list =
    let unusedVars = ResizeArray<string * Ast.Span>()

    let rec traverse expr =
        match expr with
        | Ast.Let(name, value, body, span) ->
            // Skip variables prefixed with underscore (intentionally unused)
            if not (name.StartsWith("_")) then
                // Check if this variable is used in the body
                let references = collectReferences name body
                if List.isEmpty references then
                    unusedVars.Add(name, span)
            traverse value
            traverse body

        | Ast.LetRec(name, param, fnBody, inExpr, span) ->
            // Check recursive function usage
            if not (name.StartsWith("_")) then
                let referencesInBody = collectReferences name fnBody
                let referencesInExpr = collectReferences name inExpr
                if List.isEmpty referencesInBody && List.isEmpty referencesInExpr then
                    unusedVars.Add(name, span)
            // Check parameter usage
            if not (param.StartsWith("_")) then
                let paramRefs = collectReferences param fnBody
                if List.isEmpty paramRefs then
                    unusedVars.Add(param, span)
            traverse fnBody
            traverse inExpr

        | Ast.Lambda(param, body, span) ->
            if not (param.StartsWith("_")) then
                let references = collectReferences param body
                if List.isEmpty references then
                    unusedVars.Add(param, span)
            traverse body

        | Ast.LambdaAnnot(param, _, body, span) ->
            if not (param.StartsWith("_")) then
                let references = collectReferences param body
                if List.isEmpty references then
                    unusedVars.Add(param, span)
            traverse body

        | Ast.LetPat(_, value, body, _) ->
            traverse value
            traverse body

        | Ast.Match(scrutinee, clauses, _) ->
            traverse scrutinee
            for (_, clauseBody) in clauses do
                traverse clauseBody

        | Ast.App(fn, arg, _) ->
            traverse fn
            traverse arg

        | Ast.If(cond, thenExpr, elseExpr, _) ->
            traverse cond
            traverse thenExpr
            traverse elseExpr

        | Ast.Add(l, r, _) | Ast.Subtract(l, r, _) | Ast.Multiply(l, r, _) | Ast.Divide(l, r, _)
        | Ast.Equal(l, r, _) | Ast.NotEqual(l, r, _) | Ast.LessThan(l, r, _) | Ast.GreaterThan(l, r, _)
        | Ast.LessEqual(l, r, _) | Ast.GreaterEqual(l, r, _) | Ast.And(l, r, _) | Ast.Or(l, r, _)
        | Ast.Cons(l, r, _) ->
            traverse l
            traverse r

        | Ast.Negate(e, _) | Ast.Annot(e, _, _) ->
            traverse e

        | Ast.Tuple(exprs, _) | Ast.List(exprs, _) ->
            exprs |> List.iter traverse

        | Ast.Number _ | Ast.Bool _ | Ast.String _ | Ast.Var _ | Ast.EmptyList _ -> ()

    traverse ast
    unusedVars |> Seq.toList

/// Analyze document and return all diagnostics
/// Returns list of diagnostics (empty if no errors)
let analyze (uri: string) (source: string) : Diagnostic list =
    match parseFunLang source uri with
    | Error parseDiag ->
        // Parse error - stop here, don't try to typecheck
        [parseDiag]
    | Ok ast ->
        // Parse succeeded, now typecheck
        let typeDiags =
            match typecheckAst ast with
            | Ok _ -> []
            | Error typeDiag -> [typeDiag]

        // Check for unused variables
        let unusedVars = findUnusedVariables ast
        let unusedDiags =
            unusedVars
            |> List.map (fun (name, span) ->
                {
                    Range = spanToLspRange span
                    Severity = Some DiagnosticSeverity.Warning
                    Code = Some (U2.C2 "unused-variable")
                    CodeDescription = None
                    Source = Some "funlang"
                    Message = sprintf "Unused variable '%s'" name
                    Tags = Some [| DiagnosticTag.Unnecessary |]
                    RelatedInformation = None
                    Data = None
                })

        // Combine type errors and unused variable warnings
        typeDiags @ unusedDiags

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
