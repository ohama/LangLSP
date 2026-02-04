module LangLSP.Server.References

open Ionide.LanguageServerProtocol.Types
open LangLSP.Server.AstLookup
open LangLSP.Server.Definition
open LangLSP.Server.DocumentSync
open LangLSP.Server.Protocol
open Ast

/// Collect all variable references (Var nodes) matching a symbol name
/// Traverses entire AST and returns list of spans for all Var nodes with matching name
let collectReferences (varName: string) (ast: Expr) : Span list =
    let refs = ResizeArray<Span>()

    let rec traverse expr =
        match expr with
        | Var(name, span) when name = varName ->
            refs.Add(span)

        | Let(_, value, body, _) ->
            traverse value
            traverse body

        | LetRec(_, _, fnBody, inExpr, _) ->
            traverse fnBody
            traverse inExpr

        | Lambda(_, body, _) ->
            traverse body

        | LambdaAnnot(_, _, body, _) ->
            traverse body

        | LetPat(_, value, body, _) ->
            traverse value
            traverse body

        | Match(scrutinee, clauses, _) ->
            traverse scrutinee
            for (_, clauseBody) in clauses do
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

        | Number _ | Bool _ | String _ | EmptyList _ | Var _ -> ()

    traverse ast
    refs |> Seq.toList

/// Collect references to a SPECIFIC binding (shadowing-aware)
/// Only returns Var nodes that resolve to the definition at defSpan
let collectReferencesForBinding (varName: string) (defSpan: Span) (ast: Expr) : Span list =
    // Find all Var nodes with matching name
    let allRefs = collectReferences varName ast

    // Filter to only those that resolve to our specific definition
    allRefs
    |> List.filter (fun refSpan ->
        // Create Position from the reference span
        let pos : Position = {
            Line = uint32 refSpan.StartLine
            Character = uint32 refSpan.StartColumn
        }
        // Check if this reference resolves to our target definition
        match findDefinitionForVar varName ast pos with
        | Some foundDefSpan ->
            foundDefSpan.StartLine = defSpan.StartLine &&
            foundDefSpan.StartColumn = defSpan.StartColumn
        | None -> false
    )

/// Handle textDocument/references request
/// Returns all locations where a symbol is referenced
let handleReferences (p: ReferenceParams) : Async<Location[] option> =
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
                    // Get identifier and definition span for the symbol at cursor
                    let identOpt, defSpanOpt =
                        match node with
                        | Var(name, _) ->
                            // Variable reference - find its definition
                            let def = findDefinitionForVar name ast pos
                            (Some name, def)
                        | Let(name, _, _, span) ->
                            // Let binding site
                            (Some name, Some span)
                        | Lambda(param, _, span) ->
                            // Lambda parameter
                            (Some param, Some span)
                        | LambdaAnnot(param, _, _, span) ->
                            // Annotated lambda parameter
                            (Some param, Some span)
                        | LetRec(name, _, _, _, span) ->
                            // Recursive function binding
                            (Some name, Some span)
                        | _ ->
                            // Not a symbol we can find references for
                            (None, None)

                    match identOpt, defSpanOpt with
                    | Some varName, Some defSpan ->
                        // Collect shadowing-aware references
                        let refSpans = collectReferencesForBinding varName defSpan ast

                        // Include declaration if requested
                        let allSpans =
                            if p.Context.IncludeDeclaration then
                                defSpan :: refSpans
                            else
                                refSpans

                        // Convert to Location[]
                        let locations =
                            allSpans
                            |> List.map (fun span ->
                                {
                                    Uri = uri
                                    Range = spanToLspRange span
                                })
                            |> Array.ofList

                        return Some locations

                    | Some varName, None ->
                        // Found identifier but no definition (shouldn't happen often)
                        // Fall back to collecting all references with matching name
                        let refSpans = collectReferences varName ast

                        let locations =
                            refSpans
                            |> List.map (fun span ->
                                {
                                    Uri = uri
                                    Range = spanToLspRange span
                                })
                            |> Array.ofList

                        return Some locations

                    | _ ->
                        // No identifier at cursor
                        return None

            with _ ->
                return None
    }
