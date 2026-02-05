# FunLang LSP 튜토리얼

F#으로 만드는 Language Server — LSP 입문자를 위한 단계별 한국어 튜토리얼

## 이 튜토리얼은

ML 계열 함수형 언어 FunLang을 위한 Language Server를 처음부터 끝까지 구현하는 과정을 다룹니다. LSP(Language Server Protocol)의 기본 개념부터 VS Code Extension 패키징까지, 실제 동작하는 코드를 단계별로 작성하면서 LSP를 이해할 수 있습니다.

## 구현하는 기능

| 기능 | 설명 |
|------|------|
| Diagnostics | 문법/타입 오류 실시간 표시 |
| Hover | 변수/함수 타입 정보 표시 |
| Completion | 키워드 및 심볼 자동 완성 |
| Go to Definition | 정의 위치로 이동 |
| Find References | 모든 참조 찾기 |
| Rename Symbol | 심볼 이름 일괄 변경 |
| Code Actions | 코드 개선 제안 |

## 기술 스택

- **언어**: F# (.NET 10)
- **LSP 라이브러리**: Ionide.LanguageServerProtocol 0.7.0
- **테스트**: Expecto + FsCheck
- **에디터**: VS Code

## 시작하기

[LSP 개념 소개](01-lsp-concepts.md)부터 시작하세요.
