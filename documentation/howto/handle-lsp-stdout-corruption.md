---
created: 2026-02-05
description: LSP 서버의 stdout에 비-프로토콜 출력이 섞여 JSON-RPC가 깨지는 문제 해결
---

# LSP stdout 오염 방지

LSP는 stdout으로 JSON-RPC 메시지를 주고받는다. stdout에 다른 출력이 섞이면 프로토콜이 깨진다.

## The Insight

LSP 프로토콜은 `Content-Length: N\r\n\r\n{json}` 형식으로 stdout을 사용한다. 빌드 도구, 패키지 매니저, 로거 등이 stdout에 출력하면 클라이언트가 파싱에 실패한다. 이 문제는 **서버가 정상 시작된 후에도 발생**하며, 특정 요청 시점에만 나타날 수 있어 디버깅이 어렵다.

## Why This Matters

증상:
- VS Code에서 "Server returned invalid response" 에러
- LSP 기능이 간헐적으로 동작하지 않음
- 서버 로그에는 정상인데 클라이언트에서 실패
- 재현이 어려움 (빌드 캐시 상태에 따라 달라짐)

실제 사례: .NET의 NuGet 패키지 복원 시 `NU1902` 보안 경고가 stdout에 출력되어 LSP JSON-RPC 프로토콜을 깨뜨렸다. 서버 코드 자체에는 문제가 없었다.

## Recognition Pattern

- LSP 서버가 "가끔" 동작하지 않을 때
- 새 패키지 추가 후 LSP가 깨질 때
- CI에서는 되는데 로컬에서 안 될 때 (또는 그 반대)
- `Content-Length` 파싱 에러가 클라이언트 로그에 보일 때

## The Approach

stdout을 오염시킬 수 있는 모든 소스를 차단한다.

### Step 1: 빌드 경고를 stdout에서 제거

.NET 프로젝트에서 NuGet 보안 경고(`NU1902`)를 억제한다:

```xml
<!-- LangLSP.Server.fsproj -->
<PropertyGroup>
  <NoWarn>$(NoWarn);NU1902</NoWarn>
</PropertyGroup>
```

다른 잠재적 경고도 확인:
- `NU1903` (known vulnerability)
- `NU1904` (deprecated package)
- 커스텀 MSBuild warning

### Step 2: 서버 로깅을 파일로 리다이렉트

LSP 서버에서 `Console.WriteLine`, `printfn` 등을 절대 사용하지 않는다. 로거는 반드시 파일이나 stderr로 출력한다.

```fsharp
// ❌ BAD: stdout 오염
printfn "Debug: processing request"

// ✅ GOOD: 파일 로깅 (Serilog 예시)
open Serilog
let logger = LoggerConfiguration()
                .WriteTo.File("/tmp/lsp-server.log")
                .CreateLogger()
logger.Information("Processing request")
```

### Step 3: 실행 환경 점검

`dotnet run`으로 서버를 실행하면 dotnet CLI 자체 출력이 섞일 수 있다. Production에서는 빌드된 바이너리를 직접 실행한다:

```bash
# 개발 시 (dotnet run 출력 주의)
dotnet run --project src/LangLSP.Server/

# 프로덕션 (바이너리 직접 실행)
dotnet publish -c Release -o ./server
./server/LangLSP.Server
```

### Step 4: 진단 방법

stdout에 뭐가 나오는지 확인:

```bash
# 서버 stdout을 파일로 캡처
dotnet run --project src/LangLSP.Server/ > /tmp/lsp-stdout.log 2>/tmp/lsp-stderr.log

# JSON-RPC가 아닌 줄 찾기
grep -v "^Content-Length:" /tmp/lsp-stdout.log | grep -v "^{" | grep -v "^$"
```

VS Code에서 확인:
1. Output 패널 → Language Server 채널 선택
2. `"FunLang Language Server"` 에서 에러 메시지 확인

## Example

```xml
<!-- ❌ BAD: NuGet 보안 경고가 stdout으로 출력 -->
<!-- 패키지 복원 시 아래 메시지가 stdout에 나감 -->
<!-- warning NU1902: Package 'Foo' has a known moderate severity vulnerability -->

<!-- ✅ GOOD: 경고 억제 -->
<PropertyGroup>
  <NoWarn>$(NoWarn);NU1902;NU1903;NU1904</NoWarn>
</PropertyGroup>
```

```javascript
// VS Code extension에서 서버 시작 시 stderr를 분리
const serverOptions: ServerOptions = {
  run: { command: serverPath, transport: TransportKind.stdio },
  debug: { command: serverPath, transport: TransportKind.stdio }
};
// TransportKind.stdio = stdin/stdout for JSON-RPC, stderr for logs
```

## 체크리스트

- [ ] 서버 프로젝트에 stdout으로 출력하는 코드가 없는가?
- [ ] NuGet 보안 경고(NU1902/1903/1904)를 `<NoWarn>`로 억제했는가?
- [ ] 로거가 파일 또는 stderr로만 출력하는가?
- [ ] `dotnet run` 대신 빌드된 바이너리를 production에서 사용하는가?
- [ ] 서버 stdout을 캡처하여 비-프로토콜 출력이 없는지 확인했는가?

## 관련 문서

- [LSP Specification - Base Protocol](https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/#baseProtocol) - JSON-RPC over stdio 스펙
