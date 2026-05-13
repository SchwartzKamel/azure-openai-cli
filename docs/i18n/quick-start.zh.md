# az-ai -- 快速入门

> **翻译说明：** 本文档为尽力翻译版本。欢迎母语为中文的用户在 v3.0 发布之前进行审校。
> 如发现翻译错误，请参考 `s04off1-the-translation` 提交 Issue。
> Translation note: best-effort Simplified Chinese (zh-CN). Native-speaker review
> wanted before v3.0. File an issue referencing s04off1-the-translation.

---

## 安装

### 使用预编译二进制文件（推荐）

从 [Releases 页面](https://github.com/SchwartzKamel/azure-openai-cli/releases)
下载适合您平台的二进制文件。

```bash
# Linux x64 示例（请将版本号替换为最新版本）
tar -xf az-ai-2.2.0-linux-x64.tar.gz
mv az-ai ~/.local/bin/
chmod +x ~/.local/bin/az-ai
```

macOS (Apple Silicon) 用户：

```bash
tar -xf az-ai-2.2.0-osx-arm64.tar.gz
mv az-ai ~/.local/bin/
chmod +x ~/.local/bin/az-ai
```

### 从源代码编译（需要 .NET 10 SDK）

```bash
git clone https://github.com/SchwartzKamel/azure-openai-cli
cd azure-openai-cli
make setup && make install
```

安装后验证：

```bash
az-ai --version
```

---

## 设置凭据

创建 `~/.config/az-ai/env` 文件并填写 Azure OpenAI 凭据。
需要以下三个环境变量：

- `AZUREOPENAIENDPOINT` -- Azure OpenAI 资源的终端节点 URL
- `AZUREOPENAIAPI` -- API 密钥（注意：变量名为 "API"，不是 "KEY"）
- `AZUREOPENAIMODEL` -- 模型部署名称（多个名称用逗号分隔）

```bash
mkdir -p ~/.config/az-ai
cat > ~/.config/az-ai/env << 'EOF'
export AZUREOPENAIENDPOINT="https://your-resource.openai.azure.com/"
export AZUREOPENAIAPI="your-api-key-here"
export AZUREOPENAIMODEL="gpt-4o,gpt-4o-mini"
EOF
chmod 600 ~/.config/az-ai/env
```

此文件在启动时会自动加载，无需在 Shell 中手动 `source`。

> **提示：** 首次运行时若未配置凭据，程序会启动交互式设置向导。
> 随时可通过 `az-ai --setup` 重新运行向导。

---

## 第一个命令

简单测试：

```bash
az-ai "你好，请用中文回复我。"
```

总结文件内容的示例：

```bash
az-ai --raw "请用中文将以下文件内容总结为三行：$(cat README.md)"
```

`--raw` 参数可以去除旋转动画和多余的换行符，
适合与 Espanso 或 AutoHotkey 等文本扩展工具配合使用。

指定模型的示例：

```bash
az-ai --model gpt-4o "请提供详细的技术说明。"
```

---

## 后续步骤

本文档仅介绍了基本用法。
完整功能文档请参阅英文版 [README](../../README.md)。

主要功能：

- `az-ai --agent "任务"` -- 工具调用代理模式
- `az-ai --ralph "任务" --validate "验证命令"` -- 自主自我修正循环
- `az-ai --image "提示词"` -- 图像生成模式
- `az-ai --doctor` -- 配置诊断

其他语言的快速入门指南请参见 [`docs/i18n/`](README.md)。

---

## 翻译说明

| 术语 | 翻译 | 备注 |
|------|------|------|
| "credentials" | 凭据 | 微软官方中文技术文档常用译法 |
| "deployment name" | 部署名称 | 遵循 Azure 官方中文术语 |
| "endpoint" | 终端节点 | Azure 门户官方译法 |
| "text expander" | 文本扩展工具 | 描述性译法 |
| "autonomous self-correcting loop" | 自主自我修正循环 | [?] 可能有更自然的表达方式 |
