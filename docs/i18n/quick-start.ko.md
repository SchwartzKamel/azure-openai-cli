# az-ai -- 빠른 시작

> **번역 참고:** 이 문서는 영어 원문의 최선 번역입니다. v3.0 출시 전에 한국어 원어민의
> 검토를 환영합니다. 오류를 발견하신 경우 `s04off1-the-translation`을 참조하여
> Issue를 작성해 주세요.
> Translation note: best-effort Korean (ko). Native-speaker review wanted before v3.0.
> File an issue referencing s04off1-the-translation.

---

## 설치

### 사전 빌드된 바이너리 사용 (권장)

[Releases 페이지](https://github.com/SchwartzKamel/azure-openai-cli/releases)에서
사용 중인 플랫폼에 맞는 바이너리를 다운로드하세요.

```bash
# Linux x64 예시 (버전 번호를 최신 버전으로 교체하세요)
tar -xf az-ai-2.2.0-linux-x64.tar.gz
mv az-ai ~/.local/bin/
chmod +x ~/.local/bin/az-ai
```

macOS (Apple Silicon) 사용자:

```bash
tar -xf az-ai-2.2.0-osx-arm64.tar.gz
mv az-ai ~/.local/bin/
chmod +x ~/.local/bin/az-ai
```

### 소스 코드에서 빌드 (.NET 10 SDK 필요)

```bash
git clone https://github.com/SchwartzKamel/azure-openai-cli
cd azure-openai-cli
make setup && make install
```

설치 확인:

```bash
az-ai --version
```

---

## 자격 증명 설정

`~/.config/az-ai/env` 파일을 생성하고 Azure OpenAI 자격 증명을 입력합니다.
다음 세 가지 환경 변수가 필요합니다:

- `AZUREOPENAIENDPOINT` -- Azure OpenAI 리소스의 엔드포인트 URL
- `AZUREOPENAIAPI` -- API 키 (변수 이름은 "API"이며, "KEY"가 아닙니다)
- `AZUREOPENAIMODEL` -- 모델 배포 이름 (여러 개일 경우 쉼표로 구분)

```bash
mkdir -p ~/.config/az-ai
cat > ~/.config/az-ai/env << 'EOF'
export AZUREOPENAIENDPOINT="https://your-resource.openai.azure.com/"
export AZUREOPENAIAPI="your-api-key-here"
export AZUREOPENAIMODEL="gpt-4o,gpt-4o-mini"
EOF
chmod 600 ~/.config/az-ai/env
```

이 파일은 `az-ai` 시작 시 자동으로 로드됩니다.
셸에서 `source`를 실행할 필요가 없습니다.

> **팁:** `az-ai`를 처음 실행할 때 자격 증명이 설정되어 있지 않으면
> 대화형 설정 마법사가 시작됩니다.
> `az-ai --setup` 명령으로 언제든지 다시 실행할 수 있습니다.

---

## 첫 번째 명령

기본 테스트:

```bash
az-ai "안녕하세요. 한국어로 대답해 주세요."
```

파일 내용 요약 예시:

```bash
az-ai --raw "이 파일을 한국어로 세 줄로 요약해 주세요: $(cat README.md)"
```

`--raw` 플래그를 사용하면 스피너와 불필요한 줄 바꿈 없이 출력됩니다.
Espanso나 AutoHotkey 같은 텍스트 확장 도구와 연동할 때 유용합니다.

모델을 지정하는 경우:

```bash
az-ai --model gpt-4o "자세한 기술 설명을 부탁합니다."
```

---

## 다음 단계

이 문서는 핵심 사용법만 소개합니다.
전체 기능 문서는 영어 [README](../../README.md)를 참조하세요.

주요 기능:

- `az-ai --agent "작업"` -- 도구 호출 에이전트 모드
- `az-ai --ralph "작업" --validate "검증 명령"` -- 자율 자기 수정 루프 [?]
- `az-ai --image "프롬프트"` -- 이미지 생성 모드
- `az-ai --doctor` -- 설정 진단

다른 언어의 빠른 시작 가이드: [`docs/i18n/`](README.md).

---

## 번역 참고 사항

한글은 사전 구성된 음절 블록(U+AC00..U+D7A3)으로 표현되며,
az-ai의 `ReadFileTool`은 분해된 자모(NFD) 형식을 NFKC로 정규화합니다.
macOS에서 생성된 한글 파일 경로도 올바르게 처리됩니다.

| 원어 | 번역 | 비고 |
|------|------|------|
| "credentials" | 자격 증명 | IT 분야 표준 한국어 표현 |
| "deployment name" | 배포 이름 | Azure 공식 한국어 용어 |
| "endpoint" | 엔드포인트 | 한국어 IT 문서의 관용적 표기 |
| "text expander" | 텍스트 확장 도구 | 기능적 설명 |
| "autonomous self-correcting loop" | 자율 자기 수정 루프 | [?] 더 자연스러운 표현이 있을 수 있음 |
| "wizard" | 마법사 | Microsoft Windows 용어 관례에 따름 |
