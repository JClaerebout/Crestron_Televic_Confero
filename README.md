# Televic Confero Control

Crestron module help and programming reference — SIMPL+ / SIMPL#.

Provides individual microphone toggles, ordered speaker and request lists, first/last active microphone feedback, meeting control, recording control, and discussion settings. Feedback represents confirmed controller state.

| Module information | Details |
| --- | --- |
| Symbol name | Televic Confero Control |
| Application | Televic Confero discussion, meeting, and recording control |
| Control interface | Authenticated HTTP API with notification monitoring |
| Processor target | Crestron 4-Series |
| Library | `Televic Confero.clz`; SIMPL# .NET Framework 4.7.2 |
| PDF reference | [Module help, revision 1.0](Televic_Confero_Control_Help.pdf) |

## Contents

1. [Installation and connection](#installation-and-connection)
2. [Digital inputs](#digital-inputs)
3. [Analog inputs and configuration](#analog-inputs-and-configuration)
4. [Digital and analog outputs](#digital-and-analog-outputs)
5. [Serial outputs and row mapping](#serial-outputs-and-row-mapping)
6. [Demo interface](#demo-interface)
7. [Build and testing](#build-and-testing)

## Installation and connection

### Required files and equipment

Use a Crestron 4-Series program, a Televic Confero controller with API access, and a valid API token. Keep `Televic Confero Control.usp`, its matching `.ush` symbol, and `Televic Confero.clz` together in the module location used by the SIMPL project. Use matching files from the same build.

### Setup procedure

1. Add **Televic Confero Control** to the SIMPL program.
2. Set the two string parameters below.
3. Wire connection/status feedback and the required controls.
4. Compile and load the program.
5. Pulse `Initialize` after program initialization, or hold it high at startup.
6. Confirm `Connected_fb` is high, then verify seat numbers and names before enabling operator controls.

| Parameter | Format / function |
| --- | --- |
| `IP` | Up to 255 characters. Hostname or IP address, with optional scheme and port. Examples: `192.168.1.50`, `confero.example.local:9080`, `http://192.168.1.50:9080`. |
| `API_Key` | Up to 255 characters. API Bearer token only, without the `Bearer ` prefix. |

HTTP defaults to port 9080; HTTPS defaults to 9443. An explicit `http://` or `https://` scheme overrides `DEFAULT_HTTPS`. HTTPS uses normal certificate validation. Authentication headers and tokens are never printed.

### Initialization and connection indication

Every `Initialize` rising edge cancels the previous monitoring session and starts a replacement. Releasing it leaves monitoring running. `Refresh` restarts an existing session and reloads snapshots; use `Initialize` for the first connection. No stop-monitoring input is exposed.

`Connected_fb` rises after a successful authenticated speaker snapshot. Other metadata may still be loading, and optional-feature failures can be present while it remains high. Consult `Status_fb` for details.

## Digital inputs

Command inputs act on a rising edge; use momentary pulses. Commands require established speaker connectivity and are ignored while disconnected. `Initialize` and `Refresh` manage the monitoring session.

| Signal | Function |
| --- | --- |
| `Initialize` | Start or replace monitoring using `IP` and `API_Key`. Also recognized if already high at module startup. |
| `Refresh` | Restart the existing session and reload all snapshots. |
| `Meeting_Start` | Start a new meeting from the local template. |
| `Meeting_Stop` | Stop the current meeting. |
| `Start_Selected_Meeting` | Start the scheduled meeting at `Selected_Meeting`. |
| `Clear_Speakers` | Clear the complete active speaker list. |
| `Clear_Requests` | Clear the complete request-to-speak list. |
| `Turn_Off_Selected_Speaker` | Turn off the microphone at `Selected_Speaker` in the current speaker list. |
| `Enable_Selected_Request` | Enable the microphone at `Selected_Request`; the command also clears its request flag. |
| `Recording_Start` | Request recording state `recording`. |
| `Recording_Stop` | Request recording state `idle`. |
| `Wireless_Turn_Off` | Send the device `turnOff` action. Verify its scope on the installed system before exposing the control. |
| `Mic_Toggle[1..100]` | Toggle the seat mapped to this row using its latest confirmed microphone state. Match the button with `Mic_On_fb` on the same row. |

### Button behavior

A successful microphone command does not guarantee the controller admitted that seat. Use `Mic_On_fb` for indication. Rapid repeated pulses can use the same confirmed state before feedback arrives; allow feedback to settle between operations. Commands execute in order and refresh confirmed metadata.

## Analog inputs and configuration

| Signal | Accepted values / behavior |
| --- | --- |
| `Selected_Speaker` | 1-based displayed speaker row. Set before pulsing `Turn_Off_Selected_Speaker`. |
| `Selected_Request` | 1-based displayed request row. Set before pulsing `Enable_Selected_Request`. |
| `Selected_Meeting` | 1-based displayed scheduled-meeting row. Set before pulsing `Start_Selected_Meeting`. |
| `Discussion_Mode` | On change: 1 = Direct Speak; 2 = Request; 3 = Group; 4 = Operator; 5 = Hands Free. Other values are ignored. |
| `Number_Of_Open_Mics` | On change: 1–100. Other values are ignored. Independent of `MIC_COUNT`. |

Selections of 0, rows beyond configured capacity, and empty rows produce no action. A selected row resolves to the current seat number or scheduled-meeting ID when the action is triggered. A changing list can therefore change the target represented by a row.

Mode and open-mic changes apply directly, preserving the other current controller setting. Received mode options are preserved; switching to a mode not read in this session uses the Q-SYS plugin defaults. The former `Apply_Discussion_Settings` input remains reserved as `_skip_` to preserve subsequent input positions.

### Compile-time constants

Edit these values near the top of `Televic Confero Control.usp`, rebuild, and update the symbol in SIMPL Windows.

| Constant | Default | Purpose |
| --- | --- | --- |
| `MIC_COUNT` | 100 | Seat control and feedback rows. |
| `SPEAKER_LIST_COUNT` | 15 | Displayed active-speaker rows. |
| `QUEUE_LIST_COUNT` | 15 | Displayed request rows. |
| `MEETING_LIST_COUNT` | 5 | Displayed scheduled-meeting rows. |
| `REFRESH_SECONDS` | 30 | Metadata reconciliation interval; minimum 10 seconds. |
| `DEFAULT_HTTPS` | 0 | 0 = HTTP, 1 = HTTPS unless `IP` includes a scheme. |
| `DEBUG_PRINT` | 0 | 0 = off; 1 = HTTP results/errors; 2 = also feedback summaries. |

Capacities allocate module pins and do not limit the controller's microphones. Counts report the complete list size even when only the first configured rows fit. Choose positive capacities within SIMPL's signal limits. Array ranges below describe the current source configuration.

## Digital and analog outputs

| Digital output | High / low meaning |
| --- | --- |
| `Connected_fb` | 1 = authenticated speaker monitoring established. 0 = initialization, failure, or retry. Does not certify every optional feature. |
| `Meeting_Running_fb` | 1 = current meeting metadata contains a meeting ID. |
| `Recording_Active_fb` | 1 = recording state is exactly `recording`. 0 can also mean unknown; consult `Recording_State_fb`. |
| `Mic_On_fb[1..100]` | 1 = mapped seat microphone confirmed on. |
| `Mic_Request_fb[1..100]` | 1 = mapped seat requesting to speak. |

| Analog output | Meaning |
| --- | --- |
| `First_Active_Mic` | Actual seat number of the first item in the full ordered speaker list. |
| `Last_Active_Mic` | Actual seat number of the last item in the full ordered speaker list. |
| `Mic_Count_fb` | Total number of seats returned by the controller. |
| `Speaker_Count_fb` | Total number of active speakers. |
| `Request_Count_fb` | Total number of requests to speak. |
| `Meeting_Count_fb` | Total number of scheduled meetings. |
| `Discussion_Mode_fb` | 1–5 using the input mode mapping; 0 when unavailable or unrecognized. |
| `Number_Of_Open_Mics_fb` | Current controller `maximumNumberOfSpeakers`; 0 when unavailable. |

### First and last active microphone

These outputs contain seat numbers, not row indexes or activation timestamps. Both are 0 when the speaker list is empty or monitoring is disconnected. They are equal when one microphone is active. The full speaker list is used even if displayed speaker rows are truncated.

### Monitoring and recovery

Notifications cover Discussion, Meeting, Room, and Recording. Speaker/request notifications update the ordered lists. Startup, manual refresh, history discontinuity, and commands reconcile snapshots. Periodic metadata refresh includes seats, participant names, scheduled meetings, settings, and recording state without polling either list endpoint.

Reconciliation runs between event polls, so an idle poll can delay it by up to the 40-second event timeout plus request time. Optional-feature requests have a 10-second timeout. An idle event timeout triggers a speaker snapshot and renews the poll with the same cursor; connection feedback stays high if the snapshot succeeds. Communication failures clear feedback and row mappings, then retry after two seconds. Detection can take up to the 40-second HTTP timeout.

Optional-feature errors, including meeting licensing and rejected recording commands, report through `Status_fb` without disabling working speaker monitoring. Outputs are written only when their values change, including serial text and individual list rows.

## Serial outputs and row mapping

| Serial output | Contents |
| --- | --- |
| `Meeting_Title_fb` | Current meeting title, or empty when unavailable. |
| `Meeting_ID_fb` | Current meeting ID, or empty when unavailable. |
| `Recording_State_fb` | Controller recording state text; `Unknown` when unavailable. |
| `Status_fb` | Connection/metadata status and most recent command result or error. The command result is retained across metadata refreshes. |
| `Mic_Number[1..100]` | Actual seat number as text. Unused seat rows output `"0"`. |
| `Mic_Name[1..100]` | Participant name from the current meeting; fallback `Seat N`. Empty for unused rows. |
| `Mic_Role[1..100]` | Controller seat role; empty for unused rows. |
| `Speaker_Number[1..15]` | Seat number as text in controller speaker order. Empty for unused rows. |
| `Speaker_Name[1..15]` | Participant name or `Seat N` in the matching speaker row. Empty for unused rows. |
| `Request_Number[1..15]` | Seat number as text in controller request order. Empty for unused rows. |
| `Request_Name[1..15]` | Participant name or `Seat N` in the matching request row. Empty for unused rows. |
| `Scheduled_Meeting_Name[1..5]` | Scheduled meeting name in controller list order. Empty for unused rows. |
| `Scheduled_Meeting_ID[1..5]` | Stable scheduled-meeting ID for the matching row. Empty for unused rows. |

### Three distinct numbering systems

| Row type | Mapping example |
| --- | --- |
| Seat rows | Sorted by actual seat number. If seats are 3, 12, and 40, `Mic_Toggle[2]` controls seat 12 and `Mic_Number[2]` is `"12"`. |
| Speaker/request rows | Preserve controller list order. `Selected_Speaker = 2` targets the second current speaker, regardless of seat number. |
| Scheduled-meeting rows | `Selected_Meeting` selects the displayed row; the action uses that row's stable ID. |

## Demo interface

The included Crestron Construct demo provides an example touchpanel interface. The [help PDF](Televic_Confero_Control_Help.pdf) shows the interface on its final page.

Open `televic test.smw` to inspect the SIMPL Windows example wiring. Open `Televic Confero Demo/Televic Confero Demo/Televic Confero Demo.cuip` in Crestron Construct to edit the interface. The repository includes interface output files `Televic Confero Demo.chd2` and `.chd_`.

Module row capacities and interface list sizes are configured separately. Check joins and displayed row counts when adapting the demo.

## Build and testing

Library source is in `Televic Confero c#/Televic Confero/`: `Class1.cs` handles connection/events, and `Module.cs` implements controls and metadata. Local HTTP integration fixtures are in `tests/`.

Run `./build.ps1` to rebuild the C# project in Release configuration, copy its CLZ beside the USP, and compile the USP for 4-Series. Run `./build.ps1 -Test` to also compile and run both local HTTP integration fixtures. The script uses fixed paths for Visual Studio 2022 Community MSBuild, the installed Crestron SIMPL+ compiler, and the .NET Framework C# compiler; adjust those paths if your installation differs. The project targets .NET Framework 4.7.2 and references the bundled Crestron SIMPL# SDK 2.21.237.

The script writes SIMPL+ compiler output to `compile.log` and checks for zero compiler errors and generated module code. It builds the library and SIMPL+ module only; compile the SIMPL Windows program and export the Construct interface separately after changing their wiring or layout. After changing module capacities or pins, update the symbol in SIMPL Windows before recompiling the program.

The integration checks cover speaker ordering, empty and single-speaker states, removal/reactivation, connection failure/recovery, authentication, repeated initialization, notification cursors, and recovery from an actual 40-second idle event timeout without dropping connection feedback. The full-module HTTP fixture additionally checks noncontiguous seat mapping, independent row capacities, all commands, participant names, settings options, external queue/recording events, and optional-feature failures. Tests do not require the shared simulator. Hardware behavior, TLS, and SIMPL wiring still require validation on a Crestron processor connected to Confero.

The build normalizes the USP to CRLF because the installed SIMPL+ compiler can silently treat the rest of an LF-only file as a line comment.

The shared fake API is available in `ConferoFakeAPI-TestKit` through a Windows directory junction to the Q-SYS project's test kit. Edits through either project immediately affect the same files. The linked folder is excluded from this repository's Git tracking; commit simulator changes in the Q-SYS repository.

Run `powershell -NoProfile -ExecutionPolicy Bypass -File .\link-fake-api.ps1` to create the link after cloning, or pass `-TargetPath 'C:\path\to\ConferoFakeAPI-TestKit'` if the Q-SYS project is elsewhere. Start the simulator from this workspace with `python ./ConferoFakeAPI-TestKit/ConferoFakeAPI.py --seats 10 --token qsys-test-token`, then open `http://127.0.0.1:9080/`. For Crestron testing, set `IP` to the simulator computer's address and `API_Key` to `qsys-test-token`. The shared folder's `TESTING.md` contains further simulator instructions.
