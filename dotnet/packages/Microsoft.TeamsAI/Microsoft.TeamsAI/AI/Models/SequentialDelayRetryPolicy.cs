using Microsoft.Extensions.Logging;
using System.ClientModel.Primitives;

namespace Microsoft.Teams.AI.AI.Models
{
    /// <summary>
    /// A customized delay retry policy that uses a fixed sequence of delays that are iterated through as the number of retries increases.
    /// </summary>
    internal class SequentialDelayRetryPolicy : ClientRetryPolicy
    {
        private List<TimeSpan> _delays;
        private readonly ILogger logger;

        public SequentialDelayRetryPolicy(List<TimeSpan> delays, int _, ILogger logger) : base(delays.Count)
        {
            this._delays = delays;
            this.logger = logger;
        }

        protected override TimeSpan GetNextDelay(PipelineMessage message, int tryCount)
        {
            logger?.LogWarning("SequentialDelayRetryPolicy.GetNextDelay; Message:{Message};", message);
            int index = tryCount - 1;
            if (index < 0) { index = 0; }
            return index >= _delays.Count ? _delays[_delays.Count - 1] : _delays[index];
        }
    }
}
