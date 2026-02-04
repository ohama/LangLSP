module LangLSP.Tests.HoverTests

open Expecto
open Ionide.LanguageServerProtocol.Types
open LangLSP.Server.Hover
open LangLSP.Server.DocumentSync

/// Create HoverParams for testing
let makeHoverParams uri line char : HoverParams =
    {
        TextDocument = { Uri = uri }
        Position = { Line = uint32 line; Character = uint32 char }
        WorkDoneToken = None
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

/// Helper to set up document and perform hover
let setupAndHover uri text line char =
    clearAll()  // Clear document storage
    handleDidOpen (makeDidOpenParams uri text)
    handleHover (makeHoverParams uri line char) |> Async.RunSynchronously

/// Extract markup content value from hover
let getHoverValue (hover: Hover option) : string option =
    hover
    |> Option.bind (fun h ->
        match h.Contents with
        | U3.C1 markup -> Some markup.Value
        | _ -> None)

[<Tests>]
let hoverTests =
    testList "Hover" [
        testList "Keyword hover" [
            testCase "hover over 'let' shows Korean explanation" <| fun _ ->
                let hover = setupAndHover "file:///test.fun" "let x = 42 in x" 0 0
                Expect.isSome hover "Should return hover"
                match hover.Value.Contents with
                | U3.C1 markup ->
                    Expect.stringContains markup.Value "let" "Should mention keyword"
                    Expect.stringContains markup.Value "변수" "Should have Korean explanation"
                | _ -> failtest "Expected MarkupContent"

            testCase "hover over 'if' shows Korean explanation" <| fun _ ->
                let hover = setupAndHover "file:///test.fun" "if true then 1 else 0" 0 0
                Expect.isSome hover "Should return hover"
                match hover.Value.Contents with
                | U3.C1 markup ->
                    Expect.stringContains markup.Value "조건" "Should explain conditionals"
                | _ -> failtest "Expected MarkupContent"

            testCase "hover over 'fun' shows Korean explanation" <| fun _ ->
                let hover = setupAndHover "file:///test.fun" "fun x -> x" 0 0
                Expect.isSome hover "Should return hover"
                match hover.Value.Contents with
                | U3.C1 markup ->
                    Expect.stringContains markup.Value "익명" "Should explain anonymous function"
                | _ -> failtest "Expected MarkupContent"

            testCase "hover over 'match' shows Korean explanation" <| fun _ ->
                let hover = setupAndHover "file:///test.fun" "match x with | _ -> 0" 0 0
                Expect.isSome hover "Should return hover"
                match hover.Value.Contents with
                | U3.C1 markup ->
                    Expect.stringContains markup.Value "패턴" "Should explain pattern matching"
                | _ -> failtest "Expected MarkupContent"

            testCase "hover over 'true' shows Korean explanation" <| fun _ ->
                let hover = setupAndHover "file:///test.fun" "true" 0 0
                Expect.isSome hover "Should return hover"
                match hover.Value.Contents with
                | U3.C1 markup ->
                    Expect.stringContains markup.Value "불리언" "Should explain boolean"
                | _ -> failtest "Expected MarkupContent"
        ]

        testList "Type hover" [
            testCase "hover over variable shows inferred int type" <| fun _ ->
                let hover = setupAndHover "file:///test.fun" "let x = 42 in x" 0 14
                Expect.isSome hover "Should return hover for variable"
                match hover.Value.Contents with
                | U3.C1 markup ->
                    Expect.stringContains markup.Value "int" "Should show int type"
                | _ -> failtest "Expected MarkupContent"

            testCase "hover over function shows signature" <| fun _ ->
                let hover = setupAndHover "file:///test.fun" "let f = fun x -> x + 1 in f" 0 26
                Expect.isSome hover "Should return hover for function"
                match hover.Value.Contents with
                | U3.C1 markup ->
                    Expect.stringContains markup.Value "int -> int" "Should show function signature"
                | _ -> failtest "Expected MarkupContent"

            testCase "hover over polymorphic function shows generic type" <| fun _ ->
                let hover = setupAndHover "file:///test.fun" "let id = fun x -> x in id" 0 23
                Expect.isSome hover "Should return hover"
                match hover.Value.Contents with
                | U3.C1 markup ->
                    Expect.stringContains markup.Value "'a" "Should show polymorphic type variable"
                    Expect.stringContains markup.Value "->" "Should show arrow"
                | _ -> failtest "Expected MarkupContent"

            testCase "hover over number literal shows int type" <| fun _ ->
                let hover = setupAndHover "file:///test.fun" "42" 0 0
                Expect.isSome hover "Should return hover for number"
                match hover.Value.Contents with
                | U3.C1 markup ->
                    Expect.stringContains markup.Value "int" "Should show int type"
                | _ -> failtest "Expected MarkupContent"

            testCase "hover over boolean literal shows bool type" <| fun _ ->
                let hover = setupAndHover "file:///test.fun" "let x = false in x" 0 17
                Expect.isSome hover "Should return hover for bool"
                match hover.Value.Contents with
                | U3.C1 markup ->
                    Expect.stringContains markup.Value "bool" "Should show bool type"
                | _ -> failtest "Expected MarkupContent"
        ]

        testList "Edge cases" [
            testCase "hover on parse error returns None" <| fun _ ->
                let hover = setupAndHover "file:///test.fun" "let x =" 0 4
                Expect.isNone hover "Should return None on parse error"

            testCase "hover outside code returns None" <| fun _ ->
                let hover = setupAndHover "file:///test.fun" "42" 5 0
                Expect.isNone hover "Should return None for out-of-bounds"

            testCase "hover on empty document returns None" <| fun _ ->
                let hover = setupAndHover "file:///test.fun" "" 0 0
                Expect.isNone hover "Should return None for empty document"

            testCase "hover returns markdown format" <| fun _ ->
                let hover = setupAndHover "file:///test.fun" "let x = 42 in x" 0 14
                Expect.isSome hover "Should return hover"
                match hover.Value.Contents with
                | U3.C1 markup ->
                    Expect.equal markup.Kind MarkupKind.Markdown "Should use Markdown kind"
                | _ -> failtest "Expected MarkupContent"

            testCase "hover on unknown document returns None" <| fun _ ->
                clearAll()
                let hover = handleHover (makeHoverParams "file:///unknown.fun" 0 0) |> Async.RunSynchronously
                Expect.isNone hover "Should return None for unknown document"
        ]
    ] |> testSequenced  // Run tests sequentially due to shared state
