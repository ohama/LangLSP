# LangLSP

FunLang을 위한 VS Code Language Server Protocol (LSP) 구현.

[LangTutorial](https://github.com/ohama/LangTutorial)에서 구현한 FunLang 언어에 대한 IDE 지원을 제공합니다.

## 목표

- FunLang 소스 코드에 대한 IDE 지원
- 실시간 문법 오류 표시
- 타입 추론 결과 표시
- 코드 자동 완성
- 정의로 이동 (Go to Definition)
- 호버 시 타입 정보 표시

## 프로젝트 구조

```
LangLSP/
├── .claude/              # Claude 설정 (submodule)
├── LangTutorial/         # FunLang 언어 구현 (submodule)
├── docs/                 # 문서
│   ├── howto/            # 개발 지식 문서
│   └── LangLSP-setup.md  # 프로젝트 설정 가이드
└── src/                  # LSP 서버 구현 (예정)
```

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

- **F#** — LSP 서버 구현
- **OmniSharp LSP** — Language Server Protocol 라이브러리
- **VS Code Extension** — 클라이언트 확장

## 개발 상태

🚧 **초기 설정 단계**

## 관련 프로젝트

- [LangTutorial](https://github.com/ohama/LangTutorial) — FunLang 언어 구현 및 튜토리얼
- [Claude-Config](https://github.com/ohama/Claude-Config) — Claude 설정

## 라이선스

MIT
