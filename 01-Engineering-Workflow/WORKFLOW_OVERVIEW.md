# AI Agent Platform Engineering Workflow Overview

This document outlines how the AI Agent Platform (C# & F#) maps to the full-lifecycle engineering methodology of AI-EOS.

## Phase Responsibilities

1. **03-Knowledge-System (Knowledge Base & Requirements)**
   - Stores original analysis reports, such as `HERMES_PARITY_REPORT.md`.
   - This is the starting point for business logic, used to provide background context to the LLM and analyze the gap between our current capabilities and existing systems (e.g., Hermes).

2. **07-Architecture (Architecture & Design)**
   - Stores high-level system designs translated from requirements, including multi-turn interaction flows and core loop conditions.
   - Contains all architecture files migrated from the original `docs/`:
     - `CONVERSATION_LOOP_WORKFLOW.md` (Conversation lifecycle)
     - `ITERATION_LOOP_DESIGN.md` (Core 5-layer architecture loop design)
     - `MULTI_TURN_TOOL_WORKFLOW.md` (Multi-turn tool execution workflow)
     - `HERMES_LOOP_CONDITIONS.md` (Boundary condition logic)

3. **08-Specification-Driven-Development (SDD)**
   - Stores testable specifications (BDD / Acceptance Criteria). For example, `AGENT_LOOP_BDD_SPECS.md` derived from the `ITERATION_LOOP_DESIGN`.
   - This layer acts as a bridge between Implementation and Verification, allowing tests to be automated.

4. **13-Agent-Framework**
   - Dedicated to specific Agent capability specifications. As the project expands, if generic Agent interfaces or base classes are extracted, they can be documented here.
   
5. **implementations/ (Code Implementation)**
   - Contains C# and F# implementations (Phase 5), built directly according to Phase 07 and 08 documents.
   - Language-specific feature designs (e.g., F# functional porting) are placed in `07-Architecture/fsharp/`, while pure code remains in their respective source repositories.
