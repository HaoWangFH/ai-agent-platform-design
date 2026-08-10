using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Skight.AgentPlatform;
using Skight.AgentPlatform.Server.Protos;

namespace Skight.AgentPlatform.Server.Services
{
    public class AgentGrpcService : AgentService.AgentServiceBase
    {
        private readonly AgentSessionManager _sessionManager;

        public AgentGrpcService(AgentSessionManager sessionManager)
        {
            _sessionManager = sessionManager;
        }

        public override async Task<TurnResponse> ExecuteTurn(TurnRequest request, ServerCallContext context)
        {
            var runner = _sessionManager.GetOrCreateSession(request.UserId, request.SessionId);
            var result = await runner.RunAsync(request.UserInput);

            return new TurnResponse
            {
                FinalResponse = result.FinalResponse ?? string.Empty,
                ApiCalls = result.ApiCalls,
                Completed = result.Completed,
                ExitReason = result.ExitReason ?? string.Empty
            };
        }

        public override Task<SteerResponse> SteerTurn(SteerRequest request, ServerCallContext context)
        {
            var runner = _sessionManager.GetOrCreateSession(request.UserId, request.SessionId);
            runner.EnqueueSteering(request.SteeringText);

            return Task.FromResult(new SteerResponse
            {
                Success = true,
                Message = "Steering message enqueued successfully."
            });
        }

        public override async Task<MemoryQueryResponse> QueryMemory(MemoryQueryRequest request, ServerCallContext context)
        {
            var store = _sessionManager.GetMemoryStore();
            var items = await store.SearchAsync(new MemoryQuery(request.UserId, request.Query, Limit: request.TopK > 0 ? request.TopK : 5));

            var response = new MemoryQueryResponse();
            response.Items.AddRange(items.Select(i => new MemoryItem
            {
                Key = i.Key,
                Value = i.Value,
                Score = i.Score
            }));

            return response;
        }

        public override async Task<MemorySaveResponse> SaveMemory(MemorySaveRequest request, ServerCallContext context)
        {
            var store = _sessionManager.GetMemoryStore();
            await store.StoreAsync(request.UserId, request.Key, request.Value);

            return new MemorySaveResponse { Success = true };
        }
    }
}
