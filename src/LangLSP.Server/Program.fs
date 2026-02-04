module LangLSP.Server.Program

open System
open System.IO
open Serilog
open Ionide.LanguageServerProtocol
open Ionide.LanguageServerProtocol.Server
open Ionide.LanguageServerProtocol.Types
open StreamJsonRpc
open LangLSP.Server.DocumentSync
open LangLSP.Server.Diagnostics

/// FunLang LSP Client - handles communication to VS Code
type FunLangLspClient(sendNotification: ClientNotificationSender, sendRequest: ClientRequestSender) =
    inherit LspClient()

    interface ILspClient with
        /// Publish diagnostics to the client
        member _.TextDocumentPublishDiagnostics(p: PublishDiagnosticsParams) =
            sendNotification "textDocument/publishDiagnostics" (box p)
            |> Async.Ignore

/// FunLang LSP Server implementation
type FunLangLspServer(lspClient: FunLangLspClient) =
    inherit LspServer()

    let mutable clientCapabilities: ClientCapabilities option = None

    /// Handle initialize request
    override _.Initialize(p: InitializeParams) = async {
        Log.Information("Initialize request received from client")
        clientCapabilities <- Some p.Capabilities

        let result: InitializeResult = {
            Capabilities = Server.serverCapabilities
            ServerInfo = Some { Name = "funlang-lsp"; Version = Some "0.1.0" }
        }
        Log.Information("Server initialized with capabilities")
        return Ok result
    }

    /// Handle initialized notification
    override _.Initialized(_p: InitializedParams) = async {
        Log.Information("Client initialized notification received")
        return ()
    }

    /// Handle textDocument/didOpen notification
    override _.TextDocumentDidOpen(p: DidOpenTextDocumentParams) = async {
        let uri = p.TextDocument.Uri
        let text = p.TextDocument.Text
        Log.Debug("Document opened: {Uri}", uri)

        // Store document
        handleDidOpen p

        // Analyze and publish diagnostics
        let diagnostics = analyze uri text
        do! publishDiagnostics (lspClient :> ILspClient) uri diagnostics
        Log.Debug("Published {Count} diagnostics for {Uri}", diagnostics.Length, uri)
    }

    /// Handle textDocument/didChange notification
    override _.TextDocumentDidChange(p: DidChangeTextDocumentParams) = async {
        let uri = p.TextDocument.Uri
        Log.Debug("Document changed: {Uri}", uri)

        // Update document
        handleDidChange p

        // Analyze and publish diagnostics
        match getDocument uri with
        | Some text ->
            let diagnostics = analyze uri text
            do! publishDiagnostics (lspClient :> ILspClient) uri diagnostics
            Log.Debug("Published {Count} diagnostics for {Uri}", diagnostics.Length, uri)
        | None ->
            Log.Warning("Document not found after change: {Uri}", uri)
    }

    /// Handle textDocument/didClose notification
    override _.TextDocumentDidClose(p: DidCloseTextDocumentParams) = async {
        let uri = p.TextDocument.Uri
        Log.Debug("Document closed: {Uri}", uri)

        // Clear diagnostics
        do! clearDiagnostics (lspClient :> ILspClient) uri

        // Remove document
        handleDidClose p
    }

    /// Handle shutdown request
    override _.Shutdown() = async {
        Log.Information("Shutdown request received")
        return Ok ()
    }

    /// Handle exit notification
    override _.Exit() = async {
        Log.Information("Exit notification received")
        return ()
    }

    /// Dispose resources
    override _.Dispose() =
        Log.Information("Server disposing")
        ()

[<EntryPoint>]
let main argv =
    // Set up Serilog logging to a file
    Log.Logger <-
        LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(Path.GetTempPath(), "funlang-lsp.log"),
                rollingInterval = RollingInterval.Day,
                outputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
            )
            .CreateLogger()

    try
        Log.Information("FunLang LSP Server starting...")

        // Set up stdin/stdout for LSP communication
        let input = Console.OpenStandardInput()
        let output = Console.OpenStandardOutput()

        Log.Information("Starting LSP server on stdin/stdout")

        // Client creator function
        let clientCreator (notifySender: ClientNotificationSender, requestSender: ClientRequestSender) =
            new FunLangLspClient(notifySender, requestSender)

        // Server creator function
        let serverCreator (client: FunLangLspClient) =
            new FunLangLspServer(client) :> ILspServer

        // Customize RPC handler (use defaults)
        let customizeRpc (handler: IJsonRpcMessageHandler) : JsonRpc =
            new JsonRpc(handler)

        // Start the server using Ionide.LanguageServerProtocol
        let closeReason =
            Server.start
                (Server.defaultRequestHandlings ())
                input
                output
                clientCreator
                serverCreator
                customizeRpc

        Log.Information("LSP Server shutdown: {Reason}", closeReason)
        0
    with ex ->
        Log.Fatal(ex, "LSP Server crashed")
        1
