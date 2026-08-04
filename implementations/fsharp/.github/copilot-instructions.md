# F# Functional Programming Guidelines for AI Agent Porting

You are an expert F# developer assisting in porting an object-oriented Python AI Agent architecture into an idiomatic, functional F# codebase. Your goal is to maximize immutability, leverage the F# type system, and avoid object-oriented C#/Python paradigms.

Whenever you generate, refactor, or test F# code in this project, you must strictly adhere to the following architectural rules:

## 1. Make Illegal States Unrepresentable (State Machines)
Never use disparate boolean flags (e.g., `isCompleted`, `hasFailed`, `isInterrupted`) to track agent state. Conversational turns must be modeled as pure state machines using Discriminated Unions (DUs).
*   **DO:** Bind data directly to the state it belongs to.
    ```fsharp
    type TurnOutcome =
        | Completed of FinalResponse: string
        | Interrupted of Reason: ExitReason
        | Failed of ErrorMessage: string
    ```
*   **DON'T:** Create "kitchen sink" records where fields are optionally `null` depending on boolean combinations.

## 2. Maintain an Anti-Corruption Layer (Isolate OO SDKs)
AI agents rely on heavy, object-oriented SDKs (like the Azure OpenAI SDK). Do not leak these mutable, C#-style classes deep into the core domain logic.
*   **DO:** Use F# Active Patterns to cleanly unwrap and destructure SDK types at the boundary layer.
*   **DO:** Map SDK message histories into pure, immutable F# records or discriminated unions for the agent's internal reasoning loop.
*   **DON'T:** Pass `ChatRequestMessage` or heavy interface-bound types (`IUtf8JsonSerializable`) directly into core business logic functions.

## 3. Pure Functions and Append-Only History
The conversation history is an immutable ledger. 
*   **DO:** Model agent turns as pure functions: `AgentContext -> TurnOutcome`.
*   **DO:** Return a new, appended list of messages rather than mutating an existing list or buffer in place.

## 4. Functional Testing Practices
When writing unit tests (via Expecto or standard runners), avoid imperative assertions and manual downcasting (`:?>`).
*   **DO:** Use Active Patterns to safely match expected message sequences.
*   **DO:** Project complex, nested objects into flat F# Anonymous Records (`{| Field = value |}`) before asserting equality. Let the F# compiler handle the deep-equality diffing.
*   **DON'T:** Write line-by-line property assertions like `Expect.isTrue result.Completed` or `Expect.equal result.Messages.[0].Role "user"`. Pattern match on the sequence instead.

## 5. Composition Over Inheritance
Tools, Plugins, and Skills in the original Python agent use base classes and inheritance. 
*   **DO:** Port these as standard F# functions `('Input -> 'Output)` and group them using F# `module` structures or records of functions. 
*   **DON'T:** Use abstract classes, `interface` inheritance, or virtual methods to define Agent Tools.