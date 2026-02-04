# LangLSP Repository 설정 가이드

LangLSP repo를 만들고 `.claude/`와 `LangTutorial`을 submodule로 추가하는 방법.

## 목표 구조

```
LangLSP/
├── .gitmodules           # submodule 설정 파일
├── .claude/              # Claude-Config
├── LangTutorial/         # LangTutorial
└── src/                  # LangLSP 자체 코드 (추후)
```

## Submodule URL

- `.claude/`: https://github.com/ohama/Claude-Config
- `LangTutorial`: https://github.com/ohama/LangTutorial

## 설정 단계

### 1. GitHub에서 LangLSP repo 생성

GitHub 웹에서 `ohama/LangLSP` repository를 먼저 생성 (빈 repo, README 없이).

### 2. 로컬에서 설정

```bash
# 새 디렉토리 생성
cd ~/vibe-coding
mkdir LangLSP
cd LangLSP
git init

# .claude submodule 추가
git submodule add https://github.com/ohama/Claude-Config .claude

# LangTutorial submodule 추가
git submodule add https://github.com/ohama/LangTutorial LangTutorial

# 초기 커밋
git add .
git commit -m "Initial commit with submodules"

# 원격 연결 및 push
git remote add origin https://github.com/ohama/LangLSP
git branch -M main
git push -u origin main
```

## Clone 방법

다른 곳에서 LangLSP를 clone할 때:

```bash
# recursive 옵션으로 submodule도 함께 clone
git clone --recursive https://github.com/ohama/LangLSP

# 또는 별도로 submodule 초기화
git clone https://github.com/ohama/LangLSP
cd LangLSP
git submodule update --init --recursive
```

## Submodule 업데이트

```bash
# 모든 submodule 최신화
git submodule update --remote --merge

# 특정 submodule만 업데이트
cd LangTutorial
git pull origin master
cd ..
git add LangTutorial
git commit -m "Update LangTutorial submodule"
```

## 참고사항

- LangTutorial 내부에도 `.claude/` submodule이 있어 중복됨
- 둘 다 같은 `Claude-Config` repo를 가리키므로 문제없음
- 필요시 LangTutorial에서 submodule 제거 가능
