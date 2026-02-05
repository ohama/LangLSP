# LangLSP

FunLang을 위한 VS Code Language Server Protocol (LSP) 구현.

[LangTutorial](https://github.com/ohama/LangTutorial)에서 구현한 FunLang 언어에 대한 IDE 지원을 제공합니다.

## Documentation

[FunLang LSP 튜토리얼](https://ohama.github.io/LangLSP/)

## 기능

| 기능 | 설명 |
|------|------|
| Diagnostics | 문법/타입 오류 실시간 표시 |
| Hover | 변수/함수 타입 정보 표시 |
| Completion | 키워드 및 심볼 자동 완성 |
| Go to Definition | 정의 위치로 이동 |
| Find References | 모든 참조 찾기 |
| Rename Symbol | 심볼 이름 일괄 변경 |
| Code Actions | 코드 개선 제안 |

## 프로젝트 구조

```
LangLSP/
├── src/
│   ├── LangLSP/             # LSP 서버 (F#)
│   └── LangLSP.Tests/       # 테스트 (Expecto + FsCheck)
├── client/                   # VS Code Extension (TypeScript)
│   └── funlang-0.1.0.vsix   # 패키징된 확장
├── documentation/
│   ├── tutorial/             # 12개 LSP 구현 튜토리얼
│   └── howto/                # 개발 지식 문서
├── LangTutorial/             # FunLang 언어 구현 (submodule)
└── docs/                     # mdBook 빌드 출력 (GitHub Pages)
```

## 튜토리얼

`documentation/tutorial/`에서 LSP 구현 튜토리얼을 단계별로 진행합니다.

- **대상 언어**: [LangTutorial](https://github.com/ohama/LangTutorial)에서 정의한 FunLang
- **구현 언어**: F#
- **대상 독자**: LSP 입문자 (한국어)

1. **LSP 개념 소개** — Language Server Protocol 아키텍처
2. **라이브러리 선택** — Ionide vs OmniSharp 비교
3. **프로젝트 설정** — F# LSP 서버 프로젝트 구성
4. **문서 동기화** — 문서 열기/변경/닫기 처리
5. **진단(Diagnostics)** — 실시간 문법/타입 오류 표시
6. **Hover** — 타입 정보 및 키워드 설명 표시
7. **자동 완성** — 키워드 및 심볼 자동 완성
8. **Go to Definition** — 정의로 이동
9. **Find References** — 모든 참조 찾기
10. **Rename Symbol** — 심볼 이름 일괄 변경
11. **Code Actions** — 미사용 변수 제거, 타입 오류 수정
12. **VS Code Extension** — 확장 패키징 및 배포

## FunLang 소개

FunLang은 ML 계열의 함수형 프로그래밍 언어입니다:

- **Hindley-Milner 타입 추론** — 타입 명시 없이 타입 자동 추론
- **Let-polymorphism** — 다형 함수 지원
- **패턴 매칭** — 리스트, 튜플, 와일드카드 패턴
- **일급 함수** — 클로저, 재귀 함수
- **자체 호스팅 표준 라이브러리** — map, filter, fold 등

```ocaml
(* 팩토리얼 *)
let rec fact n = if n <= 1 then 1 else n * fact (n - 1)

(* 리스트 맵 *)
map (fun x -> x * 2) [1, 2, 3]  (* [2, 4, 6] *)

(* 패턴 매칭 *)
match xs with
| [] -> 0
| h :: t -> h + sum t
```

## 기술 스택

- **F#** (.NET 10) — LSP 서버 구현
- **Ionide.LanguageServerProtocol** 0.7.0 — LSP 라이브러리
- **Expecto + FsCheck** — 테스트 (119개)
- **TypeScript** — VS Code Extension 클라이언트
- **mdBook** — 튜토리얼 문서 사이트

## 관련 프로젝트

- [LangTutorial](https://github.com/ohama/LangTutorial) — FunLang 언어 구현 및 튜토리얼

## 라이선스

MIT
