using CommandLine;
using System.ComponentModel.DataAnnotations;
using brainflow;

namespace restfulness_predictor;

public class Options
{
    [Option('b', "boardid",
        HelpText =
            "Board name. Valid options: SYNTHETIC_BOARD, GANGLION_NATIVE_BOARD, MUSE_2_BOARD, MUSE_S_BOARD, PLAYBACK_FILE_BOARD.",
        Default = BoardIds.SYNTHETIC_BOARD)]
    [ValidateBoardId]
    public BoardIds BoardId { get; set; }
    
    [Option('f', "filepath", HelpText = "Path to file. Required when using the PLAYBACK_FILE_BOARD option.", Required = false)]
    public string FilePath { get; set; }

    [Option('p', "bandpass", HelpText = "Bandpass filter range (low, high). Not in use.", Default = new[] { 2.0, 45.0 })]
    public IEnumerable<double> Bandpass { get; set; }

    [Option('s', "bandstop", HelpText = "Bandstop filter range (low, high). Not in use.", Default = new[] { 48.0, 52.0 })]
    public IEnumerable<double> Bandstop { get; set; }

    [Option('i', "ica", HelpText = "Use ICA. Experimental feature, use is not recommended.", Default = false)]
    public bool Ica { get; set; }
}

public class ValidateBoardIdAttribute : ValidationAttribute
{
    private readonly BoardIds[] _validBoardIds =
    {
        BoardIds.SYNTHETIC_BOARD, 
        BoardIds.GANGLION_NATIVE_BOARD,
        BoardIds.MUSE_2_BOARD,
        BoardIds.MUSE_S_BOARD,
        BoardIds.PLAYBACK_FILE_BOARD
    };

    // protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    // {
    //     var boardId = (BoardIds)value;
    //     return _validBoardIds.Contains(boardId)
    //         ? ValidationResult.Success
    //         : new ValidationResult($"Invalid boardid: {boardId}");
    // }
    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        var options = (Options)validationContext.ObjectInstance;
        var boardId = (BoardIds)value;
        
        // TODO: The idea is here, but this does not work as is
        if (boardId == BoardIds.PLAYBACK_FILE_BOARD && string.IsNullOrWhiteSpace(options.FilePath))
        {
            return new ValidationResult("When using the PLAYBACK_FILE_BOARD option, you must provide a file path.");
        }

        if (boardId == BoardIds.PLAYBACK_FILE_BOARD && !File.Exists(options.FilePath))
        {
            return new ValidationResult("The specified file path does not exist.");
        }

        return _validBoardIds.Contains(boardId)
            ? ValidationResult.Success
            : new ValidationResult($"Invalid boardid: {boardId}");
    }
}