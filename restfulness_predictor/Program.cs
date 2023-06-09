using System.Globalization;
using brainflow.math;
using brainflow;
using CommandLine;

namespace restfulness_predictor;

internal class Program
{
    private static void Main(string[] args)
    {
        Console.WriteLine("\n");
        Console.WriteLine("Initializing program...");
        var options = new Options();
        Parser.Default.ParseArguments<Options>(args)
            .WithParsed(o => { options = o; })
            .WithNotParsed(errors => { HandleParsingErrors(errors); });

        PrintOptions(options);

        QueryDevLogging();

        var mode = QueryApplicationMode();
        StartPredictor(options, mode);
    }

    private static int QueryInterval()
    {
        Console.WriteLine("\n");
        Console.WriteLine("Type the interval in milliseconds between each measurement (default is 5000)");
        var userInput = Console.ReadLine();
        try
        {
            return userInput != null ? int.Parse(userInput) : 5000;
        }
        catch (FormatException e)
        {
            if (userInput != "") Console.WriteLine(e.Message);
            Console.WriteLine("Using default");
        }

        return 5000;
    }

    // This is in my opinion not the most logical place to handle the recording logic.
    // The Predictions recording is inside the Predictor class, so it would make sense to handle it there.
    // But maybe this gives us more flexibility?
    // Ideally I would want the recording struct to be outside of the Predictor class but then I can't access the struct
    // inside this method.
    private static void OnRestfulnessScoreUpdated(double score, Predictions recording)
    {
        Console.WriteLine($"Restfulness score: {score}");
        recording.Timestamps.Add(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - recording.StartTime);
        recording.PredictedScores.Add(score);
    }

    private static void PrintOptions(Options options)
    {
        Console.WriteLine();
        Console.WriteLine($"Board ID: {options.BoardId}");
        Console.WriteLine($"Sampling rate: {BoardShim.get_sampling_rate((int)options.BoardId)}");
        // TODO: These frequencies shown are not the actual frequencies used in the program. Make them correct.
        Console.WriteLine($"Bandpass frequencies: {string.Join(" - ", options.Bandpass)}");
        Console.WriteLine($"Bandstop frequencies: {string.Join(" - ", options.Bandstop)}");
    }

    private static void HandleParsingErrors(IEnumerable<Error> errors)
    {
        Console.WriteLine("Error parsing command-line arguments:");
        foreach (var error in errors) Console.WriteLine(error.ToString());
    }

    private static void SavePredictions(Predictions recording)
    {
        var filePath = Path.Combine(Environment.CurrentDirectory, "recordings");
        if (!Directory.Exists(filePath)) Directory.CreateDirectory(filePath);
        filePath = Path.Combine(filePath, recording.FileName);

        // Format the data to be saved in a csv file.
        // Timestamps are in seconds with precision of 2 decimals.
        // Make sure the decimal separator is a dot.
        var timeDeltasInSeconds = recording.Timestamps.Select(x => Math.Round((double)x / 1000, 2)).ToList();
        var timestamps = string.Join(",", timeDeltasInSeconds.Select(x => x.ToString(new CultureInfo("en-US"))));
        var scores = string.Join(",", recording.PredictedScores.Select(x => x.ToString(new CultureInfo("en-US"))));

        Console.WriteLine($"Saving recording to {filePath}");

        using (var writer = new StreamWriter(filePath))
        {
            writer.WriteLine(timestamps);
            writer.WriteLine(scores);
        }
    }

    private static void SaveRawData(RawData data)
    {
        var filePath = Path.Combine(Environment.CurrentDirectory, "raw_data");
        if (!Directory.Exists(filePath)) Directory.CreateDirectory(filePath);

        for (var i = 0; i < data.FileNames.Count; i++)
        {
            var fileName = Path.Combine(filePath, data.FileNames[i]);
            var epoch = data.EpochList[i];

            Console.WriteLine($"Saving raw data to {fileName}");

            using (var writer = new StreamWriter(fileName))
            {
                for (var j = 0; j < epoch.GetLength(0); j++)
                {
                    var row = string.Join(",", epoch.GetRow(j).Select(x => x.ToString(new CultureInfo("en-US"))));
                    writer.WriteLine(row);
                }
            }
        }
    }

    private static void QueryDevLogging()
    {
        Console.WriteLine("\nDo you want to enable developer logging? (y/n)");
        Console.WriteLine("Default is n");
        var userInput = Console.ReadLine();
        if (userInput == "y") Predictor.EnableDevLogging();
    }

    private static void PrintRecording(Predictions recording)
    {
        Console.WriteLine($"File name: {recording.FileName}");
        Console.WriteLine($"Start time: {recording.StartTime}");
        Console.WriteLine($"Timestamps: {string.Join(", ", recording.Timestamps)}");
        Console.WriteLine($"Predicted scores: {string.Join(", ", recording.PredictedScores)}");
    }

    private static int QueryApplicationMode()
    {
        Console.WriteLine("\n");
        Console.WriteLine("Save data or predictions?");
        Console.WriteLine("Type '0' to save predictions (default)");
        Console.WriteLine("Type '1' to save data");
        var userInput = Console.ReadLine();
        if (userInput == null)
        {
            Console.WriteLine("Using default");
            return 0;
        }

        try
        {
            var mode = int.Parse(userInput) switch
            {
                0 => 0,
                1 => 1,
                _ => 0
            };
            return mode;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            Console.WriteLine("Using default");
            return 0;
        }
    }

    private static void StartPredictor(Options options, int mode)
    {
        var interval = QueryInterval();
        var predictor = new Predictor(options.BoardId, options.Bandpass.ToArray(), options.Bandstop.ToArray(), interval,
            options.Ica);
        if (mode == 0) predictor.OnRestfulnessScoreUpdated += OnRestfulnessScoreUpdated;
        Console.WriteLine("\nProgram initialized");

        Console.WriteLine("Type 'start' to start streaming data");
        Console.WriteLine("Type 'stop' to stop streaming data and quit application");

        // TODO: Put this in a separate method.
        // The problem is that we need to call predictor.StartStream() from here
        // And we would need to have a switch or an if statement here anyways...

        var userInput = Console.ReadLine();
        switch (userInput)
        {
            case "start":
                Console.WriteLine();
                Console.WriteLine("Streaming data...");
                if (mode == 0) predictor.StartPredictSession();
                else predictor.StartRecordSession();
                break;
            default:
                Console.WriteLine("Exiting application...");
                predictor.ReleaseSession();
                return;
        }

        Console.ReadLine(); // Keeps the program running until the user hits enter
        predictor.ReleaseSession();
        // PrintRecording(predictor.Recording);
        switch (mode)
        {
            case 0:
                SavePredictions(predictor.Recording);
                break;
            case 1:
                SaveRawData(predictor.Data);
                break;
        }

        Console.WriteLine("Exiting application...");
    }
}