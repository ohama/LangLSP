# Phase 3: Completion - Research

**Researched:** 2026-02-04
**Domain:** LSP textDocument/completion protocol implementation in F#
**Confidence:** HIGH

## Summary

Completion (autocomplete) is a core LSP feature that provides intelligent suggestions as users type. The research focused on the LSP 3.17 completion protocol, Ionide.LanguageServerProtocol implementation patterns, and best practices for implementing keyword and scope-based symbol completion.

The standard approach for LSP completion involves:
1. Implementing `textDocument/completion` request handler that returns `CompletionItem[]` or `CompletionList`
2. Using `CompletionItemKind` enum to categorize completions (Keyword, Variable, Function, etc.)
3. Leveraging existing AST traversal infrastructure to collect in-scope symbols
4. Providing type information in `detail` field using the existing type checker

Phase 3 builds directly on Phase 2's AstLookup module and Definition module's `collectDefinitions` function, which already implements scope tracking. The main work is creating a Completion module, registering it with the server, and writing comprehensive tests and a Korean tutorial.

**Primary recommendation:** Reuse Definition.collectDefinitions for scope tracking, add keyword list as static data, return CompletionList with isIncomplete=false for simplicity, use existing Type.formatTypeNormalized for type annotations.

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Ionide.LanguageServerProtocol | 0.7.0 | LSP protocol types and server infrastructure | Official F# LSP library, already in use for Phases 1-2, provides CompletionParams, CompletionItem, CompletionList types |
| FunLang type checker | (project) | Type inference for completion item annotations | Hindley-Milner type checker already integrated, provides Type.formatTypeNormalized for display |
| Expecto | 10.2.3+ | Unit testing framework | Already in use for Phases 1-2, provides testCase and testList |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| FsCheck | (via Expecto.FsCheck) | Property-based testing | For testing completion filtering/sorting properties if needed |
| Serilog | 4.2.0 | Logging | Already configured, useful for debugging completion triggers |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| CompletionList | CompletionItem[] | CompletionList allows isIncomplete flag for streaming, but adds complexity. Use simple array for Phase 3 MVP. |
| Static keyword list | Parser token list | Dynamic list from parser is more maintainable but requires parser changes. Static list is simpler for Phase 3. |
| collectDefinitions reuse | New scope walker | New walker would allow finer-grained scope rules but duplicates existing code. Reuse for consistency. |

**Installation:**
```bash
# No new dependencies needed - all libraries already installed in Phases 1-2
dotnet restore
```

## Architecture Patterns

### Recommended Project Structure
```
src/LangLSP.Server/
├── Completion.fs         # New: textDocument/completion handler
├── AstLookup.fs          # Existing: findNodeAtPosition
├── Definition.fs         # Existing: collectDefinitions (reuse for scope)
├── Protocol.fs           # Existing: spanToLspRange
├── Server.fs             # Update: register completion handler
└── ...

src/LangLSP.Tests/
└── CompletionTests.fs    # New: unit tests for completion
```

### Pattern 1: Completion Request Handler

**What:** Standard LSP request handler pattern for textDocument/completion
**When to use:** Main entry point for completion requests from editor

**Example:**
```fsharp
// Completion.fs
module LangLSP.Server.Completion

open Ionide.LanguageServerProtocol.Types
open LangLSP.Server.DocumentSync
open LangLSP.Server.AstLookup
open LangLSP.Server.Definition

/// Handle textDocument/completion request
let handleCompletion (p: CompletionParams) : Async<CompletionList option> =
    async {
        let uri = p.TextDocument.Uri
        let pos = p.Position

        match getDocument uri with
        | None -> return None
        | Some text ->
            try
                let lexbuf = FSharp.Text.Lexing.LexBuffer<char>.FromString(text)
                let ast = Parser.start Lexer.tokenize lexbuf

                // Collect keyword completions
                let keywords = getKeywordCompletions()

                // Collect symbol completions from scope
                let symbols = getSymbolCompletions ast pos

                let items = keywords @ symbols |> Array.ofList
                return Some {
                    IsIncomplete = false
                    Items = items
                }
            with _ ->
                return None
    }
```

### Pattern 2: Keyword Completion Items

**What:** Static list of language keywords with CompletionItemKind.Keyword
**When to use:** For all completion requests (keywords are always in scope)

**Example:**
```fsharp
/// FunLang keywords for completion
let funlangKeywords = [
    "let"; "in"; "if"; "then"; "else"; "match"; "with"
    "fun"; "rec"; "true"; "false"
]

/// Create completion items for keywords
let getKeywordCompletions () : CompletionItem list =
    funlangKeywords
    |> List.map (fun kw -> {
        Label = kw
        Kind = Some CompletionItemKind.Keyword
        Detail = Some "keyword"
        Documentation = None
        InsertText = Some kw
        InsertTextFormat = Some InsertTextFormat.PlainText
        // Other fields: use defaults or None
        SortText = None
        FilterText = None
        // ... (see full CompletionItem type in Ionide docs)
    })
```

### Pattern 3: Scope-Based Symbol Completion

**What:** Collect variables/functions in scope using existing Definition.collectDefinitions
**When to use:** For symbol completion (reuses existing scope tracking)

**Example:**
```fsharp
/// Get symbol completions from current scope
let getSymbolCompletions (ast: Expr) (pos: Position) : CompletionItem list =
    let definitions = Definition.collectDefinitions ast

    // Filter to symbols defined before cursor position
    let inScope =
        definitions
        |> List.filter (fun (name, span) ->
            span.StartLine < int pos.Line ||
            (span.StartLine = int pos.Line && span.StartColumn < int pos.Character))

    // Create completion items with type information
    inScope
    |> List.map (fun (name, span) ->
        let typeInfo = tryGetTypeForSymbol name ast
        {
            Label = name
            Kind = Some CompletionItemKind.Variable  // Or Function based on context
            Detail = typeInfo |> Option.map Type.formatTypeNormalized
            Documentation = None
            InsertText = Some name
            InsertTextFormat = Some InsertTextFormat.PlainText
            SortText = None
            FilterText = None
        })
```

### Pattern 4: Type Information in Completion Items

**What:** Use existing type checker to annotate completion items
**When to use:** For all symbol completions to show type information

**Example:**
```fsharp
/// Try to get type for a symbol by name
let tryGetTypeForSymbol (name: string) (ast: Expr) : Type.Type option =
    // Reuse Hover module's findVarTypeInAst logic
    match Hover.findVarTypeInAst name ast with
    | Some ty -> Some ty
    | None ->
        // Try typechecking the whole AST
        match TypeCheck.typecheck ast with
        | Ok _ ->
            // Additional heuristics if needed
            None
        | Error _ -> None
```

### Pattern 5: Server Capability Registration

**What:** Register completion provider capability in server initialization
**When to use:** Required for VS Code to send completion requests

**Example:**
```fsharp
// Server.fs
let serverCapabilities : ServerCapabilities =
    { ServerCapabilities.Default with
        TextDocumentSync = ...
        DefinitionProvider = Some (U2.C1 true)
        HoverProvider = Some (U2.C1 true)
        CompletionProvider = Some {
            ResolveProvider = Some false  // No lazy resolution for Phase 3
            TriggerCharacters = None      // No trigger chars for Phase 3 MVP
            AllCommitCharacters = None
            WorkDoneProgress = None
        }
    }

// Register handler in Program.fs message loop
match msg with
| "textDocument/completion" ->
    let p = deserialize<CompletionParams> params
    let! result = Completion.handleCompletion p
    return serialize result
```

### Anti-Patterns to Avoid

- **Don't filter on server side prematurely:** LSP clients do fuzzy matching. Return all in-scope symbols and let the client filter. Only filter out symbols not in scope (defined after cursor position).
- **Don't use complex trigger characters initially:** Start with no trigger characters (user invokes explicitly). Add `.` or other triggers in later phases after basic completion works.
- **Don't implement completionItem/resolve prematurely:** For Phase 3, return complete items immediately. Lazy resolution adds complexity without benefit for small symbol tables.
- **Don't duplicate scope tracking logic:** Reuse Definition.collectDefinitions instead of writing new AST traversal code.

## Don't Hand-Roll

Problems that look simple but have existing solutions:

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Scope tracking | New AST traversal for variables in scope | Definition.collectDefinitions | Already implemented and tested in Phase 2, handles shadowing heuristics |
| Type formatting | Custom type-to-string converter | Type.formatTypeNormalized | Already handles polymorphic types ('a, 'b), function arrows, tuples |
| Position comparison | Custom line/column comparison logic | Existing pattern in Definition module | Handles 0-based LSP coordinates, tested with edge cases |
| LSP type conversion | Manual JSON serialization | Ionide.LanguageServerProtocol.Types | Provides all LSP types, handles protocol versioning |

**Key insight:** Phase 3 leverages substantial infrastructure from Phases 1-2. The main new code is the keyword list, the completion handler glue logic, and tests. Avoid rewriting existing AST/type/scope logic.

## Common Pitfalls

### Pitfall 1: Completion List vs Array Confusion

**What goes wrong:** Returning `CompletionItem[]` when client expects `CompletionList`, or vice versa.

**Why it happens:** LSP spec allows both `CompletionItem[] | CompletionList | null` as return type.

**How to avoid:** Use `CompletionList` with `IsIncomplete = false` for consistency. Ionide types use option types, so return `Some { IsIncomplete = false; Items = items }`.

**Warning signs:** Completions don't appear in VS Code, or JSON serialization errors in logs.

### Pitfall 2: 0-based vs 1-based Position Indexing

**What goes wrong:** Using Span coordinates (1-based in FsLexYacc docs) directly with LSP Position (0-based).

**Why it happens:** FsLexYacc documentation states Position is 1-based, but LexBuffer.FromString creates 0-based positions.

**How to avoid:** Follow existing pattern: LexBuffer.FromString produces 0-based coordinates matching LSP. Use `int pos.Line` and `int pos.Character` directly (already correct in AstLookup and Definition modules).

**Warning signs:** Completions appear at wrong cursor position, scope filtering is off by one line.

### Pitfall 3: Including Out-of-Scope Symbols

**What goes wrong:** Completion list includes variables defined after the cursor position.

**Why it happens:** collectDefinitions returns all definitions without position filtering.

**How to avoid:** Filter definitions by comparing span.StartLine/StartColumn with cursor position (pattern from Definition.findDefinitionForVar).

**Warning signs:** User sees variables in completion list that don't exist yet in code flow.

### Pitfall 4: Missing Type Information

**What goes wrong:** Completion items show symbol names without type annotations.

**Why it happens:** Type inference requires full AST context, not just symbol name.

**How to avoid:** Reuse Hover.findVarTypeInAst to get type for each symbol, use Type.formatTypeNormalized for display.

**Warning signs:** Completion items are less useful than Hover (which shows types correctly).

### Pitfall 5: Trigger Characters Breaking Basic Completion

**What goes wrong:** Adding trigger characters like `.` or space causes completions to not appear.

**Why it happens:** Trigger character implementation requires careful context handling and filtering logic.

**How to avoid:** Phase 3 MVP: Set `TriggerCharacters = None` in CompletionProvider capability. Add triggers in Phase 4 after basic completion is solid.

**Warning signs:** Completion works when manually invoked (Ctrl+Space) but not when typing.

## Code Examples

Verified patterns from official sources:

### Creating CompletionParams for Testing

```fsharp
// CompletionTests.fs
let makeCompletionParams uri line char : CompletionParams =
    {
        TextDocument = { Uri = uri }
        Position = { Line = uint32 line; Character = uint32 char }
        Context = None  // No trigger context for basic tests
        WorkDoneToken = None
        PartialResultToken = None
    }

let setupAndComplete uri text line char =
    clearAll()
    handleDidOpen (makeDidOpenParams uri text)
    handleCompletion (makeCompletionParams uri line char) |> Async.RunSynchronously
```

### Extracting Completion Items from Response

```fsharp
// Test helper to extract items
let getCompletionLabels (result: CompletionList option) : string list =
    result
    |> Option.map (fun list -> list.Items |> Array.map (fun item -> item.Label) |> Array.toList)
    |> Option.defaultValue []

// Test case
testCase "keyword completion includes let" <| fun _ ->
    let result = setupAndComplete "file:///test.fun" "l" 0 1
    let labels = getCompletionLabels result
    Expect.contains labels "let" "Should include 'let' keyword"
```

### Symbol Completion with Type Annotation

```fsharp
// Completion.fs
/// Create completion item for a symbol with type information
let createSymbolCompletionItem (name: string) (typeOpt: Type.Type option) : CompletionItem =
    let detail =
        match typeOpt with
        | Some ty -> Some (sprintf "%s: %s" name (Type.formatTypeNormalized ty))
        | None -> Some name

    {
        Label = name
        Kind = Some CompletionItemKind.Variable
        Detail = detail
        Documentation = None
        Deprecated = Some false
        Preselect = Some false
        SortText = None
        FilterText = None
        InsertText = Some name
        InsertTextFormat = Some InsertTextFormat.PlainText
        InsertTextMode = None
        TextEdit = None
        AdditionalTextEdits = None
        CommitCharacters = None
        Command = None
        Data = None
    }
```

### Keyword vs Symbol Differentiation

```fsharp
// Test case showing distinction
testCase "completion includes both keywords and symbols" <| fun _ ->
    let text = "let x = 42 in l"  // User types 'l' after defining x
    let result = setupAndComplete "file:///test.fun" text 0 14

    match result with
    | Some list ->
        let keywords = list.Items |> Array.filter (fun i -> i.Kind = Some CompletionItemKind.Keyword)
        let symbols = list.Items |> Array.filter (fun i -> i.Kind = Some CompletionItemKind.Variable)

        Expect.isGreaterThan keywords.Length 0 "Should have keyword completions"
        Expect.isGreaterThan symbols.Length 0 "Should have symbol completions (x)"
    | None ->
        failtest "Expected completion result"
```

### Scope Filtering Example

```fsharp
// Completion.fs - Position-based scope filtering
let getSymbolsInScope (ast: Expr) (pos: Position) : (string * Span) list =
    Definition.collectDefinitions ast
    |> List.filter (fun (_, span) ->
        // Symbol is in scope if defined before cursor
        span.StartLine < int pos.Line ||
        (span.StartLine = int pos.Line && span.StartColumn < int pos.Character))
    |> List.distinctBy fst  // Remove duplicates (last occurrence wins for shadowing)
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| CompletionItem[] only | CompletionList with isIncomplete flag | LSP 3.0 (2016) | Enables streaming completions for large symbol tables |
| Static keyword lists | Dynamic completion from language service | Modern LSP (2020+) | More accurate, context-aware suggestions |
| Server-side filtering | Client-side fuzzy matching | LSP 3.0+ | Better UX, servers return all candidates |
| Synchronous completion | Async with cancellation tokens | LSP 3.15+ | Responsive editor, cancellable requests |

**Deprecated/outdated:**
- **Trigger characters only:** Modern LSP prefers completionProvider.resolveProvider for lazy loading over trigger characters alone.
- **InsertTextFormat.Snippet without placeholder:** Use PlainText for Phase 3 simplicity. Snippets add complexity.

## Open Questions

Things that couldn't be fully resolved:

1. **Should Phase 3 include function vs variable distinction in CompletionItemKind?**
   - What we know: Definition.collectDefinitions returns all bindings uniformly
   - What's unclear: Determining if binding is function requires type checking (TArrow)
   - Recommendation: Use CompletionItemKind.Variable for all symbols in Phase 3, refine to Function in Phase 4 when type is TArrow

2. **Should completion filter by prefix on server side or let client filter?**
   - What we know: LSP clients do fuzzy matching (VS Code uses complex algorithms)
   - What's unclear: Whether filtering improves perceived performance
   - Recommendation: Return all in-scope symbols (no prefix filtering). Client handles fuzzy matching. Only filter by scope (position).

3. **How to handle completion inside incomplete expressions (parse errors)?**
   - What we know: Parser throws on syntax errors, no AST available
   - What's unclear: Recovery strategies for partial ASTs
   - Recommendation: Phase 3: Return only keywords on parse error (no AST means no symbol scope). Phase 4+: Consider error-recovery parser.

## Sources

### Primary (HIGH confidence)

- [LSP 3.17 Specification - textDocument/completion](https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/) - Official Microsoft LSP protocol specification for completion request/response structure
- [Ionide.LanguageServerProtocol Repository](https://github.com/ionide/LanguageServerProtocol) - F# LSP library source code and type definitions
- [FsAutoComplete CompletionTests.fs](https://github.com/ionide/FsAutoComplete/blob/main/test/FsAutoComplete.Tests.Lsp/CompletionTests.fs) - Production F# LSP implementation test patterns
- Existing codebase: AstLookup.fs, Definition.fs, Hover.fs, Protocol.fs - Phase 1-2 infrastructure

### Secondary (MEDIUM confidence)

- [VS Code Language Server Extension Guide](https://code.visualstudio.com/api/language-extensions/language-server-extension-guide) - Best practices for LSP implementation
- [Making an LSP for great good](https://thunderseethe.dev/posts/lsp-base/) - 2026 blog post on LSP completion implementation with scope tracking
- [Bash LSP scope-aware completion issue](https://github.com/bash-lsp/bash-language-server/issues/220) - Discussion of scope-based completion challenges
- [LSP completion filtering issue](https://github.com/microsoft/language-server-protocol/issues/898) - Debate on server vs client filtering

### Tertiary (LOW confidence)

- [Emacs lsp-mode completion](https://emacs-lsp.github.io/lsp-mode/page/settings/completion/) - Client-side configuration patterns
- [Neovim LSP trigger characters](https://github.com/neovim/neovim/issues/12997) - Edge cases with trigger character handling

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - Ionide 0.7.0 already in use, all types verified from existing codebase
- Architecture: HIGH - Patterns proven in Phases 1-2, Definition.collectDefinitions tested and working
- Pitfalls: HIGH - Based on actual bugs encountered in similar LSP implementations (FsAutoComplete, bash-lsp)

**Research date:** 2026-02-04
**Valid until:** 2026-03-04 (30 days - stable LSP 3.17 protocol, mature Ionide library)

---

**Next steps for planner:**
1. Create Completion.fs module with handleCompletion, getKeywordCompletions, getSymbolCompletions
2. Update Server.fs to register CompletionProvider capability and handler
3. Create CompletionTests.fs with keyword tests, symbol tests, scope tests, type annotation tests
4. Write Korean tutorial 07-completion.md following 06-hover.md structure
5. Integration verification: Manual test in VS Code, ensure no regressions in Hover/Definition
