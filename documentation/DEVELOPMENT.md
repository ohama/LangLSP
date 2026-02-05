# FunLang LSP 개발 가이드

FunLang Language Server 개발 및 VS Code 확장 테스트를 위한 가이드.

## 사전 요구사항

- [.NET 10 SDK](https://dotnet.microsoft.com/download) 이상
- [Node.js](https://nodejs.org/) 20 이상
- [VS Code](https://code.visualstudio.com/)

## 프로젝트 구조

```
LangLSP/
├── src/
│   ├── LangLSP.Server/        # F# LSP 서버
│   │   ├── Protocol.fs         #   LSP 프로토콜 유틸리티
│   │   ├── DocumentSync.fs     #   문서 동기화
│   │   ├── AstLookup.fs        #   AST 위치 탐색
│   │   ├── Diagnostics.fs      #   진단 (문법/타입 오류)
│   │   ├── Hover.fs            #   호버 정보
│   │   ├── Completion.fs       #   자동 완성
│   │   ├── Definition.fs       #   정의로 이동
│   │   ├── References.fs       #   참조 찾기
│   │   ├── Rename.fs           #   이름 변경
│   │   ├── CodeActions.fs      #   코드 액션
│   │   └── Server.fs           #   서버 진입점
│   └── LangLSP.Tests/         # Expecto + FsCheck 테스트
├── client/                     # VS Code 확장
│   ├── src/extension.ts        #   LSP 클라이언트 진입점
│   ├── syntaxes/               #   TextMate 문법 정의
│   ├── snippets/               #   코드 스니펫
│   ├── language-configuration.json
│   └── package.json            #   확장 매니페스트
├── examples/                   # 테스트용 .fun 예제 파일
├── LangTutorial/               # FunLang 구현 (submodule)
└── documentation/
    └── tutorial/               # 한국어 LSP 튜토리얼 (01~12)
```

## 빌드

### LSP 서버 빌드

```bash
dotnet build src/LangLSP.Server/
```

### VS Code 확장 클라이언트 빌드

```bash
cd client && npm install && npm run compile
```

### 전체 빌드 (서버 + 클라이언트)

```bash
dotnet build src/LangLSP.Server/ && cd client && npm run compile
```

## 테스트

### 단위 테스트 실행

Expecto + FsCheck 기반 119개 테스트:

```bash
dotnet test src/LangLSP.Tests/
```

상세 출력:

```bash
dotnet test src/LangLSP.Tests/ --verbosity normal
```

또는 직접 실행:

```bash
dotnet run --project src/LangLSP.Tests/
```

### 테스트 구성

| 모듈 | 테스트 수 | 내용 |
|------|----------|------|
| DocumentSyncTests | 8 | 문서 열기/닫기/변경 |
| ProtocolTests | 7 | 위치 변환, FsCheck 속성 |
| DiagnosticsTests | 11 | 문법/타입 오류 진단 |
| AstLookupTests | 24 | AST 위치 탐색 |
| HoverTests | 17 | 호버 타입 정보 |
| DefinitionTests | 18 | 정의로 이동 |
| CompletionTests | 19 | 자동 완성 |
| ReferencesTests | 14 | 참조 찾기 |
| RenameTests | 11 | 이름 변경 |
| CodeActionsTests | 12 | 코드 액션 |
| IntegrationTests | 3 | LSP 전체 라이프사이클 |

## VS Code 확장 테스트

### 방법 1: 개발 모드 (권장)

설치/제거 없이 확장을 바로 테스트합니다:

```bash
code --extensionDevelopmentPath="/home/shoh/vibe-coding/LangLSP/client" \
     /home/shoh/vibe-coding/LangLSP/examples/
```

VS Code가 확장 개발 호스트 모드로 열리며, `examples/` 폴더의 `.fun` 파일로 기능을 확인할 수 있습니다. VS Code를 닫으면 확장도 자동으로 해제됩니다.

> **참고:** 개발 모드에서는 LSP 서버를 `dotnet run`으로 실행합니다. 서버 소스 수정 후 VS Code를 다시 열면 변경사항이 반영됩니다.

### 방법 2: VSIX 설치

패키징된 확장을 설치하여 실제 사용 환경과 동일하게 테스트합니다:

```bash
# VSIX 빌드 (아래 "VSIX 패키징" 섹션 참고)
# 설치
code --install-extension /home/shoh/vibe-coding/LangLSP/client/funlang-0.1.0.vsix

# 예제 파일 열기
code /home/shoh/vibe-coding/LangLSP/examples/

# 제거
code --uninstall-extension funlang.funlang
```

### 테스트용 예제 파일

`examples/` 디렉토리에 기능별 테스트 파일이 있습니다:

| 파일 | 테스트 대상 |
|------|------------|
| `01-syntax-highlighting.fun` | 구문 강조 (키워드, 연산자, 문자열, 주석) |
| `02-comments.fun` | 한 줄/블록/중첩 주석, 주석 토글 |
| `03-hover-and-types.fun` | 호버 시 타입 정보 |
| `04-completion.fun` | Ctrl+Space 자동 완성 |
| `05-definition-and-references.fun` | F12 정의 이동, Shift+F12 참조 찾기 |
| `06-rename.fun` | F2 이름 변경 |
| `07-code-actions.fun` | 미사용 변수 경고, Quick Fix |
| `08-diagnostics.fun` | 문법/타입 오류 실시간 표시 |
| `09-all-features.fun` | 전체 기능 종합 테스트 |

### 기능 확인 체크리스트

- [ ] **구문 강조** — `.fun` 파일 열면 키워드, 문자열, 주석에 색상 적용
- [ ] **주석 토글** — `Ctrl+/` (한 줄), `Ctrl+Shift+A` (블록)
- [ ] **자동 닫기** — `(`, `[`, `"`, `(*` 입력 시 닫기 문자 자동 삽입
- [ ] **주석 연속** — `//` 주석 끝에서 Enter → 다음 줄에 `// ` 자동 추가
- [ ] **스니펫** — `let`, `match`, `if`, `fun`, `letrec`, `matchlist` 입력 후 선택
- [ ] **호버** — 변수 위에 마우스 → 타입 정보 표시
- [ ] **자동 완성** — `Ctrl+Space` → 키워드 + 스코프 내 변수 제안
- [ ] **정의로 이동** — `F12` → 변수/함수 정의 위치
- [ ] **참조 찾기** — `Shift+F12` → 모든 사용 위치
- [ ] **이름 변경** — `F2` → 정의 + 모든 참조 일괄 변경
- [ ] **코드 액션** — 미사용 변수에 노란 밑줄 + 💡 Quick Fix
- [ ] **진단** — 타입 오류 시 빨간 밑줄 + 오류 메시지

### 디버깅

LSP 서버 로그 확인:

```bash
# 서버 로그 (Serilog file logging)
tail -f /tmp/LangLSP.log
```

VS Code Output 패널에서 "FunLang Language Server" 채널을 선택하면 클라이언트 측 로그를 볼 수 있습니다.

## VSIX 패키징

배포 가능한 `.vsix` 파일 빌드:

```bash
# 1. F# 서버 퍼블리시
dotnet publish src/LangLSP.Server/LangLSP.Server.fsproj -c Release -o client/server

# 2. TypeScript 컴파일
cd client && npm run compile

# 3. VSIX 패키징
npx vsce package --allow-missing-repository

# 결과: client/funlang-0.1.0.vsix
```

> **참고:** `client/server/`와 `client/*.vsix`는 `.gitignore`에 의해 Git 추적에서 제외됩니다.

## 코딩 컨벤션

### F# 서버

- **에러 처리**: `Option`/`Result` 사용, 예외 대신 `try/catch` 후 `None` 반환
- **로깅**: Serilog 파일 로깅 (`/tmp/LangLSP.log`), stdout 사용 금지 (LSP 프로토콜 간섭)
- **좌표계**: 0-based 줄/열 (LSP 표준, LexBuffer.FromString과 일치)
- **테스트**: Expecto `testSequenced` 사용 (공유 상태), FsCheck 속성 기반 테스트

### VS Code 확장

- **서버 모드**: `fs.existsSync(server/)` — 있으면 번들된 바이너리, 없으면 `dotnet run`
- **활성화**: `activationEvents: []` — `.fun` 파일 기여로 자동 활성화 (VS Code 1.74+)
