# 03-Knowledge-System: Knowledge Base & Domain Guide

> **Purpose:** Central repository for capturing operational domain knowledge, AI agent design patterns, terminology, and reusable knowledge assets.

---

## 📚 Knowledge Index

1. **AI Agent Platform Architectural Knowledge**:
   - For all architecture design, conversation loop specifications, comparative parity reports, and deep-dive analysis files, see [`07-Architecture/`](../07-Architecture/).

2. **Specification & Behavior Driven Specs**:
   - For BDD specs and feature specifications, see [`08-Specification-Driven-Development/`](../08-Specification-Driven-Development/).

3. **Master Task Backlog**:
   - For active and roadmap implementation task lists, see [`14-Tasks/`](../14-Tasks/).

4. **Domain Terminology & Concepts**:
   - **Pre-Verify Quality Gate (`pre_verify`)**: Automatic turn outcome interception when dirty file mutations occur without execution of build or unit test verification.
   - **Pre-API Steering Drain (`/steer`)**: Mid-turn async steering message insertion into turn context while preserving `user -> assistant -> tool -> user` strict OpenAI role alternation.
   - **Context Compaction Engine (`context_compressor`)**: Automatic payload budget management that prunes middle history turns into a `[TURN SUMMARY]` when window utilization reaches 80%.
   - **Interactive Clarification Gateway (`clarify_tool`)**: Structured user choice prompt mechanism with automatic non-interactive fallback.
