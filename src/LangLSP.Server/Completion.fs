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

/// Scan text for let/let rec bindings before cursor as fallback when parsing fails
let scanTextForSymbols (text: string) (pos: Position) : CompletionItem list =
    let lines = text.Split('\n')
    let letPattern = System.Text.RegularExpressions.Regex(@"\blet\s+rec\s+(\w+)|\blet\s+(\w+)")
    [
        for lineIdx in 0 .. min (int pos.Line) (lines.Length - 1) do
            let line = lines.[lineIdx]
            for m in letPattern.Matches(line) do
                let name =
                    if m.Groups.[1].Success then m.Groups.[1].Value
                    else m.Groups.[2].Value
                // Skip keywords and wildcard
                if name <> "rec" && name <> "_" then
                    yield {
                        Label = name
                        LabelDetails = None
                        Kind = Some CompletionItemKind.Variable
                        Detail = Some name
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
                    }
    ]
    |> List.rev
    |> List.distinctBy (fun item -> item.Label)
    |> List.rev

/// Handle textDocument/completion request
let handleCompletion (p: CompletionParams) : Async<CompletionList option> =
    async {
        let uri = p.TextDocument.Uri
        let pos = p.Position

        match getDocument uri with
        | None -> return None
        | Some text ->
            let keywords = getKeywordCompletions()
            try
                let lexbuf = FSharp.Text.Lexing.LexBuffer<char>.FromString(text)
                let ast = Parser.start Lexer.tokenize lexbuf

                let symbols = getSymbolCompletions ast pos

                return Some {
                    IsIncomplete = false
                    Items = (keywords @ symbols) |> Array.ofList
                    ItemDefaults = None
                }
            with _ ->
                // On parse error, scan text for symbols as fallback
                let symbols = scanTextForSymbols text pos
                return Some {
                    IsIncomplete = false
                    Items = (keywords @ symbols) |> Array.ofList
                    ItemDefaults = None
                }
    }
