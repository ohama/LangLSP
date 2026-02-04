# Project State

## Project Reference

See: .planning/PROJECT.md (updated 2025-02-04)

**Core value:** LSP 입문자가 실제 동작하는 Language Server를 만들면서 LSP 개념을 이해할 수 있는 실용적인 튜토리얼
**Current focus:** Phase 2 - Core Navigation (COMPLETE)

## Current Position

Phase: 2 of 5 (Core Navigation) — COMPLETE
Plan: 3 of 3 in current phase (all complete)
Status: Phase 2 complete - Ready for Phase 3
Last activity: 2026-02-04 — Completed 02-03-PLAN.md (Go to Definition)

Progress: [████████████████████] 100% (Phase 2: 3/3 plans complete)

## Performance Metrics

**Velocity:**
- Total plans completed: 11
- Average duration: 5.2min
- Total execution time: 0.95 hours

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 01-lsp-foundation | 8 | 32min | 4min |
| 02-core-navigation | 3 | 26min | 8.7min |

**Recent Trend:**
- Last 5 plans: 01-08 (10min), 02-01 (8min), 02-02 (7min), 02-03 (11min)
- Trend: Navigation features averaging 8.7min (includes coordinate system fixes)

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

### Pending Todos

None yet.

### Blockers/Concerns

None yet.

## Session Continuity

Last session: 2026-02-04
Stopped at: Completed 02-03-PLAN.md - Go to Definition with 14 tests passing
Resume file: None
Next action: Proceed to Phase 3 (Find References, Rename, Code Actions)
