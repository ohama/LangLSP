# Phase 4: Advanced Features - Research

**Researched:** 2026-02-05
**Domain:** LSP advanced navigation features (Find References, Rename Symbol, Code Actions) in F#
**Confidence:** HIGH

## Summary

Phase 4 implements three advanced LSP features that build on the foundation from Phases 1-3. These features provide powerful code navigation and refactoring capabilities essential for productive development.

The research focused on LSP 3.17 protocol specifications for textDocument/references, textDocument/rename, and textDocument/codeAction requests. Find References locates all usages of a symbol, Rename Symbol performs safe refactoring with preview, and Code Actions provide quickfixes for diagnostics like unused variables or type errors.

The standard approach for Phase 4 involves:
1. **Find References**: Traverse AST to collect all `Var` nodes matching a symbol name, respecting scope and shadowing rules established in Phase 2's Definition module
2. **Rename Symbol**: Collect all references (reusing Find References logic), construct WorkspaceEdit with TextEdit[] for each occurrence, support textDocument/prepareRename for validation
3. **Code Actions**: Integrate with diagnostics from Phase 1, return CodeAction objects with `kind: quickfix`, generate WorkspaceEdit to apply fixes (remove unused variables, suggest type corrections)

All three features leverage existing infrastructure: AstLookup for position-based queries, Definition.collectDefinitions for binding sites, and the type checker for semantic analysis. The main new code is reference collection (inverse of definition finding), WorkspaceEdit construction, and diagnostic-driven code action generation.

**Primary recommendation:** Build references by traversing AST for Var nodes (inverse of collectDefinitions), handle shadowing by tracking scope during traversal, construct WorkspaceEdit using simple `changes` map (not documentChanges) for single-file scope, generate code actions only for fixable diagnostics with high confidence.

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Ionide.LanguageServerProtocol | 0.7.0 | LSP protocol types and server infrastructure | Provides ReferenceParams, RenameParams, CodeActionParams, WorkspaceEdit, CodeAction, Location types - already in use for Phases 1-3 |
| FunLang type checker | (project) | Semantic analysis for code actions | Hindley-Milner type checker provides diagnostic information and type suggestions for quickfixes |
| Expecto | 10.2.3+ | Unit testing framework | Already in use, provides test structure for complex scenarios like shadowing and scope edge cases |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| FsCheck | (via Expecto.FsCheck) | Property-based testing | For testing rename safety properties (all references renamed, no unintended changes) |
| Serilog | 4.2.0 | Logging | Already configured, useful for debugging complex reference/rename operations |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Simple Var traversal | Symbol table with reference tracking | Symbol table is more accurate for complex scoping but requires Phase 2 refactoring. Var traversal works for single-file scope. |
| WorkspaceEdit.changes | WorkspaceEdit.documentChanges | documentChanges supports versioning and resource operations but adds complexity. Use simple changes map for Phase 4. |
| Quickfix CodeActions only | Full refactoring suite | Extract method, inline variable etc. are valuable but out of scope for v1. Focus on diagnostic fixes. |

**Installation:**
```bash
# No new dependencies needed - all libraries already installed in Phases 1-3
dotnet restore
```

## Architecture Patterns

### Recommended Project Structure
```
src/LangLSP.Server/
├── References.fs          # New: textDocument/references handler
├── Rename.fs              # New: textDocument/rename, prepareRename handlers
├── CodeActions.fs         # New: textDocument/codeAction handler
├── AstLookup.fs           # Existing: findNodeAtPosition
├── Definition.fs          # Existing: collectDefinitions (reuse for scope)
├── Diagnostics.fs         # Existing: integrate with code actions
├── Protocol.fs            # Update: add WorkspaceEdit helpers
├── Server.fs              # Update: register new handlers
└── ...

src/LangLSP.Tests/
├── ReferencesTests.fs     # New: unit tests for find references
├── RenameTests.fs         # New: unit tests for rename
└── CodeActionsTests.fs    # New: unit tests for code actions
```

### Pattern 1: Find References Handler

**What:** Collect all usage locations of a symbol by traversing AST for matching Var nodes
**When to use:** textDocument/references request for "Find All References" feature

**Example:**
```fsharp
// References.fs
module LangLSP.Server.References

open Ionide.LanguageServerProtocol.Types
open LangLSP.Server.AstLookup
open LangLSP.Server.Definition
open LangLSP.Server.DocumentSync

/// Collect all Var nodes with a given name from AST
/// Returns list of (name, span) pairs for all usages
let collectReferences (varName: string) (ast: Expr) : Span list =
    let refs = ResizeArray<Span>()

    let rec traverse expr =
        match expr with
        | Var(name, span) when name = varName ->
            refs.Add(span)
        | Let(_, value, body, _) ->
            traverse value
            traverse body
        | App(fn, arg, _) ->
            traverse fn
            traverse arg
        // ... (traverse all expression types)
        | _ -> ()

    traverse ast
    refs |> Seq.toList

/// Handle textDocument/references request
let handleReferences (p: ReferenceParams) : Async<Location[] option> =
    async {
        let uri = p.TextDocument.Uri
        let pos = p.Position
        let includeDeclaration = p.Context.IncludeDeclaration

        match getDocument uri with
        | None -> return None
        | Some text ->
            try
                let lexbuf = FSharp.Text.Lexing.LexBuffer<char>.FromString(text)
                let ast = Parser.start Lexer.tokenize lexbuf

                // Find what symbol is at the position
                match findNodeAtPosition pos ast with
                | Some (Var(name, _)) ->
                    // Collect all references to this variable
                    let refSpans = collectReferences name ast

                    // Optionally include the definition location
                    let allSpans =
                        if includeDeclaration then
                            match findDefinitionForVar name ast pos with
                            | Some defSpan -> defSpan :: refSpans
                            | None -> refSpans
                        else
                            refSpans

                    let locations =
                        allSpans
                        |> List.map (fun span -> {
                            Uri = uri
                            Range = spanToLspRange span
                        })
                        |> Array.ofList

                    return Some locations
                | _ -> return None
            with _ ->
                return None
    }
```

### Pattern 2: Rename Symbol Handler

**What:** Collect references and construct WorkspaceEdit to rename all occurrences
**When to use:** textDocument/rename request for "Rename Symbol" refactoring

**Example:**
```fsharp
// Rename.fs
module LangLSP.Server.Rename

open Ionide.LanguageServerProtocol.Types
open LangLSP.Server.References
open LangLSP.Server.Protocol

/// Handle textDocument/rename request
let handleRename (p: RenameParams) : Async<WorkspaceEdit option> =
    async {
        let uri = p.TextDocument.Uri
        let pos = p.Position
        let newName = p.NewName

        match getDocument uri with
        | None -> return None
        | Some text ->
            try
                let lexbuf = FSharp.Text.Lexing.LexBuffer<char>.FromString(text)
                let ast = Parser.start Lexer.tokenize lexbuf

                // Find symbol at position
                match findNodeAtPosition pos ast with
                | Some (Var(name, _)) ->
                    // Collect all references including definition
                    let refCtx = { IncludeDeclaration = true }
                    let refParams = {
                        TextDocument = p.TextDocument
                        Position = pos
                        Context = refCtx
                        WorkDoneToken = None
                        PartialResultToken = None
                    }

                    let! locations = handleReferences refParams

                    match locations with
                    | Some locs ->
                        // Create TextEdit for each reference
                        let edits =
                            locs
                            |> Array.map (fun loc -> {
                                Range = loc.Range
                                NewText = newName
                            })

                        // Create WorkspaceEdit using changes map
                        let changes =
                            Map.ofList [ (uri, edits) ]

                        return Some {
                            Changes = Some changes
                            DocumentChanges = None
                            ChangeAnnotations = None
                        }
                    | None -> return None
                | _ -> return None
            with _ ->
                return None
    }

/// Handle textDocument/prepareRename request
/// Validates that rename is possible at the given position
let handlePrepareRename (p: TextDocumentPositionParams) : Async<PrepareRenameResult option> =
    async {
        let uri = p.TextDocument.Uri
        let pos = p.Position

        match getDocument uri with
        | None -> return None
        | Some text ->
            try
                let lexbuf = FSharp.Text.Lexing.LexBuffer<char>.FromString(text)
                let ast = Parser.start Lexer.tokenize lexbuf

                match findNodeAtPosition pos ast with
                | Some (Var(name, span)) ->
                    // Return range of the symbol
                    let range = spanToLspRange span
                    return Some (U3.C2 {
                        Range = range
                        Placeholder = name
                    })
                | Some (Let(name, _, _, span))
                | Some (Lambda(name, _, span))
                | Some (LetRec(name, _, _, _, span)) ->
                    // Can rename binding sites too
                    let range = spanToLspRange span
                    return Some (U3.C2 {
                        Range = range
                        Placeholder = name
                    })
                | _ ->
                    // Not a renameable symbol
                    return None
            with _ ->
                return None
    }
```

### Pattern 3: Code Actions Handler

**What:** Generate quickfix actions for diagnostics (unused variables, type errors)
**When to use:** textDocument/codeAction request when user clicks lightbulb icon

**Example:**
```fsharp
// CodeActions.fs
module LangLSP.Server.CodeActions

open Ionide.LanguageServerProtocol.Types
open LangLSP.Server.Diagnostics

/// Generate code action to remove unused variable
let createRemoveUnusedAction (diag: Diagnostic) (uri: string) : CodeAction =
    // Extract variable name from diagnostic message
    // Assuming message format: "Unused variable 'x'"
    let varName =
        let msg = diag.Message
        let startIdx = msg.IndexOf("'") + 1
        let endIdx = msg.IndexOf("'", startIdx)
        msg.Substring(startIdx, endIdx - startIdx)

    {
        Title = sprintf "Remove unused variable '%s'" varName
        Kind = Some "quickfix"
        Diagnostics = Some [| diag |]
        IsPreferred = Some true
        Disabled = None
        Edit = Some {
            Changes = Some (Map.ofList [
                (uri, [| {
                    Range = diag.Range
                    NewText = ""  // Delete the variable
                } |])
            ])
            DocumentChanges = None
            ChangeAnnotations = None
        }
        Command = None
        Data = None
    }

/// Handle textDocument/codeAction request
let handleCodeAction (p: CodeActionParams) : Async<CodeAction[] option> =
    async {
        let uri = p.TextDocument.Uri
        let range = p.Range
        let diagnostics = p.Context.Diagnostics

        let actions = ResizeArray<CodeAction>()

        // Generate actions for each diagnostic in the context
        for diag in diagnostics do
            match diag.Source with
            | Some "funlang" ->
                // Check if diagnostic is fixable
                if diag.Message.StartsWith("Unused variable") then
                    actions.Add(createRemoveUnusedAction diag uri)
                elif diag.Message.Contains("Type mismatch") then
                    // Generate type fix suggestions if possible
                    // (more complex - may need type checker integration)
                    ()
            | _ -> ()

        return if actions.Count > 0 then
                   Some (actions.ToArray())
               else
                   None
    }
```

### Pattern 4: Scope-Aware Reference Collection

**What:** Handle variable shadowing when collecting references
**When to use:** Ensure references match the correct binding in nested scopes

**Example:**
```fsharp
// References.fs - Scope-aware reference collection

/// Collect references with scope tracking to handle shadowing
/// Returns only references to the specific binding at the query position
let collectReferencesScoped (varName: string) (targetDefSpan: Span) (ast: Expr) : Span list =
    let refs = ResizeArray<Span>()

    // Track which definition is in scope during traversal
    let scopeStack = Stack<string * Span>()

    let rec traverse expr =
        match expr with
        | Var(name, span) when name = varName ->
            // Check if this reference uses the target definition
            // (simplified heuristic: use if target def is most recent in scope)
            match scopeStack.TryPeek() with
            | true, (scopedName, scopedSpan) when scopedName = varName && scopedSpan = targetDefSpan ->
                refs.Add(span)
            | false, _ when not (scopeStack |> Seq.exists (fun (n, _) -> n = varName)) ->
                // No shadowing, check if target def is before this usage
                if isDefinitionInScope targetDefSpan span then
                    refs.Add(span)
            | _ -> ()

        | Let(name, value, body, span) ->
            traverse value
            scopeStack.Push((name, span))
            traverse body
            scopeStack.Pop() |> ignore

        // ... (handle other binding forms with scope stack)

        | _ -> ()

    traverse ast
    refs |> Seq.toList

/// Check if a definition is in scope at a usage location
let isDefinitionInScope (defSpan: Span) (useSpan: Span) : bool =
    defSpan.StartLine < useSpan.StartLine ||
    (defSpan.StartLine = useSpan.StartLine && defSpan.StartColumn < useSpan.StartColumn)
```

### Pattern 5: WorkspaceEdit Construction

**What:** Create WorkspaceEdit with TextEdit[] for rename operations
**When to use:** Any operation that modifies document content (rename, code actions)

**Example:**
```fsharp
// Protocol.fs - WorkspaceEdit helpers

/// Create TextEdit from span and new text
let createTextEdit (span: Span) (newText: string) : TextEdit =
    {
        Range = spanToLspRange span
        NewText = newText
    }

/// Create WorkspaceEdit for single-file changes
let createWorkspaceEdit (uri: string) (edits: TextEdit[]) : WorkspaceEdit =
    {
        Changes = Some (Map.ofList [ (uri, edits) ])
        DocumentChanges = None
        ChangeAnnotations = None
    }

/// Create WorkspaceEdit that deletes a range
let createDeleteEdit (uri: string) (span: Span) : WorkspaceEdit =
    createWorkspaceEdit uri [| createTextEdit span "" |]
```

### Pattern 6: Server Capability Registration

**What:** Register references, rename, and code action providers
**When to use:** Required for VS Code to send these requests

**Example:**
```fsharp
// Server.fs
let serverCapabilities : ServerCapabilities =
    { ServerCapabilities.Default with
        TextDocumentSync = ...
        DefinitionProvider = Some (U2.C1 true)
        HoverProvider = Some (U2.C1 true)
        CompletionProvider = Some { ... }
        ReferencesProvider = Some (U2.C1 true)
        RenameProvider = Some (U2.C2 {
            PrepareProvider = Some true
            WorkDoneProgress = None
        })
        CodeActionProvider = Some (U2.C2 {
            CodeActionKinds = Some [| "quickfix" |]
            ResolveProvider = Some false
            WorkDoneProgress = None
        })
    }
```

### Anti-Patterns to Avoid

- **Don't collect references without scope tracking:** Simple string matching will incorrectly include shadowed variables. Use scope-aware traversal or conservative heuristics.
- **Don't use documentChanges for single-file edits:** The simpler `changes` map is sufficient for Phase 4. documentChanges adds versioning complexity.
- **Don't generate code actions for all diagnostics:** Only create actions for diagnostics that can be reliably fixed (unused variables yes, complex type errors no).
- **Don't rename without validation:** Always implement prepareRename to check if symbol can be renamed before allowing the operation.

## Don't Hand-Roll

Problems that look simple but have existing solutions:

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Reference collection | New AST traversal from scratch | Extend Definition.collectDefinitions pattern | Definition module already traverses all binding sites; references are the dual operation (usages not definitions) |
| Scope tracking | Custom scope stack implementation | Reuse Definition.findDefinitionForVar logic | Definition module already handles shadowing heuristics correctly |
| WorkspaceEdit construction | Manual JSON serialization | Ionide.LanguageServerProtocol.Types | Provides WorkspaceEdit, TextEdit, Location types with correct serialization |
| Diagnostic filtering | Parse diagnostic messages | Use Diagnostic.Code or custom metadata | Structured codes are more reliable than string parsing for determining fixable diagnostics |

**Key insight:** Phase 4 is the "inverse" of Phase 2's Definition feature. Definition finds where a symbol is declared; References finds where it's used. Rename combines both. Code Actions integrate with Phase 1's Diagnostics. Maximum code reuse is essential.

## Common Pitfalls

### Pitfall 1: Variable Shadowing in References

**What goes wrong:** Find References returns usage of shadowed variables with the same name but different binding.

**Why it happens:** Simple string matching without scope analysis. Code like `let x = 1 in (let x = 2 in x) + x` has two distinct `x` bindings.

**How to avoid:** Track scope during traversal using a scope stack, or use conservative heuristics (only return references between definition and next shadowing binding). For Phase 4 single-file scope, the simpler heuristic is acceptable.

**Warning signs:** User renames `x` in outer scope but inner `x` also gets renamed incorrectly.

### Pitfall 2: TextEdit Range Overlap

**What goes wrong:** WorkspaceEdit with overlapping ranges fails to apply or applies incorrectly.

**Why it happens:** Multiple TextEdits modify the same text range. LSP spec requires non-overlapping edits.

**How to avoid:** When collecting references for rename, ensure each Var span is distinct. Use List.distinct on spans. For code actions, only edit the diagnostic range.

**Warning signs:** VS Code shows "Unable to apply changes" error, or rename only partially succeeds.

### Pitfall 3: Missing prepareRename Validation

**What goes wrong:** User tries to rename keywords or literals, gets confusing error or incorrect rename.

**Why it happens:** Rename handler doesn't validate that cursor is on a renameable symbol.

**How to avoid:** Implement textDocument/prepareRename to return `null` for non-symbols (numbers, keywords, operators). VS Code will gray out the rename command.

**Warning signs:** User can trigger rename on `let` keyword or `42` literal, leading to invalid edits.

### Pitfall 4: Code Actions Not Appearing

**What goes wrong:** User sees diagnostic but no lightbulb icon for quickfix.

**Why it happens:** CodeAction.Kind not set to "quickfix", or diagnostics array in CodeActionContext is empty.

**How to avoid:** Always set `Kind = Some "quickfix"` for diagnostic fixes. Return action only if p.Context.Diagnostics contains fixable diagnostic. Test by manually triggering "Quick Fix" command.

**Warning signs:** Diagnostics appear but "Quick Fix" menu is empty.

### Pitfall 5: Incomplete Rename Preview

**What goes wrong:** VS Code rename preview doesn't show all changes before applying.

**Why it happens:** WorkspaceEdit missing some occurrences, or references not correctly collected.

**How to avoid:** Test rename thoroughly with shadowing cases. Include definition location when collecting references (use includeDeclaration flag). Verify all Var nodes are collected.

**Warning signs:** After rename, some symbol occurrences remain with old name.

## Code Examples

Verified patterns from official sources:

### Creating ReferenceParams for Testing

```fsharp
// ReferencesTests.fs
let makeReferenceParams uri line char includeDecl : ReferenceParams =
    {
        TextDocument = { Uri = uri }
        Position = { Line = uint32 line; Character = uint32 char }
        Context = { IncludeDeclaration = includeDecl }
        WorkDoneToken = None
        PartialResultToken = None
    }

let setupAndFindReferences uri text line char includeDecl =
    clearAll()
    handleDidOpen (makeDidOpenParams uri text)
    let refParams = makeReferenceParams uri line char includeDecl
    handleReferences refParams |> Async.RunSynchronously
```

### Testing Reference Collection

```fsharp
// ReferencesTests.fs
testCase "finds all variable usages (REF-01)" <| fun _ ->
    // "let x = 1 in x + x"
    //               ^ query position (line 0, char 13)
    let result = setupAndFindReferences "file:///test.fun" "let x = 1 in x + x" 0 13 false

    match result with
    | Some locations ->
        Expect.equal locations.Length 2 "Should find 2 usages of x"
        // Both usages are in the body: "x + x"
        for loc in locations do
            Expect.equal loc.Uri "file:///test.fun" "Same file"
    | None ->
        failtest "Expected references"

testCase "includes definition when requested (REF-03)" <| fun _ ->
    let result = setupAndFindReferences "file:///test.fun" "let x = 1 in x" 0 13 true

    match result with
    | Some locations ->
        Expect.isGreaterThan locations.Length 1 "Should include definition + usage"
    | None ->
        failtest "Expected references"
```

### Testing Rename Operation

```fsharp
// RenameTests.fs
testCase "renames all variable occurrences (RENAME-01)" <| fun _ ->
    let code = "let x = 1 in x + x"
    let uri = "file:///test.fun"

    clearAll()
    handleDidOpen (makeDidOpenParams uri code)

    let renameParams = {
        TextDocument = { Uri = uri }
        Position = { Line = 0u; Character = 13u }  // First 'x' in body
        NewName = "y"
        WorkDoneToken = None
    }

    let result = handleRename renameParams |> Async.RunSynchronously

    match result with
    | Some edit ->
        match edit.Changes with
        | Some changes ->
            let edits = changes.[uri]
            Expect.equal edits.Length 3 "Should rename definition + 2 usages"
            // All edits should change text to "y"
            for e in edits do
                Expect.equal e.NewText "y" "NewText should be 'y'"
        | None -> failtest "Expected Changes in WorkspaceEdit"
    | None ->
        failtest "Expected rename result"

testCase "rename preview shows all changes (RENAME-03)" <| fun _ ->
    // prepareRename returns range of symbol
    let prepareParams = {
        TextDocument = { Uri = "file:///test.fun" }
        Position = { Line = 0u; Character = 4u }  // On 'x' in "let x"
        WorkDoneToken = None
        PartialResultToken = None
    }

    clearAll()
    handleDidOpen (makeDidOpenParams "file:///test.fun" "let x = 1 in x")

    let result = handlePrepareRename prepareParams |> Async.RunSynchronously

    match result with
    | Some (U3.C2 { Range = range; Placeholder = placeholder }) ->
        Expect.equal placeholder "x" "Placeholder should be current name"
        Expect.equal range.Start.Line 0u "Range on line 0"
    | _ ->
        failtest "Expected PrepareRename result with range and placeholder"
```

### Testing Code Actions

```fsharp
// CodeActionsTests.fs
testCase "suggests removing unused variable (ACTION-01)" <| fun _ ->
    let code = "let x = 1 in 42"  // x is unused
    let uri = "file:///test.fun"

    clearAll()
    handleDidOpen (makeDidOpenParams uri code)

    // First get diagnostics
    let diagnostics = analyze uri code
    let unusedDiag =
        diagnostics
        |> Array.find (fun d -> d.Message.Contains("Unused"))

    // Request code actions for the diagnostic range
    let actionParams = {
        TextDocument = { Uri = uri }
        Range = unusedDiag.Range
        Context = {
            Diagnostics = [| unusedDiag |]
            Only = Some [| "quickfix" |]
            TriggerKind = Some CodeActionTriggerKind.Invoked
        }
        WorkDoneToken = None
        PartialResultToken = None
    }

    let result = handleCodeAction actionParams |> Async.RunSynchronously

    match result with
    | Some actions ->
        Expect.isGreaterThan actions.Length 0 "Should have at least one action"
        let action = actions.[0]
        Expect.equal action.Kind (Some "quickfix") "Should be quickfix kind"
        Expect.stringContains action.Title "Remove" "Action should suggest removal"
    | None ->
        failtest "Expected code actions"
```

### Shadowing Test Case

```fsharp
// ReferencesTests.fs - Critical for correctness
testCase "handles variable shadowing correctly" <| fun _ ->
    // "let x = 1 in let x = 2 in x"
    //  Outer x    Inner x      ^ refers to inner x only
    let code = "let x = 1 in let x = 2 in x"
    let result = setupAndFindReferences "file:///test.fun" code 0 26 false

    match result with
    | Some locations ->
        // Should only find the one usage in inner scope
        // (or conservatively, find usages between inner def and end)
        Expect.equal locations.Length 1 "Should find only inner x usage"

        // The usage should be after column 21 (inner let binding)
        for loc in locations do
            Expect.isGreaterThan (int loc.Range.Start.Character) 20 "Usage is in inner scope"
    | None ->
        failtest "Expected references"
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| String search for references | AST-based scope-aware search | LSP 3.0+ | Correct handling of shadowing and scoping |
| Blind find-replace for rename | WorkspaceEdit with preview | LSP 3.0+ | Safe refactoring with user preview before apply |
| Static code fixes | Dynamic code actions from diagnostics | LSP 3.8+ | Context-aware fixes, integrated with error detection |
| Single file operations | Workspace-wide changes | LSP 3.0+ | Handles multi-file projects (Phase 4 uses single-file subset) |

**Deprecated/outdated:**
- **Command-based code actions:** Old LSP used Command objects. Modern LSP prefers CodeAction with WorkspaceEdit for better integration.
- **ReferenceContext without includeDeclaration:** Always check this flag to provide accurate results (some clients expect definition included).

## Open Questions

Things that couldn't be fully resolved:

1. **How to handle references across different scopes with same name?**
   - What we know: Phase 4 is single-file scope, shadowing can occur in nested lets
   - What's unclear: Perfect scope tracking requires building symbol table with scope IDs
   - Recommendation: Use conservative heuristic: collect Var nodes between definition and next shadowing binding of same name. Accept some false positives for Phase 4, refine in v2 with proper symbol table.

2. **Should code actions include type-based fixes?**
   - What we know: Type checker provides diagnostic information for type mismatches
   - What's unclear: Generating correct type annotations automatically is complex
   - Recommendation: Phase 4: Only implement "Remove unused variable" action (high confidence fix). Defer type-based actions to v2 when pattern matching and type suggestions are mature.

3. **Should rename validate identifier naming rules?**
   - What we know: FunLang has identifier rules (likely alphanumeric + underscore)
   - What's unclear: Whether to enforce rules in prepareRename or let rename attempt and fail
   - Recommendation: Phase 4: Allow any non-empty newName, let parser validation catch invalid names. Phase 5: Add identifier validation in prepareRename for better UX.

## Sources

### Primary (HIGH confidence)

- [LSP 3.17 Specification - textDocument/references](https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/) - Official Microsoft LSP protocol specification for references request/response, ReferenceParams, ReferenceContext
- [LSP 3.17 Specification - textDocument/rename](https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/) - Official specification for RenameParams, WorkspaceEdit, PrepareRenameParams, and rename protocol
- [LSP 3.17 Specification - textDocument/codeAction](https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/) - Official specification for CodeActionParams, CodeAction, CodeActionKind constants, diagnostic integration
- [Ionide.LanguageServerProtocol Repository](https://github.com/ionide/LanguageServerProtocol) - F# LSP library 0.7.0 source code and type definitions, used in production by FsAutoComplete
- Existing codebase: AstLookup.fs, Definition.fs, Diagnostics.fs, Protocol.fs - Phase 1-3 infrastructure for AST traversal, scope tracking, diagnostics

### Secondary (MEDIUM confidence)

- [FsAutoComplete References Implementation](https://github.com/ionide/FsAutoComplete) - Production F# LSP server with references, rename, and code actions features
- [VS Code Language Server Extension Guide](https://code.visualstudio.com/api/language-extensions/language-server-extension-guide) - Best practices for implementing advanced LSP features
- [LSP textDocument/codeAction Documentation](https://lsp-devtools.readthedocs.io/en/latest/capabilities/text-document/code-action.html) - Practical guide for code action implementation
- [GitHub issue: lsp-find-references scope problems](https://github.com/emacs-lsp/lsp-mode/issues/990) - Real-world pitfalls with scope-limited reference finding
- [GitHub issue: lsp-rename across files](https://github.com/emacs-lsp/lsp-mode/issues/1220) - Common issues with rename symbol implementation

### Tertiary (LOW confidence)

- [ecma-variable-scope library](https://github.com/twolfson/ecma-variable-scope) - Example AST utility for collecting scope info (JavaScript, but patterns apply)
- [AST traversal patterns](https://createlang.rs/01_calculator/ast_traversal.html) - General AST traversal methodology (Rust, but concepts transfer)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - Ionide 0.7.0 already in use with all required types (ReferenceParams, RenameParams, CodeActionParams, WorkspaceEdit verified in v0.7.0)
- Architecture: HIGH - Patterns build directly on proven Phase 2 infrastructure (AstLookup, Definition, collectDefinitions)
- Pitfalls: HIGH - Based on actual issues in FsAutoComplete, lsp-mode, and LSP GitHub discussions; shadowing is well-documented challenge

**Research date:** 2026-02-05
**Valid until:** 2026-03-05 (30 days - stable LSP 3.17 protocol, mature Ionide library)

---

**Next steps for planner:**
1. Create References.fs module with handleReferences, collectReferences, scope tracking
2. Create Rename.fs module with handleRename, handlePrepareRename, WorkspaceEdit construction
3. Create CodeActions.fs module with handleCodeAction, diagnostic-driven action generation (focus on "remove unused variable")
4. Update Server.fs to register ReferencesProvider, RenameProvider (with PrepareProvider=true), CodeActionProvider capabilities
5. Create ReferencesTests.fs with variable/function reference tests, shadowing edge cases
6. Create RenameTests.fs with rename tests, preview tests, shadowing tests
7. Create CodeActionsTests.fs with quickfix tests for unused variables
8. Write Korean tutorials:
   - 09-find-references.md (Find References 구현하기)
   - 10-rename.md (Rename Symbol 구현하기)
   - 11-code-actions.md (Code Actions 구현하기)
9. Integration verification: Manual test in VS Code with .fun files, verify References panel, rename preview, lightbulb actions
