using System.Text;
using Azure;
using Azure.AI.OpenAI;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace OpenAiExperimentation;

public class Summarizer
{
	private readonly ILogger<Summarizer> logger;
	private readonly string endpoint;
	private readonly string key;
	private bool ready;

	public Summarizer(ILogger<Summarizer> logger)
	{
		this.logger = logger;
		endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_ENDPOINT") ?? "";
		key = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")?? "";
		if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(key))
		{
			logger.LogError("OpenAI API endpoint or key is not set, summarizer will not work.");
			ready = false;
		}
		else ready = true;
	}

	public async Task<Result<string>> SummarizeText(string text, CancellationToken cancellationToken)
	{
		if (!ready)
		{
			return new Result<string>(new Exception("Summarizer is not ready."));
		}
		ready = false;
		logger.LogInformation("Summarizing text...");
		var client = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(key));
		var chatClient = client.GetChatClient("gpt-4o-mini");

		var updates = chatClient.CompleteChatStreamingAsync([
			new SystemChatMessage("You are an assistant that summarizes a given text. The summary starts with the topic of the text and then provides a brief overview of the main points."),
			new UserChatMessage(text)
		]);

		StringBuilder sb = new StringBuilder();
		await foreach(var update in updates)
		{
			if (update.Role.HasValue)
			{
				string s = $"{update.Role.Value}: ";
				Console.Write(s);
				sb.Append(s);
			}
			foreach (var part in update.ContentUpdate)
			{
				string s = $"{part.Text}";
				Console.Write(s);
				sb.Append(s);
			}
		}
		ready = true;
		return new Result<string>(sb.ToString());
	}
}
