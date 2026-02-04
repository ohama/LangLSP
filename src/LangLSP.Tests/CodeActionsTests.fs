module LangLSP.Tests.CodeActionsTests

open Expecto
open Ionide.LanguageServerProtocol.Types
open LangLSP.Server.CodeActions
open LangLSP.Server.Diagnostics
open LangLSP.Server.DocumentSync

/// Create CodeActionParams for testing
let makeCodeActionParams uri line char diagnostics : CodeActionParams =
    {
        TextDocument = { Uri = uri }
        Range = {
            Start = { Line = uint32 line; Character = uint32 char }
            End = { Line = uint32 line; Character = uint32 char }
        }
        Context = {
            Diagnostics = diagnostics
            Only = None
            TriggerKind = None
        }
        WorkDoneToken = None
        PartialResultToken = None
    }

/// Create DidOpenTextDocumentParams for testing
let makeDidOpenParams uri text : DidOpenTextDocumentParams =
    {
        TextDocument = {
            Uri = uri
            LanguageId = "funlang"
            Version = 1
            Text = text
        }
    }

/// Helper: setup document and get code actions
let setupAndGetActions uri text line char diagnostics =
    clearAll()
    handleDidOpen (makeDidOpenParams uri text)
    handleCodeAction (makeCodeActionParams uri line char diagnostics) |> Async.RunSynchronously

/// Create unused variable diagnostic for testing
let makeUnusedVarDiagnostic varName range : Diagnostic =
    {
        Range = range
        Severity = Some DiagnosticSeverity.Warning
        Code = Some (U2.C2 "unused-variable")
        CodeDescription = None
        Source = Some "funlang"
        Message = sprintf "Unused variable '%s'" varName
        Tags = Some [| DiagnosticTag.Unnecessary |]
        RelatedInformation = None
        Data = None
    }

/// Create type error diagnostic for testing
let makeTypeErrorDiagnostic message range : Diagnostic =
    {
        Range = range
        Severity = Some DiagnosticSeverity.Error
        Code = None
        CodeDescription = None
        Source = Some "funlang"
        Message = message
        Tags = None
        RelatedInformation = None
        Data = None
    }

[<Tests>]
let codeActionsTests =
    testSequenced <| testList "CodeActions" [
        testList "Unused variable quickfix (ACTION-01)" [
            test "suggests prefix underscore for unused variable" {
                let uri = "file:///test.fun"
                let text = "let x = 1 in 42"
                let range = {
                    Start = { Line = 0u; Character = 4u }
                    End = { Line = 0u; Character = 5u }
                }
                let diag = makeUnusedVarDiagnostic "x" range

                let result = setupAndGetActions uri text 0 4 [| diag |]
                Expect.isSome result "Should return code actions"

                match result with
                | Some actions ->
                    Expect.isGreaterThanOrEqual actions.Length 1 "Should have at least 1 action"
                    let action = actions.[0]
                    Expect.stringContains action.Title "underscore" "Action title should mention underscore"
                | None -> failtest "Expected code actions"
            }

            test "quickfix edits variable name" {
                let uri = "file:///test.fun"
                let text = "let y = 5 in 10"
                let range = {
                    Start = { Line = 0u; Character = 4u }
                    End = { Line = 0u; Character = 5u }
                }
                let diag = makeUnusedVarDiagnostic "y" range

                let result = setupAndGetActions uri text 0 4 [| diag |]

                match result with
                | Some actions when actions.Length > 0 ->
                    let action = actions.[0]
                    Expect.isSome action.Edit "Action should have Edit"

                    match action.Edit with
                    | Some edit ->
                        match edit.Changes with
                        | Some changes ->
                            let edits = changes.[uri]
                            Expect.isGreaterThanOrEqual edits.Length 1 "Should have at least 1 edit"
                            Expect.stringContains edits.[0].NewText "_y" "Should contain '_y'"
                        | None -> failtest "Expected Changes in WorkspaceEdit"
                    | None -> failtest "Expected Edit"
                | _ -> failtest "Expected code actions with edit"
            }

            test "no quickfix for used variable" {
                let uri = "file:///test.fun"
                let text = "let x = 1 in x"
                // No diagnostics - variable is used
                let result = setupAndGetActions uri text 0 4 [| |]
                Expect.isNone result "Should return None when no diagnostics"
            }

            test "action is marked as preferred" {
                let uri = "file:///test.fun"
                let text = "let z = 99 in 0"
                let range = {
                    Start = { Line = 0u; Character = 4u }
                    End = { Line = 0u; Character = 5u }
                }
                let diag = makeUnusedVarDiagnostic "z" range

                let result = setupAndGetActions uri text 0 4 [| diag |]

                match result with
                | Some actions when actions.Length > 0 ->
                    let action = actions.[0]
                    Expect.equal action.IsPreferred (Some true) "Should be marked as preferred"
                | _ -> failtest "Expected code actions"
            }
        ]

        testList "Type error code action (ACTION-02)" [
            test "shows expected type for type mismatch" {
                let uri = "file:///test.fun"
                let text = "if 1 then 2 else 3"  // Type error: condition is int, not bool
                let range = {
                    Start = { Line = 0u; Character = 3u }
                    End = { Line = 0u; Character = 4u }
                }
                let diag = makeTypeErrorDiagnostic "Type mismatch: expected Bool, got Int" range

                let result = setupAndGetActions uri text 0 3 [| diag |]
                Expect.isSome result "Should return code action"

                match result with
                | Some actions ->
                    Expect.isGreaterThanOrEqual actions.Length 1 "Should have at least 1 action"
                    let action = actions.[0]
                    Expect.stringContains action.Title "expected" "Action should show expected type"
                | None -> failtest "Expected code actions"
            }

            test "type error action has no edit" {
                let uri = "file:///test.fun"
                let text = "1 + true"  // Type error
                let range = {
                    Start = { Line = 0u; Character = 4u }
                    End = { Line = 0u; Character = 8u }
                }
                let diag = makeTypeErrorDiagnostic "Type mismatch: expected Int, got Bool" range

                let result = setupAndGetActions uri text 0 4 [| diag |]

                match result with
                | Some actions when actions.Length > 0 ->
                    let action = actions.[0]
                    Expect.isNone action.Edit "Type error action should have no Edit (informational)"
                | _ -> failtest "Expected code actions"
            }

            test "no action for non-error diagnostics without code" {
                let uri = "file:///test.fun"
                let text = "let x = 1 in x"
                // No diagnostics
                let result = setupAndGetActions uri text 0 0 [| |]
                Expect.isNone result "Should return None when no diagnostics"
            }
        ]

        testList "Unused variable detection" [
            test "detects unused let binding" {
                let uri = "file:///test.fun"
                let text = "let unused = 42 in 99"

                let diagnostics = analyze uri text
                let unusedDiags = diagnostics |> List.filter (fun d ->
                    match d.Code with
                    | Some (U2.C2 "unused-variable") -> true
                    | _ -> false)

                Expect.isNonEmpty unusedDiags "Should detect unused variable"
            }

            test "does not flag used variable" {
                let uri = "file:///test.fun"
                let text = "let used = 42 in used"

                let diagnostics = analyze uri text
                let unusedDiags = diagnostics |> List.filter (fun d ->
                    match d.Code with
                    | Some (U2.C2 "unused-variable") -> true
                    | _ -> false)

                Expect.isEmpty unusedDiags "Should not flag used variable"
            }

            test "does not flag underscore-prefixed" {
                let uri = "file:///test.fun"
                let text = "let _unused = 42 in 99"

                let diagnostics = analyze uri text
                let unusedDiags = diagnostics |> List.filter (fun d ->
                    match d.Code with
                    | Some (U2.C2 "unused-variable") -> true
                    | _ -> false)

                Expect.isEmpty unusedDiags "Should not flag underscore-prefixed variable"
            }

            test "unused warnings have Warning severity" {
                let uri = "file:///test.fun"
                let text = "let x = 1 in 42"

                let diagnostics = analyze uri text
                let unusedDiags = diagnostics |> List.filter (fun d ->
                    match d.Code with
                    | Some (U2.C2 "unused-variable") -> true
                    | _ -> false)

                Expect.isNonEmpty unusedDiags "Should have unused diagnostic"
                let diag = unusedDiags.[0]
                Expect.equal diag.Severity (Some DiagnosticSeverity.Warning) "Should be Warning severity"
            }

            test "unused warnings have Unnecessary tag" {
                let uri = "file:///test.fun"
                let text = "let x = 1 in 42"

                let diagnostics = analyze uri text
                let unusedDiags = diagnostics |> List.filter (fun d ->
                    match d.Code with
                    | Some (U2.C2 "unused-variable") -> true
                    | _ -> false)

                Expect.isNonEmpty unusedDiags "Should have unused diagnostic"
                let diag = unusedDiags.[0]
                Expect.isSome diag.Tags "Should have Tags"
                Expect.contains (diag.Tags.Value |> Array.toList) DiagnosticTag.Unnecessary "Should have Unnecessary tag"
            }
        ]

        testList "Edge cases" [
            test "no actions when no diagnostics" {
                let uri = "file:///test.fun"
                let text = "42"
                let result = setupAndGetActions uri text 0 0 [| |]
                Expect.isNone result "Should return None with no diagnostics"
            }

            test "handles multiple diagnostics" {
                let uri = "file:///test.fun"
                let text = "let x = 1 in let y = 2 in 99"

                let range1 = { Start = { Line = 0u; Character = 4u }; End = { Line = 0u; Character = 5u } }
                let range2 = { Start = { Line = 0u; Character = 17u }; End = { Line = 0u; Character = 18u } }

                let diag1 = makeUnusedVarDiagnostic "x" range1
                let diag2 = makeUnusedVarDiagnostic "y" range2

                let result = setupAndGetActions uri text 0 4 [| diag1; diag2 |]
                Expect.isSome result "Should return actions for multiple diagnostics"
            }
        ]
    ]
