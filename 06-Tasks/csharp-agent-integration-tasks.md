# Task Implementation Todo List: C# Agent Platform Phase 2

Use this step-by-step checklist to guide the implementation of the Phase 2 tools as outlined in the Architecture and Specification documents.

### Step 1: Pre-requisites & Submodule Prep
- [ ] Ensure all current changes on `main` are tested and passing in CI.
- [ ] Create a new feature branch in the `ai-agent-platform-design` repository for `feature/phase2-tools`.

### Step 2: Implement Git Automation (`GitTools`)
- [ ] Create `GitTools.cs` in `src/Skight.AgentPlatform`.
- [ ] Implement `git_status()` using `TerminalTool.ExecuteCommandAsync`.
- [ ] Implement `git_commit(string message)`.
- [ ] Implement `git_push()`.
- [ ] Add unit tests in `tests/Skight.AgentPlatform.Tests` mocking the terminal output.
- [ ] Wire `GitTools` into `Tools.RegisterCoreTools`.

### Step 3: Implement Security Hooks (`ApprovalGuard`)
- [ ] Enhance the existing `ApprovalGuard.cs` to intercept `ToolRegistry.ExecuteToolAsync`.
- [ ] Define an array of dangerous regex patterns (e.g., `rm -rf`, `del /s /q`).
- [ ] If a match is detected, pause the turn and return an `ApprovalRequired` state.
- [ ] Modify `Program.cs` to prompt the user (Y/N) when an approval is required, before resuming execution.

### Step 4: Implement Subagent Delegation (`DelegateTool`)
- [ ] Create `DelegateTool.cs`.
- [ ] Implement `delegate_task(string role, string task)` that spins up a new isolated `AgentRunner`.
- [ ] Ensure the subagent uses the same `ToolRegistry` but an isolated `AgentSessionState`.
- [ ] Test recursive subagent spawning limits (cap at depth = 1).
- [ ] Wire `DelegateTool` into `Tools.RegisterCoreTools`.

### Step 5: Final Review & Integration
- [ ] Run the full test suite (`dotnet test`).
- [ ] Use the agent to recursively call `git_commit` and push its own code!
- [ ] Merge the feature branch and update the submodule pointer in the main `wiki` repository.
