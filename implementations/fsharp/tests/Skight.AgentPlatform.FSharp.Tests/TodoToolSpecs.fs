namespace Skight.AgentPlatform.FSharp.Tests

open System
open System.Text.Json
open Expecto
open Skight.AgentPlatform.FSharp

module TodoToolSpecs =

    [<Tests>]
    let tests =
        testList "TODO Tool Module Specs" [

            testAsync "Feature: Add, List, and Complete TODO Checklist" {
                let addJson = JsonSerializer.Serialize({| task = "Write unit tests" |})
                let completeJson = JsonSerializer.Serialize({| id = 1 |})

                let! addRes = TodoTool.addTodoAsync addJson |> Async.AwaitTask
                let! listBefore = TodoTool.listTodosAsync () |> Async.AwaitTask
                let! completeRes = TodoTool.completeTodoAsync completeJson |> Async.AwaitTask
                let! listAfter = TodoTool.listTodosAsync () |> Async.AwaitTask

                Expect.stringContains addRes "Write unit tests" "Add TODO should report task name"
                Expect.stringContains listBefore "[ ] Write unit tests" "List should show uncompleted task"
                Expect.stringContains listAfter "[x] Write unit tests" "List should show completed task"
            }
        ]
