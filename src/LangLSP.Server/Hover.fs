module LangLSP.Server.Hover

open Ionide.LanguageServerProtocol.Types
open LangLSP.Server.AstLookup
open LangLSP.Server.DocumentSync
open LangLSP.Server.Protocol
open Ast
open Type

/// Korean explanations for FunLang keywords
let keywordExplanations = Map.ofList [
    ("let", "변수 또는 함수를 정의합니다.\n예: `let x = 5`")
    ("in", "let 바인딩의 범위를 지정합니다.\n예: `let x = 1 in x + 2`")
    ("if", "조건 분기를 수행합니다.\n예: `if x > 0 then 1 else -1`")
    ("then", "if의 참 분기를 시작합니다.")
    ("else", "if의 거짓 분기를 시작합니다.")
    ("match", "패턴 매칭을 시작합니다.\n예: `match xs with | [] -> 0 | h::t -> h`")
    ("with", "match의 패턴 절을 시작합니다.")
    ("fun", "익명 함수를 정의합니다.\n예: `fun x -> x + 1`")
    ("rec", "재귀 함수를 정의합니다.\n예: `let rec sum n = if n = 0 then 0 else n + sum (n-1)`")
    ("true", "불리언 참 값입니다.")
    ("false", "불리언 거짓 값입니다.")
]

/// Extract word at position from source text
let getWordAtPosition (text: string) (pos: Position) : string option =
    let lines = text.Split('\n')
    let lineIdx = int pos.Line
    if lineIdx >= lines.Length then None
    else
        let line = lines.[lineIdx]
        let col = int pos.Character
        if col >= line.Length then None
        else
            // Find word boundaries (alphanumeric chars)
            let isWordChar c = System.Char.IsLetterOrDigit(c) || c = '_'
            let mutable startCol = col
            while startCol > 0 && isWordChar line.[startCol - 1] do
                startCol <- startCol - 1
            let mutable endCol = col
            while endCol < line.Length && isWordChar line.[endCol] do
                endCol <- endCol + 1
            if startCol = endCol then None
            else Some (line.Substring(startCol, endCol - startCol))

/// Create hover for keyword
let createKeywordHover (keyword: string) (pos: Position) : Hover option =
    keywordExplanations
    |> Map.tryFind keyword
    |> Option.map (fun explanation ->
        {
            Contents = U3.C1 {
                Kind = MarkupKind.Markdown
                Value = sprintf "**%s** (키워드)\n\n%s" keyword explanation
            }
            Range = None  // Could calculate exact keyword range if needed
        })

/// Create hover with type information
let createTypeHover (ty: Type.Type) (span: Span) : Hover =
    let typeStr = Type.formatTypeNormalized ty
    {
        Contents = U3.C1 {
            Kind = MarkupKind.Markdown
            Value = sprintf "```funlang\n%s\n```" typeStr
        }
        Range = Some (spanToLspRange span)
    }

/// Handle textDocument/hover request
let handleHover (p: HoverParams) : Async<Hover option> =
    async {
        let uri = p.TextDocument.Uri
        let pos = p.Position

        match getDocument uri with
        | None -> return None
        | Some text ->
            // Check for keyword hover first
            match getWordAtPosition text pos with
            | Some word when keywordExplanations.ContainsKey word ->
                return createKeywordHover word pos
            | _ ->
                // Try AST-based hover for types
                try
                    let lexbuf = FSharp.Text.Lexing.LexBuffer<char>.FromString(text)
                    let ast = Parser.start Lexer.tokenize lexbuf
                    match findNodeAtPosition pos ast with
                    | None -> return None
                    | Some node ->
                        // Type check the specific node to get its type
                        match TypeCheck.typecheck node with
                        | Error _ -> return None
                        | Ok ty ->
                            return Some (createTypeHover ty (spanOf node))
                with _ ->
                    return None
    }
