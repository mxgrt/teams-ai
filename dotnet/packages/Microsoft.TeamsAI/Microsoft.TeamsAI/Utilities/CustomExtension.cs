using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Teams.AI.Application;
using Microsoft.Teams.AI.State;

namespace Microsoft.Teams.AI;

public static class CustomExtension
{
    private static readonly Dictionary<string, DateTime> globalStateDict = new Dictionary<string, DateTime>();

    public static void SetUserAuthenticationStatusAsSucceeded(this ITurnContext turnContext) // this works only for process within same machine, some issue with using turnState.Conversations on LOCAL to test.
    {
        lock (globalStateDict)
        {
            if (!globalStateDict.ContainsKey(turnContext.Activity.Conversation.Id))
            {
                try
                {
                    globalStateDict.Add(turnContext.Activity.Conversation.Id, DateTime.UtcNow);
                }
                catch { }
            }
        }
    }

    internal static bool IsUserAuthenticationSuccessful(this ITurnContext turnContext)
    {
        if (globalStateDict.ContainsKey(turnContext.Activity.Conversation.Id))
        {
            var insertedTime = globalStateDict[turnContext.Activity.Conversation.Id];
            lock (globalStateDict)
            {
                try
                {
                    globalStateDict.Remove(turnContext.Activity.Conversation.Id);
                }
                catch { }
            }
            return insertedTime > DateTime.UtcNow.AddMinutes(-5); // expire after 5 minutes
        }
        return false;
    }

    public static StreamingResponse TryGetStreamer(IMemory memory)
    {
        var streamer = (StreamingResponse?)memory.GetValue("temp.streamer");
        return streamer!;
    }

    internal static async Task EndStreamAsync(StreamingResponse streamer, IMemory memory)
    {
        await streamer.EndStream();
        memory?.DeleteValue("temp.streamer");
    }

    public static bool IsTextMessageActivity(this ITurnContext turnContext)
    {
        return turnContext.Activity?.Type == ActivityTypes.Message
            && !string.IsNullOrWhiteSpace(turnContext.Activity?.Text);
    }

    internal static StreamingResponse GetOrCreateStreamerFromMemory(IMemory memory, ITurnContext context, bool? enableFeedbackLoop, string feedbackLoopType, string startStreamingMessage, ILogger logger)
    {
        // Attach to any existing streamer
        var streamer = TryGetStreamer(memory);
        if (streamer == null)
        {
            // Create streamer and send initial message
            streamer = new StreamingResponse(context, logger);
            memory.SetValue("temp.streamer", streamer);

            if (enableFeedbackLoop != null)
            {
                streamer.EnableFeedbackLoop = enableFeedbackLoop;

                if (streamer.EnableFeedbackLoop == true && feedbackLoopType != null)
                {
                    streamer.FeedbackLoopType = feedbackLoopType;
                }
            }

            streamer.EnableGeneratedByAILabel = true;

            if (!string.IsNullOrEmpty(startStreamingMessage))
            {
                streamer.QueueInformativeUpdate(startStreamingMessage);
            }
        }

        return streamer;
    }
}
