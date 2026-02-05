module LangLSP.Tests.IntegrationTests

open Expecto
open Ionide.LanguageServerProtocol.Types
open LangLSP.Server.DocumentSync
open LangLSP.Server.Diagnostics
open LangLSP.Server.Hover
open LangLSP.Server.Completion
open LangLSP.Server.Definition
open LangLSP.Server.References
open LangLSP.Server.Rename
open LangLSP.Server.CodeActions

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

/// Create HoverParams for testing
let makeHoverParams uri line char : HoverParams =
    {
        TextDocument = { Uri = uri }
        Position = { Line = uint32 line; Character = uint32 char }
        WorkDoneToken = None
    }

/// Create CompletionParams for testing
let makeCompletionParams uri line char : CompletionParams =
    {
        TextDocument = { Uri = uri }
        Position = { Line = uint32 line; Character = uint32 char }
        Context = None
        WorkDoneToken = None
        PartialResultToken = None
    }

/// Create DefinitionParams for testing
let makeDefinitionParams uri line char : DefinitionParams =
    {
        TextDocument = { Uri = uri }
        Position = { Line = uint32 line; Character = uint32 char }
        WorkDoneToken = None
        PartialResultToken = None
    }

/// Create ReferenceParams for testing
let makeReferenceParams uri line char includeDecl : ReferenceParams =
    {
        TextDocument = { Uri = uri }
        Position = { Line = uint32 line; Character = uint32 char }
        Context = { IncludeDeclaration = includeDecl }
        WorkDoneToken = None
        PartialResultToken = None
    }

/// Create RenameParams for testing
let makeRenameParams uri line char newName : RenameParams =
    {
        TextDocument = { Uri = uri }
        Position = { Line = uint32 line; Character = uint32 char }
        NewName = newName
        WorkDoneToken = None
    }

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

/// Extract location from Definition result
let extractLocation (def: Definition option) : Location option =
    match def with
    | Some (Definition.C1 loc) -> Some loc
    | Some (Definition.C2 locs) when locs.Length > 0 -> Some locs.[0]
    | _ -> None

/// Extract completion labels from result
let getCompletionLabels (result: CompletionList option) : string list =
    result
    |> Option.map (fun list -> list.Items |> Array.map (fun item -> item.Label) |> Array.toList)
    |> Option.defaultValue []

/// Extract edits count from WorkspaceEdit
let countEdits (workspaceEdit: WorkspaceEdit option) : int =
    match workspaceEdit with
    | Some edit ->
        match edit.Changes with
        | Some changes ->
            changes.Values
            |> Seq.map Array.length
            |> Seq.sum
        | None -> 0
    | None -> 0

[<Tests>]
let tests =
    testSequenced <| testList "Integration" [
        testList "LSP lifecycle" [
            test "full lifecycle - open, analyze, hover, complete, definition, references, rename, close" {
                // Test source covers multiple features
                let testSource = "let add = fun x -> fun y -> x + y in\nlet result = add 1 2 in\nresult"
                let uri = "file:///integration-test.fun"

                // 1. Clear and open document
                clearAll()
                handleDidOpen (makeDidOpenParams uri testSource)

                // 2. Diagnostics - expect no errors (code is valid)
                let diagnostics = analyze uri testSource
                let errors = diagnostics |> List.filter (fun d -> d.Severity = Some DiagnosticSeverity.Error)
                Expect.isEmpty errors "Valid code should have no errors"

                // 3. Hover on 'add' usage in line 1
                // "let result = add 1 2 in"
                //              ^ position (1, 13)
                let hoverResult = handleHover (makeHoverParams uri 1 13) |> Async.RunSynchronously
                Expect.isSome hoverResult "Should get hover on 'add'"
                match hoverResult.Value.Contents with
                | U3.C1 markup ->
                    Expect.stringContains markup.Value "->" "Should show function type"
                | _ -> failtest "Expected MarkupContent"

                // 4. Completion at line 1, column 0 (beginning of line 2)
                let completionResult = handleCompletion (makeCompletionParams uri 1 0) |> Async.RunSynchronously
                Expect.isSome completionResult "Should get completions"
                let labels = getCompletionLabels completionResult
                Expect.contains labels "add" "Should suggest 'add' in scope"

                // 5. Go to Definition on 'add' in line 1
                // "let result = add 1 2 in"
                //              ^ position (1, 13)
                let definitionResult = handleDefinition (makeDefinitionParams uri 1 13) |> Async.RunSynchronously
                Expect.isSome definitionResult "Should find definition of 'add'"
                match extractLocation definitionResult with
                | Some loc ->
                    Expect.equal (int loc.Range.Start.Line) 0 "Should point to line 0 where 'add' is defined"
                | None -> failtest "Expected location"

                // 6. Find References on 'add' from line 0 definition, excludeDecl
                let referencesResult = handleReferences (makeReferenceParams uri 0 4 false) |> Async.RunSynchronously
                Expect.isSome referencesResult "Should find references to 'add'"
                match referencesResult with
                | Some locations ->
                    Expect.isGreaterThanOrEqual locations.Length 1 "Should find at least 1 usage of 'add'"
                | None -> failtest "Expected locations"

                // 7. Rename 'result' to 'answer' at line 2
                // "result"
                // ^ position (2, 0)
                let renameResult = handleRename (makeRenameParams uri 2 0 "answer") |> Async.RunSynchronously
                Expect.isSome renameResult "Should return rename edits"
                let editCount = countEdits renameResult
                Expect.isGreaterThanOrEqual editCount 2 "Should rename definition and usage(s)"

                // 8. Close document
                let closeParams = { TextDocument = { Uri = uri } }
                handleDidClose closeParams

                // 9. Verify document is closed
                let doc = getDocument uri
                Expect.isNone doc "Document should be removed after close"
            }

            test "lifecycle with parse error - graceful degradation" {
                let invalidSource = "let x ="  // Incomplete
                let uri = "file:///parse-error-test.fun"

                // 1. Clear and open
                clearAll()
                handleDidOpen (makeDidOpenParams uri invalidSource)

                // 2. Diagnostics - expect at least one error
                let diagnostics = analyze uri invalidSource
                let errors = diagnostics |> List.filter (fun d -> d.Severity = Some DiagnosticSeverity.Error)
                Expect.isNonEmpty errors "Parse error should produce error diagnostic"

                // 3. Hover at (0, 4) on 'x' - expect None (parse failed)
                let hoverResult = handleHover (makeHoverParams uri 0 4) |> Async.RunSynchronously
                Expect.isNone hoverResult "Hover should return None on parse error"

                // 4. Completion at (0, 0) - expect keywords (graceful degradation)
                let completionResult = handleCompletion (makeCompletionParams uri 0 0) |> Async.RunSynchronously
                Expect.isSome completionResult "Should return keywords even with parse error"
                let labels = getCompletionLabels completionResult
                Expect.contains labels "let" "Should include keyword completions"

                // 5. Definition at (0, 4) - expect None
                let definitionResult = handleDefinition (makeDefinitionParams uri 0 4) |> Async.RunSynchronously
                Expect.isNone definitionResult "Definition should return None on parse error"

                // 6. References at (0, 4) - expect None
                let referencesResult = handleReferences (makeReferenceParams uri 0 4 false) |> Async.RunSynchronously
                Expect.isNone referencesResult "References should return None on parse error"

                // 7. Close document
                let closeParams = { TextDocument = { Uri = uri } }
                handleDidClose closeParams
            }

            test "lifecycle with type error - diagnostics and code actions" {
                let typeErrorSource = "let x = 1 + true in x"  // Type error
                let uri = "file:///type-error-test.fun"

                // 1. Clear and open
                clearAll()
                handleDidOpen (makeDidOpenParams uri typeErrorSource)

                // 2. Diagnostics - expect type error
                let diagnostics = analyze uri typeErrorSource
                let typeErrors = diagnostics |> List.filter (fun d ->
                    d.Severity = Some DiagnosticSeverity.Error &&
                    d.Message.Contains("Type") || d.Message.Contains("type"))
                Expect.isNonEmpty typeErrors "Type error should be detected"

                // 3. Hover on 'x' in 'in x' - may work or return None, just verify no crash
                // "let x = 1 + true in x"
                //                     ^ position (0, 20)
                let hoverResult = handleHover (makeHoverParams uri 0 20) |> Async.RunSynchronously
                // Either Some or None is acceptable, just checking no exception
                Expect.isTrue true "Hover should not crash on type error"

                // 4. Code Actions with type error diagnostic
                let typeErrorDiag = typeErrors.[0]
                let codeActionResult = handleCodeAction (makeCodeActionParams uri 0 8 [| typeErrorDiag |]) |> Async.RunSynchronously
                Expect.isSome codeActionResult "Should return code action for type error"
                match codeActionResult with
                | Some actions ->
                    Expect.isGreaterThanOrEqual actions.Length 1 "Should have at least one code action"
                | None -> failtest "Expected code actions"

                // 5. Close document
                let closeParams = { TextDocument = { Uri = uri } }
                handleDidClose closeParams
            }
        ]
    ]
