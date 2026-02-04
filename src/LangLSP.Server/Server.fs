module LangLSP.Server.Server

open Ionide.LanguageServerProtocol.Types
open LangLSP.Server.DocumentSync

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
    }

/// Create the initialize result
let initializeResult : InitializeResult = {
    Capabilities = serverCapabilities
    ServerInfo = Some { Name = "funlang-lsp"; Version = Some "0.1.0" }
}

/// Document synchronization handlers
/// These will be registered when the full LSP message loop is implemented
module Handlers =
    /// Handle textDocument/didOpen notification
    let textDocumentDidOpen = handleDidOpen

    /// Handle textDocument/didChange notification
    let textDocumentDidChange = handleDidChange

    /// Handle textDocument/didClose notification
    let textDocumentDidClose = handleDidClose
