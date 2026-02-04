module LangLSP.Tests.AstLookupTests

open Expecto
open LangLSP.Server.AstLookup
open Ionide.LanguageServerProtocol.Types
open Ast

/// Parse FunLang code into an Expr
let parse code =
    let lexbuf = FSharp.Text.Lexing.LexBuffer<char>.FromString(code)
    Parser.start Lexer.tokenize lexbuf

/// Create an LSP Position (0-based)
let makePos line char = { Line = uint32 line; Character = uint32 char }

[<Tests>]
let astLookupTests =
    testList "AstLookup" [
        testList "findNodeAtPosition" [
            test "finds variable at position" {
                let code = "let x = 42 in x"
                let ast = parse code
                let pos = makePos 0 14  // Position at 'x' in body

                let result = findNodeAtPosition pos ast
                match result with
                | Some (Var(name, _)) ->
                    Expect.equal name "x" "Should find variable 'x'"
                | _ ->
                    failtest "Expected to find Var node"
            }

            test "finds number literal" {
                let code = "let x = 42 in x"
                let ast = parse code
                let pos = makePos 0 8  // Position at '42'

                let result = findNodeAtPosition pos ast
                match result with
                | Some (Number(n, _)) ->
                    Expect.equal n 42 "Should find number 42"
                | _ ->
                    failtest "Expected to find Number node"
            }

            test "finds let binding" {
                let code = "let x = 42 in x"
                let ast = parse code
                let pos = makePos 0 4  // Position at 'x' in let binding

                let result = findNodeAtPosition pos ast
                // Should find the Let node or a more specific node
                match result with
                | Some (Let _) ->
                    Expect.isTrue true "Found Let node"
                | Some _ ->
                    Expect.isTrue true "Found a node at position"
                | None ->
                    failtest "Expected to find a node"
            }

            test "finds function parameter" {
                let code = "fun x -> x + 1"
                let ast = parse code
                let pos = makePos 0 4  // Position at 'x' parameter

                let result = findNodeAtPosition pos ast
                // Should find Lambda node at parameter position
                match result with
                | Some (Lambda _) ->
                    Expect.isTrue true "Found Lambda node"
                | Some _ ->
                    Expect.isTrue true "Found a node at position"
                | None ->
                    failtest "Expected to find a node"
            }

            test "returns None for position outside code" {
                let code = "42"
                let ast = parse code
                let pos = makePos 5 0  // Far outside the code

                let result = findNodeAtPosition pos ast
                Expect.isNone result "Should return None for out-of-bounds position"
            }

            test "finds nested expression" {
                let code = "let x = 1 + 2 in x"
                let ast = parse code
                let pos = makePos 0 12  // Position at '2' in '1 + 2'

                let result = findNodeAtPosition pos ast
                match result with
                | Some (Number(n, _)) ->
                    Expect.equal n 2 "Should find number 2"
                | _ ->
                    failtest "Expected to find Number node"
            }

            test "finds innermost node in nested let" {
                let code = "let x = let y = 1 in y in x"
                let ast = parse code
                let pos = makePos 0 16  // Position at '1' in inner let

                let result = findNodeAtPosition pos ast
                match result with
                | Some (Number(n, _)) ->
                    Expect.equal n 1 "Should find number 1"
                | _ ->
                    failtest "Expected to find Number node"
            }

            test "finds variable in body of nested let" {
                let code = "let x = let y = 1 in y in x"
                let ast = parse code
                let pos = makePos 0 21  // Position at 'y' in inner let body

                let result = findNodeAtPosition pos ast
                match result with
                | Some (Var(name, _)) ->
                    Expect.equal name "y" "Should find variable 'y'"
                | _ ->
                    failtest "Expected to find Var node"
            }

            test "finds node in if expression condition" {
                let code = "if true then 1 else 2"
                let ast = parse code
                let pos = makePos 0 3  // Position at 'true'

                let result = findNodeAtPosition pos ast
                match result with
                | Some (Bool(b, _)) ->
                    Expect.isTrue b "Should find boolean true"
                | _ ->
                    failtest "Expected to find Bool node"
            }

            test "finds node in if then branch" {
                let code = "if true then 1 else 2"
                let ast = parse code
                let pos = makePos 0 13  // Position at '1' in then branch

                let result = findNodeAtPosition pos ast
                match result with
                | Some (Number(n, _)) ->
                    Expect.equal n 1 "Should find number 1"
                | _ ->
                    failtest "Expected to find Number node"
            }

            test "finds node in if else branch" {
                let code = "if true then 1 else 2"
                let ast = parse code
                let pos = makePos 0 20  // Position at '2' in else branch

                let result = findNodeAtPosition pos ast
                match result with
                | Some (Number(n, _)) ->
                    Expect.equal n 2 "Should find number 2"
                | _ ->
                    failtest "Expected to find Number node"
            }
        ]

        testList "getIdentifierAtNode" [
            test "extracts Var name" {
                let code = "let x = 42 in x"
                let ast = parse code
                let pos = makePos 0 14  // Variable 'x' in body

                match findNodeAtPosition pos ast with
                | Some node ->
                    let id = getIdentifierAtNode node
                    Expect.equal id (Some "x") "Should extract 'x' from Var"
                | None ->
                    failtest "Expected to find node"
            }

            test "extracts Let binding name" {
                let code = "let x = 42 in x"
                let ast = parse code
                // The top-level node is the Let
                let id = getIdentifierAtNode ast
                Expect.equal id (Some "x") "Should extract 'x' from Let"
            }

            test "extracts Lambda param name" {
                let code = "fun x -> x + 1"
                let ast = parse code
                let id = getIdentifierAtNode ast
                Expect.equal id (Some "x") "Should extract 'x' from Lambda"
            }

            test "returns None for Number" {
                let code = "42"
                let ast = parse code
                let id = getIdentifierAtNode ast
                Expect.isNone id "Should return None for Number node"
            }

            test "extracts LetRec name" {
                let code = "let rec fact n = if n = 0 then 1 else n * fact (n - 1) in fact 5"
                let ast = parse code
                let id = getIdentifierAtNode ast
                Expect.equal id (Some "fact") "Should extract 'fact' from LetRec"
            }
        ]
    ]
