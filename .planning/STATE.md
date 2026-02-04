# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2025-02-04)

**Core value:** LSP 입문자가 실제 동작하는 Language Server를 만들면서 LSP 개념을 이해할 수 있는 실용적인 튜토리얼
**Current focus:** Phase 4 - Advanced Features (IN PROGRESS)

## Current Position

Phase: 4 of 5 (Advanced Features)
Plan: 2 of 3 in current phase
Status: In progress
Last activity: 2026-02-04 — Completed 04-02-PLAN.md (Rename Symbol and Code Actions)

Progress: [██████████████████░░] 90% (18 of 20 total plans complete)

## Performance Metrics

**Velocity:**
- Total plans completed: 18
- Average duration: 3.9min
- Total execution time: 1.2 hours

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 01-lsp-foundation | 8 | 32min | 4min |
| 02-core-navigation | 5 | 32min | 6.4min |
| 03-completion | 3 | 9min | 3min |
| 04-advanced-features | 2 | 5min | 2.5min |

**Recent Trend:**
- Last 5 plans: 03-02 (3min), 03-03 (3min), 04-01 (2min), 04-02 (3min)
- Trend: Fast execution for implementation-only plans (no tests/tutorials)

*Updated after each plan completion*

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table.
Recent decisions affecting current work:

- F# LSP 서버 구현: FunLang이 F#으로 구현되어 타입 체커 재사용 가능
- Ionide.LanguageServerProtocol 사용: F# 네이티브, 경량, FsAutoComplete 검증됨
- 한국어 튜토리얼: 한국어 LSP 튜토리얼 부족, 대상 독자 명확
- 8가지 LSP 기능 구현: Table stakes 4개 + Find References, Rename, Code Actions
- FunLang v5.0/v6.0 활용: Span, Diagnostic 모듈로 LSP Diagnostics 구현 단순화
- Simplified server skeleton (01-01): Full LSP message loop deferred to next phase per investigation needs
- Serilog file logging (01-01): LSP uses stdout, so file logging to /tmp for debugging
- Korean tutorial with actual code snippets (01-04): Tutorial references real implementation for actionability
- ConcurrentDictionary for document storage (01-03): Thread-safe storage for concurrent LSP notifications
- U2 union type handling (01-03): Ionide 0.7.0 uses U2<C1,C2> for incremental vs full sync
- Sequential test execution (01-03): Expecto tests with shared state need testSequenced
- Clamp invalid spans instead of wrapping (01-05): FunLang (0,0) spans clamped to prevent uint.MaxValue wrap-around
- FsCheck with 500 iterations (01-05): Property-based testing for position conversion invariants
- dotnet run for development (01-06): VS Code extension uses dotnet run for easy server debugging
- Auto-activation via empty activationEvents (01-06): VS Code 1.74+ auto-activates for contributed languages
- Actual code examples in tutorials (01-07): Reference real implementation to ensure accuracy and actionability
- In-depth Korean documentation (01-07): Target audience needs deep understanding, not surface-level overview
- Suppress NU1902 warning (01-08): NuGet security warnings corrupt LSP protocol by printing to stdout
- LexBuffer.FromString 0-based coordinates (02-01): LexBuffer.FromString uses 0-based lines/columns, matching LSP exactly
- Inclusive EndColumn in spans (02-01): Position at column N matches span ending at column N
- Innermost node traversal (02-01): Recursive AST traversal checks children first, returns most specific match
- Keyword check before AST lookup (02-02): Keywords aren't in AST, check word at cursor first
- Whole-AST typecheck validation (02-02): Typecheck whole AST first, then extract node types for context
- findVarTypeInAst for variable types (02-02): Search binding sites and typecheck bound values
- U3.C1 for MarkupContent (02-02): Ionide 0.7.0 uses U3<MarkupContent, MarkedString, MarkedString[]>
- Position-based shadowing resolution (02-03): Closest preceding definition wins for shadowed variables
- 0-based coordinate passthrough (02-03): Protocol.fs no longer subtracts 1 (matches LexBuffer.FromString)
- Comprehensive tutorial coverage (02-04): 677 lines exceeds target, includes full function implementations
- Korean tutorial style (02-05): Follow existing 05-diagnostics.md format with code examples
- CompletionItemKind.Variable for all symbols (03-01): Defer Function vs Variable distinction to future phase
- No trigger characters in Phase 3 (03-01): User invokes completion explicitly, add triggers later
- Graceful degradation on parse errors (03-01): Return only keywords when AST unavailable
- Reuse existing infrastructure for completion (03-01): collectDefinitions for scope, findVarTypeInAst for types
- testSequenced for completion tests (03-02): Shared document state requires sequential test execution
- Test helper pattern (03-02): getCompletionLabels and findCompletionItem helpers for cleaner tests
- Comprehensive tutorial structure (03-03): 938 lines covering protocol, implementation, testing, and pitfalls
- Tutorial exceeds target length (03-03): Comprehensive coverage justified, similar to 06-hover.md pattern
- Shadowing resolution via definition cross-check (04-01): collectReferencesForBinding uses findDefinitionForVar to verify each Var refers to target definition
- WorkspaceEdit helpers for Rename (04-01): createTextEdit and createWorkspaceEdit added to Protocol.fs in References plan
- findNameInSource for tight spans (04-02): Let/LetRec/Lambda spans cover whole expressions, search source text for exact name location for rename edits
- DiagnosticTag.Unnecessary for unused vars (04-02): Enables VS Code fading/dimming UI treatment for unused variables
- Warning severity for unused variables (04-02): Yellow squiggle, not red error - allows code to run while signaling cleanup
- Skip underscore-prefixed variables (04-02): Convention for intentionally unused bindings (e.g., _result for side effects)

### Pending Todos

None yet.

### Blockers/Concerns

None yet.

## Session Continuity

Last session: 2026-02-05
Stopped at: Completed 04-02-PLAN.md (Rename Symbol and Code Actions) - Rename.fs, CodeActions.fs, updated Diagnostics with unused vars, Server.fs registration
Resume file: None
Next action: Continue Phase 4 with 04-03 (Testing and regression verification) - handle test updates for unused variable diagnostics
