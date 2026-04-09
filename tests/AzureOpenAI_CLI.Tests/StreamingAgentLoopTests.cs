using System.Text;
using OpenAI.Chat;

namespace AzureOpenAI_CLI.Tests;

/// <summary>
/// Tests for the streaming agent loop accumulation logic used by RunAgentLoop.
///
/// The agent loop now uses CompleteChatStreamingAsync for all rounds (including the
/// final text response). Streaming updates arrive as fragments that must be
/// accumulated correctly:
///   - Tool call fragments arrive with Index/ToolCallId/FunctionName on first chunk,
///     then FunctionArgumentsUpdate on subsequent chunks.
///   - Text content arrives as ContentUpdate parts that are streamed to console.
///   - FinishReason appears only on the last streaming update.
///
/// These tests validate the accumulation patterns without requiring a live API
/// connection — they use OpenAIChatModelFactory to create realistic update objects.
/// </summary>
public class StreamingAgentLoopTests
{
    // ── Tool call fragment accumulation ──────────────────────────────

    [Fact]
    public void ToolCallAccumulation_SingleToolCall_AssemblesCorrectly()
    {
        // Arrange — simulate streaming fragments for one tool call
        // Fragment 1: index=0, id+name set, first arg chunk
        // Fragment 2: index=0, id+name null, more arg data
        // Fragment 3: finish reason
        var updates = new[]
        {
            OpenAIChatModelFactory.StreamingChatCompletionUpdate(
                toolCallUpdates: new[]
                {
                    OpenAIChatModelFactory.StreamingChatToolCallUpdate(
                        index: 0,
                        toolCallId: "call_abc123",
                        functionName: "get_datetime",
                        functionArgumentsUpdate: BinaryData.FromString("{"))
                }),
            OpenAIChatModelFactory.StreamingChatCompletionUpdate(
                toolCallUpdates: new[]
                {
                    OpenAIChatModelFactory.StreamingChatToolCallUpdate(
                        index: 0,
                        functionArgumentsUpdate: BinaryData.FromString("}"))
                }),
            OpenAIChatModelFactory.StreamingChatCompletionUpdate(
                finishReason: ChatFinishReason.ToolCalls),
        };

        // Act — replicate the accumulation logic from RunAgentLoop
        var toolCallsById = new Dictionary<int, (string Id, string Name, StringBuilder Args)>();
        bool isToolCallRound = false;

        foreach (var update in updates)
        {
            if (update.ToolCallUpdates is { Count: > 0 })
            {
                isToolCallRound = true;
                foreach (var tcUpdate in update.ToolCallUpdates)
                {
                    if (!toolCallsById.ContainsKey(tcUpdate.Index))
                        toolCallsById[tcUpdate.Index] = (tcUpdate.ToolCallId, tcUpdate.FunctionName, new StringBuilder());

                    if (tcUpdate.FunctionArgumentsUpdate is not null)
                        toolCallsById[tcUpdate.Index].Args.Append(tcUpdate.FunctionArgumentsUpdate.ToString());
                }
            }

            if (update.FinishReason == ChatFinishReason.ToolCalls)
                isToolCallRound = true;
        }

        // Assert — single tool call assembled correctly
        Assert.True(isToolCallRound);
        Assert.Single(toolCallsById);
        Assert.True(toolCallsById.ContainsKey(0));

        var (id, name, args) = toolCallsById[0];
        Assert.Equal("call_abc123", id);
        Assert.Equal("get_datetime", name);
        Assert.Equal("{}", args.ToString());
    }

    [Fact]
    public void ToolCallAccumulation_MultipleToolCalls_AssemblesAllCorrectly()
    {
        // Arrange — two tool calls in the same round, interleaved fragments
        var updates = new[]
        {
            // First fragments for both tool calls
            OpenAIChatModelFactory.StreamingChatCompletionUpdate(
                toolCallUpdates: new[]
                {
                    OpenAIChatModelFactory.StreamingChatToolCallUpdate(
                        index: 0,
                        toolCallId: "call_first",
                        functionName: "read_file",
                        functionArgumentsUpdate: BinaryData.FromString("{\"path\":\"/tmp/")),
                    OpenAIChatModelFactory.StreamingChatToolCallUpdate(
                        index: 1,
                        toolCallId: "call_second",
                        functionName: "get_datetime",
                        functionArgumentsUpdate: BinaryData.FromString("{")),
                }),
            // Continuation fragments
            OpenAIChatModelFactory.StreamingChatCompletionUpdate(
                toolCallUpdates: new[]
                {
                    OpenAIChatModelFactory.StreamingChatToolCallUpdate(
                        index: 0,
                        functionArgumentsUpdate: BinaryData.FromString("test.txt\"}")),
                    OpenAIChatModelFactory.StreamingChatToolCallUpdate(
                        index: 1,
                        functionArgumentsUpdate: BinaryData.FromString("}")),
                }),
            OpenAIChatModelFactory.StreamingChatCompletionUpdate(
                finishReason: ChatFinishReason.ToolCalls),
        };

        // Act
        var toolCallsById = new Dictionary<int, (string Id, string Name, StringBuilder Args)>();
        bool isToolCallRound = false;

        foreach (var update in updates)
        {
            if (update.ToolCallUpdates is { Count: > 0 })
            {
                isToolCallRound = true;
                foreach (var tcUpdate in update.ToolCallUpdates)
                {
                    if (!toolCallsById.ContainsKey(tcUpdate.Index))
                        toolCallsById[tcUpdate.Index] = (tcUpdate.ToolCallId, tcUpdate.FunctionName, new StringBuilder());

                    if (tcUpdate.FunctionArgumentsUpdate is not null)
                        toolCallsById[tcUpdate.Index].Args.Append(tcUpdate.FunctionArgumentsUpdate.ToString());
                }
            }

            if (update.FinishReason == ChatFinishReason.ToolCalls)
                isToolCallRound = true;
        }

        // Assert — both tool calls assembled independently
        Assert.True(isToolCallRound);
        Assert.Equal(2, toolCallsById.Count);

        // Tool call 0
        Assert.Equal("call_first", toolCallsById[0].Id);
        Assert.Equal("read_file", toolCallsById[0].Name);
        Assert.Equal("{\"path\":\"/tmp/test.txt\"}", toolCallsById[0].Args.ToString());

        // Tool call 1
        Assert.Equal("call_second", toolCallsById[1].Id);
        Assert.Equal("get_datetime", toolCallsById[1].Name);
        Assert.Equal("{}", toolCallsById[1].Args.ToString());
    }

    [Fact]
    public void ToolCallAccumulation_ManyArgumentChunks_ConcatenatesAll()
    {
        // Arrange — arguments streamed as many small fragments
        var argChunks = new[] { "{\"co", "mma", "nd\":", "\"echo", " hel", "lo\"}" };
        var updates = new List<StreamingChatCompletionUpdate>();

        // First chunk has id + name + first arg piece
        updates.Add(OpenAIChatModelFactory.StreamingChatCompletionUpdate(
            toolCallUpdates: new[]
            {
                OpenAIChatModelFactory.StreamingChatToolCallUpdate(
                    index: 0,
                    toolCallId: "call_chunked",
                    functionName: "shell_exec",
                    functionArgumentsUpdate: BinaryData.FromString(argChunks[0]))
            }));

        // Remaining chunks
        for (int i = 1; i < argChunks.Length; i++)
        {
            updates.Add(OpenAIChatModelFactory.StreamingChatCompletionUpdate(
                toolCallUpdates: new[]
                {
                    OpenAIChatModelFactory.StreamingChatToolCallUpdate(
                        index: 0,
                        functionArgumentsUpdate: BinaryData.FromString(argChunks[i]))
                }));
        }

        updates.Add(OpenAIChatModelFactory.StreamingChatCompletionUpdate(
            finishReason: ChatFinishReason.ToolCalls));

        // Act
        var toolCallsById = new Dictionary<int, (string Id, string Name, StringBuilder Args)>();
        foreach (var update in updates)
        {
            if (update.ToolCallUpdates is { Count: > 0 })
            {
                foreach (var tcUpdate in update.ToolCallUpdates)
                {
                    if (!toolCallsById.ContainsKey(tcUpdate.Index))
                        toolCallsById[tcUpdate.Index] = (tcUpdate.ToolCallId, tcUpdate.FunctionName, new StringBuilder());

                    if (tcUpdate.FunctionArgumentsUpdate is not null)
                        toolCallsById[tcUpdate.Index].Args.Append(tcUpdate.FunctionArgumentsUpdate.ToString());
                }
            }
        }

        // Assert — all chunks concatenated into valid JSON
        Assert.Single(toolCallsById);
        Assert.Equal("{\"command\":\"echo hello\"}", toolCallsById[0].Args.ToString());
    }

    [Fact]
    public void ToolCallAccumulation_OrderedByIndex_ProducesCorrectToolCallList()
    {
        // Arrange — three tool calls with non-sequential index arrival
        var toolCallsById = new Dictionary<int, (string Id, string Name, StringBuilder Args)>
        {
            [2] = ("call_c", "tool_c", new StringBuilder("{}")),
            [0] = ("call_a", "tool_a", new StringBuilder("{\"x\":1}")),
            [1] = ("call_b", "tool_b", new StringBuilder("{\"y\":2}")),
        };

        // Act — replicate the ordering from RunAgentLoop
        var toolCallList = toolCallsById.OrderBy(kv => kv.Key)
            .Select(kv => ChatToolCall.CreateFunctionToolCall(
                kv.Value.Id, kv.Value.Name,
                BinaryData.FromString(kv.Value.Args.ToString())))
            .ToList();

        // Assert — ordered by index
        Assert.Equal(3, toolCallList.Count);
        Assert.Equal("tool_a", toolCallList[0].FunctionName);
        Assert.Equal("tool_b", toolCallList[1].FunctionName);
        Assert.Equal("tool_c", toolCallList[2].FunctionName);
    }

    // ── Text content streaming ──────────────────────────────────────

    [Fact]
    public void TextAccumulation_MultipleContentUpdates_ConcatenatesAll()
    {
        // Arrange — text arrives in multiple streaming chunks
        var textChunks = new[] { "Hello", ", ", "world", "!" };
        var updates = new List<StreamingChatCompletionUpdate>();

        foreach (var chunk in textChunks)
        {
            var content = new ChatMessageContent();
            content.Add(ChatMessageContentPart.CreateTextPart(chunk));
            updates.Add(OpenAIChatModelFactory.StreamingChatCompletionUpdate(
                contentUpdate: content));
        }

        updates.Add(OpenAIChatModelFactory.StreamingChatCompletionUpdate(
            finishReason: ChatFinishReason.Stop));

        // Act — replicate text accumulation from RunAgentLoop
        var textBuilder = new StringBuilder();
        bool isToolCallRound = false;

        foreach (var update in updates)
        {
            foreach (var part in update.ContentUpdate)
            {
                if (!isToolCallRound)
                    textBuilder.Append(part.Text);
            }
        }

        // Assert
        Assert.Equal("Hello, world!", textBuilder.ToString());
        Assert.False(isToolCallRound);
    }

    [Fact]
    public void TextAccumulation_EmptyResponse_ProducesEmptyString()
    {
        // Arrange — model returns finish reason with no content
        var updates = new[]
        {
            OpenAIChatModelFactory.StreamingChatCompletionUpdate(
                finishReason: ChatFinishReason.Stop),
        };

        // Act
        var textBuilder = new StringBuilder();
        foreach (var update in updates)
        {
            foreach (var part in update.ContentUpdate)
                textBuilder.Append(part.Text);
        }

        // Assert
        Assert.Equal("", textBuilder.ToString());
    }

    // ── Finish reason detection ─────────────────────────────────────

    [Fact]
    public void FinishReason_ToolCalls_SetsToolCallFlag()
    {
        // Arrange
        var updates = new[]
        {
            OpenAIChatModelFactory.StreamingChatCompletionUpdate(
                toolCallUpdates: new[]
                {
                    OpenAIChatModelFactory.StreamingChatToolCallUpdate(
                        index: 0, toolCallId: "call_1", functionName: "test",
                        functionArgumentsUpdate: BinaryData.FromString("{}"))
                }),
            OpenAIChatModelFactory.StreamingChatCompletionUpdate(
                finishReason: ChatFinishReason.ToolCalls),
        };

        // Act
        bool isToolCallRound = false;
        foreach (var update in updates)
        {
            if (update.ToolCallUpdates is { Count: > 0 })
                isToolCallRound = true;
            if (update.FinishReason == ChatFinishReason.ToolCalls)
                isToolCallRound = true;
        }

        // Assert
        Assert.True(isToolCallRound);
    }

    [Fact]
    public void FinishReason_Stop_DoesNotSetToolCallFlag()
    {
        // Arrange
        var content = new ChatMessageContent();
        content.Add(ChatMessageContentPart.CreateTextPart("Done"));
        var updates = new[]
        {
            OpenAIChatModelFactory.StreamingChatCompletionUpdate(contentUpdate: content),
            OpenAIChatModelFactory.StreamingChatCompletionUpdate(
                finishReason: ChatFinishReason.Stop),
        };

        // Act
        bool isToolCallRound = false;
        foreach (var update in updates)
        {
            if (update.ToolCallUpdates is { Count: > 0 })
                isToolCallRound = true;
            if (update.FinishReason == ChatFinishReason.ToolCalls)
                isToolCallRound = true;
        }

        // Assert
        Assert.False(isToolCallRound);
    }

    [Fact]
    public void FinishReason_Length_DoesNotSetToolCallFlag()
    {
        // Arrange — truncated response (hit max tokens)
        var content = new ChatMessageContent();
        content.Add(ChatMessageContentPart.CreateTextPart("Truncated..."));
        var updates = new[]
        {
            OpenAIChatModelFactory.StreamingChatCompletionUpdate(contentUpdate: content),
            OpenAIChatModelFactory.StreamingChatCompletionUpdate(
                finishReason: ChatFinishReason.Length),
        };

        // Act
        bool isToolCallRound = false;
        foreach (var update in updates)
        {
            if (update.ToolCallUpdates is { Count: > 0 })
                isToolCallRound = true;
            if (update.FinishReason == ChatFinishReason.ToolCalls)
                isToolCallRound = true;
        }

        // Assert — Length should NOT trigger tool-call path
        Assert.False(isToolCallRound);
    }

    // ── Tool call to ChatToolCall conversion ────────────────────────

    [Fact]
    public void ChatToolCallCreation_ValidData_ProducesCorrectObject()
    {
        // Arrange — assembled tool call data
        string id = "call_xyz789";
        string name = "shell_exec";
        string argsJson = "{\"command\":\"ls -la\"}";

        // Act — replicate the conversion from RunAgentLoop
        var toolCall = ChatToolCall.CreateFunctionToolCall(
            id, name, BinaryData.FromString(argsJson));

        // Assert
        Assert.Equal("shell_exec", toolCall.FunctionName);
        Assert.Equal("{\"command\":\"ls -la\"}", toolCall.FunctionArguments.ToString());
    }

    [Fact]
    public void ChatToolCallCreation_EmptyArgs_ProducesValidObject()
    {
        // Arrange — tool with no arguments
        var toolCall = ChatToolCall.CreateFunctionToolCall(
            "call_empty", "get_datetime", BinaryData.FromString("{}"));

        // Assert
        Assert.Equal("get_datetime", toolCall.FunctionName);
        Assert.Equal("{}", toolCall.FunctionArguments.ToString());
    }

    // ── AssistantChatMessage from tool calls ────────────────────────

    [Fact]
    public void AssistantMessage_FromToolCallList_CanBeCreated()
    {
        // Arrange — build tool calls as the streaming loop does
        var toolCalls = new List<ChatToolCall>
        {
            ChatToolCall.CreateFunctionToolCall("call_1", "read_file",
                BinaryData.FromString("{\"path\":\"/etc/hosts\"}")),
            ChatToolCall.CreateFunctionToolCall("call_2", "get_datetime",
                BinaryData.FromString("{}")),
        };

        // Act — this is the constructor used in the streaming agent loop
        var assistantMsg = new AssistantChatMessage(toolCalls);

        // Assert — message was created (not null) — the constructor accepts tool calls
        Assert.NotNull(assistantMsg);
    }

    // ── ToolChatMessage from results ────────────────────────────────

    [Fact]
    public void ToolMessage_WithResult_CanBeCreated()
    {
        // Arrange
        string toolCallId = "call_abc";
        string result = "File contents here";

        // Act
        var toolMsg = new ToolChatMessage(toolCallId, result);

        // Assert
        Assert.NotNull(toolMsg);
    }

    // ── Edge cases ──────────────────────────────────────────────────

    [Fact]
    public void ToolCallAccumulation_NullFunctionArgumentsUpdate_SkipsAppend()
    {
        // Arrange — an update with no argument data (initial fragment, name only)
        var updates = new[]
        {
            OpenAIChatModelFactory.StreamingChatCompletionUpdate(
                toolCallUpdates: new[]
                {
                    OpenAIChatModelFactory.StreamingChatToolCallUpdate(
                        index: 0,
                        toolCallId: "call_npe",
                        functionName: "get_datetime")
                    // No functionArgumentsUpdate
                }),
            OpenAIChatModelFactory.StreamingChatCompletionUpdate(
                toolCallUpdates: new[]
                {
                    OpenAIChatModelFactory.StreamingChatToolCallUpdate(
                        index: 0,
                        functionArgumentsUpdate: BinaryData.FromString("{}"))
                }),
            OpenAIChatModelFactory.StreamingChatCompletionUpdate(
                finishReason: ChatFinishReason.ToolCalls),
        };

        // Act
        var toolCallsById = new Dictionary<int, (string Id, string Name, StringBuilder Args)>();
        foreach (var update in updates)
        {
            if (update.ToolCallUpdates is { Count: > 0 })
            {
                foreach (var tcUpdate in update.ToolCallUpdates)
                {
                    if (!toolCallsById.ContainsKey(tcUpdate.Index))
                        toolCallsById[tcUpdate.Index] = (tcUpdate.ToolCallId, tcUpdate.FunctionName, new StringBuilder());

                    if (tcUpdate.FunctionArgumentsUpdate is not null)
                        toolCallsById[tcUpdate.Index].Args.Append(tcUpdate.FunctionArgumentsUpdate.ToString());
                }
            }
        }

        // Assert — no crash, args assembled from second fragment only
        Assert.Single(toolCallsById);
        Assert.Equal("{}", toolCallsById[0].Args.ToString());
    }

    [Fact]
    public void ToolCallAccumulation_EmptyToolCallUpdates_NotTreatedAsToolRound()
    {
        // Arrange — update with empty ToolCallUpdates list (count = 0)
        var content = new ChatMessageContent();
        content.Add(ChatMessageContentPart.CreateTextPart("Just text"));
        var updates = new[]
        {
            OpenAIChatModelFactory.StreamingChatCompletionUpdate(
                contentUpdate: content,
                toolCallUpdates: Array.Empty<StreamingChatToolCallUpdate>()),
            OpenAIChatModelFactory.StreamingChatCompletionUpdate(
                finishReason: ChatFinishReason.Stop),
        };

        // Act
        var toolCallsById = new Dictionary<int, (string Id, string Name, StringBuilder Args)>();
        bool isToolCallRound = false;

        foreach (var update in updates)
        {
            if (update.ToolCallUpdates is { Count: > 0 })
            {
                isToolCallRound = true;
            }
            if (update.FinishReason == ChatFinishReason.ToolCalls)
                isToolCallRound = true;
        }

        // Assert — empty list does not trigger tool call path
        Assert.False(isToolCallRound);
        Assert.Empty(toolCallsById);
    }

    [Fact]
    public void TextAccumulation_ToolCallRound_DoesNotAccumulateText()
    {
        // Arrange — some models send partial text before deciding on tool calls
        var textContent = new ChatMessageContent();
        textContent.Add(ChatMessageContentPart.CreateTextPart("I'll look that up"));

        var updates = new[]
        {
            OpenAIChatModelFactory.StreamingChatCompletionUpdate(contentUpdate: textContent),
            OpenAIChatModelFactory.StreamingChatCompletionUpdate(
                toolCallUpdates: new[]
                {
                    OpenAIChatModelFactory.StreamingChatToolCallUpdate(
                        index: 0,
                        toolCallId: "call_mixed",
                        functionName: "web_fetch",
                        functionArgumentsUpdate: BinaryData.FromString("{\"url\":\"https://example.com\"}"))
                }),
            OpenAIChatModelFactory.StreamingChatCompletionUpdate(
                finishReason: ChatFinishReason.ToolCalls),
        };

        // Act — replicate the exact logic from RunAgentLoop
        var textBuilder = new StringBuilder();
        bool isToolCallRound = false;

        foreach (var update in updates)
        {
            if (update.ToolCallUpdates is { Count: > 0 })
                isToolCallRound = true;

            foreach (var part in update.ContentUpdate)
            {
                if (!isToolCallRound)
                    textBuilder.Append(part.Text);
            }

            if (update.FinishReason == ChatFinishReason.ToolCalls)
                isToolCallRound = true;
        }

        // Assert — text from before the tool call flag was set IS accumulated
        // (this matches the RunAgentLoop behavior: text before tool call detection
        // is captured but ultimately ignored because isToolCallRound is true)
        Assert.True(isToolCallRound);
        Assert.Equal("I'll look that up", textBuilder.ToString());
    }

    // ── Round counting / totalToolCalls tracking ────────────────────

    [Fact]
    public void TotalToolCalls_AccrossStreamedRounds_AccumulatesCorrectly()
    {
        // Arrange — simulate the accumulation pattern across two rounds
        int totalToolCalls = 0;

        // Round 1: 2 tool calls streamed
        var round1ToolCalls = new Dictionary<int, (string Id, string Name, StringBuilder Args)>
        {
            [0] = ("call_1", "read_file", new StringBuilder("{}")),
            [1] = ("call_2", "get_datetime", new StringBuilder("{}")),
        };
        var round1List = round1ToolCalls.OrderBy(kv => kv.Key)
            .Select(kv => ChatToolCall.CreateFunctionToolCall(
                kv.Value.Id, kv.Value.Name,
                BinaryData.FromString(kv.Value.Args.ToString())))
            .ToList();
        totalToolCalls += round1List.Count;

        // Round 2: 1 tool call
        var round2ToolCalls = new Dictionary<int, (string Id, string Name, StringBuilder Args)>
        {
            [0] = ("call_3", "shell_exec", new StringBuilder("{\"command\":\"ls\"}")),
        };
        var round2List = round2ToolCalls.OrderBy(kv => kv.Key)
            .Select(kv => ChatToolCall.CreateFunctionToolCall(
                kv.Value.Id, kv.Value.Name,
                BinaryData.FromString(kv.Value.Args.ToString())))
            .ToList();
        totalToolCalls += round2List.Count;

        // Assert
        Assert.Equal(3, totalToolCalls);
    }

    // ── Negative tests ──────────────────────────────────────────────

    [Fact]
    public void ToolCallAccumulation_ToolCallWithNoFinishReason_StillDetectedAsToolRound()
    {
        // Arrange — ToolCallUpdates present but no FinishReason.ToolCalls
        // (shouldn't happen in practice, but tests the guard clause)
        var updates = new[]
        {
            OpenAIChatModelFactory.StreamingChatCompletionUpdate(
                toolCallUpdates: new[]
                {
                    OpenAIChatModelFactory.StreamingChatToolCallUpdate(
                        index: 0, toolCallId: "call_orphan", functionName: "test",
                        functionArgumentsUpdate: BinaryData.FromString("{}"))
                }),
            // No finish reason at all
        };

        // Act
        var toolCallsById = new Dictionary<int, (string Id, string Name, StringBuilder Args)>();
        bool isToolCallRound = false;

        foreach (var update in updates)
        {
            if (update.ToolCallUpdates is { Count: > 0 })
            {
                isToolCallRound = true;
                foreach (var tcUpdate in update.ToolCallUpdates)
                {
                    if (!toolCallsById.ContainsKey(tcUpdate.Index))
                        toolCallsById[tcUpdate.Index] = (tcUpdate.ToolCallId, tcUpdate.FunctionName, new StringBuilder());

                    if (tcUpdate.FunctionArgumentsUpdate is not null)
                        toolCallsById[tcUpdate.Index].Args.Append(tcUpdate.FunctionArgumentsUpdate.ToString());
                }
            }
        }

        // Assert — tool call presence alone sets the flag (defensive)
        Assert.True(isToolCallRound);
        Assert.Single(toolCallsById);
    }

    [Fact]
    public void ToolCallAccumulation_NoUpdatesAtAll_IsNotToolRound()
    {
        // Arrange — empty stream (degenerate case)
        var updates = Array.Empty<StreamingChatCompletionUpdate>();

        // Act
        bool isToolCallRound = false;
        var toolCallsById = new Dictionary<int, (string Id, string Name, StringBuilder Args)>();

        foreach (var update in updates)
        {
            if (update.ToolCallUpdates is { Count: > 0 })
                isToolCallRound = true;
        }

        // Assert
        Assert.False(isToolCallRound);
        Assert.Empty(toolCallsById);
    }

    [Fact]
    public void FinishReasonDetection_ContentFilter_NotTreatedAsToolCalls()
    {
        // Arrange — content filter finish reason should NOT be treated as tool calls
        var updates = new[]
        {
            OpenAIChatModelFactory.StreamingChatCompletionUpdate(
                finishReason: ChatFinishReason.ContentFilter),
        };

        // Act
        bool isToolCallRound = false;
        foreach (var update in updates)
        {
            if (update.FinishReason == ChatFinishReason.ToolCalls)
                isToolCallRound = true;
        }

        // Assert — ContentFilter != ToolCalls
        Assert.False(isToolCallRound);
    }

    [Fact]
    public void ToolCallCondition_IsToolCallRoundTrue_ButEmptyDict_SkipsToolExecution()
    {
        // Arrange — edge case: isToolCallRound set by FinishReason but no actual tool calls
        bool isToolCallRound = true;
        var toolCallsById = new Dictionary<int, (string Id, string Name, StringBuilder Args)>();

        // Act — replicate the guard condition from RunAgentLoop
        bool shouldExecuteTools = isToolCallRound && toolCallsById.Count > 0;

        // Assert — empty dict prevents tool execution even if flag is set
        Assert.False(shouldExecuteTools);
    }
}
