---
phase: 01-lsp-foundation
plan: 05
subsystem: diagnostics
tags: [diagnostics, type-checking, fscheck, property-testing, error-reporting]
requires: [01-03]
provides:
  - Real-time diagnostics publishing
  - FunLang type checker integration
  - Position conversion (1-based to 0-based)
  - Comprehensive test coverage (unit + property-based)
affects: [01-06, 01-07]
key-files:
  created:
    - src/LangLSP.Server/Diagnostics.fs
    - src/LangLSP.Tests/DiagnosticsTests.fs
    - src/LangLSP.Tests/ProtocolTests.fs
  modified:
    - src/LangLSP.Server/Server.fs
    - src/LangLSP.Server/Protocol.fs
    - src/LangLSP.Server/LangLSP.Server.fsproj
    - src/LangLSP.Tests/LangLSP.Tests.fsproj
tech-stack:
  added:
    - FsCheck for property-based testing
  patterns:
    - Result type for error handling
    - Property-based testing for invariants
    - Clamping for boundary conditions
decisions:
  - decision: Clamp invalid (0,0) spans to (0,0) instead of wrapping to uint.MaxValue
    rationale: FunLang may produce (0,0) spans for some errors; clamping prevents uint wrap-around
    file: src/LangLSP.Server/Protocol.fs
  - decision: Separate parseFunLang and typecheckAst functions
    rationale: Parse errors should not attempt type checking; allows precise error reporting
    file: src/LangLSP.Server/Diagnostics.fs
  - decision: Use FsCheck with 500 iterations for position conversion
    rationale: Property-based testing validates invariants across random inputs; 500 iterations is standard
    file: src/LangLSP.Tests/ProtocolTests.fs
metrics:
  tasks: 5
  commits: 4
  files_created: 3
  files_modified: 4
  tests_added: 11
  duration: 5min
  completed: 2026-02-04
---

# Phase 1 Plan 5: Diagnostics Publishing Summary

Real-time diagnostics publishing with FunLang type checker integration and comprehensive property-based testing.

## One-liner

Integrated FunLang's type checker to publish syntax and type errors as LSP diagnostics, with FsCheck property tests verifying 1-based to 0-based position conversion across 500 random cases.

## What Was Built

### Core Functionality (Diagnostics.fs)

**Purpose:** Parse FunLang source code and publish diagnostics to LSP clients

**Key functions:**
- `parseFunLang`: Parse source and catch syntax errors → Result<Ast.Expr, Diagnostic>
- `typecheckAst`: Run FunLang's type checker → Result<Type.Type, Diagnostic>
- `analyze`: Return all diagnostics (parse + type errors) for a document
- `publishDiagnostics`: Send diagnostics to LSP client via ILspClient
- `clearDiagnostics`: Clear diagnostics on document close

**Integration:**
- Uses FunLang's `Parser.start` + `Lexer.tokenize` for parsing
- Uses FunLang's `TypeCheck.typecheckWithDiagnostic` for type checking
- Converts FunLang diagnostics to LSP format via `Protocol.diagnosticToLsp`

### Document Sync Integration (Server.fs)

**Updated handlers to orchestrate diagnostics:**

1. **didOpen:** Store document → Analyze → Publish diagnostics
2. **didChange:** Update document → Analyze → Publish diagnostics
3. **didClose:** Clear diagnostics → Remove document

All handlers now accept `ILspClient` parameter for diagnostics publishing.

### Position Conversion Fix (Protocol.fs)

**Problem:** FunLang Span (1-based) needs conversion to LSP Range (0-based)

**Solution:** Added clamping to handle edge case where FunLang produces (0,0) spans:
```fsharp
let clamp x = max 0 (x - 1)
```

This prevents `(0,0)` from wrapping to `uint.MaxValue` when subtracting 1.

### Test Coverage

**Unit Tests (DiagnosticsTests.fs):** 6 tests
- Valid code produces no diagnostics
- Syntax error produces diagnostic
- Type error (Int + Bool) produces diagnostic
- Unbound variable produces diagnostic
- Diagnostic has correct source "funlang"
- Diagnostic range is 0-based

**Property Tests (ProtocolTests.fs):** 5 tests (3 property-based @ 500 iterations each)
- `spanToLspRange` converts 1-based to 0-based correctly (500 random cases)
- `spanToLspRange` preserves line ordering invariant (500 cases)
- `spanToLspRange` preserves character ordering on same line (500 cases)
- Edge case: First line/column (1,1) → (0,0)
- Edge case: Invalid (0,0) span clamped to (0,0)

**Custom FsCheck generator:** Creates valid Span values with constraints:
- 1-based positive values
- Start line ≤ end line
- Same line: start column ≤ end column

## How It Works

### Diagnostics Flow

```
User types → didChange notification
↓
DocumentSync.handleDidChange (update text)
↓
Server.textDocumentDidChange (orchestrate)
↓
Diagnostics.analyze (parse + typecheck)
↓
publishDiagnostics (send to client)
↓
Client displays red squiggles
```

### Error Detection

1. **Parse errors:** Caught by `parseFunLang` via try-catch around `Parser.start`
2. **Type errors:** Returned by `TypeCheck.typecheckWithDiagnostic` as `Result<Type, Diagnostic>`
3. **Position info:** FunLang's Span contains exact error location (line/column)

### Position Conversion

**FunLang (1-based):**
- First line = 1, first column = 1

**LSP (0-based):**
- First line = 0, first character = 0

**Conversion with clamping:**
```fsharp
Line = uint32 (max 0 (span.StartLine - 1))
Character = uint32 (max 0 (span.StartColumn - 1))
```

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing functionality] Added clamping for invalid spans**

- **Found during:** Task 3 (Diagnostics unit tests)
- **Issue:** Test "diagnostic range is 0-based" failed with `uint32.MaxValue` (4294967295)
- **Root cause:** FunLang produced (0,0) spans for some errors, which wrapped to max uint when subtracting 1
- **Fix:** Added clamping in `spanToLspRange`: `let clamp x = max 0 (x - 1)`
- **Files modified:** `src/LangLSP.Server/Protocol.fs`
- **Commit:** 27e765e

This is a critical fix to prevent position wrap-around that would cause incorrect diagnostic locations.

## Test Results

**All tests passing:** 16/16 ✓

**Breakdown:**
- DocumentSync: 5 tests
- Diagnostics: 6 tests
- Protocol (FsCheck): 5 tests (3 property-based with 500 iterations each)

**Total FsCheck iterations:** 1,500 random test cases for position conversion

**No test failures, no deviations from expected behavior.**

## What's Next

### Immediate Next Steps (Phase 1)

- **01-06:** Implement hover provider to show type information on cursor hover
- **01-07:** Implement completion provider for variable suggestions

### Dependencies This Unlocks

- Diagnostics are now published on every file change
- Type information is available via `analyze` function
- Position conversion is tested and reliable
- Future features can use `Diagnostics.analyze` to get type info for hover/completion

## Next Phase Readiness

**Ready for Phase 2** (LSP Message Loop):
- ✓ Document sync handlers work
- ✓ Diagnostics publishing works
- ✓ Position conversion tested
- ✓ Integration with FunLang type checker proven

**Blockers:** None

**Concerns:** None - all must-haves achieved, tests passing

## Files Changed

### Created (3 files)
- `src/LangLSP.Server/Diagnostics.fs` - Diagnostics publishing module (90 lines)
- `src/LangLSP.Tests/DiagnosticsTests.fs` - Unit tests (70 lines)
- `src/LangLSP.Tests/ProtocolTests.fs` - FsCheck property tests (95 lines)

### Modified (4 files)
- `src/LangLSP.Server/Server.fs` - Wire diagnostics to document sync
- `src/LangLSP.Server/Protocol.fs` - Add clamping to spanToLspRange
- `src/LangLSP.Server/LangLSP.Server.fsproj` - Add Diagnostics.fs to compile order
- `src/LangLSP.Tests/LangLSP.Tests.fsproj` - Add test files to compile order

## Decisions Made

### Technical Decisions

1. **Clamp invalid (0,0) spans to (0,0) instead of wrapping**
   - **Rationale:** FunLang may produce (0,0) spans for some errors; clamping prevents uint wrap-around
   - **Alternative considered:** Throw exception on invalid span
   - **Why this way:** Graceful degradation - better to show diagnostic at (0,0) than crash

2. **Separate parseFunLang and typecheckAst functions**
   - **Rationale:** Parse errors should not attempt type checking; allows precise error reporting
   - **Alternative considered:** Single `analyze` function doing both
   - **Why this way:** Composition and testability - can test parse and typecheck independently

3. **Use FsCheck with 500 iterations for position conversion**
   - **Rationale:** Property-based testing validates invariants across random inputs; 500 iterations is standard
   - **Alternative considered:** Manually written edge case tests only
   - **Why this way:** Catches edge cases humans wouldn't think of; validates invariants hold universally

## Links

- **Plan:** `.planning/phases/01-lsp-foundation/01-05-PLAN.md`
- **Commits:**
  - 154d6ab: feat(01-05): implement Diagnostics module
  - 6db2599: feat(01-05): wire diagnostics to document sync events
  - 27e765e: test(01-05): add Diagnostics unit tests
  - 014eff8: test(01-05): add FsCheck property tests for position conversion
