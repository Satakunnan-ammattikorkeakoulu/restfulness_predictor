// TODO: Refactor to know the state of the predictor
// TODO: Refactor the bandpass and bandstop frequencies
//      - When used disable the filter used in get_avg_band_powers functions

using System.Timers;
using brainflow;
using brainflow.math;
using Timer = System.Timers.Timer;

namespace restfulness_predictor;

public class Predictor
{
    public Predictions Recording;
    public RawData Data;

    private bool _isStreaming;
    private double _restfulnessScore;
    private bool _predictEventSubscribed;
    private bool _recordEventSubscribed;
    private bool _ica;
    private bool _firstPrediction = true;

    private readonly BoardShim _boardShim;
    private readonly MLModel _model;
    private readonly int _samplingRate;
    private readonly int[] _eegChannels;
    private readonly Timer _timer = new();
    private readonly double[] _bandstopFrequencies;
    private readonly double[] _bandpassFrequencies;
    private readonly int _predictionInterval;
    private readonly int _dataCount;


    /// <summary>
    /// Initializes the predictor with the provided boardId, bandpass and bandstop.
    /// </summary>
    /// <param name="boardId"></param>
    /// <param name="bandpassFrequencies">First value is low and second is high.</param>
    /// <param name="bandstopFrequencies">First value is low and second is high.</param>
    /// <param name="predictionInterval">The interval in milliseconds at which the predictor will make a predictions.</param>
    /// <param name="ica"></param>
    public Predictor(BoardIds boardId, double[] bandpassFrequencies, double[] bandstopFrequencies,
        int predictionInterval, bool ica)
    {
        // TODO: To make the PLAYBACK_FILE_BOARD we need to do stuff here. If the boardId is PLAYBACK_FILE_BOARD the BrainFlowInputParams needs: 
        //  - .file for the file path
        //  - .master_board for the boardId of the board that was used to record the file
        
        var inputParams = new BrainFlowInputParams();
        _boardShim = new BoardShim((int)boardId, inputParams);
        _samplingRate = BoardShim.get_sampling_rate(_boardShim.get_board_id());
        _eegChannels = BoardShim.get_eeg_channels(_boardShim.get_board_id());
        _boardShim.prepare_session();

        var modelParams = new BrainFlowModelParams((int)BrainFlowMetrics.RESTFULNESS,
            (int)BrainFlowClassifiers.DEFAULT_CLASSIFIER);
        _model = new MLModel(modelParams);
        _model.prepare();

        _bandpassFrequencies = bandpassFrequencies;
        _bandstopFrequencies = bandstopFrequencies;
        _predictionInterval = predictionInterval;
        _dataCount = (int)Math.Round(_samplingRate * (_predictionInterval / 1000.0));

        Recording = new Predictions();
        _ica = ica;
    }

    /// <summary>
    /// Score that indicates how restful the user is. The higher the score, the more restful the user is.
    /// Value is between [0, 1].
    /// </summary>
    public double RestfulnessScore
    {
        get => _restfulnessScore;
        private set
        {
            // Console.WriteLine("Restfulness score updated");
            _restfulnessScore = value;
            OnRestfulnessScoreUpdated?.Invoke(_restfulnessScore, Recording);
        }
    }

    /// <summary>
    /// Triggered when the RestfulnessScore is updated by the predictor.
    /// The event provides the updated RestfulnessScore and the Recordings struct as a parameter to any registered listeners.
    /// The score value is between [0, 1].
    /// </summary>
    public event Action<double, Predictions> OnRestfulnessScoreUpdated;


    /// <summary>
    /// Starts the stream and invokes the Predict method at given interval. In practice the interval should be
    /// 500ms or greater or else the model will not have enough data to even try making a prediction.
    /// </summary>
    /// <exception cref="BrainFlowError">Thrown when there is an error with BrainFlow. Eg. Board is not initialized.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the stream is already running.</exception>
    /// <exception cref="ArgumentException">Thrown if the intervalMs is less than 500.</exception>
    public void StartPredictSession()
    {
        if (_predictionInterval < 500) throw new ArgumentException("Interval must be greater than 499.");
        if (_isStreaming) throw new InvalidOperationException("Cannot start stream, it is already running.");
        Console.WriteLine("Starting stream...");
        _boardShim.start_stream();
        _isStreaming = true;
        _timer.Interval = _predictionInterval;
        _timer.Elapsed += Predict;
        _predictEventSubscribed = true;
        Recording.StartTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _timer.Start();
    }

    /// <summary>
    /// Starts the stream and invokes the Record method at given interval. In practice the interval should be
    /// 500ms or greater or else the epochs will be too small to be useful.
    /// </summary>
    /// <exception cref="BrainFlowError">Thrown when there is an error with BrainFlow. Eg. Board is not initialized.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the stream is already running.</exception>
    /// <exception cref="ArgumentException">Thrown if the intervalMs is less than 500.</exception>
    public void StartRecordSession()
    {
        if (_predictionInterval < 500) throw new ArgumentException("Interval must be greater than 499.");
        if (_isStreaming) throw new InvalidOperationException("Cannot start stream, it is already running.");
        Console.WriteLine("Starting stream");
        Data = new RawData();
        _boardShim.start_stream();
        _isStreaming = true;
        _timer.Interval = _predictionInterval;
        _timer.Elapsed += Record;
        _recordEventSubscribed = true;
        _timer.Start();
    }

    /// <summary>
    /// Stops the stream and cancels the Predict method invoke.
    /// </summary>
    /// <exception cref="BrainFlowError">Thrown when there is an error with BrainFlow. Eg. Board is not initialized.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the stream is not running.</exception>
    private void StopStream()
    {
        if (!_isStreaming) throw new InvalidOperationException("Cannot stop stream, it is currently not running.");
        // Console.WriteLine("Stopping stream");
        _boardShim.stop_stream();
        _isStreaming = false;
        if (_predictEventSubscribed)
        {
            _timer.Elapsed -= Predict;
            _predictEventSubscribed = false;
        }

        if (_recordEventSubscribed)
        {
            _timer.Elapsed -= Record;
            _recordEventSubscribed = false;
        }

        _timer.Stop();
    }

    /// <summary>
    /// Enable BrainFlow logging for debugging purposes. Files will be saved in the log folder in the current directory.
    /// Naming convention: bf_{yyyy-MM-dd_HH-mm-ss}.log and ml_{yyyy-MM-dd_HH-mm-ss}.log
    /// </summary>
    /// <exception cref="BrainFlowError">Thrown when there is an error with BrainFlow. Eg. Log file is locked for writing.</exception>
    public static void EnableDevLogging()
    {
        Console.WriteLine();
        Console.WriteLine("Enabling dev logging");
        var logPath = Path.Combine(Environment.CurrentDirectory, "log");
        var timeStamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        if (!Directory.Exists(logPath)) Directory.CreateDirectory(logPath);
        var bfLog = Path.Combine(logPath, $"bf_{timeStamp}.log");
        var mlLog = Path.Combine(logPath, $"ml_{timeStamp}.log");
        BoardShim.set_log_file(bfLog);
        MLModel.set_log_file(mlLog);
        MLModel.enable_dev_ml_logger();
        BoardShim.enable_dev_board_logger();
    }

    /// <summary>
    /// Release the BrainFlow session and ML model. Also stops the stream if it is still running.
    /// </summary>
    /// <exception cref="BrainFlowError"></exception>
    public void ReleaseSession()
    {
        if (_isStreaming) StopStream();
        _boardShim.release_session();
        _model.release();
    }


    private void Predict(object sender, ElapsedEventArgs e)
    {
        // TODO: See if you could make this cleaner
        Tuple<double[], double[]> bands;
        if (_firstPrediction)
        {
            _firstPrediction = false;
            var data = _boardShim.get_current_board_data(_dataCount);

            // FilterData(ref data); // This is disabled for now. Once we get to refactoring the Filtering logic we can enable this.

            // Calculate avg and stddev (in that order) of band powers across all channels. Bands are 1-4, 4-8, 8-13, 13-30, 30-50 Hz.
            // The last parameter should apply the following filters in order:
            // Band stop: 48 - 52 Hz, Butterworth, order 4
            // Band stop 58 - 62 Hz, Butterworth, order 4
            // Band pass: 2 - 45 Hz, Butterworth, order 4
            bands = _ica
                ? GetAvgBandPowersWithIca(data)
                : DataFilter.get_avg_band_powers(data, _eegChannels, _samplingRate, true);
        }
        else
        {
            var firstData = _boardShim.get_board_data(_dataCount);
            var secondData = _boardShim.get_current_board_data(_dataCount);
            var data = ConcatenateData(firstData, secondData);

            // FilterData(ref data); // This is disabled for now. Once we get to refactoring the Filtering logic we can enable this.

            // Calculate avg and stddev (in that order) of band powers across all channels. Bands are 1-4, 4-8, 8-13, 13-30, 30-50 Hz.
            // The last parameter should apply the following filters in order:
            // Band stop: 48 - 52 Hz, Butterworth, order 4
            // Band stop 58 - 62 Hz, Butterworth, order 4
            // Band pass: 2 - 45 Hz, Butterworth, order 4
            bands = _ica
                ? GetAvgBandPowersWithIca(data)
                : DataFilter.get_avg_band_powers(data, _eegChannels, _samplingRate, true);
        }

        var featureVector = bands.Item1;
        // The result type is double[] but it contains only one element.
        RestfulnessScore = _model.predict(featureVector)[0];
    }

    private void Record(object sender, ElapsedEventArgs e)
    {
        Console.WriteLine("Epoch saved");
        var data = _boardShim.get_board_data();
        Data.FileNames.Add(DateTimeOffset.Now.ToUnixTimeMilliseconds() + ".csv");
        Data.EpochList.Add(data);
    }

    private void FilterData(ref double[,] data)
    {
        foreach (var i in _eegChannels)
        {
            DataFilter.perform_bandstop(data, i, _samplingRate, _bandstopFrequencies[0], _bandstopFrequencies[1],
                3, (int)FilterTypes.BUTTERWORTH, 0.0);
            DataFilter.perform_bandpass(data, i, _samplingRate, _bandpassFrequencies[0], _bandpassFrequencies[1],
                4, (int)FilterTypes.BUTTERWORTH, 0.0);
        }
    }

    private Tuple<double[], double[]> GetAvgBandPowersWithIca(double[,] data)
    {
        var icaData = PerformIca(data);
        var rows = icaData.GetLength(0);
        var channels = Enumerable.Range(0, rows).ToArray();
        return DataFilter.get_avg_band_powers(icaData, channels, _samplingRate, true);
    }

    private double[,] PerformIca(double[,] data)
    {
        var cols = data.Columns() / 2;
        var rows = _eegChannels.Length * 2;
        var list = new List<double[]>();
        var result = new double[rows, cols];

        foreach (var c in _eegChannels)
        {
            var icaData = data.GetRow(c).Reshape(2, cols);
            var (_, _, _, s) = DataFilter.perform_ica(icaData, 2);
            list.Add(s.GetRow(0));
            list.Add(s.GetRow(1));
        }

        for (var i = 0; i < list.Count; i++)
        for (var j = 0; j < cols; j++)
            result[i, j] = list[i][j];

        return result;
    }

    private double[,] ConcatenateData(double[,] first, double[,] second)
    {
        var firstRows = first.GetLength(0);
        var firstCols = first.GetLength(1);
        var secondRows = second.GetLength(0);
        var secondCols = second.GetLength(1);

        if (firstRows != secondRows)
            throw new ArgumentException("Data must have the same number of rows.");
        var result = new double[firstRows, firstCols + secondCols];
        for (var i = 0; i < firstRows; i++)
        for (var j = 0; j < firstCols; j++)
            result[i, j] = first[i, j];
        for (var i = 0; i < secondRows; i++)
        for (var j = 0; j < secondCols; j++)
            result[i, j + firstCols] = second[i, j];
        return result;
    }
}