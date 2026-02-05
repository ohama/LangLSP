module LangLSP.Server.CodeActions

open Serilog
open Ionide.LanguageServerProtocol.Types
open LangLSP.Server.Protocol
open LangLSP.Server.DocumentSync

/// Create "Prefix with underscore" quickfix action for unused variables
let createPrefixUnderscoreAction (diagnostic: Diagnostic) (uri: string) : CodeAction =
    // Extract variable name from message "Unused variable 'x'"
    let message = diagnostic.Message
    let varName =
        if message.Contains("'") then
            let startIdx = message.IndexOf("'") + 1
            let endIdx = message.LastIndexOf("'")
            if endIdx > startIdx then
                message.Substring(startIdx, endIdx - startIdx)
            else
                "variable"
        else
            "variable"

    let newName = "_" + varName
    let edit = {
        Range = diagnostic.Range
        NewText = newName
    }

    let workspaceEdit = createWorkspaceEdit uri [| edit |]

    {
        Title = sprintf "Prefix '%s' with underscore" varName
        Kind = Some "quickfix"
        Diagnostics = Some [| diagnostic |]
        Edit = Some workspaceEdit
        Command = None
        IsPreferred = Some true
        Disabled = None
        Data = None
    }

/// Create informational action showing expected type for type errors
let createTypeInfoAction (diagnostic: Diagnostic) : CodeAction =
    // Extract expected type from diagnostic message if present
    // Example: "Type mismatch: expected Int, got Bool"
    let message = diagnostic.Message
    let typeInfo =
        if message.Contains("expected") then
            let startIdx = message.IndexOf("expected")
            message.Substring(startIdx)
        else
            "Type mismatch - see diagnostic message"

    {
        Title = sprintf "Info: %s" typeInfo
        Kind = Some "quickfix"
        Diagnostics = Some [| diagnostic |]
        Edit = None  // Informational only, no automatic fix
        Command = None
        IsPreferred = None
        Disabled = None
        Data = None
    }

/// Handle textDocument/codeAction request
/// Returns code actions (quickfixes) for diagnostics at cursor position
let handleCodeAction (p: CodeActionParams) : Async<CodeAction[] option> =
    async {
        let uri = p.TextDocument.Uri
        let diagnostics = p.Context.Diagnostics

        Log.Information("CodeAction request: {Uri}, diagnostics count: {Count}", uri, diagnostics.Length)
        for diag in diagnostics do
            Log.Information("  Diagnostic: Code={Code}, Severity={Severity}, Message={Message}, Range={Range}",
                diag.Code, diag.Severity, diag.Message, diag.Range)

        if Array.isEmpty diagnostics then
            Log.Information("CodeAction: no diagnostics, returning None")
            return None
        else
            let actions = ResizeArray<CodeAction>()

            for diag in diagnostics do
                // Check diagnostic code/message to determine action type
                match diag.Code with
                | Some (U2.C2 "unused-variable") ->
                    // ACTION-01: Prefix with underscore
                    Log.Information("CodeAction: matched unused-variable, creating prefix action")
                    let action = createPrefixUnderscoreAction diag uri
                    actions.Add(action)

                | _ ->
                    // Check if it's a type error (severity Error)
                    Log.Information("CodeAction: code did not match 'unused-variable', code={Code}", diag.Code)
                    if diag.Severity = Some DiagnosticSeverity.Error then
                        // ACTION-02: Informational type hint
                        let action = createTypeInfoAction diag
                        actions.Add(action)

            Log.Information("CodeAction: returning {Count} actions", actions.Count)
            if actions.Count > 0 then
                return Some (actions.ToArray())
            else
                return None
    }
