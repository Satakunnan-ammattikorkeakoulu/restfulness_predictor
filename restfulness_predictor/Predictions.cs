namespace restfulness_predictor;

public struct Predictions
{
    public List<long> Timestamps { get; set; }
    public List<double> PredictedScores { get; set; }
    public long StartTime { get; set; }
    public string FileName { get; }

    public Predictions()
    {
        FileName = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".csv";
        StartTime = 0;
        Timestamps = new List<long>();
        PredictedScores = new List<double>();
    }
}