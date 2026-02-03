# FunLang LSP

## What This Is

FunLang을 위한 VS Code Language Server와 LSP 구현 튜토리얼. LangTutorial에서 구현한 ML 계열 함수형 언어 FunLang에 대한 IDE 지원을 제공하며, 그 구현 과정을 LSP 입문자가 따라할 수 있는 단계별 한국어 튜토리얼로 문서화한다.

## Core Value

LSP 입문자가 실제 동작하는 Language Server를 만들면서 LSP 개념을 이해할 수 있는 실용적인 튜토리얼.

## Requirements

### Validated

(None yet — ship to validate)

### Active

- [ ] VS Code에서 FunLang 파일(.fun) 열면 LSP 서버 연결
- [ ] 실시간 문법/타입 오류 표시 (Diagnostics)
- [ ] 변수/함수 위에 마우스 올리면 타입 정보 표시 (Hover)
- [ ] 키워드 및 심볼 자동 완성 (Completion)
- [ ] 정의로 이동 (Go to Definition)
- [ ] .vsix 파일로 VS Code 확장 패키징
- [ ] LSP 기초 개념 설명 튜토리얼
- [ ] F# 프로젝트 설정 튜토리얼
- [ ] 텍스트 동기화 구현 튜토리얼
- [ ] Diagnostics 구현 튜토리얼
- [ ] Hover 구현 튜토리얼
- [ ] Completion 구현 튜토리얼
- [ ] Go to Definition 구현 튜토리얼
- [ ] VS Code 확장 패키징 튜토리얼

### Out of Scope

- VS Code Marketplace 배포 — v1에서는 로컬 .vsix 설치만 지원
- 영어 튜토리얼 — 한국어 독자 대상
- 멀티 파일 분석 — 싱글 파일 MVP에 집중
- Semantic Tokens, Inlay Hints — LSP 3.16+ 고급 기능, 기본 완성 후 고려

## Context

**FunLang 언어:**
- LangTutorial 서브모듈에 F#으로 구현된 ML 계열 함수형 언어
- Hindley-Milner 타입 추론, Let-polymorphism, 패턴 매칭 지원
- Lexer, Parser, Type Checker, Evaluator 모두 구현 완료

**튜토리얼 대상:**
- LSP 입문자 (언어/프레임워크 무관)
- F# 경험 없어도 따라할 수 있도록 설명

**기존 자료:**
- LangTutorial/tutorial/ 에 FunLang 언어 구현 튜토리얼 존재
- 이 프로젝트는 LSP 구현에 집중

## Constraints

- **Tech Stack**: F# — FunLang이 F#으로 구현되어 있어 재사용 용이
- **LSP Library**: Ionide.LanguageServerProtocol 0.7.0 — F# 네이티브, LSP 3.17 지원
- **Runtime**: .NET 10 LTS + F# 10
- **Testing**: Expecto + FsCheck — F# 네이티브, 속성 기반 테스트 지원
- **Logging**: Serilog — 구조화된 로깅, .NET 생태계 표준
- **Target Editor**: VS Code — 가장 널리 사용되는 에디터

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| F#로 LSP 서버 구현 | FunLang이 F#로 구현되어 있어 타입 체커 등 재사용 가능 | — Pending |
| Ionide.LanguageServerProtocol 사용 | F# 네이티브, 경량, FsAutoComplete에서 검증됨. OmniSharp은 C# 중심으로 DI 오버헤드 | — Pending |
| 한국어 튜토리얼 | 한국어 LSP 튜토리얼 부족, 대상 독자 명확 | — Pending |
| 8가지 LSP 기능 구현 | Table stakes 4개 + Find References, Rename, Code Actions로 실용적 범위 | — Pending |

---
*Last updated: 2025-02-03 after initialization*
