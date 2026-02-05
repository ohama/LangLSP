# FunLang LSP

## What This Is

FunLang을 위한 VS Code Language Server와 LSP 구현 튜토리얼. LangTutorial에서 구현한 ML 계열 함수형 언어 FunLang에 대한 IDE 지원(Diagnostics, Hover, Completion, Go to Definition, Find References, Rename, Code Actions)을 제공하며, 그 구현 과정을 LSP 입문자가 따라할 수 있는 12개의 단계별 한국어 튜토리얼로 문서화한다.

## Core Value

LSP 입문자가 실제 동작하는 Language Server를 만들면서 LSP 개념을 이해할 수 있는 실용적인 튜토리얼.

## Requirements

### Validated

- ✓ VS Code에서 FunLang 파일(.fun) 열면 LSP 서버 연결 — v1.0
- ✓ 실시간 문법/타입 오류 표시 (Diagnostics) — v1.0
- ✓ 변수/함수 위에 마우스 올리면 타입 정보 표시 (Hover) — v1.0
- ✓ 키워드 및 심볼 자동 완성 (Completion) — v1.0
- ✓ 정의로 이동 (Go to Definition) — v1.0
- ✓ 모든 참조 찾기 (Find References) — v1.0
- ✓ 심볼 이름 일괄 변경 (Rename) — v1.0
- ✓ 코드 액션 (미사용 변수 제거, 타입 오류 수정 제안) — v1.0
- ✓ .vsix 파일로 VS Code 확장 패키징 — v1.0
- ✓ TextMate 문법 강조, 언어 설정, 코드 스니펫 — v1.0
- ✓ LSP 기초 개념 설명 튜토리얼 — v1.0
- ✓ F# 프로젝트 설정 튜토리얼 — v1.0
- ✓ 텍스트 동기화 구현 튜토리얼 — v1.0
- ✓ Diagnostics 구현 튜토리얼 — v1.0
- ✓ Hover 구현 튜토리얼 — v1.0
- ✓ Completion 구현 튜토리얼 — v1.0
- ✓ Go to Definition 구현 튜토리얼 — v1.0
- ✓ Find References 구현 튜토리얼 — v1.0
- ✓ Rename Symbol 구현 튜토리얼 — v1.0
- ✓ Code Actions 구현 튜토리얼 — v1.0
- ✓ VS Code 확장 패키징 튜토리얼 — v1.0
- ✓ 119개 단위/통합 테스트 (Expecto + FsCheck) — v1.0

### Active

(None — planning next milestone)

### Out of Scope

- VS Code Marketplace 배포 — v2에서 고려
- 영어 튜토리얼 — v2에서 번역 고려
- 멀티 파일 분석 — 싱글 파일 MVP 완성, v2에서 고려
- Semantic Tokens, Inlay Hints — LSP 3.16+ 고급 기능, v2에서 고려
- Call Hierarchy, Workspace Symbols — v2에서 고려
- Debugging (DAP) — 별도 프로젝트
- LSP 외 에디터 지원 (Neovim, Emacs) — v2+

## Context

**Current State (v1.0 shipped):**
- LSP 서버: 7개 기능 (Diagnostics, Hover, Completion, Definition, References, Rename, Code Actions)
- 테스트: 119개 (단위 + 통합), FsCheck 속성 기반 테스트 포함
- VS Code 확장: funlang-0.1.0.vsix (3.6 MB), TextMate 문법, 6개 코드 스니펫
- 튜토리얼: 12개 한국어 문서 (~11,000줄)
- Howto 가이드: 4개 개발 지식 문서
- 코드: ~25,850줄 (F#, TypeScript, Markdown)

**FunLang 언어:**
- LangTutorial 서브모듈에 F#으로 구현된 ML 계열 함수형 언어
- Hindley-Milner 타입 추론, Let-polymorphism, 패턴 매칭 지원
- Lexer, Parser, Type Checker, Evaluator 모두 구현 완료
- **v5.0**: Span 타입 (모든 AST 노드에 소스 위치), Diagnostic 모듈 (에러 표현/변환)
- **v6.0**: TypeExpr (타입 어노테이션 AST), Bidir.fs (양방향 타입 체커)

**튜토리얼 대상:**
- LSP 입문자 (언어/프레임워크 무관)
- F# 경험 없어도 따라할 수 있도록 설명

## Constraints

- **Tech Stack**: F# — FunLang이 F#으로 구현되어 있어 재사용 용이
- **LSP Library**: Ionide.LanguageServerProtocol 0.7.0 — F# 네이티브, LSP 3.17 지원
- **Runtime**: .NET 10 LTS + F# 10
- **Testing**: Expecto + FsCheck — F# 네이티브, 속성 기반 테스트 지원
- **Logging**: Serilog — 구조화된 로깅, .NET 생태계 표준
- **Error Handling**: Option/Result 타입 사용 — Exception 대신 함수형 에러 처리
- **Target Editor**: VS Code — 가장 널리 사용되는 에디터

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| F#로 LSP 서버 구현 | FunLang이 F#로 구현되어 있어 타입 체커 등 재사용 가능 | ✓ Good — Parser, TypeChecker, Evaluator 모듈 직접 호출 |
| Ionide.LanguageServerProtocol 사용 | F# 네이티브, 경량, FsAutoComplete에서 검증됨 | ✓ Good — U2/U3 union types 학습 필요했으나 잘 동작 |
| 한국어 튜토리얼 | 한국어 LSP 튜토리얼 부족, 대상 독자 명확 | ✓ Good — 12개 문서 ~11,000줄 완성 |
| 8가지 LSP 기능 구현 | Table stakes 4개 + Find References, Rename, Code Actions | ✓ Good — 7개 기능 + Code Actions 포함 |
| ConcurrentDictionary for document storage | LSP 알림 동시 처리를 위한 thread-safe 저장소 | ✓ Good — 동시성 문제 없음 |
| Serilog 파일 로깅 | LSP가 stdout 사용하므로 /tmp에 파일 로깅 | ✓ Good — stdout 오염 방지 |
| FsCheck 500 iterations | 위치 변환 속성 기반 테스트 | ✓ Good — 경계값 버그 발견 |
| Framework-dependent publish | .NET 런타임 필요하지만 VSIX 크기 3.6MB로 소형화 | ✓ Good — self-contained 50MB+ 대비 경량 |
| dotnet run for development mode | VS Code 확장에서 개발 시 dotnet run 직접 실행 | ✓ Good — 디버깅 편의성 확보 |

---
*Last updated: 2026-02-05 after v1.0 milestone*
