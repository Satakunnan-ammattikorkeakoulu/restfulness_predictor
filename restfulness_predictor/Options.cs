// TODO: Figure out how to deal with the "potential null reference" warnings.

using CommandLine;
using System.ComponentModel.DataAnnotations;
using brainflow;

namespace restfulness_predictor;

public class Options
{
    [Option('b', "boardid",
        HelpText =
            "Board name. Valid options: SYNTHETIC_BOARD, GANGLION_NATIVE_BOARD, MUSE_2_BOARD, MUSE_S_BOARD.",
        Default = BoardIds.SYNTHETIC_BOARD)]
    [ValidateBoardId]
    public BoardIds BoardId { get; set; }

    [Option('p', "bandpass", HelpText = "Bandpass filter range (low, high).", Default = new[] { 0.5, 40.0 })]
    public IEnumerable<double> Bandpass { get; set; }

    [Option('s', "bandstop", HelpText = "Bandstop filter range (low, high).", Default = new[] { 49.0, 51.0 })]
    public IEnumerable<double> Bandstop { get; set; }

    [Option('i', "ica", HelpText = "Use ICA. Experimental feature, use is not recommended.", Default = false)]
    public bool Ica { get; set; }
}

public class ValidateBoardIdAttribute : ValidationAttribute
{
    private readonly BoardIds[] _validBoardIds =
    {
        BoardIds.SYNTHETIC_BOARD, BoardIds.GANGLION_NATIVE_BOARD, BoardIds.MUSE_2_BOARD, BoardIds.MUSE_S_BOARD
    };

    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        var boardId = (BoardIds)value;
        return _validBoardIds.Contains(boardId)
            ? ValidationResult.Success
            : new ValidationResult($"Invalid boardid: {boardId}");
    }
}