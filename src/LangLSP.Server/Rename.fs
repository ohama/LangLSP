module LangLSP.Server.Rename

open Ionide.LanguageServerProtocol.Types
open LangLSP.Server.AstLookup
open LangLSP.Server.Definition
open LangLSP.Server.References
open LangLSP.Server.DocumentSync
open LangLSP.Server.Protocol
open Ast

/// Find the exact character range of an identifier name within source text
/// Given a definition span (which may cover whole expression), find just the name
/// Searches first ~15 chars after startCol for the identifier
let findNameInSource (text: string) (name: string) (startLine: int) (startCol: int) : Span option =
    let lines = text.Split('\n')
    if startLine < lines.Length then
        let line = lines.[startLine]
        let searchEnd = min (startCol + 15) line.Length
        let searchArea = line.Substring(startCol, searchEnd - startCol)
        let idx = searchArea.IndexOf(name)
        if idx >= 0 then
            let nameStart = startCol + idx
            Some {
                FileName = ""
                StartLine = startLine
                StartColumn = nameStart
                EndLine = startLine
                EndColumn = nameStart + name.Length - 1
            }
        else None
    else None

/// Handle textDocument/prepareRename request
/// Validates that cursor is on a renameable symbol and returns range + placeholder
let handlePrepareRename (p: TextDocumentPositionParams) : Async<PrepareRenameResult option> =
    async {
        match getDocument p.TextDocument.Uri with
        | None -> return None
        | Some text ->
            try
                let lexbuf = FSharp.Text.Lexing.LexBuffer<char>.FromString(text)
                let ast = Parser.start Lexer.tokenize lexbuf

                match findNodeAtPosition p.Position ast with
                | None -> return None
                | Some node ->
                    let result =
                        match node with
                        | Var(name, span) ->
                            // Variable reference - renameable
                            Some (U3.C2 { Range = spanToLspRange span; Placeholder = name })

                        | Let(name, _, _, span) ->
                            // Let binding - find tight range for name
                            match findNameInSource text name span.StartLine span.StartColumn with
                            | Some nameSpan ->
                                Some (U3.C2 { Range = spanToLspRange nameSpan; Placeholder = name })
                            | None ->
                                // Fallback to start position
                                let fallbackRange = {
                                    Start = { Line = uint32 span.StartLine; Character = uint32 span.StartColumn }
                                    End = { Line = uint32 span.StartLine; Character = uint32 (span.StartColumn + name.Length) }
                                }
                                Some (U3.C2 { Range = fallbackRange; Placeholder = name })

                        | LetRec(name, _, _, _, span) ->
                            // Recursive function - find tight range
                            match findNameInSource text name span.StartLine span.StartColumn with
                            | Some nameSpan ->
                                Some (U3.C2 { Range = spanToLspRange nameSpan; Placeholder = name })
                            | None ->
                                let fallbackRange = {
                                    Start = { Line = uint32 span.StartLine; Character = uint32 span.StartColumn }
                                    End = { Line = uint32 span.StartLine; Character = uint32 (span.StartColumn + name.Length) }
                                }
                                Some (U3.C2 { Range = fallbackRange; Placeholder = name })

                        | Lambda(param, _, span) ->
                            // Lambda parameter - find tight range
                            match findNameInSource text param span.StartLine span.StartColumn with
                            | Some nameSpan ->
                                Some (U3.C2 { Range = spanToLspRange nameSpan; Placeholder = param })
                            | None ->
                                let fallbackRange = {
                                    Start = { Line = uint32 span.StartLine; Character = uint32 span.StartColumn }
                                    End = { Line = uint32 span.StartLine; Character = uint32 (span.StartColumn + param.Length) }
                                }
                                Some (U3.C2 { Range = fallbackRange; Placeholder = param })

                        | LambdaAnnot(param, _, _, span) ->
                            // Annotated lambda parameter - find tight range
                            match findNameInSource text param span.StartLine span.StartColumn with
                            | Some nameSpan ->
                                Some (U3.C2 { Range = spanToLspRange nameSpan; Placeholder = param })
                            | None ->
                                let fallbackRange = {
                                    Start = { Line = uint32 span.StartLine; Character = uint32 span.StartColumn }
                                    End = { Line = uint32 span.StartLine; Character = uint32 (span.StartColumn + param.Length) }
                                }
                                Some (U3.C2 { Range = fallbackRange; Placeholder = param })

                        | _ ->
                            // Not a renameable symbol (numbers, keywords, operators)
                            None

                    return result

            with _ ->
                return None
    }

/// Handle textDocument/rename request
/// Collects all references + definition and returns WorkspaceEdit
let handleRename (p: RenameParams) : Async<WorkspaceEdit option> =
    async {
        match getDocument p.TextDocument.Uri with
        | None -> return None
        | Some text ->
            try
                let lexbuf = FSharp.Text.Lexing.LexBuffer<char>.FromString(text)
                let ast = Parser.start Lexer.tokenize lexbuf

                match findNodeAtPosition p.Position ast with
                | None -> return None
                | Some node ->
                    // Determine variable name and definition span
                    let varNameOpt, defSpanOpt =
                        match node with
                        | Var(name, _) ->
                            // Variable reference - find its definition
                            let def = findDefinitionForVar name ast p.Position
                            (Some name, def)

                        | Let(name, _, _, span) ->
                            // Let binding site
                            (Some name, Some span)

                        | LetRec(name, _, _, _, span) ->
                            // Recursive function binding
                            (Some name, Some span)

                        | Lambda(param, _, span) ->
                            // Lambda parameter
                            (Some param, Some span)

                        | LambdaAnnot(param, _, _, span) ->
                            // Annotated lambda parameter
                            (Some param, Some span)

                        | _ ->
                            // Not a renameable symbol
                            (None, None)

                    match varNameOpt, defSpanOpt with
                    | Some varName, Some defSpan ->
                        // Collect all scoped references
                        let references = collectReferencesForBinding varName defSpan ast

                        // For definition site, get tight name-only span from source
                        let defNameSpan =
                            match findNameInSource text varName defSpan.StartLine defSpan.StartColumn with
                            | Some nameSpan -> nameSpan
                            | None ->
                                // Fallback: create span from definition position
                                {
                                    FileName = ""
                                    StartLine = defSpan.StartLine
                                    StartColumn = defSpan.StartColumn
                                    EndLine = defSpan.StartLine
                                    EndColumn = defSpan.StartColumn + varName.Length - 1
                                }

                        // Combine definition name span + references
                        let allSpans = defNameSpan :: references

                        // Remove duplicates (may have overlapping spans)
                        let distinctSpans =
                            allSpans
                            |> List.distinctBy (fun span ->
                                (span.StartLine, span.StartColumn, span.EndLine, span.EndColumn))

                        // Create TextEdit for each span
                        let edits =
                            distinctSpans
                            |> List.map (fun span -> createTextEdit span p.NewName)
                            |> Array.ofList

                        // Create WorkspaceEdit
                        let workspaceEdit = createWorkspaceEdit p.TextDocument.Uri edits

                        return Some workspaceEdit

                    | _ ->
                        return None

            with _ ->
                return None
    }
