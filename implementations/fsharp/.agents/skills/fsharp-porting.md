---
name: fsharp-porting
description: Functional Programming guidelines for porting Python OO agents to F#
---

# F# Functional Programming Guidelines for AI Agent Porting

You are an expert F# developer assisting in porting an object-oriented Python AI Agent architecture into an idiomatic, functional F# codebase. Your goal is to maximize immutability, leverage the F# type system, apply Domain-Driven Design (DDD) principles, and avoid object-oriented C#/Python paradigms.

Whenever you generate, refactor, or test F# code in this project, you must strictly adhere to the following architectural rules:

## 1. Make Illegal States Unrepresentable (State Machines)
Never use disparate boolean flags (e.g., `isCompleted`, `hasFailed`) or nullable fields to track agent state. Conversational turns must be modeled as pure state machines using Discriminated Unions (DUs).
*   **DO:** Bind data directly to the state it belongs to.
    ```fsharp
    type TurnOutcome =
        | Completed of FinalResponse: string
        | Interrupted of Reason: ExitReason
        | Failed of ErrorMessage: string
    ```
*   **DON'T:** Create "kitchen sink" records where fields are optionally `null` depending on boolean combinations.

## 2. Parse, Don't Validate (Smart Constructors)
Do not pass primitive types (like raw strings for Agent IDs or API keys) deep into the core domain where they must be re-validated.
*   **DO:** Use single-case Discriminated Unions to wrap primitives (e.g., `type AgentId = AgentId of string`).
*   **DO:** Use the Smart Constructor pattern. Make the underlying type's constructor private and expose a module function that returns a `Result<AgentId, string>`. Once instantiated, the type itself is the guarantee of validity.

## 3. Implement an Anti-Corruption Layer (Isolate OO SDKs)
AI agents rely on heavy, object-oriented SDKs (like the Azure OpenAI SDK). Do not leak these mutable, C#-style classes deep into the core domain logic.
*   **DO:** Build an Anti-Corruption Layer (ACL) at the I/O boundary. 
*   **DO:** Use F# Active Patterns to cleanly unwrap and destructure SDK types at this boundary.
*   **DO:** Map SDK message histories into pure, immutable F# records before passing them into the agent's reasoning loop.
*   **DON'T:** Pass `ChatRequestMessage` or heavy interface-bound types (`IUtf8JsonSerializable`) directly into core business logic functions.

## 4. Pure Functions and Append-Only History
The conversation history is an immutable ledger. 
*   **DO:** Model agent turns as pure functions: `AgentContext -> TurnOutcome`.
*   **DO:** Return a new, appended list of messages rather than mutating an existing list or buffer in place.

## 5. Functional Streaming (TaskSeq)
When handling streaming LLM responses, do not use C#-style `IAsyncEnumerable` loops with mutable variables or string builders in the core domain.
*   **DO:** Use `FSharp.Control.TaskSeq` to process the stream at the ACL boundary.
*   **DO:** Map the incoming mutable SDK chunk objects into pure F# Discriminated Unions (e.g., `| TextChunk of string | ToolCall of string`) and `yield` them to the core agent logic.

## 6. Functional Testing Practices
When writing unit tests, avoid imperative assertions and manual downcasting (`:?>`).
*   **DO:** Project complex, nested objects into flat F# Anonymous Records (`{| Field = value |}`) before asserting equality to let the compiler handle diffing.
*   **DON'T:** Write line-by-line property assertions like `Expect.isTrue result.Completed`. Pattern match on the sequence and state instead.