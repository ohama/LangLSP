module LangLSP.Server.Hover

open Serilog
open Ionide.LanguageServerProtocol.Types
open LangLSP.Server.AstLookup
open LangLSP.Server.DocumentSync
open LangLSP.Server.Protocol
open Ast
open Type
open Elaborate

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

/// Get type for a simple node (literal, direct type inference)
/// For complex nodes like Var, we need context from the whole AST
let getNodeType (node: Expr) : Type.Type option =
    match node with
    | Number(_, _) -> Some TInt
    | Bool(_, _) -> Some TBool
    | String(_, _) -> Some TString
    | EmptyList _ -> None  // Can't determine element type without context
    | _ ->
        // For other nodes, try direct typecheck (works for lambdas, etc.)
        match TypeCheck.typecheck node with
        | Ok ty -> Some ty
        | Error _ -> None

/// Find variable type by searching binding sites in AST
/// Returns the type of the value bound to the variable
let rec findVarTypeInAst (varName: string) (ast: Expr) : Type.Type option =
    match ast with
    | Let(name, value, body, _) when name = varName ->
        // Found the binding - typecheck the value
        match TypeCheck.typecheck value with
        | Ok ty -> Some ty
        | Error _ -> None
    | Let(_, _, body, _) ->
        // Not this binding, search in body
        findVarTypeInAst varName body
    | LetRec(name, param, fnBody, inExpr, span) when name = varName ->
        // Recursive function - synthesize "let rec f x = body in f" to get function type
        let synthExpr = LetRec(name, param, fnBody, Var(name, span), span)
        match TypeCheck.typecheck synthExpr with
        | Ok ty -> Some ty
        | Error _ -> None
    | LetRec(_, _, _, inExpr, _) ->
        findVarTypeInAst varName inExpr
    | Lambda(param, body, _) when param = varName ->
        // Lambda parameter - need context to determine type
        None
    | Lambda(_, body, _) ->
        findVarTypeInAst varName body
    | LambdaAnnot(param, typeAnnot, body, _) when param = varName ->
        // Annotated lambda parameter - convert TypeExpr to Type
        Some (elaborateTypeExpr typeAnnot)
    | LambdaAnnot(_, _, body, _) ->
        findVarTypeInAst varName body
    | If(_, thenExpr, elseExpr, _) ->
        // Search in both branches
        match findVarTypeInAst varName thenExpr with
        | Some ty -> Some ty
        | None -> findVarTypeInAst varName elseExpr
    | App(fn, arg, _) ->
        match findVarTypeInAst varName fn with
        | Some ty -> Some ty
        | None -> findVarTypeInAst varName arg
    | _ -> None

/// Handle textDocument/hover request
let handleHover (p: HoverParams) : Async<Hover option> =
    async {
        let uri = p.TextDocument.Uri
        let pos = p.Position
        Log.Debug("handleHover: uri={Uri}, line={Line}, char={Char}", uri, pos.Line, pos.Character)

        match getDocument uri with
        | None ->
            Log.Warning("handleHover: document not found for {Uri}", uri)
            return None
        | Some text ->
            Log.Debug("handleHover: document found, length={Len}", text.Length)
            // Check for keyword hover first
            match getWordAtPosition text pos with
            | Some word when keywordExplanations.ContainsKey word ->
                Log.Debug("handleHover: keyword hover for '{Word}'", word)
                return createKeywordHover word pos
            | wordOpt ->
                Log.Debug("handleHover: not a keyword, word={Word}", wordOpt)
                // Try AST-based hover for types
                try
                    let lexbuf = FSharp.Text.Lexing.LexBuffer<char>.FromString(text)
                    let ast = Parser.start Lexer.tokenize lexbuf
                    Log.Debug("handleHover: parsed AST successfully")

                    // Find the node at position
                    match findNodeAtPosition pos ast with
                    | None ->
                        Log.Debug("handleHover: no AST node at position")
                        return None
                    | Some node ->
                        Log.Debug("handleHover: found node {Node}", node)
                        // Get type based on node kind
                        let nodeType =
                            match node with
                            | Var(name, _) ->
                                match findVarTypeInAst name ast with
                                | Some ty -> Some ty
                                | None -> None
                            | Let(name, value, _, _) ->
                                // Cursor on let binding name — show bound value type
                                match TypeCheck.typecheck value with
                                | Ok ty -> Some ty
                                | Error _ -> None
                            | LetRec(name, param, fnBody, _, span) ->
                                // Cursor on let rec binding name — synthesize to get function type
                                let synthExpr = LetRec(name, param, fnBody, Var(name, span), span)
                                match TypeCheck.typecheck synthExpr with
                                | Ok ty -> Some ty
                                | Error _ -> None
                            | _ -> getNodeType node

                        match nodeType with
                        | None ->
                            Log.Debug("handleHover: no type for node")
                            return None
                        | Some ty ->
                            Log.Debug("handleHover: returning type {Type}", Type.formatTypeNormalized ty)
                            return Some (createTypeHover ty (spanOf node))
                with ex ->
                    Log.Error("handleHover: exception during hover: {Message}", ex.Message)
                    return None
    }
