---
created: 2026-02-02
description: SSH 커스텀 호스트로 git submodule 설정하는 방법
---

# Git Submodule을 SSH 커스텀 호스트로 설정하기

여러 GitHub 계정을 사용할 때 SSH config의 Host alias를 활용하여 submodule을 설정하는 방법.

## The Insight

GitHub 계정이 여러 개일 때, SSH config의 Host alias를 사용하면 계정별로 다른 SSH 키를 자동 선택할 수 있다. Submodule URL도 이 alias를 사용해야 push/pull이 정상 동작한다.

## Why This Matters

- HTTPS URL 사용 시 매번 인증 필요
- 기본 `github.com` 사용 시 잘못된 SSH 키가 선택될 수 있음
- Permission denied 에러로 push/pull 실패

## Recognition Pattern

- 여러 GitHub 계정 사용
- `~/.ssh/config`에 Host alias 설정됨
- 새 repo에 submodule 추가 필요

## The Approach

1. SSH config의 Host alias 확인
2. 모든 git URL에 alias 사용 (origin, submodule)
3. 기존 디렉토리 있으면 백업 후 submodule 추가

### Step 1: SSH Config 확인

`~/.ssh/config`에서 사용할 Host alias 확인.

```bash
cat ~/.ssh/config
```

예시:
```
Host github-ohama
    HostName github.com
    User git
    IdentityFile ~/.ssh/id_ed25519_github_ohama
    IdentitiesOnly yes
```

### Step 2: Origin 설정

SSH alias를 사용하여 remote origin 설정.

```bash
# 새로 추가
git remote add origin git@github-ohama:username/repo.git

# 기존 변경
git remote set-url origin git@github-ohama:username/repo.git

# 확인
git remote -v
```

### Step 3: Submodule 추가

SSH alias URL로 submodule 추가.

```bash
git submodule add git@github-ohama:username/sub-repo.git path/to/submodule
```

**기존 디렉토리가 있는 경우:**

```bash
# 백업
mv existing-dir/important-file /tmp/

# 삭제
rm -rf existing-dir

# submodule 추가
git submodule add git@github-ohama:username/repo.git existing-dir

# 백업 복원
mv /tmp/important-file existing-dir/
```

### Step 4: 커밋 및 푸시

```bash
git add .
git commit -m "Add submodules"
git push -u origin master
```

## Example

실제 LangLSP 프로젝트 설정:

```bash
# 1. Origin 설정
git remote add origin git@github-ohama:ohama/LangLSP.git

# 2. .claude submodule 추가 (기존 디렉토리 있음)
mv .claude/settings.local.json /tmp/
rm -rf .claude
git submodule add git@github-ohama:ohama/Claude-Config.git .claude
mv /tmp/settings.local.json .claude/

# 3. LangTutorial submodule 추가
git submodule add git@github-ohama:ohama/LangTutorial.git LangTutorial

# 4. 커밋 및 푸시
git commit -m "Initial commit with submodules"
git push -u origin master
```

결과 `.gitmodules`:
```ini
[submodule ".claude"]
    path = .claude
    url = git@github-ohama:ohama/Claude-Config.git
[submodule "LangTutorial"]
    path = LangTutorial
    url = git@github-ohama:ohama/LangTutorial.git
```

## 체크리스트

- [ ] SSH config에 Host alias 설정됨
- [ ] SSH 키 권한 확인 (`chmod 600 ~/.ssh/id_*`)
- [ ] `ssh -T git@github-ohama`로 연결 테스트
- [ ] 모든 URL이 SSH alias 사용 (origin, submodule)

## Clone 방법

다른 곳에서 clone할 때:

```bash
# submodule 포함 clone
git clone --recursive git@github-ohama:username/repo.git

# 또는 별도 초기화
git clone git@github-ohama:username/repo.git
cd repo
git submodule update --init --recursive
```

## Submodule 업데이트

```bash
# 모든 submodule 최신화
git submodule update --remote --merge

# 변경사항 커밋
git add .
git commit -m "Update submodules"
```

## 관련 문서

- [Git Submodules 공식 문서](https://git-scm.com/book/en/v2/Git-Tools-Submodules)
