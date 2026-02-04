module LangLSP.Server.AstLookup

open Ast
open Ionide.LanguageServerProtocol.Types

/// Check if LSP Position (0-based) is within FunLang Span (0-based in practice)
/// Note: FsLexYacc Position.Line is documented as 1-based, but LexBuffer.FromString creates 0-based positions
let positionInSpan (lspPos: Position) (span: Span) : bool =
    // Both LSP and FunLang use 0-based line and column (LexBuffer.FromString default)
    let line = int lspPos.Line
    let col = int lspPos.Character

    // Check if position is within span
    if line < span.StartLine || line > span.EndLine then
        false
    elif line = span.StartLine && line = span.EndLine then
        // Single line span
        col >= span.StartColumn && col <= span.EndColumn
    elif line = span.StartLine then
        // Position is on start line
        col >= span.StartColumn
    elif line = span.EndLine then
        // Position is on end line
        col <= span.EndColumn
    else
        // Position is on a middle line
        true

/// Find the innermost AST node containing the given LSP position
/// Returns None if position is outside the AST
let rec findNodeAtPosition (lspPos: Position) (expr: Expr) : Expr option =
    let span = spanOf expr

    if not (positionInSpan lspPos span) then
        None
    else
        // Position is within this node, check children for more specific match
        let childMatch =
            match expr with
            // Binary operators
            | Add(left, right, _)
            | Subtract(left, right, _)
            | Multiply(left, right, _)
            | Divide(left, right, _)
            | Equal(left, right, _)
            | NotEqual(left, right, _)
            | LessThan(left, right, _)
            | GreaterThan(left, right, _)
            | LessEqual(left, right, _)
            | GreaterEqual(left, right, _)
            | And(left, right, _)
            | Or(left, right, _)
            | App(left, right, _)
            | Cons(left, right, _) ->
                match findNodeAtPosition lspPos left with
                | Some node -> Some node
                | None -> findNodeAtPosition lspPos right

            // Unary operators
            | Negate(inner, _)
            | Annot(inner, _, _) ->
                findNodeAtPosition lspPos inner

            // Let bindings
            | Let(_, bindExpr, bodyExpr, _) ->
                match findNodeAtPosition lspPos bindExpr with
                | Some node -> Some node
                | None -> findNodeAtPosition lspPos bodyExpr

            | LetPat(_, bindExpr, bodyExpr, _) ->
                match findNodeAtPosition lspPos bindExpr with
                | Some node -> Some node
                | None -> findNodeAtPosition lspPos bodyExpr

            | LetRec(_, _, bindExpr, bodyExpr, _) ->
                match findNodeAtPosition lspPos bindExpr with
                | Some node -> Some node
                | None -> findNodeAtPosition lspPos bodyExpr

            // Control flow
            | If(cond, thenExpr, elseExpr, _) ->
                match findNodeAtPosition lspPos cond with
                | Some node -> Some node
                | None ->
                    match findNodeAtPosition lspPos thenExpr with
                    | Some node -> Some node
                    | None -> findNodeAtPosition lspPos elseExpr

            // Functions
            | Lambda(_, body, _) ->
                findNodeAtPosition lspPos body

            | LambdaAnnot(_, _, body, _) ->
                findNodeAtPosition lspPos body

            // Collections
            | Tuple(exprs, _)
            | List(exprs, _) ->
                exprs
                |> List.tryPick (findNodeAtPosition lspPos)

            // Pattern matching
            | Match(scrutinee, clauses, _) ->
                match findNodeAtPosition lspPos scrutinee with
                | Some node -> Some node
                | None ->
                    clauses
                    |> List.tryPick (fun (_, clauseExpr) ->
                        findNodeAtPosition lspPos clauseExpr)

            // Leaf nodes - no children
            | Number _
            | Bool _
            | String _
            | Var _
            | EmptyList _ ->
                None

        // Return child match if found, otherwise return this node
        match childMatch with
        | Some node -> Some node
        | None -> Some expr

/// Extract identifier name if the node is a Var or binding site
let getIdentifierAtNode (expr: Expr) : string option =
    match expr with
    | Var(name, _) -> Some name
    | Let(name, _, _, _) -> Some name
    | Lambda(param, _, _) -> Some param
    | LambdaAnnot(param, _, _, _) -> Some param
    | LetRec(name, _, _, _, _) -> Some name
    | _ -> None
