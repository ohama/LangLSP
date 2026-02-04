module LangLSP.Server.Completion

open Ionide.LanguageServerProtocol.Types
open LangLSP.Server.DocumentSync
open LangLSP.Server.Definition
open LangLSP.Server.Hover
open Ast
open Type

/// FunLang keywords for completion
let funlangKeywords = [
    "let"; "in"; "if"; "then"; "else"; "match"; "with"
    "fun"; "rec"; "true"; "false"
]

/// Create completion items for keywords
let getKeywordCompletions () : CompletionItem list =
    funlangKeywords
    |> List.map (fun kw ->
        {
            Label = kw
            LabelDetails = None
            Kind = Some CompletionItemKind.Keyword
            Detail = Some "keyword"
            Documentation = None
            Deprecated = Some false
            Preselect = Some false
            SortText = None
            FilterText = None
            InsertText = Some kw
            InsertTextFormat = Some InsertTextFormat.PlainText
            InsertTextMode = None
            TextEdit = None
            TextEditText = None
            AdditionalTextEdits = None
            CommitCharacters = None
            Command = None
            Data = None
            Tags = None
        })

/// Get symbol completions from current scope
/// Filters to symbols defined before cursor position
let getSymbolCompletions (ast: Expr) (pos: Position) : CompletionItem list =
    let definitions = Definition.collectDefinitions ast

    // Filter to symbols defined before cursor position (scope filtering)
    let inScope =
        definitions
        |> List.filter (fun (_, span) ->
            span.StartLine < int pos.Line ||
            (span.StartLine = int pos.Line && span.StartColumn < int pos.Character))
        // Remove duplicates - for shadowing, keep last occurrence
        |> List.rev
        |> List.distinctBy fst
        |> List.rev

    // Create completion items with type information
    inScope
    |> List.map (fun (name, span) ->
        // Try to get type for this symbol
        let typeInfo = Hover.findVarTypeInAst name ast

        let detail =
            match typeInfo with
            | Some ty -> Some (sprintf "%s: %s" name (Type.formatTypeNormalized ty))
            | None -> Some name

        {
            Label = name
            LabelDetails = None
            Kind = Some CompletionItemKind.Variable
            Detail = detail
            Documentation = None
            Deprecated = Some false
            Preselect = Some false
            SortText = None
            FilterText = None
            InsertText = Some name
            InsertTextFormat = Some InsertTextFormat.PlainText
            InsertTextMode = None
            TextEdit = None
            TextEditText = None
            AdditionalTextEdits = None
            CommitCharacters = None
            Command = None
            Data = None
            Tags = None
        })

/// Handle textDocument/completion request
let handleCompletion (p: CompletionParams) : Async<CompletionList option> =
    async {
        let uri = p.TextDocument.Uri
        let pos = p.Position

        match getDocument uri with
        | None -> return None
        | Some text ->
            try
                let lexbuf = FSharp.Text.Lexing.LexBuffer<char>.FromString(text)
                let ast = Parser.start Lexer.tokenize lexbuf

                let keywords = getKeywordCompletions()
                let symbols = getSymbolCompletions ast pos

                return Some {
                    IsIncomplete = false
                    Items = (keywords @ symbols) |> Array.ofList
                    ItemDefaults = None
                }
            with _ ->
                // On parse error, return only keywords
                return Some {
                    IsIncomplete = false
                    Items = getKeywordCompletions() |> Array.ofList
                    ItemDefaults = None
                }
    }
