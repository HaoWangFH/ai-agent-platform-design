namespace Skight.AgentPlatform.FSharp.Tests

open Expecto
open Skight.AgentPlatform.FSharp

module ClarifyToolSpecs =

    [<Tests>]
    let tests =
        testList "Clarify Tool Gateway Specs (clarify_tool)" [

            testAsync "Feature: clarify_tool invokes callback and returns user selection" {
                let mutable askedQuestion = ""
                let callback : ClarifyTool.ClarificationCallback =
                    fun q options isMultiSelect -> async {
                        askedQuestion <- q
                        return options.[1] // Select 2nd option
                    }

                let handler = ClarifyTool.createHandler (Some callback)
                let argsJson = """{ "question": "Which database?", "options": ["SQLite", "PostgreSQL", "Redis"] }"""

                let! result = handler argsJson
                Expect.equal askedQuestion "Which database?" "Question passed to callback"
                Expect.stringContains result "User selected: PostgreSQL" "Selected option returned"
            }

            testAsync "Feature: clarify_tool non-interactive mode defaults to first option" {
                let handler = ClarifyTool.createHandler None
                let argsJson = """{ "question": "Which database?", "options": ["SQLite", "PostgreSQL"] }"""

                let! result = handler argsJson
                Expect.stringContains result "User selected (default): SQLite" "Non-interactive default to first option"
            }
        ]
