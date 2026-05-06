; ============================================================
;  az-ai Prompt Templates — AutoHotkey v2 hotkeys
; ============================================================
;
;  Maps the five canonical task templates to Ctrl+Shift+(letter) hotkeys.
;  Each template inserts a master system prompt + task-specific template.
;
;  Templates (from docs/prompts/task-templates.md):
;    A. Knowledge Q&A / guidance           → Ctrl+Shift+Q
;    B. Architecture design                → Ctrl+Shift+R
;    C. Code generation                    → Ctrl+Shift+C
;    D. Data workflow (ETL/MLOps)          → Ctrl+Shift+D
;    E. Cost & ROI assessment              → Ctrl+Shift+L
;
;  Each hotkey:
;    1. Pops InputBox for task-specific parameters.
;    2. Prepends master system prompt.
;    3. Adds task template to the system prompt.
;    4. Sends to az-ai via WSL.
;    5. Pastes result at cursor.
;
;  Latency: ~3–5 s (Azure round-trip).
;
; ============================================================

#Requires AutoHotkey v2.0
#SingleInstance Force

; ──────────────────────────────────────────────────────────────
; Run `az-ai-wrap` in WSL with stdinText and custom system prompt.
; ──────────────────────────────────────────────────────────────
RunAzAi(stdinText, systemPrompt, maxTokens := 800, temperature := 0.3) {
    sysEsc := StrReplace(systemPrompt, "'", "'\''")
    bashCmd := "tr -d '\r' | az-ai-wrap --raw"
             . " --max-tokens " maxTokens
             . " --temperature " temperature
             . " --system '" sysEsc "' 2>/dev/null"
    fullCmd := A_ComSpec ' /c wsl.exe -e bash -c "' bashCmd '"'
    
    shell := ComObject("WScript.Shell")
    exec  := shell.Exec(fullCmd)
    exec.StdIn.Write(stdinText)
    exec.StdIn.Close()
    
    out := exec.StdOut.ReadAll()
    return RTrim(out, "`r`n")
}

; ──────────────────────────────────────────────────────────────
; Paste `text` at cursor, preserving clipboard.
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
;  Master System Prompt (prepended to all template calls)
; ============================================================

masterPrompt := "You are az-ai, a concise, secure assistant that helps design, implement, and optimize Azure AI solutions. Your tone: clear, precise, and actionable. No fluff. Ask clarifying questions if input is ambiguous. Do not hallucinate; if uncertain, state what is known and propose a plan to verify. Prioritize safety, privacy, and governance. Cite sources or indicate best-practice guidance. Provide outputs in structured, easily parseable formats. Avoid long digressions; present plan → implementation details → next steps."

; ============================================================
;  Template A: Knowledge Q&A / Guidance (Ctrl+Shift+Q)
; ============================================================

^+q:: {
    global masterPrompt
    
    ib := InputBox(
        "Template A: Knowledge Q&A`n`nAsk an Azure-focused question.`nInclude context if helpful.`n`n",
        "az-ai — Q&A Template",
        "w600 h200"
    )
    if (ib.Result != "OK" || ib.Value = "")
        return
    
    templateA := "Provide a concise, Azure-focused answer to the user's question. If relevant, include a short checklist of implementation steps. Cite sources when applicable. If ambiguous, ask 1 clarifying question before answering."
    
    systemPrompt := masterPrompt "`n`n" templateA
    result := RunAzAi(ib.Value, systemPrompt, 800, 0.3)
    PasteText(result)
}

; ============================================================
;  Template B: Architecture Design (Ctrl+Shift+R)
; ============================================================

^+r:: {
    global masterPrompt
    
    ib := InputBox(
        "Template B: Architecture Design`n`nDescribe your goal, constraints, and existing resources.`n`n",
        "az-ai — Architecture Template",
        "w600 h240"
    )
    if (ib.Result != "OK" || ib.Value = "")
        return
    
    templateB := "Given the user goal, propose a high-level Azure AI solution architecture. Include components and their roles, data flow (input → processing → output), security and compliance considerations, and rough cost estimate (ranges, not exact figures). Then provide a compact implementation plan with 4–6 milestones."
    
    systemPrompt := masterPrompt "`n`n" templateB
    result := RunAzAi(ib.Value, systemPrompt, 1500, 0.3)
    PasteText(result)
}

; ============================================================
;  Template C: Code Generation (Ctrl+Shift+C)
; ============================================================

^+c:: {
    global masterPrompt
    
    ib := InputBox(
        "Template C: Code Generation`n`nLanguage: [Python/PowerShell/CLI/C#/Terraform]`nRuntime: [local/Azure/Docker]`nTask description:`n`n",
        "az-ai — Code Generation Template",
        "w600 h240"
    )
    if (ib.Result != "OK" || ib.Value = "")
        return
    
    templateC := "Generate a minimal, reproducible example including prerequisites, installation steps, code comments, basic tests, and troubleshooting. Use placeholders for secrets. Format as: Prerequisites | Installation | Code | How to Run | Tests | Troubleshooting."
    
    systemPrompt := masterPrompt "`n`n" templateC
    result := RunAzAi(ib.Value, systemPrompt, 1200, 0.2)
    PasteText(result)
}

; ============================================================
;  Template D: Data Workflow / ETL / MLOps (Ctrl+Shift+D)
; ============================================================

^+d:: {
    global masterPrompt
    
    ib := InputBox(
        "Template D: Data Workflow / ETL / MLOps`n`nData sources, processing goals, model/output type, governance constraints:`n`n",
        "az-ai — Data Workflow Template",
        "w600 h240"
    )
    if (ib.Result != "OK" || ib.Value = "")
        return
    
    templateD := "Design an end-to-end data ingestion, preprocessing, and model deployment workflow in Azure. Include data sources and ingestion, storage and partitioning, compute (preprocessing, training, validation), model deployment and serving, monitoring and alerting, governance and compliance. Provide a textual data flow diagram and component details."
    
    systemPrompt := masterPrompt "`n`n" templateD
    result := RunAzAi(ib.Value, systemPrompt, 1500, 0.3)
    PasteText(result)
}

; ============================================================
;  Template E: Cost & ROI Assessment (Ctrl+Shift+L)
; ============================================================

^+l:: {
    global masterPrompt
    
    ib := InputBox(
        "Template E: Cost & ROI Assessment`n`nProposed solution, estimated usage, target SLA, budget/constraints:`n`n",
        "az-ai — Cost & ROI Template",
        "w600 h240"
    )
    if (ib.Result != "OK" || ib.Value = "")
        return
    
    templateE := "Provide a cost/ROI assessment for the proposed Azure AI solution. Include key assumptions (volume, duration, pricing tier), financial model (costs vs. benefits), optimization options (autoscale, reserved instances, caching, batching), break-even analysis (if applicable), and decision criteria (when to optimize, when to accept higher cost)."
    
    systemPrompt := masterPrompt "`n`n" templateE
    result := RunAzAi(ib.Value, systemPrompt, 1200, 0.3)
    PasteText(result)
}

; ============================================================
;  Reference Card (Ctrl+Shift+T)
; ============================================================

^+t:: {
    MsgBox(
        0,
        "az-ai Prompt Templates — Quick Reference",
        "
(
Template A: Knowledge Q&A / Guidance
  Hotkey: Ctrl+Shift+Q
  Use: Answer technical questions, provide guidance, clarify Azure concepts.

Template B: Architecture Design / Solution Planning
  Hotkey: Ctrl+Shift+R
  Use: Design end-to-end Azure AI solutions, plan architecture, propose strategies.

Template C: Code or Template Generation
  Hotkey: Ctrl+Shift+C
  Use: Generate minimal, reproducible code examples, templates, configs.

Template D: Data Processing / ETL / MLOps Workflow
  Hotkey: Ctrl+Shift+D
  Use: Design data pipelines, MLOps workflows, data governance solutions.

Template E: Cost, ROI, and Optimization
  Hotkey: Ctrl+Shift+L
  Use: Assess financial viability, calculate break-even, recommend optimizations.

──────────────────────────────────────────────────
Full details: docs/prompts/task-templates.md
Master prompt: docs/prompts/system-prompt-master.md
)"
    )
}
