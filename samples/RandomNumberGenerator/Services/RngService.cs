using System;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace RandomNumberGenerator
{
    public class RngService : RandomNumberGenerator.RandomNumberGeneratorBase
    {
        private readonly ILogger<RngService> _logger;
        private readonly Random _rando = new Random();

        public RngService(ILogger<RngService> logger)
        {
            _logger = logger;
        }

        public override Task<RandomNumberReply> GetRandomNumber(RandomNumberRequest request, ServerCallContext context)
        {
            _logger.LogInformation("Generating a new random number between {Minimum} and {Maximum} for {Client}", request.Min, request.Max, context.Peer);
            return Task.FromResult(new RandomNumberReply
            {
                Result = _rando.Next(request.Min, request.Max)
            });
        }
    }
}
