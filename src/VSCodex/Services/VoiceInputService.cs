using System;
using System.Globalization;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Speech.Recognition;

namespace VSCodex.Services;

public interface IVoiceInputService : IDisposable
{
    IObservable<string> Transcript { get; }
    IObservable<string> Status { get; }
    bool IsAvailable { get; }
    bool IsListening { get; }
    void Start();
    void Stop();
}

public sealed class VoiceInputService : IVoiceInputService
{
    private readonly Subject<string> _transcript = new Subject<string>();
    private readonly Subject<string> _status = new Subject<string>();
    private SpeechRecognitionEngine? _recognizer;
    private bool _isAvailable;
    private bool _isListening;

    public VoiceInputService()
    {
        try
        {
            _recognizer = CreateRecognizer();
            _isAvailable = true;
            _status.OnNext("Voice input ready");
        }
        catch (Exception ex)
        {
            _isAvailable = false;
            _status.OnNext("Voice input unavailable: " + ex.Message);
        }
    }

    public IObservable<string> Transcript => _transcript.AsObservable();
    public IObservable<string> Status => _status.AsObservable();
    public bool IsAvailable => _isAvailable;
    public bool IsListening => _isListening;

    public void Start()
    {
        if (!_isAvailable || _recognizer == null)
        {
            _status.OnNext("Voice input is not available on this Windows installation");
            return;
        }

        if (_isListening)
        {
            return;
        }

        try
        {
            _recognizer.RecognizeAsync(RecognizeMode.Multiple);
            _isListening = true;
            _status.OnNext("Listening");
        }
        catch (Exception ex)
        {
            _status.OnNext("Voice input failed to start: " + ex.Message);
        }
    }

    public void Stop()
    {
        if (_recognizer == null || !_isListening)
        {
            return;
        }

        try
        {
            _recognizer.RecognizeAsyncCancel();
        }
        catch
        {
        }
        finally
        {
            _isListening = false;
            _status.OnNext("Voice input stopped");
        }
    }

    public void Dispose()
    {
        Stop();
        _recognizer?.Dispose();
        _transcript.Dispose();
        _status.Dispose();
    }

    private SpeechRecognitionEngine CreateRecognizer()
    {
        var recognizer = new SpeechRecognitionEngine(CultureInfo.CurrentUICulture);
        recognizer.LoadGrammar(new DictationGrammar());
        recognizer.SetInputToDefaultAudioDevice();
        recognizer.SpeechRecognized += OnSpeechRecognized;
        recognizer.RecognizeCompleted += (_, _) =>
        {
            _isListening = false;
            _status.OnNext("Voice input stopped");
        };
        return recognizer;
    }

    private void OnSpeechRecognized(object sender, SpeechRecognizedEventArgs e)
    {
        var result = e.Result;
        if (result == null || result.Confidence < 0.35 || string.IsNullOrWhiteSpace(result.Text))
        {
            return;
        }

        _transcript.OnNext(result.Text.Trim());
    }
}
