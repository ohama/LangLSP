# Project Research Summary

**Project:** FunLang LSP
**Domain:** Language Server Protocol (LSP) Implementation for Functional Language
**Researched:** 2026-02-03
**Confidence:** HIGH

## Executive Summary

FunLang LSP is a dual-purpose project: building a working Language Server for the FunLang functional language while creating a comprehensive Korean tutorial for LSP beginners. Research shows that successful LSP implementations follow a strict layered architecture (Protocol → Document Management → Language Services → Analysis Engine) and prioritize incremental computation with aggressive caching. The recommended stack centers on Ionide.LanguageServerProtocol (F#-native, LSP 3.17 compliant) with .NET 10 LTS, leveraging FunLang's existing F# compiler components (Lexer, Parser, Type Checker) to avoid reimplementing language analysis.

The critical insight from domain research is that LSP servers must balance completeness with scope discipline. While the LSP 3.17 specification defines 50+ capabilities, production implementations succeed by shipping value incrementally: start with diagnostics and hover (proving end-to-end flow), add completion and go-to-definition (table stakes features), then optimize performance before considering advanced features. For FunLang specifically, type inference adds unique complexity that demands caching from day one, and the tutorial context requires resisting scope creep—teaching 4-5 core features deeply beats superficially covering everything.

Key risks center on performance pitfalls. Research identified catastrophic issues in production LSPs: Deno's LSP had 8+ second hover delays from excessive cross-boundary synchronization, and naive type inference implementations cause exponential complexity growth. Mitigation strategies include incremental text synchronization, cached AST/type information with dirty-tracking invalidation, and debounced diagnostics (200-300ms). The recommended phased approach builds foundation (text sync, basic diagnostics) before navigation (hover, go-to-definition) before optimization (caching, incremental parsing), ensuring each phase delivers user value while validating architectural decisions.

## Key Findings

### Recommended Stack

Research strongly favors F#-native tooling to maximize reuse of FunLang's existing compiler components. Ionide.LanguageServerProtocol (v0.7.0) emerged as the clear choice: actively maintained (released March 2025), production-proven in FsAutoComplete, supports LSP 3.17 specification, and provides idiomatic F# abstractions over JSON-RPC transport. Alternatives like OmniSharp.Extensions.LanguageServer add C#-centric dependency injection overhead unsuitable for functional code, while custom LSP implementations risk spec compliance issues and duplicate solved problems.

**Core technologies:**
- **Ionide.LanguageServerProtocol 0.7.0**: LSP protocol implementation — F#-native, actively maintained, used by FsAutoComplete, lightweight and functional
- **.NET 10.0 LTS**: Runtime platform — Long-term support until Nov 2028, ships with F# 10, matches FunLang's existing compiler stack
- **FSharp.Compiler.Service 43.10.102**: Compiler API patterns — While not directly used for FunLang, provides reference patterns for type checking and analysis applicable to LSP features
- **Expecto 10.2.3+**: Unit testing framework — F#-native, parallel and async by default, functional API matches LSP's asynchronous nature
- **vscode-languageclient 10.x+**: VS Code LSP client — Official Microsoft library, handles stdio communication, supports LSP 3.17+

**Avoid:**
- System.Text.Json (Ionide.LanguageServerProtocol requires Newtonsoft.Json)
- OmniSharp.Extensions.LanguageServer (C#-first design, dependency injection overhead)
- Custom LSP implementation (spec compliance risk, duplicates solved problems)
- FAKE build system (overkill for single-project LSP server)
- .NET 9 STS (ends Nov 2026; use .NET 10 LTS ending Nov 2028)

### Expected Features

LSP feature research reveals a clear hierarchy: table stakes features users expect from any language server, differentiators that elevate quality, and anti-features that waste effort or harm usability. For tutorial purposes and MVP scope, focusing on 4-5 core features delivers maximum learning value without overwhelming beginners.

**Must have (table stakes):**
- **Diagnostics**: Real-time error checking is the #1 LSP value proposition; users expect red squiggles for syntax/type errors
- **Hover**: Type information on hover is fundamental to code understanding, especially for type-inferred languages like FunLang
- **Completion**: Keyword and symbol suggestions are baseline IDE functionality; context-aware completion is expected
- **Go to Definition**: "Where is this defined?" is the most-used navigation feature, distinguishes IDEs from text editors
- **Document Synchronization**: Foundation for all features; must track document state correctly (incremental sync preferred for performance)

**Should have (competitive differentiators):**
- **Type-Aware Hover**: Show inferred types from FunLang's Hindley-Milner type checker (tutorial's "wow" moment)
- **Enhanced Diagnostics**: Pattern match exhaustiveness warnings, type holes diagnostics (functional language advantage)
- **Signature Help**: Function parameter info while typing (moderate value, high implementation cost for curried functions)
- **Document Symbols**: Outline view and breadcrumbs for navigation (medium complexity)

**Defer (v2+):**
- **Find References**: High complexity, requires full project indexing
- **Rename Symbol**: Needs Find References first, complex scope validation
- **Code Actions**: Too language-specific, would dominate tutorial scope
- **Semantic Tokens**: LSP 3.16+ feature, overkill for beginner tutorial
- **Inlay Hints**: LSP 3.17+ feature, cool but not essential for learning LSP basics

**Anti-features to avoid:**
- Supporting every LSP 3.17 capability upfront (scope creep kills MVP delivery)
- Full workspace indexing on startup (multi-second delays users hate)
- Multi-file analysis in V1 (explodes complexity; single-file MVP first)
- Custom protocol extensions (locks into specific editors; use standard LSP only)

### Architecture Approach

LSP servers follow a layered client-server architecture with strict separation of concerns. Production implementations (FsAutoComplete, Deno LSP, TypeScript Language Server) all converge on the same pattern: Protocol Layer (JSON-RPC transport, lifecycle management) → Document Manager (text synchronization, version tracking) → Language Services (feature providers) → Analysis Engine (compiler integration). This separation enables reuse of compiler components, simplifies testing, and isolates fault domains.

**Major components:**

1. **Protocol Layer** — Handles JSON-RPC over stdio, LSP message routing, initialization/shutdown lifecycle, capability negotiation. Uses Ionide.LanguageServerProtocol for protocol handling, never mixes protocol code with language analysis logic.

2. **Document Manager** — Single source of truth for document state. Tracks open files, applies incremental text changes, manages document versions (detects out-of-order messages), invalidates affected cache entries, triggers diagnostics on change. Prevents race conditions between editor and server.

3. **Language Services** — Implements LSP feature providers (diagnostics, hover, completion, definition). Translates LSP requests to compiler API calls. Formats compiler results as LSP responses. Thin translation layer between LSP types (Position, Range) and FunLang types (Ast.Pos).

4. **Analysis Engine** — Integrates FunLang compiler components (Lexer.fs, Parser.fs, TypeCheck.fs, Infer.fs). Builds and caches AST, runs type inference, queries symbol tables. Pure compiler logic, no LSP dependencies. Implements incremental computation with dirty-tracking to avoid exponential type inference costs.

5. **Cache Layer** — Stores expensive computations (parsed AST, type information, symbol tables). Invalidates only affected entries on document changes (file-level granularity for MVP, function-level for optimization). Critical for performance: reduces hover latency from 8+ seconds to <100ms (Deno LSP case study).

**Key patterns:**
- Incremental text synchronization (editor sends deltas, not full documents)
- Lazy computation with caching (only parse/type-check when requested, cache results)
- Asynchronous request handling (long operations run in background, support cancellation)
- Separate process isolation (server crash doesn't crash editor, language independence)

**Anti-patterns to avoid:**
- Tight coupling between protocol and compiler (makes compiler non-reusable)
- Full document sync for large files (300KB JSON per keystroke kills performance)
- Synchronous blocking operations (server can't respond to other requests while blocked)
- No caching (re-parsing on every hover request is unusable)
- Ignoring document versions (out-of-order messages cause state desync)

### Critical Pitfalls

Research from production LSP implementations and postmortems identified 14 pitfalls across three severity levels. Top five critical pitfalls that cause rewrites or major issues:

1. **Type Inference Performance Explosion** — FunLang's Hindley-Milner type inference is computationally expensive. Naive implementations re-analyze entire dependency graphs on every keystroke, causing exponential complexity. Prevention: implement incremental type checking from the start, cache inference results per function/expression, use dirty-tracking to only re-check changed definitions, set complexity limits with graceful degradation.

2. **Incorrect Concurrency Model** — Processing notifications asynchronously causes client/server state drift; processing everything synchronously makes cancellation impossible. LSP spec requires: notifications processed synchronously (in order), requests processed concurrently (support cancellation). Test with rapid document changes to catch race conditions early.

3. **File URI Handling Across Platforms** — Malformed URIs break cross-platform compatibility. Common mistakes: `file://C:\path` instead of `file:///C:/path`, not URL-encoding spaces/UTF-8, case-sensitivity issues. Prevention: use System.Uri for all URI operations, test on both Windows and Linux from day one, always URL-encode special characters.

4. **Protocol Compliance and Capability Mismatches** — Server claims capabilities it doesn't implement, or doesn't implement required client requests (workspace/configuration, client/registerCapability). Causes initialization failures or empty results. Prevention: accurately declare capabilities in initialize response, test with multiple clients (VS Code, Neovim, Emacs), return proper error codes.

5. **Scope Creep Before MVP** — Attempting to implement all 50+ LSP capabilities before basic functionality works. Project stalls before delivering value, tutorial becomes too complex. Prevention: phase-based implementation (diagnostics → hover → completion → optimize → advanced features), ship usable LSP with 4-5 core features first, measure by user value not feature completeness.

**Additional high-risk pitfalls for FunLang:**
- Over-eager diagnostic publishing (debounce 200-300ms to avoid flickering errors and CPU spikes)
- Ignoring incremental parsing (re-parsing entire file on every keystroke scales O(n) per keystroke)
- Missing cancellation token handling (long operations don't stop when user cancels, wastes resources)

## Implications for Roadmap

Based on research findings, a phased implementation approach emerges clearly from dependency analysis and risk mitigation strategies. The recommended structure frontloads foundation and table-stakes features, defers optimization until value is proven, and saves advanced features for post-MVP.

### Phase 1: Foundation (LSP Infrastructure)
**Rationale:** Establish end-to-end flow before implementing features. Proves that VS Code can communicate with F# server, document synchronization works, and basic compilation pipeline integrates correctly. Research shows this "hello world" phase catches 70% of infrastructure bugs (URI handling, capability negotiation, process lifecycle).

**Delivers:** VS Code extension connects to F# LSP server, opens .fun files, tracks document changes, displays basic diagnostics from existing TypeCheck.fs.

**Addresses:**
- Document Synchronization (table stakes feature)
- Basic Diagnostics (table stakes feature, highest-value first feature)
- Protocol layer setup (uses Ionide.LanguageServerProtocol)
- File URI handling (Pitfall 3 mitigation)
- Capability negotiation (Pitfall 5 mitigation)

**Avoids:**
- Incorrect concurrency model (Pitfall 2): establish notification ordering from start
- Protocol compliance issues (Pitfall 5): implement initialize/shutdown lifecycle correctly
- URI handling bugs (Pitfall 3): use System.Uri, test Windows + Linux

**Research flag:** Standard patterns, skip research-phase. LSP initialization is well-documented in VS Code guides and Ionide.LanguageServerProtocol examples.

---

### Phase 2: Core Navigation (Hover + Go to Definition)
**Rationale:** Builds on Phase 1's diagnostic infrastructure by adding type information display. These features prove the symbol table and type inference integration works correctly. Hover is low-hanging fruit (type info already computed for diagnostics), go-to-definition adds location tracking.

**Delivers:** Hovering over symbols shows inferred types from Hindley-Milner type checker, clicking "Go to Definition" jumps to where symbol is defined (single-file scope initially).

**Addresses:**
- Hover (table stakes feature)
- Go to Definition (table stakes feature)
- Type-aware hover (differentiator for functional languages)
- Symbol table construction
- Position → AST node mapping

**Avoids:**
- Type inference performance explosion (Pitfall 4): cache type inference results, implement dirty-tracking
- Synchronization overhead (Pitfall 1): measure cross-boundary call frequency

**Research flag:** Standard patterns, skip research-phase. Hover and go-to-definition are extensively documented in LSP guides.

---

### Phase 3: Completion (Keywords + Symbols)
**Rationale:** Completion requires robust symbol table and scope analysis (built in Phase 2). More complex than hover because it's context-aware (what's in scope at cursor position?) and performance-sensitive (triggered frequently). Enables switching to incremental text sync (performance optimization).

**Delivers:** Typing shows keyword suggestions and in-scope symbols, filtered by context. Incremental text synchronization reduces bandwidth and enables partial re-parsing.

**Addresses:**
- Completion (table stakes feature)
- Scope analysis (which symbols are visible at position)
- Incremental sync upgrade (textDocumentSync: 2)
- Context-aware filtering

**Avoids:**
- Over-eager completion (anti-feature): trigger on specific chars only, limit suggestions to 20-30
- Ignoring incremental parsing (Pitfall 7): switch to incremental sync in this phase

**Research flag:** Standard patterns, skip research-phase. Completion is well-documented, though context-aware filtering for functional languages may need iteration.

---

### Phase 4: Performance & Polish (Caching + Optimization)
**Rationale:** Phases 1-3 prove features work; Phase 4 makes them fast. Research shows premature optimization is harmful—build features first, then profile and optimize based on real bottlenecks. Target: sub-100ms latency for hover/completion.

**Delivers:** Cached AST and type information, smart invalidation (only affected files), debounced diagnostics (200-300ms), async operations for slow requests. Server feels instant even in large files.

**Addresses:**
- Cache layer implementation
- Diagnostic debouncing (Pitfall 6 mitigation)
- Cancellation token support (Pitfall 8 mitigation)
- Performance benchmarking

**Avoids:**
- Type inference explosion (Pitfall 4): aggressive caching with dirty-tracking prevents exponential growth
- Over-eager diagnostics (Pitfall 6): debounce to avoid flickering and CPU spikes
- Missing cancellation (Pitfall 8): thread cancellation tokens through async operations

**Research flag:** May need research-phase for incremental type checking strategies. Caching invalidation for type inference in functional languages is domain-specific.

---

### Phase 5: Tutorial Documentation (Korean Guide)
**Rationale:** Tutorial creation happens in parallel with implementation but consolidates after Phases 1-4 are complete. Each implementation phase produces tutorial content, but final tutorial needs polish, screenshots, and validation that beginners can follow it.

**Delivers:** Step-by-step Korean tutorial covering LSP basics, F# project setup, each feature implementation (diagnostics, hover, completion, go-to-definition), VS Code extension packaging, .vsix installation.

**Addresses:**
- LSP conceptual overview
- Implementation walkthroughs for each feature
- F# and Ionide.LanguageServerProtocol explanations
- Packaging and distribution guide

**Avoids:**
- Scope creep (Pitfall 14): focus on 4-5 core features, defer advanced topics to "next steps" section
- Teaching too much too fast: progressive disclosure, each chapter builds on previous

**Research flag:** Skip research-phase. Tutorial writing is standard technical writing process.

---

### Phase 6: Advanced Features (Post-MVP)
**Rationale:** Nice-to-have features that enhance UX but aren't essential for tutorial or basic LSP functionality. Defer until Phases 1-5 are complete and user feedback validates core implementation.

**Delivers (potential):** Find References, Rename Symbol, Signature Help, Document Symbols, Formatting, Code Actions, Semantic Tokens, Inlay Hints.

**Addresses:**
- Differentiator features from FEATURES.md
- Advanced LSP capabilities (LSP 3.16+)

**Avoids:**
- Scope creep (Pitfall 14): only implement if core features are rock-solid and users request specific advanced features

**Research flag:** Likely needs research-phase for each advanced feature. Rename and Find References require workspace indexing (complex), Semantic Tokens and Inlay Hints are newer LSP features with sparse F# examples.

---

### Phase Ordering Rationale

**Dependency-driven order:**
- Phase 1 (Foundation) must come first: all features depend on protocol layer and document synchronization
- Phase 2 (Hover/Go-to-Definition) depends on Phase 1: needs diagnostics infrastructure, builds symbol table
- Phase 3 (Completion) depends on Phase 2: needs symbol table and scope analysis
- Phase 4 (Performance) depends on Phases 1-3: can't optimize what doesn't exist yet
- Phase 5 (Tutorial) depends on Phases 1-4: documents working implementation
- Phase 6 (Advanced) depends on Phases 1-5: builds on solid foundation

**Risk mitigation order:**
- Phase 1 addresses critical Pitfalls 2, 3, 5 (concurrency, URIs, protocol compliance) before building features
- Phase 2 introduces type inference with caching from start (Pitfall 4 mitigation)
- Phase 3 switches to incremental sync (Pitfall 7 mitigation)
- Phase 4 focuses on performance pitfalls (Pitfalls 1, 6, 8)

**Value delivery order:**
- Phase 1 delivers immediate value: red squiggles for errors (most visible LSP feature)
- Phase 2 adds exploration: "what is this symbol?" (high-frequency user action)
- Phase 3 adds productivity: autocomplete (100x/day feature)
- Phase 4 makes everything fast (quality-of-life)
- Phases 5-6 enhance but don't fundamentally change user experience

### Research Flags

**Phases likely needing deeper research during planning:**
- **Phase 4 (Performance):** Incremental type checking strategies for Hindley-Milner type inference are domain-specific. May need research-phase to study academic papers on incremental type inference or analyze how Haskell/OCaml LSPs handle this.

**Phases with standard patterns (skip research-phase):**
- **Phase 1 (Foundation):** LSP initialization, capability negotiation, document sync extensively documented in VS Code guides and LSP spec
- **Phase 2 (Hover/Go-to-Definition):** Standard LSP features with clear examples in Ionide.LanguageServerProtocol and other F# LSPs
- **Phase 3 (Completion):** Well-documented LSP feature, though context filtering may need iteration
- **Phase 5 (Tutorial):** Standard technical writing process, no technical research needed

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Core library versions verified from NuGet (Ionide.LanguageServerProtocol 0.7.0, .NET 10 LTS). FsAutoComplete provides production validation of Ionide-based LSP stack. |
| Features | HIGH | LSP 3.17 specification defines expected features clearly. Community consensus on table-stakes vs differentiators validated across multiple LSP tutorials and production implementations. |
| Architecture | HIGH | Layered architecture pattern confirmed across official LSP documentation, VS Code guides, and production implementations (FsAutoComplete, Deno LSP, TypeScript LSP). |
| Pitfalls | HIGH | All critical pitfalls sourced from production postmortems (Deno LSP 10x performance blog post, FsAutoComplete GitHub issues) and LSP protocol critique articles. FunLang-specific type inference pitfall extrapolated from Hindley-Milner complexity research. |

**Overall confidence:** HIGH

### Gaps to Address

**Type inference caching strategy:** While research identified type inference performance as a critical pitfall, specific incremental type checking strategies for Hindley-Milner systems need deeper investigation. Recommendation: Phase 4 planning should include research-phase focusing on incremental type inference papers and OCaml/Haskell LSP implementations.

**Multi-file analysis scope:** Research recommends single-file MVP, but PROJECT.md doesn't specify if go-to-definition should cross file boundaries. Recommendation: validate during Phase 2 planning whether single-file definition lookup is acceptable or if basic multi-file support is expected.

**Tutorial scope validation:** Research strongly recommends 4-5 core features for tutorial, aligning with PROJECT.md's active requirements (diagnostics, hover, completion, go-to-definition). However, PROJECT.md also lists tutorial chapters for each feature. Recommendation: validate during Phase 5 planning that tutorial depth matches beginner audience expectations (not too shallow, not overwhelming).

**Testing strategy:** Research identified insufficient testing as a minor pitfall, but didn't provide F#-specific LSP testing guidance. Expecto handles unit tests, but LSP integration testing (simulating client requests) may need additional tooling. Recommendation: include testing infrastructure setup in Phase 1 planning.

## Sources

### Primary (HIGH confidence)

**Official Documentation:**
- [LSP 3.17 Specification](https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/) — Comprehensive protocol reference, capability definitions
- [VS Code Language Server Extension Guide](https://code.visualstudio.com/api/language-extensions/language-server-extension-guide) — Official tutorial, best practices
- [Microsoft Learn - LSP Overview](https://learn.microsoft.com/en-us/visualstudio/extensibility/language-server-protocol) — Architecture overview

**Production Implementations:**
- [Ionide.LanguageServerProtocol GitHub](https://github.com/ionide/LanguageServerProtocol) — Library documentation, v0.7.0 release notes
- [FsAutoComplete GitHub](https://github.com/ionide/FsAutoComplete) — Production F# LSP using Ionide.LanguageServerProtocol, architecture reference
- [Deno LSP Optimization Blog](https://deno.com/blog/optimizing-our-lsp) — Performance pitfall case study (8s → <1s hover latency)

**Package Repositories:**
- [Ionide.LanguageServerProtocol NuGet](https://www.nuget.org/packages/Ionide.LanguageServerProtocol/) — v0.7.0 dependencies, download stats
- [FSharp.Compiler.Service NuGet](https://www.nuget.org/packages/FSharp.Compiler.Service) — v43.10.102 stable release
- [Expecto NuGet](https://www.nuget.org/packages/Expecto/) — v10.2.3 release info

### Secondary (MEDIUM confidence)

**Implementation Guides:**
- [Toptal LSP Tutorial (2026)](https://www.toptal.com/javascript/language-server-protocol-tutorial) — Feature implementation guidance
- [Ballerina Practical Guide for LSP](https://medium.com/ballerina-techblog/practical-guide-for-the-language-server-protocol-3091a122b750) — Architecture patterns
- [Prefab: LSP Tutorial - Building Custom Auto-Complete](https://prefab.cloud/blog/lsp-language-server-from-zero-to-completion/) — Beginner tutorial

**Domain Analysis:**
- [LSP: The Good, The Bad, and The Ugly](https://www.michaelpj.com/blog/2024/09/03/lsp-good-bad-ugly.html) — Protocol critique, anti-patterns
- [Merlin: Language Server for OCaml (Experience Report)](https://dl.acm.org/doi/pdf/10.1145/3236798) — Functional language LSP challenges
- [AWS Language Servers Architecture](https://github.com/aws/language-servers/blob/main/ARCHITECTURE.md) — Multi-language server patterns

**Performance Research:**
- [Type Inference: A Deep Dive](https://www.numberanalytics.com/blog/type-inference-deep-dive) — Algorithmic complexity analysis
- [Type Inference has usability problems](https://austinhenley.com/blog/typeinference.html) — UX considerations

### Tertiary (LOW confidence, needs validation)

**Concurrency and Platform Issues:**
- [tower-lsp concurrency issues #284](https://github.com/ebkalderon/tower-lsp/issues/284) — Rust LSP concurrency model debate
- [RFC 8089 - file URI Scheme](https://datatracker.ietf.org/doc/html/rfc8089) — Formal URI specification
- [clangd Windows URI bug #16729](https://github.com/anthropics/claude-code/issues/16729) — URI handling case study

**Testing Frameworks:**
- [lsp-test (Haskell)](https://hackage.haskell.org/package/lsp-test) — LSP integration testing library
- [pytest-lsp (Python)](https://pypi.org/project/pytest-lsp/) — Alternative LSP testing framework

---
*Research completed: 2026-02-03*
*Ready for roadmap: yes*
