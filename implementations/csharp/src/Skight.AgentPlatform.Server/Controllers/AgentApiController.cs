using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Skight.AgentPlatform;
using Skight.AgentPlatform.Server.Services;

namespace Skight.AgentPlatform.Server.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class AgentApiController : ControllerBase
    {
        private readonly AgentSessionManager _sessionManager;

        public AgentApiController(AgentSessionManager sessionManager)
        {
            _sessionManager = sessionManager;
        }

        public record TurnApiRequest(string UserId, string SessionId, string UserInput);
        public record SteerApiRequest(string UserId, string SessionId, string SteeringText);
        public record MemorySaveApiRequest(string UserId, string Key, string Value);

        [HttpPost("turn")]
        public async Task<IActionResult> ExecuteTurn([FromBody] TurnApiRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.UserInput))
            {
                return BadRequest("UserId and UserInput are required.");
            }

            var runner = _sessionManager.GetOrCreateSession(request.UserId, request.SessionId ?? "default");
            var result = await runner.RunAsync(request.UserInput);

            return Ok(new
            {
                result.FinalResponse,
                result.ApiCalls,
                result.Completed,
                result.ExitReason
            });
        }

        [HttpPost("steer")]
        public IActionResult SteerTurn([FromBody] SteerApiRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.SteeringText))
            {
                return BadRequest("UserId and SteeringText are required.");
            }

            var runner = _sessionManager.GetOrCreateSession(request.UserId, request.SessionId ?? "default");
            runner.EnqueueSteering(request.SteeringText);

            return Ok(new { success = true, message = "Steering message enqueued successfully." });
        }

        [HttpGet("memory")]
        public async Task<IActionResult> QueryMemory([FromQuery] string userId, [FromQuery] string query, [FromQuery] int topK = 5)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(query))
            {
                return BadRequest("UserId and Query are required.");
            }

            var store = _sessionManager.GetMemoryStore();
            var results = await store.SearchAsync(new MemoryQuery(userId, query, Limit: topK));

            return Ok(results);
        }

        [HttpPost("memory")]
        public async Task<IActionResult> SaveMemory([FromBody] MemorySaveApiRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.Key))
            {
                return BadRequest("UserId and Key are required.");
            }

            var store = _sessionManager.GetMemoryStore();
            await store.StoreAsync(request.UserId, request.Key, request.Value);

            return Ok(new { success = true });
        }
    }
}
