# GitHub Copilot Custom Instructions

You are acting as Phase 5 (Implementation) of the AI-EOS (AI Engineering Operating System).
When generating, refactoring, or reviewing code in this repository (`projects/ai-agent-platform-design`), you MUST strictly adhere to the following rules:

## General Guidelines
- **Read Specifications First (Phase 3)**: Before implementing any core logic, inspect relevant BDD specs in `05-Specification-Driven-Development/` (e.g., `GAME_CHANGER_FEATURES_SPECS.md`). Do not hallucinate behavior that contradicts these specs.
- **Adhere to Architecture (Phase 2)**: Understand that this project uses a 5-layer resilient architecture loop. Do not simplify the loop into a basic `while` loop. If you are modifying the loop, reference `04-Architecture/ITERATION_LOOP_DESIGN.md`.
- **Language Context (Phase 5)**: This repo contains both C# and F# implementations (`implementations/csharp` and `implementations/fsharp`). Only use F# idioms (immutable state, discriminated unions, pattern matching) in the F# folder, and C# OO idioms in the C# folder.
- **Testing & Stop Gates (Phase 6)**: When asked to write tests, always use the skeletons provided in `07-Testing/TEST_SKELETON_GUIDE.md` (xUnit for C#, Expecto for F#). Support the `pre_verify` code quality gate.
- **Knowledge Capture (Phase 7)**: Document new domain concepts or bug resolutions into `08-Knowledge-System/KNOWLEDGE_INDEX.md`.
- **Bilingual Documentation**: When generating or updating documentation, ALWAYS generate two versions: an English version (normal naming of `.md`) and a Chinese version (with a `.zh.md` suffix).

## Solution Guidelines
- The `AI-EOS.Docs` project is documentation-only and should not be built as part of normal solution builds.
