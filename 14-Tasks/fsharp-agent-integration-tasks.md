# Task Implementation Todo List: F# Agent Platform Phase 2

Use this step-by-step checklist to guide the implementation of the Phase 2 tools, adhering strictly to Functional Programming principles (immutability, function composition, and discriminated unions) for the F# Agent Platform.

### Step 1: Pre-requisites & Submodule Prep
- [ ] Ensure all current changes on `main` are tested and passing in CI.
- [ ] Create a new feature branch in the `ai-agent-platform-design` repository for `feature/fsharp-phase2-tools`.

### Step 2: Implement Git Automation (`GitTools.fs`)
- [ ] Create `GitTools.fs` in `src/Skight.AgentPlatform.FSharp` (ensure it is added to the `.fsproj` before `ToolRegistry.fs`).
- [ ] Define a `GitTool` module with pure functions.
- [ ] Implement `gitStatus ()` using `TerminalTool.executeCommand`.
- [ ] Implement `gitCommit (message: string)`.
- [ ] Implement `gitPush ()`.
- [ ] Add unit tests in `tests/` mocking the terminal output via function injection (e.g., passing `executeCommand` as a dependency).
- [ ] Wire the Git functions into `ToolRegistry.registerCoreTools`.

### Step 3: Implement Security Hooks (`ApprovalGuard.fs`)
- [ ] Enhance the existing `ApprovalGuard.fs` module to intercept tool execution via function composition.
- [ ] Define dangerous regex patterns (e.g., `rm -rf`, `del /s /q`) as active patterns or simple discriminator functions.
- [ ] Modify the tool execution pipeline to return an `AgentState` variant like `ApprovalRequired of string * CommandContext`.
- [ ] Update `Program.fs` to pattern match on `ApprovalRequired` and prompt the user (Y/N) before recursively calling the loop with the continuation.

### Step 4: Implement Subagent Delegation (`DelegateTool.fs`)
- [ ] Create `DelegateTool.fs`.
- [ ] Implement a `delegateTask (role: string) (task: string)` function.
- [ ] The function should spin up a fresh `AgentSession` record (immutable state) seeded with the role as the system prompt.
- [ ] Call the `AgentPipeline.runTurnLoop` function, passing the isolated state and the shared `ToolRegistry`.
- [ ] Ensure recursive delegation depth is tracked in the `AgentSession` record to prevent infinite loops (cap at depth = 1).
- [ ] Wire `delegateTask` into `ToolRegistry.registerCoreTools`.

### Step 5: Final Review & Integration
- [ ] Run the full test suite (`dotnet test`).
- [ ] Use the agent to recursively call `gitCommit` and push its own code!
- [ ] Merge the feature branch and update the submodule pointer in the main `wiki` repository.
