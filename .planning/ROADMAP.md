# Roadmap: FunLang LSP

## Overview

FunLang LSP 프로젝트는 F#으로 구현된 함수형 언어 FunLang을 위한 Language Server를 구축하고, 그 과정을 LSP 입문자가 따라할 수 있는 한국어 튜토리얼로 문서화합니다. 5개의 단계를 거쳐 기본 LSP 인프라부터 고급 기능까지 점진적으로 구현하며, 각 단계마다 해당 기능의 튜토리얼을 함께 작성합니다.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [x] **Phase 1: LSP Foundation** - Protocol setup, document sync, diagnostics + 기초 튜토리얼
- [ ] **Phase 2: Core Navigation** - Hover and Go to Definition + 튜토리얼
- [ ] **Phase 3: Completion** - Keyword and symbol autocomplete + 튜토리얼
- [ ] **Phase 4: Advanced Features** - Find References, Rename, Code Actions + 튜토리얼
- [ ] **Phase 5: VS Code Extension** - Extension packaging + 튜토리얼

## Phase Details

### Phase 1: LSP Foundation
**Goal**: VS Code에서 FunLang 파일을 열면 LSP 서버가 자동으로 시작하고, 문서 변경을 추적하며, 기본 문법/타입 오류를 실시간으로 표시한다. LSP 기초 개념과 프로젝트 설정 튜토리얼을 함께 작성한다.

**Depends on**: Nothing (first phase)

**Requirements**: LSP-01, LSP-02, LSP-03, DIAG-01, DIAG-02, DIAG-03, TEST-01, TEST-02, TEST-03, TEST-11, TUT-01, TUT-02, TUT-03, TUT-04, TUT-05

**Success Criteria** (what must be TRUE):
  1. User opens .fun file in VS Code and LSP server starts automatically
  2. User edits document and server receives change events in real-time
  3. User sees red squiggles under syntax errors with error messages
  4. User sees red squiggles under type errors with type mismatch details
  5. User types in document and diagnostics update without saving file
  6. All Document Sync unit tests pass (Expecto)
  7. All Diagnostics unit tests pass (Expecto)
  8. FsCheck property-based tests pass (position/range calculations)
  9. Tutorial explains LSP concepts (client-server architecture, JSON-RPC, capabilities)
  10. Tutorial compares Ionide.LanguageServerProtocol vs OmniSharp with rationale
  11. Tutorial walks through F# project setup with all required dependencies
  12. Tutorial shows how to implement Document Sync with code examples
  13. Tutorial shows how to implement Diagnostics with code examples

**Plans**: 8 plans

Plans:
- [x] 01-01-PLAN.md — F# LSP Server project setup with Protocol module
- [x] 01-02-PLAN.md — LSP concepts and library choice tutorials (TUT-01, TUT-02)
- [x] 01-03-PLAN.md — Document Sync implementation with tests (LSP-02, TEST-02)
- [x] 01-04-PLAN.md — Project setup tutorial (TUT-03)
- [x] 01-05-PLAN.md — Diagnostics implementation with tests (DIAG-01-03, TEST-03, TEST-11)
- [x] 01-06-PLAN.md — VS Code extension client (LSP-01 completion)
- [x] 01-07-PLAN.md — Document Sync and Diagnostics tutorials (TUT-04, TUT-05)
- [x] 01-08-PLAN.md — Integration verification checkpoint

---

### Phase 2: Core Navigation
**Goal**: 사용자가 변수와 함수 위에 마우스를 올리면 타입 정보가 표시되고, 심볼을 클릭하면 정의된 위치로 이동한다. Hover와 Go to Definition 튜토리얼을 함께 작성한다.

**Depends on**: Phase 1

**Requirements**: HOVER-01, HOVER-02, HOVER-03, GOTO-01, GOTO-02, GOTO-03, TEST-04, TEST-06, TUT-06, TUT-08

**Success Criteria** (what must be TRUE):
  1. User hovers over variable and sees its inferred type from Hindley-Milner type checker
  2. User hovers over function name and sees function signature with parameter and return types
  3. User hovers over keyword (let, if, match) and sees explanation in Korean
  4. User clicks variable and "Go to Definition" jumps to where variable was bound
  5. User clicks function call and jumps to function definition in same file
  6. All Hover unit tests pass
  7. All Go to Definition unit tests pass
  8. Tutorial shows how to implement Hover with code examples
  9. Tutorial shows how to implement Go to Definition with code examples

**Plans**: 5 plans

Plans:
- [ ] 02-01-PLAN.md — Shared AST position lookup module with tests
- [ ] 02-02-PLAN.md — Hover implementation with type display and Korean keyword explanations
- [ ] 02-03-PLAN.md — Go to Definition implementation with symbol table
- [ ] 02-04-PLAN.md — Hover tutorial in Korean (TUT-06)
- [ ] 02-05-PLAN.md — Go to Definition tutorial in Korean (TUT-08)

---

### Phase 3: Completion
**Goal**: 사용자가 타이핑할 때 키워드와 현재 스코프의 심볼이 자동 완성 목록에 나타나며, 타입 정보와 함께 표시된다. Completion 튜토리얼을 함께 작성한다.

**Depends on**: Phase 2

**Requirements**: COMP-01, COMP-02, COMP-03, TEST-05, TUT-07

**Success Criteria** (what must be TRUE):
  1. User types "l" and sees "let" keyword suggestion with autocomplete popup
  2. User types "m" and sees "match" and "fun" keywords in completion list
  3. User types variable name prefix and sees matching variables from current scope
  4. User sees completion items with type annotations (e.g., "myVar: int")
  5. User selects completion item and it inserts correctly at cursor position
  6. All Completion unit tests pass
  7. Tutorial shows how to implement Completion with code examples

**Plans**: TBD

Plans:
- TBD (created during plan-phase)

---

### Phase 4: Advanced Features
**Goal**: 사용자가 심볼의 모든 사용 위치를 찾고, 일괄 이름 변경을 수행하며, 코드 개선 제안을 받을 수 있다. Find References, Rename, Code Actions 튜토리얼을 함께 작성한다.

**Depends on**: Phase 3

**Requirements**: REF-01, REF-02, REF-03, RENAME-01, RENAME-02, RENAME-03, ACTION-01, ACTION-02, TEST-07, TEST-08, TEST-09, TUT-09, TUT-10, TUT-11

**Success Criteria** (what must be TRUE):
  1. User invokes "Find All References" on variable and sees all usage locations in References panel
  2. User invokes "Find All References" on function and sees all call sites
  3. User invokes "Rename Symbol" on variable and sees preview of all changes
  4. User confirms rename and all occurrences update simultaneously
  5. User sees lightbulb icon on unused variable with "Remove unused variable" suggestion
  6. User sees lightbulb icon on type error with fix suggestion
  7. All Find References unit tests pass
  8. All Rename Symbol unit tests pass
  9. All Code Actions unit tests pass
  10. Tutorial shows how to implement Find References with code examples
  11. Tutorial shows how to implement Rename Symbol with code examples
  12. Tutorial shows how to implement Code Actions with code examples

**Plans**: TBD

Plans:
- TBD (created during plan-phase)

---

### Phase 5: VS Code Extension
**Goal**: FunLang 언어에 대한 완성된 VS Code 확장이 .vsix 파일로 패키징되어 로컬 설치 가능하다. VS Code 확장 패키징 튜토리얼을 함께 작성한다.

**Depends on**: Phase 4

**Requirements**: EXT-01, EXT-02, EXT-03, EXT-04, TEST-10, TUT-12

**Success Criteria** (what must be TRUE):
  1. User opens .fun file and sees syntax highlighting (keywords, strings, comments)
  2. User types "//" and line comment continues on new line automatically
  3. User types "(" and closing ")" appears automatically
  4. User types "let" snippet trigger and gets template with placeholders
  5. User installs .vsix file with "code --install-extension funlang.vsix" successfully
  6. Extension appears in VS Code Extensions panel with icon and description
  7. All LSP integration tests pass (mock client end-to-end)
  8. Tutorial shows how to package VS Code extension as .vsix

**Plans**: TBD

Plans:
- TBD (created during plan-phase)

---

## Progress

**Execution Order:**
Phases execute in numeric order: 1 → 2 → 3 → 4 → 5

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. LSP Foundation | 8/8 | Complete | 2026-02-04 |
| 2. Core Navigation | 0/5 | In progress | - |
| 3. Completion | 0/TBD | Not started | - |
| 4. Advanced Features | 0/TBD | Not started | - |
| 5. VS Code Extension | 0/TBD | Not started | - |

---
*Last updated: 2026-02-04 — Phase 2 planned*
