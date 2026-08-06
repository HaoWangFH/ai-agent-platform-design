using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Skight.AgentPlatform
{
    public class TodoItem
    {
        public int Id { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
    }

    public static class TodoTool
    {
        private static readonly List<TodoItem> Todos = new();
        private static int _nextId = 1;

        public static Task<string> AddTodoAsync(string argsJson)
        {
            lock (Todos)
            {
                try
                {
                    using var doc = JsonDocument.Parse(argsJson);
                    var root = doc.RootElement;
                    if (!root.TryGetProperty("task", out var taskProp))
                    {
                        return Task.FromResult("Error: Missing 'task' argument.");
                    }

                    var item = new TodoItem
                    {
                        Id = _nextId++,
                        Description = taskProp.GetString()!,
                        IsCompleted = false
                    };
                    Todos.Add(item);
                    return Task.FromResult($"Added TODO #{item.Id}: {item.Description}");
                }
                catch (Exception ex)
                {
                    return Task.FromResult($"Error adding TODO: {ex.Message}");
                }
            }
        }

        public static Task<string> ListTodosAsync()
        {
            lock (Todos)
            {
                if (Todos.Count == 0) return Task.FromResult("TODO list is empty.");
                var sb = new StringBuilder("Current TODO List:\n");
                foreach (var todo in Todos)
                {
                    var status = todo.IsCompleted ? "[x]" : "[ ]";
                    sb.AppendLine($"#{todo.Id} {status} {todo.Description}");
                }
                return Task.FromResult(sb.ToString().TrimEnd());
            }
        }

        public static Task<string> CompleteTodoAsync(string argsJson)
        {
            lock (Todos)
            {
                try
                {
                    using var doc = JsonDocument.Parse(argsJson);
                    var root = doc.RootElement;
                    if (!root.TryGetProperty("id", out var idProp))
                    {
                        return Task.FromResult("Error: Missing 'id' argument.");
                    }

                    var id = idProp.GetInt32();
                    var todo = Todos.Find(t => t.Id == id);
                    if (todo == null) return Task.FromResult($"Error: TODO #{id} not found.");

                    todo.IsCompleted = true;
                    return Task.FromResult($"Completed TODO #{id}: {todo.Description}");
                }
                catch (Exception ex)
                {
                    return Task.FromResult($"Error completing TODO: {ex.Message}");
                }
            }
        }
    }
}
