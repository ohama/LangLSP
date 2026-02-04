---
phase: 01-lsp-foundation
plan: 03
subsystem: lsp-core
tags: [document-sync, ionide, fsharp, expecto, lsp]

# Dependency graph
requires:
  - phase: 01-01
    provides: Server skeleton with capabilities declaration
provides:
  - DocumentSync module with thread-safe document storage
  - didOpen/didChange/didClose handlers
  - ConcurrentDictionary for document tracking
  - Expecto test infrastructure with 5 passing unit tests
affects: [01-04, 01-05, diagnostics, hover, completion]

# Tech tracking
tech-stack:
  added: [Expecto 10.2.3, Expecto.FsCheck 10.2.3]
  patterns: [Module-based handlers, U2 union type handling, Sequential testing for shared state]

key-files:
  created:
    - src/LangLSP.Server/DocumentSync.fs
    - src/LangLSP.Tests/LangLSP.Tests.fsproj
    - src/LangLSP.Tests/DocumentSyncTests.fs
    - src/LangLSP.Tests/Program.fs
  modified:
    - src/LangLSP.Server/LangLSP.Server.fsproj
    - src/LangLSP.Server/Server.fs

key-decisions:
  - "Use ConcurrentDictionary for thread-safe document storage"
  - "Handle Ionide 0.7.0 U2 union types (C1 for incremental, C2 for full sync)"
  - "Tests run sequentially via testSequenced to avoid shared state conflicts"

patterns-established:
  - "Handler functions are pure: DidXParams → unit"
  - "Helper functions for testing use explicit type annotations"
  - "Test projects use Expecto with sequential execution for shared state"

# Metrics
duration: 5min
completed: 2026-02-04
---

# Phase 1 Plan 3: Document Sync Summary

**Thread-safe document storage with ConcurrentDictionary, handling incremental/full LSP text changes via Ionide U2 union types**

## Performance

- **Duration:** 5 min 22 sec
- **Started:** 2026-02-04T06:26:14Z
- **Completed:** 2026-02-04T06:31:36Z
- **Tasks:** 3
- **Files modified:** 6

## Accomplishments
- Document synchronization module tracks open documents in memory
- Incremental text change application with correct offset calculation
- Full sync fallback for complete document replacement
- 5 comprehensive unit tests covering all document lifecycle events
- Expecto test infrastructure established for future testing

## Task Commits

Each task was committed atomically:

1. **Task 1: Implement DocumentSync Module** - `b26ab04` (feat)
2. **Task 2: Wire Document Sync Handlers to Server** - `22abc8c` (feat)
3. **Task 3: Create Test Project and Document Sync Tests** - `4ca8fdd` (test)

## Files Created/Modified
- `src/LangLSP.Server/DocumentSync.fs` - Document state management with ConcurrentDictionary
- `src/LangLSP.Server/Server.fs` - Handler registration in Handlers module
- `src/LangLSP.Server/LangLSP.Server.fsproj` - Added DocumentSync.fs to compile order
- `src/LangLSP.Tests/LangLSP.Tests.fsproj` - Test project with Expecto references
- `src/LangLSP.Tests/DocumentSyncTests.fs` - 5 unit tests for document sync
- `src/LangLSP.Tests/Program.fs` - Expecto test runner entry point

## Decisions Made

**1. ConcurrentDictionary for thread-safe storage**
- Rationale: LSP servers may receive concurrent notifications from editors
- Alternative considered: lock-based Dictionary (rejected - lower performance)

**2. Handling Ionide 0.7.0 U2 union types**
- TextDocumentContentChangeEvent is U2<C1, C2> not a record with optional Range
- C1 = incremental change with Range, C2 = full document sync
- Pattern match on union cases rather than checking optional fields

**3. Sequential test execution**
- Used testSequenced to avoid parallel test interference
- Shared ConcurrentDictionary requires sequential cleanup via clearAll()
- Trade-off: Slightly slower tests but reliable execution

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed type inference for test helper functions**
- **Found during:** Task 3 (Test creation)
- **Issue:** Without type annotations, F# inferred wrong types for TextDocument fields
- **Fix:** Added explicit type annotations (DidOpenTextDocumentParams, DidCloseTextDocumentParams, DidChangeTextDocumentParams) to helper functions
- **Files modified:** src/LangLSP.Tests/DocumentSyncTests.fs
- **Verification:** Tests compile and pass
- **Committed in:** 4ca8fdd (Task 3 commit)

**2. [Rule 3 - Blocking] Added testSequenced for shared state**
- **Found during:** Task 3 (Test execution)
- **Issue:** Tests failing intermittently due to parallel execution with shared ConcurrentDictionary
- **Fix:** Wrapped test list with testSequenced to force sequential execution
- **Files modified:** src/LangLSP.Tests/DocumentSyncTests.fs
- **Verification:** All 5 tests consistently pass
- **Committed in:** 4ca8fdd (Task 3 commit)

---

**Total deviations:** 2 auto-fixed (1 bug, 1 blocking)
**Impact on plan:** Both fixes necessary for test correctness. No scope creep.

## Issues Encountered

**Ionide 0.7.0 type structure discovery**
- Issue: TextDocumentContentChangeEvent is U2 union, not record with optional Range
- Solution: Used dotnet fsi to inspect actual types at runtime
- Outcome: Correctly implemented pattern matching on U2.C1/U2.C2 cases

**Test execution environment**
- Issue: Initial tests passed in FSI but failed in Expecto due to parallel execution
- Solution: Recognized shared state issue, added testSequenced
- Lesson: Always consider concurrency for tests with shared module-level state

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

**Ready for next phases:**
- Document storage foundation complete
- Handlers registered and ready for LSP message loop integration
- Test infrastructure established for TDD development
- getDocument() function available for diagnostics/hover features

**Blockers:** None

**Notes:**
- LSP message loop (stdin/stdout) integration deferred to future phase per 01-01 plan
- Handlers module ready to be wired when message loop is implemented

---
*Phase: 01-lsp-foundation*
*Plan: 03*
*Completed: 2026-02-04*
