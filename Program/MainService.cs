using System;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Speech;

namespace Program;

public class MainService : IHostedService
{
	private readonly CancellationTokenSource cts = new CancellationTokenSource();
	private readonly ILogger<MainService> logger;
	private readonly SpeechToTextConverter converter;

	public MainService(ILogger<MainService> logger, SpeechToTextConverter speechToTextConverter)
	{
		this.logger = logger;
		this.converter = speechToTextConverter;
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
				default:
					logger.LogInformation("Unknown command.");
					break;
			}
		}
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
