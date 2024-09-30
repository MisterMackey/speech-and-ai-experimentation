using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenAiExperimentation;
using Speech;

namespace Program;

public class Program
{
	public static async Task Main(string[] args)
	{
		var builder = Host.CreateDefaultBuilder(args)
			.ConfigureServices((_, services) =>
			{
				services.AddHostedService<MainService>();
				services.AddLogging(config =>
				{
					config.AddConsole();
				});
				services.AddTransient<SpeechToTextConverter>();
				services.AddTransient<Summarizer>();
			});
		await builder.RunConsoleAsync();
	}
}