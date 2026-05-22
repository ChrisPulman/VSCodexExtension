using System;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;
using System.Speech.Recognition;
using System.Threading.Tasks;

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
    private readonly object _gate = new object();
    private readonly Subject<string> _transcript = new Subject<string>();
    private readonly BehaviorSubject<string> _status = new BehaviorSubject<string>("Voice input ready");
    private SpeechRecognitionEngine? _recognizer;
    private Task? _startTask;
    private bool _isAvailable = true;
    private bool _isListening;
    private bool _startRequested;
    private bool _disposed;

    public IObservable<string> Transcript => _transcript.AsObservable();
    public IObservable<string> Status => _status.AsObservable();

    public bool IsAvailable
    {
        get
        {
            lock (_gate)
            {
                return _isAvailable;
            }
        }
    }

    public bool IsListening
    {
        get
        {
            lock (_gate)
            {
                return _isListening;
            }
        }
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                SafeStatus("Voice input is disposed");
                return;
            }

            if (!_isAvailable)
            {
                SafeStatus("Voice input is not available on this Windows installation");
                return;
            }

            if (_isListening)
            {
                SafeStatus("Listening");
                return;
            }

            _startRequested = true;
            if (_startTask != null && !_startTask.IsCompleted)
            {
                SafeStatus("Voice input is starting");
                return;
            }

            SafeStatus("Starting voice input");
            _startTask = Task.Run(StartListeningCore);
        }
    }

    public void Stop()
    {
        SpeechRecognitionEngine? recognizer;
        var wasListening = false;
        lock (_gate)
        {
            _startRequested = false;
            recognizer = _recognizer;
            wasListening = _isListening;
            _isListening = false;
        }

        if (recognizer != null && wasListening)
        {
            try
            {
                recognizer.RecognizeAsyncCancel();
            }
            catch (Exception ex) when (IsSpeechInfrastructureException(ex))
            {
                ReleaseRecognizer(recognizer);
                SafeStatus("Voice input stopped after recognizer error: " + ex.Message);
                return;
            }
        }

        SafeStatus("Voice input stopped");
    }

    public void Dispose()
    {
        SpeechRecognitionEngine? recognizer;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _startRequested = false;
            _isListening = false;
            recognizer = _recognizer;
            _recognizer = null;
        }

        ReleaseRecognizer(recognizer);
        _transcript.Dispose();
        _status.Dispose();
    }

    private void StartListeningCore()
    {
        SpeechRecognitionEngine? recognizer = null;
        try
        {
            recognizer = GetOrCreateRecognizer();
            lock (_gate)
            {
                if (_disposed || !_startRequested)
                {
                    if (!ReferenceEquals(_recognizer, recognizer))
                    {
                        ReleaseRecognizer(recognizer);
                    }

                    return;
                }
            }

            recognizer.RecognizeAsync(RecognizeMode.Multiple);
            lock (_gate)
            {
                _isListening = true;
                _isAvailable = true;
            }

            SafeStatus("Listening");
        }
        catch (Exception ex) when (IsSpeechInfrastructureException(ex))
        {
            lock (_gate)
            {
                _isListening = false;
                _startRequested = false;
                _isAvailable = false;
                if (ReferenceEquals(_recognizer, recognizer))
                {
                    _recognizer = null;
                }
            }

            ReleaseRecognizer(recognizer);
            SafeStatus("Voice input unavailable: " + ex.Message);
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                _isListening = false;
                _startRequested = false;
            }

            SafeStatus("Voice input failed to start: " + ex.Message);
        }
    }

    private SpeechRecognitionEngine GetOrCreateRecognizer()
    {
        lock (_gate)
        {
            if (_recognizer != null)
            {
                return _recognizer;
            }
        }

        var created = CreateRecognizer();
        lock (_gate)
        {
            if (_disposed || !_startRequested)
            {
                return created;
            }

            _recognizer = created;
            return created;
        }
    }

    private SpeechRecognitionEngine CreateRecognizer()
    {
        var recognizers = SpeechRecognitionEngine.InstalledRecognizers().ToList();
        var recognizerInfo = recognizers
            .FirstOrDefault(x => Equals(x.Culture, CultureInfo.CurrentUICulture))
            ?? recognizers
                .FirstOrDefault(x => string.Equals(x.Culture.TwoLetterISOLanguageName, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase))
            ?? recognizers.FirstOrDefault();

        if (recognizerInfo == null)
        {
            throw new InvalidOperationException("No Windows speech recognizer is installed.");
        }

        var recognizer = new SpeechRecognitionEngine(recognizerInfo);
        recognizer.InitialSilenceTimeout = TimeSpan.FromSeconds(10);
        recognizer.BabbleTimeout = TimeSpan.FromSeconds(6);
        recognizer.EndSilenceTimeout = TimeSpan.FromSeconds(1);
        recognizer.EndSilenceTimeoutAmbiguous = TimeSpan.FromSeconds(1.5);
        recognizer.LoadGrammar(new DictationGrammar());
        recognizer.SetInputToDefaultAudioDevice();
        recognizer.SpeechDetected += OnSpeechDetected;
        recognizer.SpeechRecognized += OnSpeechRecognized;
        recognizer.SpeechRecognitionRejected += OnSpeechRecognitionRejected;
        recognizer.RecognizeCompleted += OnRecognizeCompleted;
        return recognizer;
    }

    private void OnSpeechDetected(object sender, EventArgs e)
    {
        if (IsListening)
        {
            SafeStatus("Voice input detected speech");
        }
    }

    private void OnRecognizeCompleted(object sender, RecognizeCompletedEventArgs e)
    {
        lock (_gate)
        {
            _isListening = false;
            if (e.Error != null)
            {
                _startRequested = false;
            }
        }

        SafeStatus(e.Error == null ? "Voice input stopped" : "Voice input stopped: " + e.Error.Message);
    }

    private void OnSpeechRecognized(object sender, SpeechRecognizedEventArgs e)
    {
        var result = e.Result;
        if (result == null || string.IsNullOrWhiteSpace(result.Text))
        {
            if (IsListening)
            {
                SafeStatus("Voice input heard speech but no transcript was available");
            }

            return;
        }

        var text = result.Text.Trim();
        SafeTranscript(text);
        SafeStatus(result.Confidence < 0.35f
            ? "Voice input captured a low-confidence transcript; review it: " + text
            : "Voice input captured: " + text);
    }

    private void OnSpeechRecognitionRejected(object sender, SpeechRecognitionRejectedEventArgs e)
    {
        if (IsListening)
        {
            SafeStatus("Voice input heard speech but could not recognize it");
        }
    }

    private void ReleaseRecognizer(SpeechRecognitionEngine? recognizer)
    {
        if (recognizer == null)
        {
            return;
        }

        try
        {
            recognizer.SpeechDetected -= OnSpeechDetected;
            recognizer.SpeechRecognized -= OnSpeechRecognized;
            recognizer.SpeechRecognitionRejected -= OnSpeechRecognitionRejected;
            recognizer.RecognizeCompleted -= OnRecognizeCompleted;
            recognizer.Dispose();
        }
        catch (Exception ex) when (IsSpeechInfrastructureException(ex))
        {
        }
    }

    private void SafeStatus(string status)
    {
        try
        {
            _status.OnNext(status);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void SafeTranscript(string text)
    {
        try
        {
            _transcript.OnNext(text);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static bool IsSpeechInfrastructureException(Exception ex)
        => ex is InvalidOperationException
        || ex is COMException
        || ex is InvalidComObjectException
        || ex is ObjectDisposedException;
}
