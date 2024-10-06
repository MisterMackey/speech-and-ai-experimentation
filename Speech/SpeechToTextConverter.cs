using System;
using System.IO;
using System.Threading.Tasks;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.CognitiveServices.Speech.Translation;
using Microsoft.Extensions.Logging;

namespace Speech;

/// <summary>
/// not really accurate anymore, does a bunch of things now
/// </summary> <summary>
/// 
/// </summary>
public class SpeechToTextConverter : IDisposable
{
	private readonly ILogger<SpeechToTextConverter> logger;
	private readonly string? key;
	private readonly string? region;
	private volatile bool ready;
	private bool disposedValue;
	private volatile bool translating;
	private TaskCompletionSource<int>? stopRecognition;

	public SpeechToTextConverter(ILogger<SpeechToTextConverter> logger)
	{
		this.logger = logger;
		this.key = Environment.GetEnvironmentVariable("SPEECH_TO_TEXT_KEY");
		this.region = Environment.GetEnvironmentVariable("SPEECH_TO_TEXT_REGION");
		if (string.IsNullOrEmpty(this.key) || string.IsNullOrEmpty(this.region))
		{
			logger.LogError("Speech to text key or region is not set, converter will not work.");
			ready = false;
		}
		else
		{
			ready = true;
		}
	}

	public async Task<Result<string>> CaptureAndConvertAudioToText(CancellationToken cancellationToken)
	{
		if (!ready)
		{
			return new Result<string>(new Exception("Speech to text converter is not ready."));
		}
		ready = false;
		var speechConfig = SpeechConfig.FromSubscription(key, region);
		speechConfig.SpeechRecognitionLanguage = "en-US";

		using var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
		using var recognizer = new SpeechRecognizer(speechConfig, audioConfig);

		logger.LogInformation("Say something...");
		var resultTask = recognizer.RecognizeOnceAsync();
		while (!cancellationToken.IsCancellationRequested)
		{
			if (resultTask.IsCompleted)
			{
				break;
			}
			await Task.Delay(100);
		}
		if (cancellationToken.IsCancellationRequested)
		{
			await recognizer.StopContinuousRecognitionAsync();
		}
		var result = await resultTask;
		ready = true;
		switch (result.Reason)
		{
			case ResultReason.RecognizedSpeech:
				return new Result<string>(result.Text);
			case ResultReason.NoMatch:
				return new Result<string>(new Exception("No speech could be recognized."));
			case ResultReason.Canceled:
				var cancellation = CancellationDetails.FromResult(result);
				return new Result<string>(new Exception($"CANCELED: Reason={cancellation.Reason}"));
			default:
				return new Result<string>(new Exception($"Unknown error. Reason={result.Reason}"));
		}
	}

	public async Task<Result<string>> CaptureAndTranslateAudioEnglishToRussian(CancellationToken cancellationToken)
	{
		if (!ready)
		{
			return new Result<string>(new Exception("Speech to text converter is not ready."));
		}
		ready = false;
		var speechTranslationConfig = SpeechTranslationConfig.FromSubscription(key, region);
		speechTranslationConfig.SpeechRecognitionLanguage = "en-US";
		speechTranslationConfig.AddTargetLanguage("ru");
		using var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
		using var recognizer = new TranslationRecognizer(speechTranslationConfig, audioConfig);

		logger.LogInformation("Say something...");
		var resultTask = recognizer.RecognizeOnceAsync();
		while (!cancellationToken.IsCancellationRequested)
		{
			if (resultTask.IsCompleted)
			{
				break;
			}
			await Task.Delay(100);
		}
		if (cancellationToken.IsCancellationRequested)
		{
			await recognizer.StopContinuousRecognitionAsync();
		}
		var result = await resultTask;
		ready = true;
		switch (result.Reason)
		{
			case ResultReason.TranslatedSpeech:
				return new Result<string>(result.Translations["ru"]);
			case ResultReason.NoMatch:
				return new Result<string>(new Exception("No speech could be recognized."));
			case ResultReason.Canceled:
				var cancellation = CancellationDetails.FromResult(result);
				return new Result<string>(new Exception($"CANCELED: Reason={cancellation.Reason}"));
			default:
				return new Result<string>(new Exception($"Unknown error. Reason={result.Reason}"));
		}
	}

	public async Task<Result<byte[]>> ConvertTextToAudioOutput(CancellationToken cancellationToken, string text, string lang = "ru-RU", string voice = "ru-RU-DariyaNeural")
	{
		if (!ready)
		{
			return new Result<byte[]>(new Exception("Speech to text converter is not ready."));
		}
		ready = false;
		var speechConfig = SpeechConfig.FromSubscription(key, region);
		speechConfig.SpeechSynthesisLanguage = lang;
		speechConfig.SpeechSynthesisVoiceName = voice;

		using var audioConfig = AudioConfig.FromDefaultSpeakerOutput();
		using var speechSynthesizer = new SpeechSynthesizer(speechConfig, audioConfig);

		var resultTask = speechSynthesizer.SpeakTextAsync(text);
		while (!cancellationToken.IsCancellationRequested)
		{
			if (resultTask.IsCompleted)
			{
				break;
			}
			await Task.Delay(100);
		}
		if (cancellationToken.IsCancellationRequested)
		{
			await speechSynthesizer.StopSpeakingAsync();
		}
		var result = await resultTask;
		ready = true;
		switch (result.Reason)
		{
			case ResultReason.SynthesizingAudioCompleted:
				return new Result<byte[]>(result.AudioData);
			case ResultReason.Canceled:
				var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
				return new Result<byte[]>(new Exception($"CANCELED: Reason={cancellation.Reason}, Details: {cancellation.ErrorDetails}"));
			default:
				return new Result<byte[]>(new Exception($"Unknown error. Reason={result.Reason}"));
		}
	}

	public async Task<Result<int>> ContinuousTranslation(CancellationToken cancellationToken, string lang, string voice, string source = "en-us")
	{
		if (translating)
		{
			return new Result<int>(new Exception("Speech to text converter is not ready."));
		}
		translating = true;
		using var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
		var speechConfig = SpeechTranslationConfig.FromSubscription(key, region);
		speechConfig.SpeechRecognitionLanguage = source;
		var targetLang = lang.Split("-").First();
		speechConfig.AddTargetLanguage(targetLang);

		// not rly danger zone but i seem to talk too fast for the default 500 ms
		//speechConfig.SetProperty(PropertyId.Speech_SegmentationSilenceTimeoutMs, "300");

		using var recognizer = new TranslationRecognizer(speechConfig, audioConfig);

		stopRecognition = new TaskCompletionSource<int>();
		recognizer.Recognizing += (s, e) =>
		{
			logger.LogInformation($"TRANSLATING: {e.Result.Text} -> {e.Result.Translations[targetLang]}");
		};
		recognizer.Recognized += (s, e) =>
		{
			if (e.Result.Reason == ResultReason.TranslatedSpeech)
			{
				logger.LogInformation($"TRANSLATED: {e.Result.Text} -> {e.Result.Translations[targetLang]}");
				_ = Task.Run(() => ConvertTextToAudioOutput(cancellationToken, e.Result.Translations[targetLang], lang, voice));
			}
			else if (e.Result.Reason == ResultReason.NoMatch)
			{
				logger.LogInformation($"NOMATCH: Speech could not be recognized.");
			}
		};
		recognizer.Canceled += (s, e) =>
		{
			logger.LogInformation($"CANCELED: Reason={e.Reason}");
			if (e.Reason == CancellationReason.Error)
			{
				logger.LogWarning($"CANCELED: ErrorCode={e.ErrorCode}");
				logger.LogWarning($"CANCELED: ErrorDetails={e.ErrorDetails}");
				logger.LogWarning($"CANCELED: Did you update the subscription info?");
			}
			stopRecognition.TrySetResult(0);
		};
		recognizer.SessionStopped += (s, e) =>
		{
			logger.LogInformation("\n    Session stopped event.");
			stopRecognition.TrySetResult(0);
		};
		await recognizer.StartContinuousRecognitionAsync();
		while (!cancellationToken.IsCancellationRequested)
		{
			if (stopRecognition.Task.IsCompleted)
			{
				break;
			}
			await Task.Delay(100);
		}
		int result = await stopRecognition.Task;
		logger.LogInformation("Stopping continuous recognition...");
		await recognizer.StopContinuousRecognitionAsync();
		translating = false;
		return result;
	}

	public void StopContinuousTranslation()
	{
		if (translating && stopRecognition is not null)
		{
			stopRecognition.TrySetResult(0);
		}
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!disposedValue)
		{
			if (disposing)
			{
				if (!ready)
				{
					Task.Delay(500).Wait();//500 ms wait for the recognition to finish (it will be canceling the operation)
				}
			}

			disposedValue = true;
		}
	}

	// // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
	// ~SpeechToTextConverter()
	// {
	//     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
	//     Dispose(disposing: false);
	// }

	public void Dispose()
	{
		// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}
