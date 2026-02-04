module LangLSP.Server.DocumentSync

open System.Collections.Concurrent
open Ionide.LanguageServerProtocol.Types

/// Thread-safe document storage (URI -> text content)
let private documents = ConcurrentDictionary<string, string>()

/// Get document text by URI (for diagnostics, hover, etc.)
let getDocument (uri: string) : string option =
    match documents.TryGetValue(uri) with
    | true, text -> Some text
    | false, _ -> None

/// Handle textDocument/didOpen
let handleDidOpen (p: DidOpenTextDocumentParams) : unit =
    let uri = p.TextDocument.Uri
    let text = p.TextDocument.Text
    documents.[uri] <- text

/// Handle textDocument/didClose
let handleDidClose (p: DidCloseTextDocumentParams) : unit =
    let uri = p.TextDocument.Uri
    documents.TryRemove(uri) |> ignore

/// Convert line/character to string offset
/// Note: LSP positions are 0-based
let private getOffset (lines: string[]) (line: int) (character: int) : int =
    let mutable offset = 0
    for i in 0 .. line - 1 do
        if i < lines.Length then
            offset <- offset + lines.[i].Length + 1  // +1 for newline
    offset + min character (if line < lines.Length then lines.[line].Length else 0)

/// Apply incremental text changes to document
/// LSP changes are applied in order; each change has a Range to replace
let private applyContentChanges (text: string) (changes: TextDocumentContentChangeEvent array) : string =
    changes
    |> Array.fold (fun currentText change ->
        match change with
        | U2.C1 incrementalChange ->
            // Incremental change: replace text at range
            let range = incrementalChange.Range
            let lines = currentText.Split([|'\n'|], System.StringSplitOptions.None)
            let startOffset = getOffset lines (int range.Start.Line) (int range.Start.Character)
            let endOffset = getOffset lines (int range.End.Line) (int range.End.Character)
            currentText.Substring(0, startOffset) + incrementalChange.Text + currentText.Substring(endOffset)
        | U2.C2 fullChange ->
            // Full sync fallback
            fullChange.Text
    ) text

/// Handle textDocument/didChange
let handleDidChange (p: DidChangeTextDocumentParams) : unit =
    let uri = p.TextDocument.Uri
    match documents.TryGetValue(uri) with
    | true, currentText ->
        let newText = applyContentChanges currentText p.ContentChanges
        documents.[uri] <- newText
    | false, _ ->
        // Document not tracked - this shouldn't happen, but handle gracefully
        ()

/// Get all tracked document URIs (for testing)
let getTrackedDocuments () : string seq =
    documents.Keys :> seq<string>

/// Clear all documents (for testing)
let clearAll () : unit =
    documents.Clear()
