```
Development branch
```

# Restfulness predictor

Test application for the brainflow restfulness classifier

---

## Instructions

Can be run from command line with `dotnet run Program.cs` or can be built and run as an executable from the command
line. If the application is built the .dll files from the repo root need to be added to the same directory as the
executable.  
Build with `dotnet build --configuration Release --self-contained true`

Parameters to include when running:

| Short | Long       | Value                       | Default         | Notes        |
|-------|------------|-----------------------------|-----------------|--------------|
| -b    | --boardid  | *                           | SYNTHETIC_BOARD |              |
| -p    | --bandpass | Two space separated doubles | 2.0 45.0        | Not in use   |
| -s    | --bandstop | Two space separated doubles | 48.0 52.0       | Not in use   |
| -i    | --ica      | N/A                         | False           | Experimental |

* Board ID is the ID of the board to connect to. Formatting is the same as the BoardIds enum in the brainflow
  library. Valid ones are:
    * SYNTHETIC_BOARD
    * GANGLION_NATIVE_BOARD
    * MUSE_2_BOARD
    * MUSE_S_BOARD
    * PLAYBACK_FILE_BOARD (Feature not complete yet)

---

## Output and logs

`.\recordings\yyyy-MM-dd_HH-mm-ss.csv`  
The output is a csv file with first row being the time deltas from the beginning of the recording. Precision is in
seconds with two decimal places. The second row is the restfulness predictions as doubles. 0 is not restful, 1 is
restful.

`.\logs\bf_yyyy-MM-dd_HH-mm-ss.log`  
`.\logs\ml-MM-dd_HH-mm-ss.log`  
If the dev logging is enabled in the application these files will be created. The first is the brainflow log and the
second is the machine learning log. These are useful for debugging and seeing what is happening in the background.

---
