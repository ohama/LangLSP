# Feature Landscape: LSP Servers

**Domain:** Language Server Protocol (LSP) implementations
**Researched:** 2026-02-03
**Confidence:** HIGH

## Table Stakes

Features users expect from ANY language server. Missing = language feels unsupported in the editor.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| **Diagnostics** | Core LSP value proposition — real-time error checking is the #1 reason to use LSP over basic syntax highlighting | Medium | Requires parser + semantic analysis. Push-based (`textDocument/publishDiagnostics`) is standard. Pull-based is LSP 3.17+ but not expected by users yet. |
| **Hover** | "What is this symbol?" is fundamental to code understanding. Users expect type/doc info on hover in modern editors | Low-Medium | Requires symbol table lookup. For typed languages, showing inferred types is critical. Format supports markdown. |
| **Completion** | Users expect suggestions for keywords, symbols, and snippets. Autocomplete is baseline IDE functionality | Medium-High | Basic keyword completion is easy. Context-aware symbol completion requires scope tracking. `completionItem/resolve` for lazy details is optimization. |
| **Go to Definition** | "Where is this defined?" is the most-used navigation feature. Distinguishes code editors from text editors | Medium | Requires symbol table with location tracking. Multi-file support adds complexity. `LocationLink` preferred over `Location` for better UX. |
| **Document Synchronization** | Foundation for all features — server must track document state correctly | Low | `textDocument/didOpen`, `didChange`, `didClose` are mandatory. Incremental sync is optimization. |

## Differentiators

Features that elevate a language server from "working" to "excellent". Not expected by beginners, but valued by power users.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| **Find References** | "Where is this used?" — essential for refactoring and code understanding | Medium-High | Requires full project indexing. Expensive for large codebases. Pairs with "Go to Definition". |
| **Rename Symbol** | Safe refactoring across files. High-value for production codebases | High | Requires references + validation. Must handle scope correctly to avoid breaking code. `textDocument/prepareRename` prevents invalid renames. |
| **Signature Help** | Shows function parameter info while typing. Huge UX win for complex APIs | Medium | Requires function signature tracking. Must trigger on `(`, `,`. Multiple overloads add complexity. |
| **Document Symbols** | Outline view, breadcrumbs, quick navigation within file | Medium | Requires AST traversal. Hierarchical structure (`DocumentSymbol`) preferred over flat list. |
| **Workspace Symbols** | Project-wide symbol search (Ctrl+T in VS Code). Power user feature | High | Requires full workspace indexing. Performance-critical for large projects. |
| **Formatting** | Auto-formatting on save. Expected for languages with official formatters | Medium-High | Complex for custom languages. Often delegated to external formatter. `textDocument/formatting` and `rangeFormatting`. |
| **Code Actions** | Quick fixes, refactorings. "Lightbulb" feature in VS Code | High | Requires identifying fixable issues + generating edits. Very language-specific. |
| **Semantic Tokens** | Better syntax highlighting based on semantic info (e.g., distinguish parameter from local variable) | High | LSP 3.16+. Replaces TextMate grammars. Requires tokenizing entire document. Performance-sensitive. |
| **Inlay Hints** | Inline type annotations, parameter names. Popular in Rust/TypeScript | Medium-High | LSP 3.17+. Requires type inference. Can clutter UI if overused. Must be toggleable. |
| **Code Lens** | Inline actionable metadata (e.g., "5 references", "Run test") | Medium | Requires custom logic per use case. Performance impact on large files. |

## Anti-Features

Features to deliberately NOT build for tutorial/MVP LSP servers. Common mistakes in this domain.

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| **Supporting Every LSP 3.17 Feature** | LSP has 50+ capabilities. Implementing all is unrealistic and dilutes focus. | Pick 4-6 core features. Rust-analyzer took years to reach feature completeness. |
| **Full Workspace Indexing Upfront** | Indexing entire projects on startup causes multi-second delays. Users hate slow initialization. | Start with single-file analysis. Add incremental indexing later. Use caching. |
| **Perfect Type Inference for Hover** | Type inference is a PhD-level topic. Getting it 100% correct is a rabbit hole. | Show "best effort" types. Fallback to syntax-based hints. Users accept approximations in young LSPs. |
| **Multi-File Analysis in V1** | Cross-file dependencies explode complexity. Import resolution, module systems, build configs all differ. | Limit to single-file for MVP. Add multi-file in V2 when single-file is solid. |
| **Custom Protocol Extensions** | Extending LSP with proprietary features locks you into specific editors. | Use standard LSP features only. If needed, contribute extensions upstream to LSP spec. |
| **Synchronous Blocking Operations** | Blocking the editor during analysis kills responsiveness. Users close laggy editors. | Use async/background processing. Return partial results. LSP supports `$/progress` for long operations. |
| **Over-Aggressive Completion** | Triggering completion on every keystroke or showing 100+ suggestions overwhelms users. | Trigger on specific chars (`.`, `::`). Limit suggestions to 20-30. Use fuzzy filtering. |
| **Formatting Without Configuration** | Auto-formatting is opinionated. Forcing a style angers users. | Make formatting opt-in. Support `.editorconfig`. Provide escape hatches. |

## Feature Dependencies

```
Foundation Layer:
  Document Synchronization
    ↓
  Parser + AST

Core Analysis:
  Parser → Symbol Table → Diagnostics
          ↓            ↓
          Hover        Completion
          ↓
          Go to Definition

Advanced Navigation:
  Symbol Table + Cross-File Analysis
    ↓
  Find References → Rename Symbol
                   ↓
                   Code Actions

Enhanced UX:
  Type System → Signature Help
              ↓
              Inlay Hints

  AST Traversal → Document Symbols → Workspace Symbols
                ↓
                Folding Ranges

Semantic Analysis:
  Full Type Inference → Semantic Tokens
                      → Inlay Hints (parameter names, inferred types)
```

**Critical Dependencies:**
- **Diagnostics, Hover, Completion, Go to Definition** all require symbol table → Build symbol table first
- **Find References** requires **Go to Definition** infrastructure (location tracking)
- **Rename** requires **Find References** (must find all uses to rename safely)
- **Semantic Tokens** requires full semantic analysis → Expensive, defer to V2+

## MVP Recommendation for FunLang Tutorial LSP

**Context:** Tutorial for LSP beginners, FunLang (ML-style functional language with type inference, pattern matching)

### Phase 1: Foundation (MUST HAVE)
1. **Document Synchronization** - Get server connected to VS Code
2. **Basic Diagnostics** - Syntax errors from existing parser
3. **Simple Hover** - Show raw AST node type (proves symbol lookup works)

**Rationale:** Establishes end-to-end flow (editor ↔ server ↔ language analysis). Users see immediate value (red squiggles).

### Phase 2: Core Navigation (TABLE STAKES)
4. **Completion** - Keywords + in-scope symbols (leverage FunLang's type environment)
5. **Go to Definition** - Local definitions first, then cross-function

**Rationale:** Completion + Go to Definition are the features users use 100x/day. Missing these makes LSP feel incomplete.

### Phase 3: Enhanced Analysis (DIFFERENTIATOR)
6. **Type-Aware Hover** - Show inferred types (leverage FunLang's Hindley-Milner type checker)
7. **Enhanced Diagnostics** - Type errors, pattern match exhaustiveness warnings

**Rationale:** FunLang has a type checker — USE IT. Showing inferred types demonstrates LSP value for typed functional languages. This is the tutorial's "wow" moment.

### Defer to Post-Tutorial
- **Find References**: High complexity, requires full project indexing
- **Rename**: Needs Find References first
- **Signature Help**: Moderate value, high implementation cost for FunLang's curried functions
- **Code Actions**: Too language-specific, would dominate tutorial
- **Semantic Tokens**: LSP 3.16+, overkill for beginner tutorial
- **Inlay Hints**: LSP 3.17+, cool but not essential for learning LSP basics

## Functional Language Considerations

**FunLang-specific features that provide exceptional value:**

| Feature | Why Important for Functional Languages |
|---------|----------------------------------------|
| **Type Inference in Hover** | Users don't write type annotations in ML-style languages — hover is the ONLY way to see inferred types. Critical. |
| **Pattern Match Analysis** | Exhaustiveness checking, redundant pattern warnings. High-value diagnostics unique to functional languages. |
| **Type Holes** | OCaml/Haskell users expect `_` placeholders to show "expected type here" in diagnostics. Great teaching tool. |
| **Scope-Aware Completion** | Functional languages have lexical scope + closures. Showing only in-scope bindings demonstrates understanding of semantics. |

**Anti-patterns for functional LSPs:**
- Don't try to implement HM type inference from scratch — reuse FunLang's existing type checker
- Don't show completion for out-of-scope bindings — functional programmers expect precision
- Don't skip location tracking in `let` bindings — definition tracking must handle nested `let`s correctly

## Feature Complexity Estimates (Tutorial Context)

**Low Complexity (1-2 tutorial chapters):**
- Document synchronization
- Basic diagnostics (reuse parser errors)
- Simple hover (AST node type)

**Medium Complexity (2-3 chapters):**
- Keyword completion
- Symbol table with location tracking
- Go to Definition (single file)
- Type-aware hover (reuse type checker)

**High Complexity (4+ chapters or out of scope):**
- Context-aware completion (filtering by type)
- Cross-file analysis
- Find References
- Rename
- Code Actions

## Sources

**Official LSP Specification:**
- [LSP 3.17 Specification](https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/) - Comprehensive capability reference
- [Language Server Protocol Official Site](https://microsoft.github.io/language-server-protocol/) - Overview and getting started

**Tutorial Resources:**
- [VS Code Language Server Extension Guide](https://code.visualstudio.com/api/language-extensions/language-server-extension-guide) - Official Microsoft tutorial
- [Prefab: LSP Tutorial - Building Custom Auto-Complete](https://prefab.cloud/blog/lsp-language-server-from-zero-to-completion/) - Practical beginner tutorial
- [Toptal: LSP Tutorial (2026)](https://www.toptal.com/javascript/language-server-protocol-tutorial) - Updated January 2026

**Implementation Guidance:**
- [Strumenta: Go To Definition in LSP](https://tomassetti.me/go-to-definition-in-the-language-server-protocol/) - Deep dive on implementation challenges
- [Medium: Understanding LSP](https://medium.com/@malintha1996/understanding-the-language-server-protocol-5c0ba3ac83d2) - Protocol concepts

**Functional Language Servers:**
- [Merlin: Language Server for OCaml (Experience Report)](https://dl.acm.org/doi/pdf/10.1145/3236798) - Academic paper on functional LSP challenges
- [Haskell Language Server Features](https://joshrotenberg.com/rust-analyzer/features/index.html) - Feature comparison
- [Typed Holes in GHC](https://downloads.haskell.org/~ghc/7.10.1/docs/html/users_guide/typed-holes.html) - Functional language feature reference

**Domain Knowledge:**
- [LSP Overview - Compile7](https://compile7.org/decompile/language-server-protocol-overview) - Protocol architecture
- [dbt Labs: Understanding LSP](https://www.getdbt.com/blog/language-server-protocol) - Use cases and value proposition
