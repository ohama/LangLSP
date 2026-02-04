# Phase 2: Core Navigation - Research

**Researched:** 2026-02-04
**Domain:** LSP textDocument/hover and textDocument/definition implementation with F# and Ionide.LanguageServerProtocol
**Confidence:** HIGH

## Summary

This phase implements two fundamental LSP features: **Hover** (showing type information on mouse hover) and **Go to Definition** (jumping to symbol definitions on click). Both features follow the same pattern: convert LSP position to AST node, query semantic information, and return formatted results.

The standard approach uses position-based AST traversal to find the syntax node at a cursor location, then queries semantic information (type inference results for hover, symbol table for definition). FunLang already has all required infrastructure: Span on every AST node, Hindley-Milner type checker, and parser. The implementation adds position mapping utilities and semantic query functions.

Ionide.LanguageServerProtocol 0.7.0 (already installed) provides F# types matching LSP 3.17 specification. Both features use `TextDocumentPositionParams` (document URI + position) as request parameters. Hover returns `Hover` with `MarkupContent`, definition returns `Location` or `Location[]`.

**Primary recommendation:** Build a shared AST position lookup module first, then implement hover (simpler: just format types) before definition (requires building symbol definition map).

## Standard Stack

The established libraries/tools for this domain:

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Ionide.LanguageServerProtocol | 0.7.0 | LSP types and protocol | F# native, matches LSP 3.17 spec, used by FsAutoComplete |
| FSharp.Compiler.Service | N/A (FunLang uses custom) | Semantic analysis | Standard for F# LSP, but FunLang has custom TypeCheck module |
| Expecto | 10.2.3 | Testing framework | Already in use, property-based testing with FsCheck integration |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Serilog | 4.2.0 | Logging | Already in use for LSP debugging |
| FsCheck | via Expecto.FsCheck 10.2.3 | Property-based testing | Generate test cases for position mapping edge cases |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Ionide.LanguageServerProtocol | OmniSharp.Extensions.LanguageServer | Ionide is lighter, F#-first, sufficient for this project |
| Custom position mapping | rope data structure library | FunLang files are small; simple line-based indexing suffices |

**Installation:**
```bash
# Already installed in Phase 1
# No new dependencies needed
```

## Architecture Patterns

### Recommended Project Structure
```
src/LangLSP.Server/
├── Protocol.fs          # Existing: LSP <-> FunLang type conversions
├── DocumentSync.fs      # Existing: document text storage
├── Diagnostics.fs       # Existing: type error reporting
├── AstLookup.fs         # NEW: position -> AST node mapping
├── Hover.fs             # NEW: hover request handler
├── Definition.fs        # NEW: definition request handler
├── Server.fs            # Update: register new capabilities and handlers
└── Program.fs           # Existing: entry point

src/LangLSP.Tests/
├── AstLookupTests.fs    # NEW: position mapping tests
├── HoverTests.fs        # NEW: hover feature tests
├── DefinitionTests.fs   # NEW: definition feature tests
└── ...existing tests
```

### Pattern 1: Position-Based AST Lookup
**What:** Convert LSP Position (0-based line/char) to FunLang AST node with Span
**When to use:** Every hover and definition request
**Example:**
```fsharp
// Source: Strumenta Go to Definition article + FunLang Span type
module LangLSP.Server.AstLookup

open Ast
open Ionide.LanguageServerProtocol.Types

/// Find AST node at LSP position (0-based) in parsed expression
/// Returns innermost node whose Span contains the position
let rec findNodeAtPosition (lspPos: Position) (expr: Expr) : Expr option =
    let span = spanOf expr
    // LSP is 0-based, FunLang Span is 1-based
    let line = int lspPos.Line + 1
    let col = int lspPos.Character + 1

    // Check if position is within this node's span
    if line < span.StartLine || line > span.EndLine then None
    elif line = span.StartLine && col < span.StartColumn then None
    elif line = span.EndLine && col > span.EndColumn then None
    else
        // Position is in this node - check children for tighter match
        let childMatch = findChildAtPosition lspPos expr
        match childMatch with
        | Some child -> Some child  // Found tighter match
        | None -> Some expr         // This node is the tightest match

and findChildAtPosition (lspPos: Position) (expr: Expr) : Expr option =
    match expr with
    | Let(_, value, body, _) ->
        findNodeAtPosition lspPos value
        |> Option.orElseWith (fun () -> findNodeAtPosition lspPos body)
    | Add(l, r, _) | Multiply(l, r, _) | App(l, r, _) ->
        findNodeAtPosition lspPos l
        |> Option.orElseWith (fun () -> findNodeAtPosition lspPos r)
    // ... handle all Expr variants
    | _ -> None
```

### Pattern 2: Type Information Formatting for Hover
**What:** Convert FunLang Type to readable LSP MarkupContent
**When to use:** Hover response construction
**Example:**
```fsharp
// Source: LSP 3.17 spec + FunLang Type.formatTypeNormalized
module LangLSP.Server.Hover

open Type
open Ionide.LanguageServerProtocol.Types

/// Create hover response with type information
let createTypeHover (ty: Type) (span: Span) : Hover =
    let typeStr = formatTypeNormalized ty
    let markdownContent = sprintf "```funlang\n%s\n```" typeStr
    {
        Contents = U2.C2 {
            Kind = MarkupKind.Markdown
            Value = markdownContent
        }
        Range = Some (Protocol.spanToLspRange span)
    }
```

### Pattern 3: Symbol Definition Tracking
**What:** Build map from variable usage to definition location during type checking
**When to use:** Definition request handler
**Example:**
```fsharp
// Source: Strumenta symbol table pattern + FunLang TypeEnv
module LangLSP.Server.Definition

open Ast
open System.Collections.Generic

/// Symbol definition map: (source file, variable name) -> definition span
type DefinitionMap = Dictionary<string * string, Span>

/// Collect all variable definitions while traversing AST
let rec collectDefinitions (uri: string) (defMap: DefinitionMap) (expr: Expr) : unit =
    match expr with
    | Let(name, value, body, span) ->
        // Register definition location
        defMap.[(uri, name)] <- span
        collectDefinitions uri defMap value
        collectDefinitions uri defMap body
    | Lambda(param, body, span) ->
        defMap.[(uri, param)] <- span
        collectDefinitions uri defMap body
    // ... handle all binding forms (LetRec, LetPat, Match clauses)
    | _ -> ()

/// Find definition location for variable at position
let findDefinition (uri: string) (defMap: DefinitionMap) (varName: string) : Location option =
    match defMap.TryGetValue((uri, varName)) with
    | true, span ->
        Some {
            Uri = uri
            Range = Protocol.spanToLspRange span
        }
    | false, _ -> None
```

### Pattern 4: Incremental Re-parsing on Document Change
**What:** Parse document and rebuild semantic info when text changes
**When to use:** textDocument/didChange handler
**Example:**
```fsharp
// Source: Strumenta caching pattern + existing DocumentSync
module LangLSP.Server.DocumentSync

type DocumentState = {
    Text: string
    ParseTree: Expr option
    TypeEnv: TypeEnv option
    DefinitionMap: DefinitionMap
}

let mutable documentStates = Map.empty<string, DocumentState>

let updateDocumentState (uri: string) (text: string) : unit =
    let parseTree =
        try Some (Parser.parse text)
        with _ -> None

    let defMap = DefinitionMap()
    parseTree |> Option.iter (collectDefinitions uri defMap)

    documentStates <- Map.add uri {
        Text = text
        ParseTree = parseTree
        TypeEnv = None  // Could cache type checking results
        DefinitionMap = defMap
    } documentStates
```

### Anti-Patterns to Avoid
- **Re-parsing on every hover/definition request:** Parse once on didChange, cache AST and semantic info
- **1-based LSP positions:** LSP uses 0-based line/column; FunLang Span is 1-based (convert at boundary)
- **Returning plaintext hover:** Use MarkupKind.Markdown with code fences for syntax highlighting
- **Null hover for keywords:** Return Korean explanations for keywords (let, if, match) per requirements

## Don't Hand-Roll

Problems that look simple but have existing solutions:

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| LSP position ↔ byte offset conversion | Custom character counting | Line-based lookup with string indexing | FunLang files are small; complex rope/piece-table overkill |
| Type pretty-printing | Custom formatter | `Type.formatTypeNormalized` | Already handles variable normalization ('a, 'b vs TVar 1000) |
| LSP protocol types | Custom type definitions | Ionide.LanguageServerProtocol.Types | Auto-generated from LSP spec, handles all variants |
| AST span extraction | Pattern matching everywhere | `Ast.spanOf` helper | Already exists, handles all 30+ Expr variants |
| Markdown escaping | String manipulation | Markdown code fences ```funlang | Client handles escaping; avoid manual backslash escaping |

**Key insight:** FunLang already has span information and type formatting. The main work is position-based lookup and wiring LSP handlers, not building infrastructure from scratch.

## Common Pitfalls

### Pitfall 1: Off-by-One Position Mapping
**What goes wrong:** Hover/definition triggers on wrong symbol or returns "not found"
**Why it happens:** LSP uses 0-based line/column, FunLang Span is 1-based (StartLine=1 for first line)
**How to avoid:** Always convert at the LSP boundary: `let line = int lspPos.Line + 1`
**Warning signs:** Tests pass for simple cases but fail for multi-line expressions

### Pitfall 2: Inclusive vs Exclusive End Positions
**What goes wrong:** Hover range highlights wrong text or extra characters
**Why it happens:** LSP Range.End is exclusive (like [start, end)), but FunLang Span.EndColumn is inclusive
**How to avoid:** When converting Span to Range, use EndColumn as-is (it already points past last char due to FsLexYacc)
**Warning signs:** Hover underline extends one character too far

### Pitfall 3: Missing Keyword Hover
**What goes wrong:** Hovering over "let" or "if" shows nothing
**Why it happens:** Keywords aren't in AST (only in tokens); position lookup finds parent expression
**How to avoid:** Check if position falls within keyword tokens before AST lookup; return hardcoded explanations
**Warning signs:** Variables and functions work, but keywords are silent

### Pitfall 4: Definition Not Found for Built-in Functions
**What goes wrong:** "Go to Definition" on `map` or `filter` fails
**Why it happens:** Prelude functions in `TypeCheck.initialTypeEnv` have no source location (defined in F# code, not FunLang)
**How to avoid:** Check if symbol is in initialTypeEnv; return error or jump to Prelude.fs documentation
**Warning signs:** User-defined functions work, but standard library functions fail

### Pitfall 5: Scope Resolution for Shadowed Variables
**What goes wrong:** Definition jumps to wrong binding (outer scope instead of inner)
**Why it happens:** Simple name-based lookup doesn't respect lexical scoping
**How to avoid:** Walk scope chain from usage site upward to parent; return first definition found
**Warning signs:** Works for unique variable names, fails for `let x = 1 in let x = 2 in x`

### Pitfall 6: Hover Performance on Large Files
**What goes wrong:** Hover becomes sluggish as file grows
**Why it happens:** Re-parsing and type checking on every hover request
**How to avoid:** Cache parse tree and type checking results in DocumentState; only invalidate on didChange
**Warning signs:** Fast on small files (<100 lines), slow on realistic code (>500 lines)

## Code Examples

Verified patterns from official sources:

### LSP Hover Response Structure
```fsharp
// Source: LSP 3.17 specification
// https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/#textDocument_hover

/// Handle textDocument/hover request
let handleHover (params: HoverParams) : Async<Hover option> =
    async {
        let uri = params.TextDocument.Uri
        let position = params.Position

        match DocumentSync.getDocument uri with
        | None -> return None
        | Some text ->
            match Parser.parse text with
            | Error _ -> return None
            | Ok ast ->
                match AstLookup.findNodeAtPosition position ast with
                | None -> return None
                | Some node ->
                    // Check for keyword hover first
                    match checkKeywordAtPosition position text with
                    | Some keywordHover -> return Some keywordHover
                    | None ->
                        // Type inference for expression
                        match TypeCheck.typecheck node with
                        | Error _ -> return None
                        | Ok ty ->
                            let hover = createTypeHover ty (spanOf node)
                            return Some hover
    }
```

### LSP Definition Response Structure
```fsharp
// Source: LSP 3.17 specification
// https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/#textDocument_definition

/// Handle textDocument/definition request
let handleDefinition (params: DefinitionParams) : Async<Location option> =
    async {
        let uri = params.TextDocument.Uri
        let position = params.Position

        match DocumentSync.getDocumentState uri with
        | None -> return None
        | Some state ->
            match state.ParseTree with
            | None -> return None
            | Some ast ->
                match AstLookup.findNodeAtPosition position ast with
                | None -> return None
                | Some node ->
                    match node with
                    | Var(name, _) ->
                        // Look up definition in symbol table
                        return Definition.findDefinition uri state.DefinitionMap name
                    | _ -> return None  // Not a variable reference
    }
```

### Korean Keyword Explanations
```fsharp
// Source: Tutorial requirement TUT-06
let keywordExplanations = Map.ofList [
    ("let", "변수 또는 함수를 정의합니다. 예: let x = 5")
    ("in", "let 바인딩의 범위를 지정합니다. 예: let x = 1 in x + 2")
    ("if", "조건 분기를 수행합니다. 예: if x > 0 then 1 else -1")
    ("then", "if의 참 분기를 시작합니다.")
    ("else", "if의 거짓 분기를 시작합니다.")
    ("match", "패턴 매칭을 시작합니다. 예: match xs with | [] -> 0 | h::t -> h")
    ("with", "match의 패턴 절을 시작합니다.")
    ("fun", "익명 함수를 정의합니다. 예: fun x -> x + 1")
    ("rec", "재귀 함수를 정의합니다. 예: let rec sum n = if n = 0 then 0 else n + sum (n-1)")
]

/// Check if position is within a keyword token
let checkKeywordAtPosition (position: Position) (text: string) : Hover option =
    // Tokenize line at position
    let lines = text.Split('\n')
    let lineIdx = int position.Line
    if lineIdx >= lines.Length then None
    else
        let line = lines.[lineIdx]
        let col = int position.Character
        // Simple word extraction (production code needs lexer integration)
        // Find word boundaries around position
        // Check if word is in keywordExplanations map
        // Return hover with Korean explanation
        None  // Placeholder - actual implementation needs token position tracking
```

### Expecto Test Structure
```fsharp
// Source: Existing DocumentSyncTests.fs pattern
module LangLSP.Tests.HoverTests

open Expecto
open Ionide.LanguageServerProtocol.Types
open LangLSP.Server.Hover

let makeHoverParams uri line char : HoverParams =
    {
        TextDocument = { Uri = uri }
        Position = { Line = uint32 line; Character = uint32 char }
        WorkDoneToken = None
    }

[<Tests>]
let hoverTests =
    testList "Hover" [
        testCase "hover over variable shows inferred type" <| fun _ ->
            let uri = "file:///test.fun"
            let text = "let x = 42 in x"
            // Setup document
            DocumentSync.handleDidOpen (makeDidOpenParams uri text)
            // Hover over 'x' usage at position (0, 14)
            let hover = handleHover (makeHoverParams uri 0 14) |> Async.RunSynchronously
            Expect.isSome hover "Should return hover"
            match hover.Value.Contents with
            | U2.C2 markup ->
                Expect.stringContains markup.Value "int" "Should show int type"
            | _ -> failtest "Expected MarkupContent"

        testCase "hover over function shows signature" <| fun _ ->
            let uri = "file:///test.fun"
            let text = "let f = fun x -> x + 1 in f"
            DocumentSync.handleDidOpen (makeDidOpenParams uri text)
            let hover = handleHover (makeHoverParams uri 0 26) |> Async.RunSynchronously
            Expect.isSome hover "Should return hover"
            match hover.Value.Contents with
            | U2.C2 markup ->
                Expect.stringContains markup.Value "int -> int" "Should show function signature"
            | _ -> failtest "Expected MarkupContent"
    ]
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| LSP 3.16 | LSP 3.17 | 2021-2022 | Adds typeHierarchy, inlineValue, diagnostic pull model (not needed for Phase 2) |
| OmniSharp.Extensions.LanguageServer | Ionide.LanguageServerProtocol | 2024-2025 | Ionide auto-generates from spec, lighter for simple servers |
| Hover returns string | Hover returns MarkupContent | LSP 3.0 (2018) | Allows markdown formatting, syntax highlighting in hover tooltips |
| Definition returns Location | Definition returns Location \| Location[] \| LocationLink[] | LSP 3.14 (2019) | Enables showing definition context; Location sufficient for Phase 2 |

**Deprecated/outdated:**
- **MarkedString in Hover:** Replaced by MarkupContent in LSP 3.0 (use MarkupContent.Kind = MarkupKind.Markdown)
- **1-based Position in some old clients:** LSP spec always used 0-based; any 1-based references are client bugs

## Open Questions

Things that couldn't be fully resolved:

1. **Keyword position detection without token stream**
   - What we know: Parser produces AST without token positions; keyword hovers need token-level info
   - What's unclear: Whether to run lexer separately for keyword detection or enhance parser to preserve token spans
   - Recommendation: Start with simple regex-based keyword detection on source line; enhance with lexer if needed

2. **Hover for pattern-bound variables in match clauses**
   - What we know: Match patterns bind variables (`match xs with | h::t -> h`), but type checker doesn't assign types to pattern variables in isolation
   - What's unclear: Whether to extend type checker to annotate pattern variable types or infer on hover
   - Recommendation: Phase 2 skips pattern variable hover; add in later phase if needed

3. **Multi-file Go to Definition**
   - What we know: FunLang has no module system yet; all code is in one file
   - What's unclear: When modules are added, how to track cross-file definitions
   - Recommendation: Design DefinitionMap to support cross-file keys `(uri, name)`; works when modules added

4. **Performance threshold for caching strategy**
   - What we know: Caching AST and type results avoids re-parsing on every request
   - What's unclear: Typical FunLang file sizes in practice; cache complexity may be premature
   - Recommendation: Start without caching; measure performance in tests; add caching if hover >100ms

## Sources

### Primary (HIGH confidence)
- [LSP 3.17 Specification](https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/) - textDocument/hover and textDocument/definition request/response types
- [Ionide.LanguageServerProtocol GitHub](https://github.com/ionide/LanguageServerProtocol) - Library structure and F# usage
- [Go To Definition in LSP - Strumenta](https://tomassetti.me/go-to-definition-in-the-language-server-protocol/) - Symbol table and scope resolution patterns
- FunLang source code (Ast.fs, Type.fs, TypeCheck.fs) - Existing span tracking and type formatting

### Secondary (MEDIUM confidence)
- [VS Code Language Server Extension Guide](https://code.visualstudio.com/api/language-extensions/language-server-extension-guide) - Hover and definition implementation examples
- [LSP Position Mapping Issues](https://github.com/microsoft/language-server-protocol/issues/96) - 0-based vs 1-based position pitfalls
- [Making an LSP - Thunderseethe's Devlog](https://thunderseethe.dev/posts/lsp-base/) - Position to AST node mapping techniques (2026)
- [FsAutoComplete GitHub](https://github.com/ionide/FsAutoComplete) - F# LSP server implementation reference

### Tertiary (LOW confidence)
- WebSearch: "Hindley-Milner type inference hover display format" - No specific format standard found; defaulting to FunLang's formatTypeNormalized
- WebSearch: "LSP server testing unit test mock client" - Expecto already in use; no F#-specific LSP test utilities found

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - Ionide.LanguageServerProtocol 0.7.0 already installed and matches LSP 3.17 spec
- Architecture: HIGH - FunLang has Span, Type.formatTypeNormalized, and all needed infrastructure; patterns verified from Strumenta and LSP spec
- Pitfalls: MEDIUM - Position mapping issues well-documented in LSP GitHub; scope resolution pitfalls inferred from general compiler knowledge

**Research date:** 2026-02-04
**Valid until:** 2026-03-04 (30 days - stable domain, LSP 3.17 is current spec, Ionide.LanguageServerProtocol actively maintained)
