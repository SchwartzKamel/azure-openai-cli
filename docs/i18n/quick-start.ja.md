# az-ai -- クイックスタート

> **翻訳注:** このドキュメントはベストエフォート翻訳です。v3.0 以前にネイティブスピーカーによる
> レビューを歓迎します。誤訳を見つけた場合は、`s04off1-the-translation` を参照して
> Issue を作成してください。
> Translation note: best-effort Japanese. Native-speaker review wanted before v3.0.
> File an issue referencing s04off1-the-translation.

---

## インストール

### ビルド済みバイナリ（推奨）

[Releases ページ](https://github.com/SchwartzKamel/azure-openai-cli/releases)から
お使いのプラットフォーム向けのバイナリをダウンロードしてください。

```bash
# Linux x64 の例（バージョン番号は最新のものに置き換えてください）
tar -xf az-ai-2.2.0-linux-x64.tar.gz
mv az-ai ~/.local/bin/
chmod +x ~/.local/bin/az-ai
```

macOS (Apple Silicon) の場合：

```bash
tar -xf az-ai-2.2.0-osx-arm64.tar.gz
mv az-ai ~/.local/bin/
chmod +x ~/.local/bin/az-ai
```

### ソースからビルド（.NET 10 SDK が必要）

```bash
git clone https://github.com/SchwartzKamel/azure-openai-cli
cd azure-openai-cli
make setup && make install
```

インストール後の確認：

```bash
az-ai --version
```

---

## 認証情報の設定

`~/.config/az-ai/env` ファイルを作成し、Azure OpenAI の認証情報を設定します。
3 つの環境変数が必要です：

- `AZUREOPENAIENDPOINT` -- Azure OpenAI リソースのエンドポイント URL
- `AZUREOPENAIAPI` -- API キー（"KEY" ではなく "API" が正式な変数名です）
- `AZUREOPENAIMODEL` -- 使用するモデルのデプロイ名（カンマ区切りで複数指定可能）

```bash
mkdir -p ~/.config/az-ai
cat > ~/.config/az-ai/env << 'EOF'
export AZUREOPENAIENDPOINT="https://your-resource.openai.azure.com/"
export AZUREOPENAIAPI="your-api-key-here"
export AZUREOPENAIMODEL="gpt-4o,gpt-4o-mini"
EOF
chmod 600 ~/.config/az-ai/env
```

このファイルは起動時に自動的に読み込まれます。
シェルで `source` する必要はありません。

> **ヒント:** 初回起動時に認証情報が未設定の場合、対話型セットアップウィザードが起動します。
> `az-ai --setup` でいつでも再実行できます。

---

## 最初のコマンド

シンプルなテスト：

```bash
az-ai "こんにちは。日本語で返事をしてください。"
```

ファイルの内容を要約する例：

```bash
az-ai --raw "次のファイルを日本語で3行に要約してください: $(cat README.md)"
```

`--raw` フラグを使うと、スピナーや余分な改行なしで出力されます。
Espanso や AutoHotkey などのテキスト展開ツールとの連携に最適です。

モデルを指定する場合：

```bash
az-ai --model gpt-4o "詳細な技術解説をお願いします。"
```

---

## 次のステップ

このドキュメントは主要な使い方のみを紹介しています。
すべての機能については英語の [README](../../README.md) を参照してください。

主な機能：

- `az-ai --agent "タスク"` -- ツール呼び出しエージェントモード
- `az-ai --ralph "タスク" --validate "検証コマンド"` -- 自律修正ループ
- `az-ai --image "プロンプト"` -- 画像生成モード
- `az-ai --doctor` -- 設定の診断

他の言語のクイックスタートは [`docs/i18n/`](README.md) を参照してください。

---

## 翻訳ノート

| 箇所 | 翻訳 | 備考 |
|------|------|------|
| "credentials" | 認証情報 | IT 分野の標準的な訳語 |
| "deployment name" | デプロイ名 | Azure 用語に従う |
| "text expander" | テキスト展開ツール | 直訳より意訳を採用 |
| "autonomous self-correcting loop" | 自律修正ループ | [?] より自然な訳が存在する可能性あり |
