using System;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Speech;
using OpenAiExperimentation;

namespace Program;

public class MainService : IHostedService
{
	private readonly CancellationTokenSource cts = new CancellationTokenSource();
	private readonly ILogger<MainService> logger;
	private readonly SpeechToTextConverter converter;
	private readonly Summarizer summarizer;

	public MainService(ILogger<MainService> logger, SpeechToTextConverter speechToTextConverter, Summarizer summarizer)
	{
		this.logger = logger;
		this.converter = speechToTextConverter;
		this.summarizer = summarizer;
	}

	public Task StartAsync(CancellationToken cancellationToken)
	{
		Task.Run(() => MainLoop(cts.Token));
		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken)
	{
		if (!cts.IsCancellationRequested)
		{
			cts.Cancel();
		}
		if (cancellationToken.IsCancellationRequested) //no grace
		{
			return Task.CompletedTask;
		}
		else
		{
			converter.Dispose(); //wait for disposal
			return Task.CompletedTask;
		}
	}

	private void MainLoop(CancellationToken cancellationToken)
	{
		Task.Delay(1000).Wait(); //so the instructions don't end in the middle of some trace log output
		while (true)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				break;
			}
			logger.LogInformation("Type 'record' [Enter] to start recording.");
			logger.LogInformation("Type 'translate' [Enter] to translate english-russian.");
			logger.LogInformation("Type 'speak' [Enter] to translate english-russian and talk.");
			logger.LogInformation("Type 'summarize text' [Enter] to summarize text (written).");
			logger.LogInformation("Type 'summarize speech' [Enter] to summarize some text (spoken).");
			logger.LogInformation("Ctrl-C to quit application.");
			var command = Console.ReadLine();
			switch (command)
			{
				case "record":
					doRecord();
					break;
				case "translate":
					doTranslate();
					break;
				case "speak":
					doSpeak();
					break;
				case "summarize text":
					doSummarizeText();
					break;
				case "summarize speech":
					doSummarizeSpeech();
					break;
				default:
					logger.LogInformation("Unknown command.");
					break;
			}
		}
	}

	private void doSummarizeSpeech()
	{
		var result = converter.CaptureAndConvertAudioToText(cts.Token).Result;
		var summary = result.Map(text => {
			logger.LogInformation($"Text to summarize: {text}");
			if (string.IsNullOrEmpty(text))
			{
				logger.LogInformation("No text to summarize.");
				return "";
			}
			var summary = summarizer.SummarizeText(text, cts.Token).Result;
			return summary;
		});
		Console.WriteLine(summary.Match(
			Succ: summary =>
			{
				Console.WriteLine($"Summary: {summary}");
				return summary;
			},
			Fail: ex =>
			{
				Console.WriteLine($"Error: {ex.Message}");
				return "Error";
			}
		));
	}

	private void doSummarizeText()
	{
		logger.LogInformation("Type the text to summarize:");
		var text = Console.ReadLine();
		logger.LogInformation($"Text to summarize: {text}");
		if (string.IsNullOrEmpty(text))
		{
			logger.LogInformation("No text to summarize.");
			return;
		}
		var result = summarizer.SummarizeText(text, cts.Token).Result;
		Console.WriteLine(result.Match(
			Succ: summary =>
			{
				Console.WriteLine($"Summary: {summary}");
				return summary;
			},
			Fail: ex =>
			{
				Console.WriteLine($"Error: {ex.Message}");
				return "Error";
			}
		));
	}

	private void doRecord()
	{
		var result = converter.CaptureAndConvertAudioToText(cts.Token).Result;
		_ = result.Match<LanguageExt.Unit>(
			Succ: text =>
			{
				Console.WriteLine($"You said: {text}");
				return LanguageExt.Unit.Default;
			},
			Fail: ex =>
			{
				Console.WriteLine($"Error: {ex.Message}");
				return LanguageExt.Unit.Default;
			}
		);
	}

	private string doTranslate()
	{
		var result = converter.CaptureAndTranslateAudioEnglishToRussian(cts.Token).Result;
		var r = result.Match(
			Succ: text =>
			{
				Console.WriteLine($"You said (in russian): {text}");
				return text;
			},
			Fail: ex =>
			{
				Console.WriteLine($"Error: {ex.Message}");
				return "Error";
			}
		);
		return r;
	}

	private void doSpeak()
	{
		string text = doTranslate();
		//this is ugly
		if (text == "Error")
		{
			return;
		}
		var result = converter.ConvertTextToAudioOutputRussian(cts.Token, text).Result;
		_ = result.Match<LanguageExt.Unit>(
			Succ: _ =>
			{
				Console.WriteLine("Text spoken.");
				return LanguageExt.Unit.Default;
			},
			Fail: ex =>
			{
				Console.WriteLine($"Error: {ex.Message}");
				return LanguageExt.Unit.Default;
			}
		);
	}
}
