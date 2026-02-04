module LangLSP.Tests.RenameTests

open Expecto
open Ionide.LanguageServerProtocol.Types
open LangLSP.Server.Rename
open LangLSP.Server.DocumentSync

/// Create RenameParams for testing
let makeRenameParams uri line char newName : RenameParams =
    {
        TextDocument = { Uri = uri }
        Position = { Line = uint32 line; Character = uint32 char }
        NewName = newName
        WorkDoneToken = None
    }

/// Create TextDocumentPositionParams for prepareRename
let makePrepareRenameParams uri line char : TextDocumentPositionParams =
    {
        TextDocument = { Uri = uri }
        Position = { Line = uint32 line; Character = uint32 char }
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

/// Helper: setup document and perform rename
let setupAndRename uri text line char newName =
    clearAll()
    handleDidOpen (makeDidOpenParams uri text)
    handleRename (makeRenameParams uri line char newName) |> Async.RunSynchronously

/// Helper: setup document and prepare rename
let setupAndPrepareRename uri text line char =
    clearAll()
    handleDidOpen (makeDidOpenParams uri text)
    handlePrepareRename (makePrepareRenameParams uri line char) |> Async.RunSynchronously

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
let renameTests =
    testSequenced <| testList "Rename" [
        testList "Variable rename (RENAME-01)" [
            test "renames all variable occurrences" {
                // "let x = 1 in x + x"
                //      ^ position (0, 4) - at definition
                let result = setupAndRename "file:///test.fun" "let x = 1 in x + x" 0 4 "newX"
                Expect.isSome result "Should return WorkspaceEdit"
                // Should rename definition + 2 usages = 3 edits
                let editCount = countEdits result
                Expect.equal editCount 3 "Should have 3 edits (def + 2 usages)"
            }

            test "renames single occurrence" {
                // "let a = 42 in a"
                //      ^ position (0, 4)
                let result = setupAndRename "file:///test.fun" "let a = 42 in a" 0 4 "newA"
                Expect.isSome result "Should return WorkspaceEdit"
                // Should rename definition + 1 usage = 2 edits
                let editCount = countEdits result
                Expect.equal editCount 2 "Should have 2 edits (def + usage)"
            }

            test "renames from usage position" {
                // "let x = 1 in x + x"
                //              ^ position (0, 13) - at usage
                let result = setupAndRename "file:///test.fun" "let x = 1 in x + x" 0 13 "y"
                Expect.isSome result "Should return WorkspaceEdit"
                let editCount = countEdits result
                Expect.equal editCount 3 "Should rename all occurrences from usage position"
            }
        ]

        testList "Function rename (RENAME-02)" [
            test "renames function and all calls" {
                // "let f = fun x -> x in f 1"
                //      ^ position (0, 4) - function definition
                let result = setupAndRename "file:///test.fun" "let f = fun x -> x in f 1" 0 4 "g"
                Expect.isSome result "Should return WorkspaceEdit"
                // Should rename function name + call site
                let editCount = countEdits result
                Expect.isGreaterThanOrEqual editCount 2 "Should rename function and calls"
            }

            test "renames recursive function" {
                // "let rec fact n = if n = 0 then 1 else n * fact (n - 1) in fact 5"
                //         ^ position around (0, 8) - function name
                let code = "let rec fact n = if n = 0 then 1 else n * fact (n - 1) in fact 5"
                let result = setupAndRename "file:///test.fun" code 0 8 "factorial"
                Expect.isSome result "Should return WorkspaceEdit"
                // Should rename function name + recursive call + final call
                let editCount = countEdits result
                Expect.isGreaterThanOrEqual editCount 2 "Should rename all function occurrences"
            }
        ]

        testList "Prepare rename (RENAME-03)" [
            test "validates rename on variable" {
                // "let x = 42 in x"
                //      ^ position (0, 4)
                let result = setupAndPrepareRename "file:///test.fun" "let x = 42 in x" 0 4
                Expect.isSome result "Should allow rename on variable"
                // Check that it returns a range with placeholder
                match result with
                | Some (U3.C2 rangeWithPlaceholder) ->
                    Expect.equal rangeWithPlaceholder.Placeholder "x" "Placeholder should be variable name"
                | _ -> failtest "Expected RangeWithPlaceholder"
            }

            test "validates rename on function" {
                // "let f = fun x -> x in f 1"
                //      ^ position (0, 4)
                let result = setupAndPrepareRename "file:///test.fun" "let f = fun x -> x in f 1" 0 4
                Expect.isSome result "Should allow rename on function"
            }

            test "rejects rename on number literal" {
                // "42" - cursor on number
                let result = setupAndPrepareRename "file:///test.fun" "42" 0 0
                Expect.isNone result "Should reject rename on number literal"
            }

            test "validates rename on lambda parameter" {
                // "fun x -> x"
                //     ^ position (0, 4) - parameter
                let result = setupAndPrepareRename "file:///test.fun" "fun x -> x" 0 4
                Expect.isSome result "Should allow rename on lambda parameter"
            }
        ]

        testList "Edge cases" [
            test "returns None for missing document" {
                clearAll()
                let result = handleRename (makeRenameParams "file:///missing.fun" 0 0 "newName") |> Async.RunSynchronously
                Expect.isNone result "Missing document should return None"
            }

            test "returns None for parse error" {
                let text = "let x ="  // Incomplete/invalid
                let result = setupAndRename "file:///test.fun" text 0 0 "newName"
                Expect.isNone result "Parse error should return None"
            }

            test "handles shadowing correctly" {
                // "let x = 1 in let x = 2 in x"
                //                   ^ position (0, 17) - inner x definition
                let result = setupAndRename "file:///test.fun" "let x = 1 in let x = 2 in x" 0 17 "y"
                Expect.isSome result "Should handle shadowed variable"
                // Should only rename inner x occurrences
                let editCount = countEdits result
                Expect.equal editCount 2 "Should rename inner x (def + usage)"
            }
        ]
    ]
