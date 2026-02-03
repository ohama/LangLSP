# Domain Pitfalls: LSP Development

**Domain:** Language Server Protocol (LSP) Implementation
**Researched:** 2026-02-03
**Context:** F# LSP server for FunLang with type inference; first LSP project + tutorial creation

## Critical Pitfalls

Mistakes that cause rewrites or major issues.

### Pitfall 1: Synchronization Overhead Between Language Boundaries
**What goes wrong:** Excessive cross-language/cross-process communication on every keystroke causes catastrophic performance degradation. Deno's LSP was reading 100k+ lines of code on every keystroke when synchronizing state between Rust and TypeScript compiler, causing 8+ second delays.

**Why it happens:** Naive implementations transfer full file contents and dependency graphs on every state change without caching or selective updates. Developers don't realize how expensive these boundary crossings are until testing at scale.

**Consequences:** LSP becomes unusable in real projects. Users report multi-second delays for completions, hovers, and diagnostics. Server gets killed due to resource exhaustion.

**Prevention:**
- Cache state on both sides of language boundaries
- Only request updates for changed local files, not remote dependencies
- Implement smart invalidation - only evict cache when files actually change
- Measure cross-boundary call frequency early in development

**Detection:**
- Profiling shows most time in serialization/deserialization
- Performance degrades linearly with project size
- Completion/hover takes >500ms consistently
- High CPU usage even when idle

**Phase mapping:** Phase 2-3 (Architecture/Core Implementation) - Design caching strategy before implementing features.

---

### Pitfall 2: Incorrect Concurrency Model
**What goes wrong:** Language servers handle notifications asynchronously when they must be processed synchronously, causing client/server state to drift out of sync. Or servers implement no concurrency at all, making cancellation impossible.

**Why it happens:** The LSP spec provides vague guidance on concurrency ("handle it yourself") while simultaneously requiring concurrency for cancellation and progress tracking to work. Developers either over-concurrently process everything or under-concurrently process nothing.

**Consequences:**
- State drift: Server processes request N+1 before notification N completes
- Stale results: Client doesn't know if results match current document state
- Broken cancellation: Single-threaded servers can't notice cancel requests
- Race conditions in document synchronization

**Prevention:**
- **Notifications MUST be processed synchronously (in order)**
- **Requests CAN be processed concurrently**
- Implement cancellation token support from the start
- Use proper async/await boundaries in F# (don't block threads)
- Test with rapid document changes (simulate fast typing)

**Detection:**
- Features show outdated information intermittently
- Errors referencing wrong line numbers
- Cancellation requests are ignored
- Server state diverges from client state

**Phase mapping:** Phase 1 (Foundation) - Choose concurrency model before implementing any request handlers.

---

### Pitfall 3: File URI Handling Across Platforms
**What goes wrong:** Malformed file URIs break cross-platform compatibility. Common mistakes: `file://C:\path` instead of `file:///C:/path` on Windows, not URL-encoding spaces/UTF-8 characters, case-sensitivity issues, using string interpolation instead of URI APIs.

**Why it happens:** RFC 8089 (file URI scheme) is subtle. Windows paths need forward slashes and three slashes in URI. Developers test on one platform and miss other platform's issues. String interpolation seems easier than proper URI libraries.

**Consequences:**
- Features fail silently on Windows or Linux
- Jump-to-definition breaks with spaces in paths
- UTF-8 filenames display as mojibake in diagnostics
- Case-insensitive file systems cause duplicate document tracking

**Prevention:**
- **Use proper URI libraries** - Don't hand-roll URI parsing
- In F#: Use `System.Uri` for all URI operations
- Always URL-encode paths with special characters
- Test on both Windows AND Linux from day one
- Handle both `file:///` and `file:/` prefixes permissively

**Detection:**
- Features work on developer's OS but fail on other platforms
- Paths with spaces break jump-to-definition
- Error: "Malformed URI" or "Invalid file path"
- Diagnostics point to wrong files

**Phase mapping:** Phase 1 (Foundation) - Implement URI utilities before any file operations.

---

### Pitfall 4: Type Inference Performance Explosion
**What goes wrong:** Naive type inference re-analyzes entire dependency graph on every keystroke, causing exponential complexity growth. Type inference with polymorphic recursion or complex higher-order types becomes computationally intractable.

**Why it happens:** Type inference algorithms are inherently expensive. FunLang has type inference, which adds unique complexity. Without incremental analysis and caching, every character typed triggers full reanalysis.

**Consequences:**
- Multi-second delays for hover/completion in medium codebases
- Server becomes unresponsive during typing
- Memory usage grows unbounded
- Users disable LSP features due to slowness

**Prevention:**
- **Implement incremental type checking from the start**
- Cache type inference results per function/expression
- Use dirty-tracking to only re-check changed definitions
- Set complexity limits for type inference (fail gracefully)
- Consider bidirectional type checking (top-down + bottom-up)
- Profile with realistic codebases (10k+ LOC)

**Detection:**
- Performance degrades non-linearly with code size
- Type checking takes >2 seconds for small changes
- Memory usage grows during editing session
- Profiling shows type inference in 90%+ of time

**Phase mapping:** Phase 3 (Type System Integration) - Research incremental type checking before implementing inference.

---

### Pitfall 5: Protocol Compliance and Capability Mismatches
**What goes wrong:** Server claims capabilities it doesn't fully implement, or doesn't implement required client requests (like `workspace/configuration`, `client/registerCapability`), causing clients to fail initialization or get empty results.

**Why it happens:** LSP spec is large and inconsistent. Developers test with one client (usually VS Code) which forgives certain omissions, then fail with other clients. Optional vs required capabilities aren't always clear.

**Consequences:**
- Server fails to initialize with certain clients
- Features return empty results silently
- Client shows "server not ready" errors
- Different behavior across VS Code, Neovim, Emacs, etc.

**Prevention:**
- **Accurately declare capabilities in initialize response**
- Don't claim capabilities you haven't implemented
- Implement required request handlers even if "optional" (workspace/configuration, client/registerCapability, window/workDoneProgress/create)
- Test with multiple clients: VS Code, Neovim, Emacs
- Return proper error codes (RequestCancelled, MethodNotFound)
- Handle missing optional client capabilities gracefully

**Detection:**
- Works in VS Code but fails in Neovim/Emacs
- Client logs show unhandled request errors
- Features work intermittently
- Empty results from valid requests

**Phase mapping:** Phase 1 (Foundation) - Implement capability negotiation correctly before adding features.

---

## Moderate Pitfalls

Mistakes that cause delays or technical debt.

### Pitfall 6: Over-Eager Diagnostic Publishing
**What goes wrong:** Publishing diagnostics on every keystroke or for entire workspace on startup causes performance issues and visual noise (flickering errors).

**Why it happens:** Developers implement `textDocument/didChange` handler and immediately publish diagnostics without debouncing or throttling. Or publish for all files in workspace during initialization.

**Consequences:**
- CPU spikes during typing
- Error squiggles flicker on every keystroke
- Workspace initialization takes minutes on large projects
- Network/serialization overhead for hundreds of diagnostic messages

**Prevention:**
- Debounce diagnostic publishing (200-300ms after last change)
- Publish diagnostics only for open/changed files, not entire workspace
- Batch diagnostic updates for multiple files
- Mark diagnostics as incremental when possible
- Profile startup time in large workspace (>100 files)

**Detection:**
- CPU spikes correlate with typing
- Diagnostics flicker/flash during editing
- Server initialization takes >10 seconds
- Log shows hundreds of publishDiagnostic calls

**Phase mapping:** Phase 4 (Diagnostics) - Implement debouncing when adding diagnostics support.

---

### Pitfall 7: Ignoring Incremental Parsing
**What goes wrong:** Re-parsing entire document on every change is O(n) per keystroke. For 10k LOC files, this causes noticeable lag.

**Why it happens:** Implementing full parser is easier than incremental parser. Developers postpone optimization "for later."

**Consequences:**
- Typing lag in large files
- Battery drain on laptops
- Server uses excessive CPU
- Features slow down proportionally to file size

**Prevention:**
- Use incremental parsing library from start (Tree-sitter, Roslyn-style red-green trees)
- If custom parser, design for incrementality early
- Cache parse trees in document state
- Only re-parse changed regions
- Benchmark with 5k+ LOC files

**Detection:**
- Typing lag increases with file size
- Profiling shows parsing in hot path
- CPU usage spikes during editing
- Parse time >50ms for small changes

**Phase mapping:** Phase 2 (Parser Integration) - Choose incremental parsing strategy before implementing.

---

### Pitfall 8: Missing Cancellation Token Handling
**What goes wrong:** Long-running operations (workspace symbol search, full-text search, type checking) don't check cancellation tokens, forcing clients to wait for completion even when user canceled.

**Why it happens:** Cancellation adds complexity. Developers forget to thread cancellation tokens through async operations. F#'s async/task don't always make cancellation obvious.

**Consequences:**
- Unresponsive UI when user rapidly types/searches
- Resource waste computing stale results
- Users perceive server as frozen
- Concurrent request handling becomes risky

**Prevention:**
- Accept cancellation token in all request handlers
- Check token periodically in long-running loops
- Use F#'s `Async.CancellationToken` or `CancellationToken` parameter
- Return error code `RequestCancelled` when cancelled
- Test by rapidly issuing then canceling requests

**Detection:**
- Operations complete even after user moves on
- CPU usage stays high after canceling action
- Client logs show "operation took too long" warnings
- Users report "laggy" or "unresponsive" server

**Phase mapping:** Phase 1 (Foundation) - Add cancellation token support to request infrastructure.

---

### Pitfall 9: Semantic Tree Update Complexity
**What goes wrong:** Building a full semantic tree (symbol table, type information, references) that's difficult to update incrementally forces full reanalysis on every change.

**Why it happens:** Eager semantic analysis builds deeply interconnected data structures that track dependencies poorly. Incremental updates become too complex.

**Consequences:**
- Recompute entire semantic model on small changes
- Memory usage for maintaining semantic state
- Complexity blocks adding features
- Performance degrades with project size

**Prevention:**
- Design for incremental semantic updates from start
- Use on-demand semantic analysis (only compute when requested)
- Cache semantic information at appropriate granularity (per-function, not per-file)
- Track dependencies between semantic nodes explicitly
- Consider "lazy" semantic trees (compute branches on demand)

**Detection:**
- Semantic analysis takes >1 second for single line change
- Adding hover/completion triggers full reanalysis
- Memory usage grows with number of edits
- Profiling shows redundant semantic computations

**Phase mapping:** Phase 3 (Semantic Analysis) - Design incremental semantic model before implementing.

---

### Pitfall 10: Legacy Protocol Support Burden
**What goes wrong:** Supporting multiple communication protocols (legacy custom protocol + LSP) doubles maintenance burden and creates incidental complexity.

**Why it happens:** Migrating existing tools to LSP while maintaining backward compatibility. FsAutoComplete experienced this: "contains considerable incidental complexity unnecessary in LSP implementation."

**Consequences:**
- Code duplication for protocol translation
- Bugs in one protocol don't affect other
- Harder to refactor core logic
- New features require dual implementation

**Prevention:**
- For greenfield projects: **LSP only from day one**
- For migration: Set sunset date for legacy protocol
- Use adapter pattern to isolate protocol differences
- Prioritize LSP testing over legacy protocol
- Document migration path for legacy users

**Detection:**
- Parallel code paths for same features
- Protocol-specific bugs
- Tests duplicated for each protocol
- New features delayed by protocol translation

**Phase mapping:** Phase 1 (Foundation) - Decision point: LSP-only or multi-protocol? Document choice.

---

## Minor Pitfalls

Mistakes that cause annoyance but are fixable.

### Pitfall 11: Vague Presentation Specifications
**What goes wrong:** LSP spec doesn't define HOW to display certain features (code lenses, inlay hints). Implementations make different assumptions, causing inconsistent UX across clients.

**Why it happens:** Spec says features "should be shown along with source text" without specifying where/how. Developers implement what makes sense for their test client.

**Consequences:**
- Features look different in VS Code vs Neovim
- User confusion about feature behavior
- Client-specific bugs
- Extra work adapting to each client

**Prevention:**
- Test features in multiple clients early
- Follow VS Code's behavior as reference (de facto standard)
- Document intended presentation in code comments
- Provide configuration for presentation preferences
- Don't rely on specific rendering behavior

**Detection:**
- Features work but look "wrong" in some clients
- User reports "doesn't look like other LSPs"
- Client-specific rendering issues
- Inconsistent spacing/positioning

**Phase mapping:** Phase 5+ (Advanced Features) - Test in multiple clients when adding code lenses, inlay hints.

---

### Pitfall 12: Optional Field Ambiguity
**What goes wrong:** LSP allows optional fields to be missing, null, or empty array with different semantics. Implementations don't handle all three states, causing subtle bugs.

**Why it happens:** Spec allows multiple "empty states" without clear semantic distinction. Languages handle null/undefined/missing differently.

**Consequences:**
- Features fail when field is null vs missing
- Empty array vs null causes different behavior
- Cross-language serialization issues (F# option vs null)
- Defensive coding throughout codebase

**Prevention:**
- Normalize optional fields on receipt (missing → None, null → None, [] → Some [])
- Use F# option types consistently
- Test with null, missing, and empty variations
- Document how your server interprets each state
- Consider strict parsing (reject unexpected states)

**Detection:**
- Null reference exceptions
- Features work with some clients, fail with others
- Serialization errors with certain clients
- "Expected array, got null" errors

**Phase mapping:** Phase 1 (Foundation) - Establish JSON deserialization rules for optionals.

---

### Pitfall 13: Insufficient Testing Strategy
**What goes wrong:** Testing only with manual editor interaction misses protocol-level bugs, race conditions, and edge cases. Regression risk grows with each feature.

**Why it happens:** LSP testing seems complex. Developers rely on "open VS Code and try it" testing. Writing LSP integration tests feels like overhead.

**Consequences:**
- Bugs escape to users
- Regressions when adding features
- Hard to reproduce issues
- Slow development cycle (manual testing)

**Prevention:**
- Use LSP testing framework (lsp-test for Haskell, pytest-lsp for any language)
- Write unit tests for core logic (parsing, type checking)
- Write integration tests simulating client requests
- Write end-to-end tests in actual editor
- Test protocol compliance (correct JSON-RPC structure)
- CI runs tests on every commit

**Detection:**
- Frequent regressions in existing features
- Bugs only found in production
- "Works on my machine" syndrome
- Slow feature development (fear of breaking things)

**Phase mapping:** Phase 1 (Foundation) - Set up testing infrastructure before implementing features.

---

### Pitfall 14: Scope Creep Before MVP
**What goes wrong:** Attempting to implement all LSP features (80+ capabilities) before getting basic functionality working. Project stalls before delivering value.

**Why it happens:** LSP spec is large. Developers want to build "complete" implementation. Underestimating complexity of advanced features. Perfectionism.

**Consequences:**
- Months of work with nothing usable
- Burnout before MVP
- Tutorial becomes too complex for beginners
- Core features delayed by advanced features

**Prevention:**
- **Phase-based implementation** (hover → completion → diagnostics → advanced)
- Ship usable LSP with 5-10 core features first
- Defer advanced features (call hierarchy, semantic tokens, inlay hints)
- For tutorial: Focus on teaching LSP fundamentals, not complete implementation
- Measure by "can users get value" not "feature completeness"

**Detection:**
- Months without usable release
- Implementing semantic tokens before hover works
- Tutorial covers 20+ features
- Roadmap has no intermediate milestones

**Phase mapping:** Phase 0 (Planning) - Define MVP scope: hover, completion, goto definition, diagnostics only.

---

## Phase-Specific Warnings

| Phase | Likely Pitfall | Mitigation |
|-------|---------------|------------|
| Foundation (LSP Infrastructure) | Incorrect capability negotiation (Pitfall 5) | Study spec sections 3.2-3.3. Test with multiple clients. |
| Foundation (LSP Infrastructure) | File URI handling bugs (Pitfall 3) | Use System.Uri. Test on Windows + Linux. |
| Foundation (LSP Infrastructure) | Missing cancellation support (Pitfall 8) | Add CancellationToken to request infrastructure. |
| Parser Integration | Ignoring incremental parsing (Pitfall 7) | Research Tree-sitter or FParsec incremental options. |
| Semantic Analysis | Semantic tree update complexity (Pitfall 9) | Design for incremental updates. Profile early. |
| Type System | Type inference explosion (Pitfall 4) | Cache inference results. Implement dirty tracking. |
| Type System | Cross-language sync overhead (Pitfall 1) | Measure call frequency. Cache aggressively. |
| Concurrency Model | Incorrect notification handling (Pitfall 2) | Notifications synchronous, requests concurrent. |
| Diagnostics | Over-eager publishing (Pitfall 6) | Implement 200-300ms debouncing. |
| Advanced Features | Scope creep (Pitfall 14) | Defer code lenses, semantic tokens, call hierarchy. |
| Tutorial Creation | Teaching too much too fast (Pitfall 14) | Focus on 3-5 core features for tutorial. |

---

## FunLang-Specific Considerations

**Type Inference Complexity:** FunLang has type inference, which is computationally expensive. This amplifies Pitfall 4 (Type Inference Performance Explosion). Requires even more aggressive caching and incremental checking than typical LSPs.

**First LSP Project:** As a learning project, risk of Pitfall 14 (Scope Creep) is HIGH. Recommendation: Build hover + completion only for first milestone. Prove LSP fundamentals work before adding diagnostics.

**Tutorial Creation:** Tutorial audience is beginners. Avoid showing complex concurrency patterns (Pitfall 2) early. Start with synchronous request handlers, add concurrency in "advanced" section.

**F# Language Choice:** F# has excellent async support, reducing Pitfall 8 (Missing Cancellation) risk IF you design for it. But F# option types vs JSON null requires careful attention to Pitfall 12 (Optional Field Ambiguity).

---

## Sources

Research compiled from multiple authoritative sources:

### Official Documentation
- [Language Server Protocol Specification 3.17](https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/)
- [VS Code Language Server Extension Guide](https://code.visualstudio.com/api/language-extensions/language-server-extension-guide)
- [RFC 8089 - The "file" URI Scheme](https://datatracker.ietf.org/doc/html/rfc8089)

### Performance Case Studies
- [How We Made the Deno Language Server Ten Times Faster | Deno](https://deno.com/blog/optimizing-our-lsp) - Detailed analysis of synchronization overhead pitfalls
- [LSP: the good, the bad, and the ugly](https://www.michaelpj.com/blog/2024/09/03/lsp-good-bad-ugly.html) - Comprehensive LSP design critique

### Implementation Lessons
- [FsAutoComplete GitHub Repository](https://github.com/ionide/FsAutoComplete) - F# LSP implementation experience
- [Consider using fsprojects/fsharp-language-server as starting point · Issue #361](https://github.com/ionide/FsAutoComplete/issues/361) - Historical protocol migration challenges

### Concurrency and Threading
- [Consider ditching concurrent handler execution · Issue #284](https://github.com/ebkalderon/tower-lsp/issues/284) - tower-lsp concurrency issues
- [The language server with child threads | Medium](https://medium.com/dailyjs/the-language-server-with-child-threads-38ae915f4910)

### Type Inference Challenges
- [Type Inference has usability problems - Austin Z. Henley](https://austinhenley.com/blog/typeinference.html)
- [Type Inference: A Deep Dive | Number Analytics](https://www.numberanalytics.com/blog/type-inference-deep-dive)

### Cross-Platform Issues
- [clangd-lsp: Malformed file:// URI on Windows · Issue #16729](https://github.com/anthropics/claude-code/issues/16729)
- [lsp: uri scheme incorrectly parsed on windows · Issue #15261](https://github.com/neovim/neovim/issues/15261)
- [lsp--uri-to-path not decoding utf-8 · Issue #646](https://github.com/emacs-lsp/lsp-mode/issues/646)

### Diagnostics and Performance
- [configurable debouncing delays · Issue #327](https://github.com/typescript-language-server/typescript-language-server/issues/327)
- [Performance issue: publishDiagnostic on startup · Issue #1019](https://github.com/eclipse/xtext-core/issues/1019)

### Testing Approaches
- [lsp-test: Functional test framework for LSP servers](https://hackage.haskell.org/package/lsp-test)
- [pytest-lsp · PyPI](https://pypi.org/project/pytest-lsp/)

### Beginner Resources
- [Implementing a Language Server…How Hard Can It Be?? — Part 1 | Medium](https://medium.com/ballerina-techblog/implementing-a-language-server-how-hard-can-it-be-part-1-introduction-c915d2437076)
- [A Practical Guide for Language Server Protocol | Medium](https://medium.com/ballerina-techblog/practical-guide-for-the-language-server-protocol-3091a122b750)
