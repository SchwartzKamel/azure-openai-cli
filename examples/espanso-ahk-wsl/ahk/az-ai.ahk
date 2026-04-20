; ============================================================
;  az-ai — AutoHotkey v2 hotkeys that call WSL
; ============================================================
;
;  IMPORTANT: This script requires AutoHotkey **v2** (not v1).
;  Install from https://www.autohotkey.com/  (pick "v2").
;  Double-click this file to run, or drop a shortcut in
;  `shell:startup` for launch-on-login.
;
;  Prereqs (inside WSL, see kit README §1):
;    * /usr/local/bin/az-ai          (the AOT binary)
;    * /usr/local/bin/az-ai-wrap     (login-shell wrapper)
;    * AZUREOPENAI* vars exported in ~/.bashrc
;
;  Hotkeys:
;    Ctrl+Shift+A   Prompt box → free-form query → paste at cursor
;    Ctrl+Shift+E   Explain selected text/code (2–3 sentences)
;    Ctrl+Shift+G   Grammar/spelling fix on selected text, replace
;    Ctrl+Shift+S   Summarize selected text in 2 sentences
;
;  Design:
;    * User text is sent via STDIN through `wsl.exe -e bash -c 'az-ai-wrap ...'`
;      so shell metachars in the input are inert.
;    * stderr goes to /dev/null on the WSL side; the hidden cmd window swallows
;      everything else. No spinner, no stats, no preamble in pasted output.
;    * The clipboard is saved and restored around every selection read/paste
;      so the user's clipboard content survives the round-trip.
;
;  Latency: ~2–3 s per hotkey in steady state. First call after a cold WSL2
;  VM may add ~1 s; subsequent calls reuse the warm VM.
;
; ============================================================

#Requires AutoHotkey v2.0
#SingleInstance Force
SetTitleMatchMode 2

; ──────────────────────────────────────────────────────────────
; Run `az-ai-wrap` in WSL with stdinText piped in; return trimmed stdout.
; Uses WScript.Shell.Exec so we can write to StdIn directly (no temp file).
; ──────────────────────────────────────────────────────────────
RunAzAi(stdinText, systemPrompt, maxTokens := 500, temperature := 0.4) {
    ; Escape single quotes for the bash-c argument (close, literal, reopen).
    sysEsc := StrReplace(systemPrompt, "'", "'\''")

    ; Everything inside bash -c '...' — user text arrives via STDIN, never as
    ; an arg, so it cannot be shell-interpolated.
    bashCmd := "tr -d '\r' | az-ai-wrap --raw"
             . " --max-tokens " maxTokens
             . " --temperature " temperature
             . " --system '" sysEsc "' 2>/dev/null"

    ; cmd.exe needs outer double quotes; inside them, bash gets single quotes.
    fullCmd := A_ComSpec ' /c wsl.exe -e bash -c "' bashCmd '"'

    shell := ComObject("WScript.Shell")
    exec  := shell.Exec(fullCmd)
    exec.StdIn.Write(stdinText)
    exec.StdIn.Close()

    out := exec.StdOut.ReadAll()
    ; Strip any stray CR/LF the AOT binary won't have emitted under --raw,
    ; but PowerShell-in-the-middle layers sometimes sneak them in.
    return RTrim(out, "`r`n")
}

; ──────────────────────────────────────────────────────────────
; Get currently selected text via a save/restore clipboard dance.
; Returns "" if nothing was selected within 1 s.
; ──────────────────────────────────────────────────────────────
GetSelectedText() {
    saved := ClipboardAll()
    A_Clipboard := ""
    Send "^c"
    ok := ClipWait(1, 1)   ; 1 s timeout, any format
    text := ok ? A_Clipboard : ""
    Sleep 30               ; let apps release the clipboard
    A_Clipboard := saved
    return text
}

; ──────────────────────────────────────────────────────────────
; Paste `text` at the cursor, preserving the user's prior clipboard.
; ──────────────────────────────────────────────────────────────
PasteText(text) {
    if (text = "")
        return
    saved := ClipboardAll()
    A_Clipboard := text
    if !ClipWait(1, 0) {
        A_Clipboard := saved
        return
    }
    Send "^v"
    Sleep 80
    A_Clipboard := saved
}

; ============================================================
;  Hotkeys
; ============================================================

; Ctrl+Shift+A — Free-form prompt box
^+a:: {
    ib := InputBox("Enter your prompt:", "az-ai", "w520 h160")
    if (ib.Result != "OK" || ib.Value = "")
        return
    result := RunAzAi(ib.Value,
        "Answer the user's prompt. Be concise. Output ONLY the answer, no preamble.",
        600, 0.5)
    PasteText(result)
}

; Ctrl+Shift+E — Explain selected text/code
^+e:: {
    text := GetSelectedText()
    if (text = "")
        return
    result := RunAzAi(text,
        "Explain the following clearly in 2-3 sentences. If it is code, be precise and technical. Output ONLY the explanation, no preamble.",
        300, 0.4)
    PasteText(result)
}

; Ctrl+Shift+G — Grammar/spelling fix, replace in place
^+g:: {
    text := GetSelectedText()
    if (text = "")
        return
    result := RunAzAi(text,
        "Fix grammar and spelling. Preserve meaning, tone, and formatting. Output ONLY the corrected text, nothing else.",
        500, 0.3)
    PasteText(result)
}

; Ctrl+Shift+S — Summarize selected text
^+s:: {
    text := GetSelectedText()
    if (text = "")
        return
    result := RunAzAi(text,
        "Summarize in exactly 2 sentences. Be concise. Output ONLY the summary.",
        150, 0.4)
    PasteText(result)
}
