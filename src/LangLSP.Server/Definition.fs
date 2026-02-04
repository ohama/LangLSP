module LangLSP.Server.Definition

open Ionide.LanguageServerProtocol.Types
open LangLSP.Server.AstLookup
open LangLSP.Server.DocumentSync
open LangLSP.Server.Protocol
open Ast

/// Collect all variable/function definitions while traversing AST
/// Returns list of (name, span) pairs for all binding sites
let collectDefinitions (ast: Expr) : (string * Span) list =
    let defs = ResizeArray<string * Span>()

    let rec traverse expr =
        match expr with
        | Let(name, value, body, span) ->
            // Register let binding
            defs.Add(name, span)
            traverse value
            traverse body

        | LetRec(name, param, fnBody, inExpr, span) ->
            // Register recursive function
            defs.Add(name, span)
            // param is also a binding site
            defs.Add(param, span)
            traverse fnBody
            traverse inExpr

        | Lambda(param, body, span) ->
            // Register lambda parameter
            defs.Add(param, span)
            traverse body

        | LambdaAnnot(param, _, body, span) ->
            // Register annotated lambda parameter
            defs.Add(param, span)
            traverse body

        | LetPat(pattern, value, body, _) ->
            // Collect bindings from pattern
            collectPatternBindings pattern
            traverse value
            traverse body

        | Match(scrutinee, clauses, _) ->
            traverse scrutinee
            for (pattern, clauseBody) in clauses do
                collectPatternBindings pattern
                traverse clauseBody

        | App(fn, arg, _) ->
            traverse fn
            traverse arg

        | If(cond, thenExpr, elseExpr, _) ->
            traverse cond
            traverse thenExpr
            traverse elseExpr

        | Add(l, r, _) | Subtract(l, r, _) | Multiply(l, r, _) | Divide(l, r, _)
        | Equal(l, r, _) | NotEqual(l, r, _) | LessThan(l, r, _) | GreaterThan(l, r, _)
        | LessEqual(l, r, _) | GreaterEqual(l, r, _) | And(l, r, _) | Or(l, r, _)
        | Cons(l, r, _) ->
            traverse l
            traverse r

        | Negate(e, _) | Annot(e, _, _) ->
            traverse e

        | Tuple(exprs, _) | List(exprs, _) ->
            exprs |> List.iter traverse

        | Number _ | Bool _ | String _ | Var _ | EmptyList _ -> ()

    and collectPatternBindings pattern =
        match pattern with
        | VarPat(name, span) -> defs.Add(name, span)
        | TuplePat(pats, _) -> pats |> List.iter collectPatternBindings
        | ConsPat(head, tail, _) ->
            collectPatternBindings head
            collectPatternBindings tail
        | WildcardPat _ | EmptyListPat _ | ConstPat _ -> ()

    traverse ast
    defs |> Seq.toList

/// Find definition span for a variable reference
/// Walks outward from usage to find the closest binding
/// For shadowing: returns the definition that appears last before usage position
let findDefinitionForVar (varName: string) (ast: Expr) (usagePos: Position) : Span option =
    let defs = collectDefinitions ast
                |> List.filter (fun (name, _) -> name = varName)

    // For shadowing: need the definition that is in scope at usagePos
    // Simplest heuristic: return the definition that appears last before usage
    // Note: LexBuffer.FromString produces 0-based coordinates (matching LSP)
    let usageLine = int usagePos.Line
    let usageCol = int usagePos.Character

    defs
    |> List.filter (fun (_, span) ->
        span.StartLine < usageLine ||
        (span.StartLine = usageLine && span.StartColumn <= usageCol))
    |> List.sortByDescending (fun (_, span) -> span.StartLine, span.StartColumn)
    |> List.tryHead
    |> Option.map snd

/// Handle textDocument/definition request
/// Returns Definition.C1 for single location (GOTO-03: single-file scope)
let handleDefinition (p: DefinitionParams) : Async<Definition option> =
    async {
        let uri = p.TextDocument.Uri
        let pos = p.Position

        match getDocument uri with
        | None -> return None
        | Some text ->
            try
                let lexbuf = FSharp.Text.Lexing.LexBuffer<char>.FromString(text)
                let ast = Parser.start Lexer.tokenize lexbuf

                match findNodeAtPosition pos ast with
                | None -> return None
                | Some node ->
                    match node with
                    | Var(name, _) ->
                        // Find where this variable was defined
                        match findDefinitionForVar name ast pos with
                        | None -> return None
                        | Some defSpan ->
                            let location : Location = {
                                Uri = uri
                                Range = spanToLspRange defSpan
                            }
                            return Some (U2.C1 location)
                    | _ ->
                        // Not a variable reference
                        return None
            with _ ->
                return None
    }
