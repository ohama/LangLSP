module LangLSP.Tests.ReferencesTests

open Expecto
open Ionide.LanguageServerProtocol.Types
open LangLSP.Server.References
open LangLSP.Server.DocumentSync

/// Create ReferenceParams for testing
let makeReferenceParams uri line char includeDecl : ReferenceParams =
    {
        TextDocument = { Uri = uri }
        Position = { Line = uint32 line; Character = uint32 char }
        Context = { IncludeDeclaration = includeDecl }
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

/// Helper: setup document and find references
let setupAndFindReferences uri text line char includeDecl =
    clearAll()
    handleDidOpen (makeDidOpenParams uri text)
    handleReferences (makeReferenceParams uri line char includeDecl) |> Async.RunSynchronously

[<Tests>]
let referencesTests =
    testSequenced <| testList "References" [
        testList "Variable references (REF-01)" [
            test "finds all variable usages" {
                // "let x = 1 in x + x"
                //              ^ position (0, 13) - first 'x' usage
                let result = setupAndFindReferences "file:///test.fun" "let x = 1 in x + x" 0 13 false
                Expect.isSome result "Should find references"
                match result with
                | Some locations ->
                    // Should find 2 usages (not the declaration)
                    Expect.equal locations.Length 2 "Should find 2 usages"
                | None -> failtest "Expected locations"
            }

            test "finds single usage" {
                // "let y = 42 in y"
                //               ^ position (0, 14)
                let result = setupAndFindReferences "file:///test.fun" "let y = 42 in y" 0 14 false
                Expect.isSome result "Should find references"
                match result with
                | Some locations ->
                    Expect.equal locations.Length 1 "Should find 1 usage"
                | None -> failtest "Expected locations"
            }

            test "returns None for literal" {
                // "42" - cursor on number literal
                let result = setupAndFindReferences "file:///test.fun" "42" 0 0 false
                Expect.isNone result "Literal has no references"
            }
        ]

        testList "Function references (REF-02)" [
            test "finds function call sites" {
                // "let f = fun x -> x in f 1"
                //                       ^ position (0, 22) - f call
                let result = setupAndFindReferences "file:///test.fun" "let f = fun x -> x in f 1" 0 22 false
                Expect.isSome result "Should find function call"
                match result with
                | Some locations ->
                    // Should find the call site
                    Expect.isGreaterThanOrEqual locations.Length 1 "Should find at least 1 call"
                | None -> failtest "Expected locations"
            }

            test "finds recursive function calls" {
                // "let rec fact n = if n = 0 then 1 else n * fact (n - 1) in fact 5"
                //                                            ^ recursive call around position 43
                let code = "let rec fact n = if n = 0 then 1 else n * fact (n - 1) in fact 5"
                let result = setupAndFindReferences "file:///test.fun" code 0 43 false
                Expect.isSome result "Should find recursive calls"
                match result with
                | Some locations ->
                    // Should find recursive call in body + call in expr
                    Expect.isGreaterThanOrEqual locations.Length 1 "Should find at least 1 call"
                | None -> failtest "Expected locations"
            }
        ]

        testList "Include declaration (REF-03)" [
            test "includes declaration when requested" {
                // "let x = 1 in x + x"
                //              ^ query at usage
                let withDecl = setupAndFindReferences "file:///test.fun" "let x = 1 in x + x" 0 13 true
                let withoutDecl = setupAndFindReferences "file:///test.fun" "let x = 1 in x + x" 0 13 false

                match withDecl, withoutDecl with
                | Some withLocs, Some withoutLocs ->
                    // With declaration should have one more location
                    Expect.isGreaterThan withLocs.Length withoutLocs.Length "Should have more locations with declaration"
                | _ -> failtest "Expected results for both queries"
            }

            test "excludes declaration when not requested" {
                // "let a = 5 in a"
                //              ^ position (0, 13)
                let result = setupAndFindReferences "file:///test.fun" "let a = 5 in a" 0 13 false
                Expect.isSome result "Should find references"
                match result with
                | Some locations ->
                    // Should only find the usage, not the declaration
                    Expect.equal locations.Length 1 "Should exclude declaration"
                | None -> failtest "Expected locations"
            }
        ]

        testList "Variable shadowing" [
            test "handles shadowed variable - inner scope" {
                // "let x = 1 in let x = 2 in x"
                //                           ^ position (0, 26) - inner x usage
                let result = setupAndFindReferences "file:///test.fun" "let x = 1 in let x = 2 in x" 0 26 false
                Expect.isSome result "Should find references for inner x"
                match result with
                | Some locations ->
                    // Should only find references to inner x (not outer x)
                    Expect.equal locations.Length 1 "Should find 1 reference to inner x"
                | None -> failtest "Expected locations"
            }

            test "handles shadowed variable - outer scope" {
                // "let x = 1 in x + (let x = 2 in x)"
                //              ^ position (0, 13) - outer x usage
                let result = setupAndFindReferences "file:///test.fun" "let x = 1 in x + (let x = 2 in x)" 0 13 false
                Expect.isSome result "Should find references for outer x"
                match result with
                | Some locations ->
                    // Should only find the outer x usage (position 13), not inner x usage
                    Expect.equal locations.Length 1 "Should find 1 reference to outer x"
                | None -> failtest "Expected locations"
            }
        ]

        testList "Edge cases" [
            test "returns None for unknown document" {
                clearAll()
                let result = handleReferences (makeReferenceParams "file:///unknown.fun" 0 0 false) |> Async.RunSynchronously
                Expect.isNone result "Unknown document should return None"
            }

            test "returns None for parse error" {
                let text = "let x ="  // Incomplete/invalid
                let result = setupAndFindReferences "file:///test.fun" text 0 0 false
                Expect.isNone result "Parse error should return None"
            }
        ]
    ]
