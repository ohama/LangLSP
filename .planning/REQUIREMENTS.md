# Requirements: FunLang LSP

**Defined:** 2025-02-03
**Core Value:** LSP 입문자가 실제 동작하는 Language Server를 만들면서 LSP 개념을 이해할 수 있는 실용적인 튜토리얼

## v1 Requirements

Requirements for initial release. Each maps to roadmap phases.

### LSP Foundation

- [ ] **LSP-01**: VS Code에서 FunLang 파일(.fun) 열면 LSP 서버 자동 시작
- [ ] **LSP-02**: 문서 열기/닫기/변경 이벤트 동기화 (Document Sync)
- [ ] **LSP-03**: LSP 서버와 VS Code 간 JSON-RPC 통신

### Diagnostics

- [ ] **DIAG-01**: 문법 오류 실시간 표시 (빨간 밑줄 + 메시지)
- [ ] **DIAG-02**: 타입 오류 실시간 표시
- [ ] **DIAG-03**: 파일 저장 없이 타이핑 중 오류 업데이트

### Hover

- [ ] **HOVER-01**: 변수 위에 마우스 올리면 추론된 타입 표시
- [ ] **HOVER-02**: 함수 위에 마우스 올리면 함수 시그니처 표시
- [ ] **HOVER-03**: 키워드 위에 마우스 올리면 설명 표시

### Completion

- [ ] **COMP-01**: 키워드 자동 완성 (let, if, match, fun 등)
- [ ] **COMP-02**: 현재 스코프의 변수/함수 자동 완성
- [ ] **COMP-03**: 타입 정보와 함께 완성 항목 표시

### Go to Definition

- [ ] **GOTO-01**: 변수 클릭 시 정의 위치로 이동
- [ ] **GOTO-02**: 함수 호출 클릭 시 함수 정의로 이동
- [ ] **GOTO-03**: 같은 파일 내 정의로 이동

### Find References

- [ ] **REF-01**: 변수의 모든 사용 위치 찾기
- [ ] **REF-02**: 함수의 모든 호출 위치 찾기
- [ ] **REF-03**: 결과를 References 패널에 표시

### Rename Symbol

- [ ] **RENAME-01**: 변수 이름 일괄 변경
- [ ] **RENAME-02**: 함수 이름 일괄 변경
- [ ] **RENAME-03**: 변경 전 미리보기 제공

### Code Actions

- [ ] **ACTION-01**: 사용하지 않는 변수 제거 제안
- [ ] **ACTION-02**: 타입 오류에 대한 수정 제안

### VS Code Extension

- [ ] **EXT-01**: FunLang 문법 강조 (TextMate grammar)
- [ ] **EXT-02**: 언어 설정 (주석, 괄호 자동 닫기)
- [ ] **EXT-03**: 코드 스니펫 (let, match, fun 등)
- [ ] **EXT-04**: .vsix 파일로 패키징

### Tutorial

- [ ] **TUT-01**: LSP 기초 개념 설명 (프로토콜, 아키텍처)
- [ ] **TUT-02**: F# LSP 라이브러리 선택 (Ionide vs OmniSharp 비교)
- [ ] **TUT-03**: F# 프로젝트 설정 튜토리얼
- [ ] **TUT-04**: Document Sync 구현 튜토리얼
- [ ] **TUT-05**: Diagnostics 구현 튜토리얼
- [ ] **TUT-06**: Hover 구현 튜토리얼
- [ ] **TUT-07**: Completion 구현 튜토리얼
- [ ] **TUT-08**: Go to Definition 구현 튜토리얼
- [ ] **TUT-09**: Find References 구현 튜토리얼
- [ ] **TUT-10**: Rename Symbol 구현 튜토리얼
- [ ] **TUT-11**: Code Actions 구현 튜토리얼
- [ ] **TUT-12**: VS Code Extension 패키징 튜토리얼

## v2 Requirements

Deferred to future release. Tracked but not in current roadmap.

### Advanced LSP Features

- **ADV-01**: Semantic Tokens (의미 기반 구문 강조)
- **ADV-02**: Inlay Hints (인라인 타입 힌트)
- **ADV-03**: Call Hierarchy (호출 계층 구조)
- **ADV-04**: Workspace Symbols (전체 심볼 검색)

### Multi-file Support

- **MULTI-01**: 여러 파일 간 타입 체크
- **MULTI-02**: 크로스 파일 Go to Definition
- **MULTI-03**: 프로젝트 전체 Find References

### Distribution

- **DIST-01**: VS Code Marketplace 배포
- **DIST-02**: 영어 튜토리얼 번역

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
|---------|--------|
| Marketplace 배포 | v1에서는 로컬 .vsix 설치만 지원 |
| 영어 튜토리얼 | 한국어 독자 대상, v2에서 번역 고려 |
| 멀티 파일 분석 | 복잡도 급증, 싱글 파일 MVP에 집중 |
| Semantic Tokens | LSP 3.16+ 기능, 기본 기능 완성 후 추가 |
| Debugging | DAP(Debug Adapter Protocol)는 별도 프로젝트 |
| LSP 외 에디터 지원 | VS Code에 집중, Neovim/Emacs는 v2+ |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| LSP-01 | Phase 1 | Pending |
| LSP-02 | Phase 1 | Pending |
| LSP-03 | Phase 1 | Pending |
| DIAG-01 | Phase 1 | Pending |
| DIAG-02 | Phase 1 | Pending |
| DIAG-03 | Phase 1 | Pending |
| HOVER-01 | Phase 2 | Pending |
| HOVER-02 | Phase 2 | Pending |
| HOVER-03 | Phase 2 | Pending |
| GOTO-01 | Phase 2 | Pending |
| GOTO-02 | Phase 2 | Pending |
| GOTO-03 | Phase 2 | Pending |
| COMP-01 | Phase 3 | Pending |
| COMP-02 | Phase 3 | Pending |
| COMP-03 | Phase 3 | Pending |
| REF-01 | Phase 4 | Pending |
| REF-02 | Phase 4 | Pending |
| REF-03 | Phase 4 | Pending |
| RENAME-01 | Phase 4 | Pending |
| RENAME-02 | Phase 4 | Pending |
| RENAME-03 | Phase 4 | Pending |
| ACTION-01 | Phase 4 | Pending |
| ACTION-02 | Phase 4 | Pending |
| EXT-01 | Phase 5 | Pending |
| EXT-02 | Phase 5 | Pending |
| EXT-03 | Phase 5 | Pending |
| EXT-04 | Phase 5 | Pending |
| TUT-01 | Phase 6 | Pending |
| TUT-02 | Phase 6 | Pending |
| TUT-03 | Phase 6 | Pending |
| TUT-04 | Phase 6 | Pending |
| TUT-05 | Phase 6 | Pending |
| TUT-06 | Phase 6 | Pending |
| TUT-07 | Phase 6 | Pending |
| TUT-08 | Phase 6 | Pending |
| TUT-09 | Phase 6 | Pending |
| TUT-10 | Phase 6 | Pending |
| TUT-11 | Phase 6 | Pending |
| TUT-12 | Phase 6 | Pending |

**Coverage:**
- v1 requirements: 39 total
- Mapped to phases: 39
- Unmapped: 0

---
*Requirements defined: 2025-02-03*
*Last updated: 2025-02-03 after roadmap creation*
