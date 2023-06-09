namespace restfulness_predictor;

public struct RawData
{
    public List<double[,]> EpochList { get; set; }
    public List<string> FileNames { get; set; }

    public RawData()
    {
        // FileName = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".csv";
        FileNames = new List<string>();
        EpochList = new List<double[,]>();
    }
}