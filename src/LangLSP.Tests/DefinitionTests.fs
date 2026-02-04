module LangLSP.Tests.DefinitionTests

open Expecto
open Ionide.LanguageServerProtocol.Types
open LangLSP.Server.Definition
open LangLSP.Server.DocumentSync

/// Create LSP Position (0-based)
let makePos line char : Position =
    { Line = uint32 line; Character = uint32 char }

/// Create DefinitionParams for testing
let makeDefinitionParams uri line char : DefinitionParams =
    {
        TextDocument = { Uri = uri }
        Position = makePos line char
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

/// Helper: setup document and get definition
let setupAndDefinition uri text line char =
    clearAll()
    handleDidOpen (makeDidOpenParams uri text)
    handleDefinition (makeDefinitionParams uri line char) |> Async.RunSynchronously

/// Extract location from Definition result
let extractLocation (def: Definition option) : Location option =
    match def with
    | Some (Definition.C1 loc) -> Some loc
    | Some (Definition.C2 locs) when locs.Length > 0 -> Some locs.[0]
    | _ -> None

[<Tests>]
let definitionTests =
    testSequenced <| testList "Definition" [
        testList "Variable definition (GOTO-01)" [
            test "finds let binding definition" {
                // "let x = 42 in x"
                //              ^ position (0, 14) - the 'x' usage
                let result = setupAndDefinition "file:///test.fun" "let x = 42 in x" 0 14
                Expect.isSome result "Should find definition"
                match extractLocation result with
                | Some loc ->
                    Expect.equal loc.Uri "file:///test.fun" "Same file"
                    Expect.equal (int loc.Range.Start.Line) 0 "Line 0"
                | None -> failtest "Expected location"
            }

            test "finds lambda parameter definition" {
                // "fun x -> x + 1"
                //          ^ position (0, 9) - the 'x' usage in body
                let result = setupAndDefinition "file:///test.fun" "fun x -> x + 1" 0 9
                Expect.isSome result "Should find definition"
                match extractLocation result with
                | Some loc ->
                    Expect.equal (int loc.Range.Start.Line) 0 "Line 0"
                | None -> failtest "Expected location"
            }

            test "finds nested let binding" {
                // "let x = 1 in let y = x in y"
                //                           ^ position (0, 26) - y usage
                let result = setupAndDefinition "file:///test.fun" "let x = 1 in let y = x in y" 0 26
                Expect.isSome result "Should find y definition"
                match extractLocation result with
                | Some loc ->
                    // y is defined at "let y" which starts around column 13
                    Expect.isGreaterThanOrEqual (int loc.Range.Start.Character) 13 "y definition after inner let"
                | None -> failtest "Expected location"
            }
        ]

        testList "Function definition (GOTO-02)" [
            test "finds function definition at call site" {
                // "let f = fun x -> x + 1 in f 5"
                //                           ^ f call at position (0, 26)
                let result = setupAndDefinition "file:///test.fun" "let f = fun x -> x + 1 in f 5" 0 26
                Expect.isSome result "Should find function definition"
                match extractLocation result with
                | Some loc ->
                    // f is defined at the start "let f"
                    Expect.equal (int loc.Range.Start.Line) 0 "Line 0"
                | None -> failtest "Expected location"
            }

            test "finds recursive function definition" {
                // "let rec fact n = if n = 0 then 1 else n * fact (n - 1) in fact 5"
                //                                             ^ recursive call at position around 43
                let code = "let rec fact n = if n = 0 then 1 else n * fact (n - 1) in fact 5"
                // fact at position 43 is inside "fact (n - 1)"
                let result = setupAndDefinition "file:///test.fun" code 0 43
                Expect.isSome result "Should find recursive function definition"
                match extractLocation result with
                | Some loc ->
                    Expect.equal (int loc.Range.Start.Line) 0 "Line 0"
                | None -> failtest "Expected location"
            }
        ]

        testList "Variable shadowing" [
            test "finds inner binding for shadowed variable" {
                // "let x = 1 in let x = 2 in x"
                //                           ^ should find inner x (position 0, 26)
                let result = setupAndDefinition "file:///test.fun" "let x = 1 in let x = 2 in x" 0 26
                Expect.isSome result "Should find definition"
                match extractLocation result with
                | Some loc ->
                    // Inner let starts around column 13
                    Expect.isGreaterThan (int loc.Range.Start.Character) 10 "Should be inner definition"
                | None -> failtest "Expected location"
            }

            test "finds outer binding for outer usage" {
                // "let x = 1 in x + (let x = 2 in x)"
                //              ^ outer x usage at position (0, 13)
                let result = setupAndDefinition "file:///test.fun" "let x = 1 in x + (let x = 2 in x)" 0 13
                Expect.isSome result "Should find outer definition"
                match extractLocation result with
                | Some loc ->
                    // Outer let starts at column 0
                    Expect.equal (int loc.Range.Start.Character) 0 "Should be outer definition"
                | None -> failtest "Expected location"
            }
        ]

        testList "Edge cases" [
            test "returns None for number literal" {
                let result = setupAndDefinition "file:///test.fun" "42" 0 0
                Expect.isNone result "Number has no definition"
            }

            test "returns None for position outside code" {
                let result = setupAndDefinition "file:///test.fun" "let x = 1 in x" 5 0
                Expect.isNone result "Position outside code"
            }

            test "returns None for undocumented URI" {
                clearAll()
                let result = handleDefinition (makeDefinitionParams "file:///unknown.fun" 0 0) |> Async.RunSynchronously
                Expect.isNone result "Unknown document"
            }

            test "returns same-file URI (GOTO-03)" {
                let result = setupAndDefinition "file:///test.fun" "let x = 42 in x" 0 14
                Expect.isSome result "Should find definition"
                match extractLocation result with
                | Some loc ->
                    Expect.equal loc.Uri "file:///test.fun" "Definition in same file"
                | None -> failtest "Expected location"
            }
        ]

        testList "collectDefinitions" [
            test "collects Let bindings" {
                let code = "let x = 1 in x"
                let lexbuf = FSharp.Text.Lexing.LexBuffer<char>.FromString(code)
                let ast = Parser.start Lexer.tokenize lexbuf
                let defs = collectDefinitions ast
                Expect.contains (defs |> List.map fst) "x" "Should collect x"
            }

            test "collects LetRec name and param" {
                let code = "let rec f n = n in f 1"
                let lexbuf = FSharp.Text.Lexing.LexBuffer<char>.FromString(code)
                let ast = Parser.start Lexer.tokenize lexbuf
                let defs = collectDefinitions ast
                let names = defs |> List.map fst
                Expect.contains names "f" "Should collect function name f"
                Expect.contains names "n" "Should collect param n"
            }

            test "collects Lambda param" {
                let code = "fun x -> x + 1"
                let lexbuf = FSharp.Text.Lexing.LexBuffer<char>.FromString(code)
                let ast = Parser.start Lexer.tokenize lexbuf
                let defs = collectDefinitions ast
                Expect.contains (defs |> List.map fst) "x" "Should collect lambda param x"
            }
        ]
    ]
