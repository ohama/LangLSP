module LangLSP.Server.Server

open Ionide.LanguageServerProtocol
open Ionide.LanguageServerProtocol.Types
open LangLSP.Server.DocumentSync
open LangLSP.Server.Definition
open LangLSP.Server.Diagnostics

/// Server capabilities declaration
let serverCapabilities : ServerCapabilities =
    { ServerCapabilities.Default with
        TextDocumentSync =
            Some (U2.C1 {
                TextDocumentSyncOptions.Default with
                    OpenClose = Some true
                    Change = Some TextDocumentSyncKind.Incremental
                    Save = Some (U2.C2 { IncludeText = Some false })
            })
        DefinitionProvider = Some (U2.C1 true)
    }

/// Create the initialize result
let initializeResult : InitializeResult = {
    Capabilities = serverCapabilities
    ServerInfo = Some { Name = "funlang-lsp"; Version = Some "0.1.0" }
}

/// Document synchronization handlers with diagnostics
/// These will be registered when the full LSP message loop is implemented
module Handlers =
    /// Handle textDocument/didOpen notification
    /// Stores document and publishes diagnostics
    let textDocumentDidOpen (lspClient: ILspClient) (p: DidOpenTextDocumentParams) : Async<unit> =
        async {
            // Store document
            handleDidOpen p
            // Publish diagnostics
            let uri = p.TextDocument.Uri
            let text = p.TextDocument.Text
            let diagnostics = analyze uri text
            do! publishDiagnostics lspClient uri diagnostics
        }

    /// Handle textDocument/didChange notification
    /// Updates document and publishes diagnostics
    let textDocumentDidChange (lspClient: ILspClient) (p: DidChangeTextDocumentParams) : Async<unit> =
        async {
            // Update document
            handleDidChange p
            // Publish diagnostics on the updated document
            let uri = p.TextDocument.Uri
            match getDocument uri with
            | Some text ->
                let diagnostics = analyze uri text
                do! publishDiagnostics lspClient uri diagnostics
            | None ->
                // Document not tracked - shouldn't happen, but handle gracefully
                ()
        }

    /// Handle textDocument/didClose notification
    /// Clears diagnostics and removes document
    let textDocumentDidClose (lspClient: ILspClient) (p: DidCloseTextDocumentParams) : Async<unit> =
        async {
            let uri = p.TextDocument.Uri
            // Clear diagnostics
            do! clearDiagnostics lspClient uri
            // Remove document
            handleDidClose p
        }

    /// Handle textDocument/definition request
    /// Navigates to definition of variable/function at cursor position
    let textDocumentDefinition (p: DefinitionParams) : Async<Definition option> =
        handleDefinition p
