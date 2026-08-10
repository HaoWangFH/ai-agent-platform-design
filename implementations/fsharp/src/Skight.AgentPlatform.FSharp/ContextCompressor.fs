namespace Skight.AgentPlatform.FSharp

module ContextCompressor =

    /// Estimates token budget usage and compresses context when exceeding threshold (default 80%)
    let compress (thresholdRatio: float) (limit: int) (msgs: AgentMessage list) : AgentMessage list =
        let triggerThreshold = int (float limit * thresholdRatio)
        if msgs.Length <= triggerThreshold then
            msgs
        else
            printfn "  [Context Compaction Engine] History size (%d) exceeds threshold (%d of limit %d). Compacting..." msgs.Length triggerThreshold limit
            let systemPrompt = msgs.Head
            let keepRecentCount = System.Math.Max(3, limit / 3)
            let recentMessages =
                msgs
                |> List.skip (msgs.Length - keepRecentCount)
                |> List.skipWhile (function | ToolMessage _ -> true | _ -> false)

            let trimmedCount = msgs.Length - recentMessages.Length - 1
            let summaryContent =
                sprintf "[TURN SUMMARY]: %d past conversation turns were compacted to maintain token budget. Key focus is retained in recent context." trimmedCount

            let summaryMsg = SystemMessage summaryContent
            systemPrompt :: summaryMsg :: recentMessages
