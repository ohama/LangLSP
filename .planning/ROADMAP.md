# Roadmap: FunLang LSP

## Overview

FunLang LSP 프로젝트는 F#으로 구현된 함수형 언어 FunLang을 위한 Language Server를 구축하고, 그 과정을 LSP 입문자가 따라할 수 있는 한국어 튜토리얼로 문서화합니다. 6개의 단계를 거쳐 기본 LSP 인프라부터 고급 기능까지 점진적으로 구현하고, 최종적으로 VS Code에서 사용 가능한 .vsix 확장과 완성된 튜토리얼을 제공합니다.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [ ] **Phase 1: LSP Foundation** - Protocol setup, document sync, and basic diagnostics
- [ ] **Phase 2: Core Navigation** - Hover and Go to Definition features
- [ ] **Phase 3: Completion** - Keyword and symbol autocomplete
- [ ] **Phase 4: Advanced Features** - Find References, Rename, and Code Actions
- [ ] **Phase 5: VS Code Extension** - Extension packaging and distribution
- [ ] **Phase 6: Tutorial Documentation** - Korean guide covering all features

## Phase Details

### Phase 1: LSP Foundation
**Goal**: VS Code에서 FunLang 파일을 열면 LSP 서버가 자동으로 시작하고, 문서 변경을 추적하며, 기본 문법/타입 오류를 실시간으로 표시한다.

**Depends on**: Nothing (first phase)

**Requirements**: LSP-01, LSP-02, LSP-03, DIAG-01, DIAG-02, DIAG-03, TEST-01, TEST-02, TEST-03

**Success Criteria** (what must be TRUE):
  1. User opens .fun file in VS Code and LSP server starts automatically
  2. User edits document and server receives change events in real-time
  3. User sees red squiggles under syntax errors with error messages
  4. User sees red squiggles under type errors with type mismatch details
  5. User types in document and diagnostics update without saving file
  6. All Document Sync unit tests pass
  7. All Diagnostics unit tests pass

**Plans**: TBD

Plans:
- TBD (created during plan-phase)

---

### Phase 2: Core Navigation
**Goal**: 사용자가 변수와 함수 위에 마우스를 올리면 타입 정보가 표시되고, 심볼을 클릭하면 정의된 위치로 이동한다.

**Depends on**: Phase 1

**Requirements**: HOVER-01, HOVER-02, HOVER-03, GOTO-01, GOTO-02, GOTO-03, TEST-04, TEST-06

**Success Criteria** (what must be TRUE):
  1. User hovers over variable and sees its inferred type from Hindley-Milner type checker
  2. User hovers over function name and sees function signature with parameter and return types
  3. User hovers over keyword (let, if, match) and sees explanation in Korean
  4. User clicks variable and "Go to Definition" jumps to where variable was bound
  5. User clicks function call and jumps to function definition in same file
  6. All Hover unit tests pass
  7. All Go to Definition unit tests pass

**Plans**: TBD

Plans:
- TBD (created during plan-phase)

---

### Phase 3: Completion
**Goal**: 사용자가 타이핑할 때 키워드와 현재 스코프의 심볼이 자동 완성 목록에 나타나며, 타입 정보와 함께 표시된다.

**Depends on**: Phase 2

**Requirements**: COMP-01, COMP-02, COMP-03, TEST-05

**Success Criteria** (what must be TRUE):
  1. User types "l" and sees "let" keyword suggestion with autocomplete popup
  2. User types "m" and sees "match" and "fun" keywords in completion list
  3. User types variable name prefix and sees matching variables from current scope
  4. User sees completion items with type annotations (e.g., "myVar: int")
  5. User selects completion item and it inserts correctly at cursor position
  6. All Completion unit tests pass

**Plans**: TBD

Plans:
- TBD (created during plan-phase)

---

### Phase 4: Advanced Features
**Goal**: 사용자가 심볼의 모든 사용 위치를 찾고, 일괄 이름 변경을 수행하며, 코드 개선 제안을 받을 수 있다.

**Depends on**: Phase 3

**Requirements**: REF-01, REF-02, REF-03, RENAME-01, RENAME-02, RENAME-03, ACTION-01, ACTION-02, TEST-07, TEST-08, TEST-09

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

**Plans**: TBD

Plans:
- TBD (created during plan-phase)

---

### Phase 5: VS Code Extension
**Goal**: FunLang 언어에 대한 완성된 VS Code 확장이 .vsix 파일로 패키징되어 로컬 설치 가능하다.

**Depends on**: Phase 4

**Requirements**: EXT-01, EXT-02, EXT-03, EXT-04, TEST-10

**Success Criteria** (what must be TRUE):
  1. User opens .fun file and sees syntax highlighting (keywords, strings, comments)
  2. User types "//" and line comment continues on new line automatically
  3. User types "(" and closing ")" appears automatically
  4. User types "let" snippet trigger and gets template with placeholders
  5. User installs .vsix file with "code --install-extension funlang.vsix" successfully
  6. Extension appears in VS Code Extensions panel with icon and description
  7. All LSP integration tests pass (mock client end-to-end)

**Plans**: TBD

Plans:
- TBD (created during plan-phase)

---

### Phase 6: Tutorial Documentation
**Goal**: LSP 입문자가 FunLang LSP 구현을 처음부터 끝까지 따라할 수 있는 완성된 한국어 튜토리얼이 존재한다.

**Depends on**: Phase 5

**Requirements**: TUT-01, TUT-02, TUT-03, TUT-04, TUT-05, TUT-06, TUT-07, TUT-08, TUT-09, TUT-10, TUT-11, TUT-12

**Success Criteria** (what must be TRUE):
  1. Tutorial explains LSP concepts (client-server architecture, JSON-RPC, capabilities) clearly
  2. Tutorial compares Ionide.LanguageServerProtocol vs OmniSharp with rationale
  3. Tutorial walks through F# project setup with all required dependencies
  4. Tutorial shows how to implement Document Sync with code examples
  5. Tutorial shows how to implement Diagnostics with code examples
  6. Tutorial shows how to implement Hover with code examples
  7. Tutorial shows how to implement Completion with code examples
  8. Tutorial shows how to implement Go to Definition with code examples
  9. Tutorial shows how to implement Find References with code examples
  10. Tutorial shows how to implement Rename Symbol with code examples
  11. Tutorial shows how to implement Code Actions with code examples
  12. Tutorial shows how to package VS Code extension as .vsix

**Plans**: TBD

Plans:
- TBD (created during plan-phase)

---

## Progress

**Execution Order:**
Phases execute in numeric order: 1 → 2 → 3 → 4 → 5 → 6

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. LSP Foundation | 0/TBD | Not started | - |
| 2. Core Navigation | 0/TBD | Not started | - |
| 3. Completion | 0/TBD | Not started | - |
| 4. Advanced Features | 0/TBD | Not started | - |
| 5. VS Code Extension | 0/TBD | Not started | - |
| 6. Tutorial Documentation | 0/TBD | Not started | - |

---
*Last updated: 2025-02-03 after roadmap creation*
