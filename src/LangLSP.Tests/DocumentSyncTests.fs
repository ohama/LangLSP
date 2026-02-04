module LangLSP.Tests.DocumentSyncTests

open Expecto
open LangLSP.Server.DocumentSync
open Ionide.LanguageServerProtocol.Types

let makeDidOpenParams uri text : DidOpenTextDocumentParams =
    {
        TextDocument = {
            Uri = uri
            LanguageId = "funlang"
            Version = 1
            Text = text
        }
    }

let makeDidCloseParams uri : DidCloseTextDocumentParams =
    { TextDocument = { Uri = uri } }

let makeDidChangeParams uri version changes : DidChangeTextDocumentParams =
    {
        TextDocument = { Uri = uri; Version = version }
        ContentChanges = changes
    }

[<Tests>]
let documentSyncTests =
    testSequenced <| testList "DocumentSync" [

        testCase "didOpen stores document text" <| fun _ ->
            clearAll()
            let uri = "file:///test.fun"
            let text = "let x = 1"
            handleDidOpen (makeDidOpenParams uri text)
            Expect.equal (getDocument uri) (Some text) "Document should be stored"

        testCase "didClose removes document" <| fun _ ->
            clearAll()
            let uri = "file:///test.fun"
            handleDidOpen (makeDidOpenParams uri "let x = 1")
            handleDidClose (makeDidCloseParams uri)
            Expect.equal (getDocument uri) None "Document should be removed"

        testCase "didChange with full sync replaces text" <| fun _ ->
            clearAll()
            let uri = "file:///test.fun"
            handleDidOpen (makeDidOpenParams uri "let x = 1")
            let change = U2.C2 { Text = "let y = 2" }
            handleDidChange (makeDidChangeParams uri 2 [| change |])
            Expect.equal (getDocument uri) (Some "let y = 2") "Text should be replaced"

        testCase "didChange with incremental sync modifies range" <| fun _ ->
            clearAll()
            let uri = "file:///test.fun"
            handleDidOpen (makeDidOpenParams uri "let x = 1")
            // Change 'x' to 'y' (position 4, length 1)
            let range = { Start = { Line = 0u; Character = 4u }; End = { Line = 0u; Character = 5u } }
            let change = U2.C1 { Range = range; RangeLength = None; Text = "y" }
            handleDidChange (makeDidChangeParams uri 2 [| change |])
            Expect.equal (getDocument uri) (Some "let y = 1") "Variable name should be changed"

        testCase "getDocument returns None for unknown URI" <| fun _ ->
            clearAll()
            Expect.equal (getDocument "file:///unknown.fun") None "Unknown document should return None"
    ]
