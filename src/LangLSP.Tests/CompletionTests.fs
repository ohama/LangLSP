module LangLSP.Tests.CompletionTests

open Expecto
open Ionide.LanguageServerProtocol.Types
open LangLSP.Server.Completion
open LangLSP.Server.DocumentSync

/// Create CompletionParams for testing
let makeCompletionParams uri line char : CompletionParams =
    {
        TextDocument = { Uri = uri }
        Position = { Line = uint32 line; Character = uint32 char }
        Context = None
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

/// Helper to set up document and perform completion
let setupAndComplete uri text line char =
    clearAll()
    handleDidOpen (makeDidOpenParams uri text)
    handleCompletion (makeCompletionParams uri line char) |> Async.RunSynchronously

/// Extract completion labels from result
let getCompletionLabels (result: CompletionList option) : string list =
    result
    |> Option.map (fun list -> list.Items |> Array.map (fun item -> item.Label) |> Array.toList)
    |> Option.defaultValue []

/// Get completion item by label
let findCompletionItem (label: string) (result: CompletionList option) : CompletionItem option =
    result
    |> Option.bind (fun list ->
        list.Items |> Array.tryFind (fun item -> item.Label = label))

[<Tests>]
let completionTests =
    testSequenced (  // Use testSequenced due to shared document state
        testList "Completion" [
            testList "Keyword completion" [
                testCase "includes 'let' keyword" <| fun _ ->
                    let result = setupAndComplete "file:///test.fun" "l" 0 1
                    let labels = getCompletionLabels result
                    Expect.contains labels "let" "Should include 'let' keyword"

                testCase "includes 'if' keyword" <| fun _ ->
                    let result = setupAndComplete "file:///test.fun" "i" 0 1
                    let labels = getCompletionLabels result
                    Expect.contains labels "if" "Should include 'if' keyword"

                testCase "includes 'match' keyword" <| fun _ ->
                    let result = setupAndComplete "file:///test.fun" "m" 0 1
                    let labels = getCompletionLabels result
                    Expect.contains labels "match" "Should include 'match' keyword"

                testCase "includes 'fun' keyword" <| fun _ ->
                    let result = setupAndComplete "file:///test.fun" "f" 0 1
                    let labels = getCompletionLabels result
                    Expect.contains labels "fun" "Should include 'fun' keyword"

                testCase "includes all FunLang keywords" <| fun _ ->
                    let result = setupAndComplete "file:///test.fun" "" 0 0
                    let labels = getCompletionLabels result
                    for kw in funlangKeywords do
                        Expect.contains labels kw (sprintf "Should include '%s' keyword" kw)

                testCase "keyword items have correct kind" <| fun _ ->
                    let result = setupAndComplete "file:///test.fun" "l" 0 1
                    let letItem = findCompletionItem "let" result
                    Expect.isSome letItem "Should find 'let' item"
                    Expect.equal letItem.Value.Kind (Some CompletionItemKind.Keyword) "Kind should be Keyword"
            ]

            testList "Symbol completion" [
                testCase "includes defined variable" <| fun _ ->
                    let text = "let x = 42 in x"
                    let result = setupAndComplete "file:///test.fun" text 0 14
                    let labels = getCompletionLabels result
                    Expect.contains labels "x" "Should include variable 'x'"

                testCase "includes multiple defined variables" <| fun _ ->
                    let text = "let a = 1 in let b = 2 in a + b"
                    let result = setupAndComplete "file:///test.fun" text 0 28
                    let labels = getCompletionLabels result
                    Expect.contains labels "a" "Should include 'a'"
                    Expect.contains labels "b" "Should include 'b'"

                testCase "includes recursive function name" <| fun _ ->
                    let text = "let rec fact n = if n = 0 then 1 else n * fact (n - 1) in f"
                    let result = setupAndComplete "file:///test.fun" text 0 58
                    let labels = getCompletionLabels result
                    Expect.contains labels "fact" "Should include recursive function 'fact'"

                testCase "includes lambda parameter" <| fun _ ->
                    let text = "fun x -> x"
                    let result = setupAndComplete "file:///test.fun" text 0 9
                    let labels = getCompletionLabels result
                    Expect.contains labels "x" "Should include lambda param 'x'"

                testCase "symbol items have correct kind" <| fun _ ->
                    let text = "let x = 42 in x"
                    let result = setupAndComplete "file:///test.fun" text 0 14
                    let xItem = findCompletionItem "x" result
                    Expect.isSome xItem "Should find 'x' item"
                    Expect.equal xItem.Value.Kind (Some CompletionItemKind.Variable) "Kind should be Variable"
            ]

            testList "Scope filtering" [
                testCase "does not include variable defined after cursor" <| fun _ ->
                    // Cursor is before 'y' is defined
                    let text = "let x = 1 in let y = 2 in x + y"
                    let result = setupAndComplete "file:///test.fun" text 0 13
                    let labels = getCompletionLabels result
                    Expect.contains labels "x" "Should include 'x' (defined before)"
                    Expect.isFalse (List.contains "y" labels) "Should NOT include 'y' (defined after)"

                testCase "respects shadowing with same-line definitions" <| fun _ ->
                    let text = "let x = 1 in x"
                    let result = setupAndComplete "file:///test.fun" text 0 13
                    let labels = getCompletionLabels result
                    Expect.contains labels "x" "Should include shadowed variable"
            ]

            testList "Type annotations" [
                testCase "int variable shows type in detail" <| fun _ ->
                    let text = "let x = 42 in x"
                    let result = setupAndComplete "file:///test.fun" text 0 14
                    let xItem = findCompletionItem "x" result
                    Expect.isSome xItem "Should find 'x'"
                    Expect.isSome xItem.Value.Detail "Should have Detail"
                    Expect.stringContains xItem.Value.Detail.Value "int" "Detail should contain 'int'"

                testCase "bool variable shows type in detail" <| fun _ ->
                    let text = "let flag = true in f"
                    let result = setupAndComplete "file:///test.fun" text 0 19
                    let flagItem = findCompletionItem "flag" result
                    Expect.isSome flagItem "Should find 'flag'"
                    Expect.isSome flagItem.Value.Detail "Should have Detail"
                    Expect.stringContains flagItem.Value.Detail.Value "bool" "Detail should contain 'bool'"

                testCase "function shows arrow type in detail" <| fun _ ->
                    let text = "let double = fun x -> x * 2 in d"
                    let result = setupAndComplete "file:///test.fun" text 0 31
                    let doubleItem = findCompletionItem "double" result
                    Expect.isSome doubleItem "Should find 'double'"
                    Expect.isSome doubleItem.Value.Detail "Should have Detail"
                    Expect.stringContains doubleItem.Value.Detail.Value "->" "Detail should contain '->'"
            ]

            testList "Edge cases" [
                testCase "returns keywords on parse error" <| fun _ ->
                    let text = "let x ="  // Incomplete expression
                    let result = setupAndComplete "file:///test.fun" text 0 7
                    Expect.isSome result "Should return result even with parse error"
                    let labels = getCompletionLabels result
                    Expect.contains labels "let" "Should include keywords"

                testCase "returns empty when document not found" <| fun _ ->
                    clearAll()
                    let result = handleCompletion (makeCompletionParams "file:///missing.fun" 0 0)
                                 |> Async.RunSynchronously
                    Expect.isNone result "Should return None for missing document"
            ]
        ]
    )
