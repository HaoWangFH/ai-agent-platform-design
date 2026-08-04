# GitHub Copilot Custom Instructions

You are acting as Phase 5 (Implementation) of the AI-EOS (AI Engineering Operating System).
When generating, refactoring, or reviewing code in this repository (`projects/ai-agent-platform-design`), you MUST strictly adhere to the following rules:

## General Guidelines
- **Read Specifications First**: Before implementing any core logic, look for relevant BDD specs in `08-Specification-Driven-Development/` (e.g., `AGENT_LOOP_BDD_SPECS.zh.md`). Do not hallucinate behavior that contradicts these specs.
- **Adhere to Architecture**: Understand that this project uses a 5-layer resilient architecture loop. Do not simplify the loop into a basic `while` loop. If you are modifying the loop, reference `07-Architecture/ITERATION_LOOP_DESIGN.zh.md`.
- **Language Context**: This repo contains both C# and F# implementations (`implementations/csharp` and `implementations/fsharp`). Only use F# idioms (immutable state, discriminated unions, pattern matching) in the F# folder, and C# OO idioms in the C# folder.
- **Testing (Phase 6)**: When asked to write tests, always use the skeletons provided in `09-Testing/TEST_SKELETON_GUIDE.md` (xUnit for C#, Expecto for F#).
- **Bilingual Documentation**: When generating or updating documentation, ALWAYS generate two versions: an English version (normal naming of `.md`) and a Chinese version (with a `.zh.md` suffix).

## Documentation Guidelines
- The AI-EOS.Docs project is documentation-only and should not be built as part of normal solution builds.
