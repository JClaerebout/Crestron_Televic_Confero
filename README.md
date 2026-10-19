# Televic Confero Control

SIMPL+ module with a SIMPL# .NET Framework 4.7.2 library, compiled for Crestron 4-Series.

Set string parameters `IP` (controller address) and `API_Key` (Bearer token, without the Bearer prefix). HTTP port 9080 is the default; HTTPS uses 9443. An explicit `http://` or `https://` scheme overrides the transport constant. `host:port` is also accepted. HTTPS uses normal certificate validation.

Pulse `Initialize` to start monitoring. A repeated pulse replaces the subscription. An input already high at module startup also initializes it. Releasing the input leaves monitoring running. `Refresh` restarts monitoring and reloads all snapshots. Notifications cover Discussion, Meeting, Room, and Recording.

The module now follows the functions and API payloads of [your Q-SYS Televic plugin](https://github.com/JClaerebout/Q-SYS-Plugin-televic-Confero/blob/main/TelevicConfero.qplug). The source attribution and MIT notice are in `THIRD-PARTY-NOTICES.md`.

Edit the following `#DEFINE_CONSTANT` values near the top of `Televic Confero Control.usp`, rebuild, and update the SIMPL symbol:

| Constant | Default | Meaning |
| --- | --- | --- |
| `MIC_COUNT` | 15 | Seat control and feedback rows |
| `SPEAKER_LIST_COUNT` | 15 | Ordered active speaker rows |
| `QUEUE_LIST_COUNT` | 15 | Ordered request queue rows |
| `MEETING_LIST_COUNT` | 20 | Scheduled meeting rows |
| `REFRESH_SECONDS` | 30 | Metadata reconciliation interval; at least 10 seconds |
| `DEFAULT_HTTPS` | 0 | 0 = HTTP, 1 = HTTPS unless IP includes a scheme |
| `DEBUG_PRINT` | 0 | 0 = off, 1 = HTTP results/errors, 2 = also feedback summaries |

Capacities allocate module pins; they do not limit microphones on the Confero controller. Counts report the full controller list size, even when only the first configured rows fit. Unused rows clear to 0/empty. First/last active mic feedback always uses the complete speaker list. Choose positive capacities within SIMPL's signal limits. The Q-SYS seat-count property allows 1–100 seats.

Feedback outputs are written only when their value changes, including serial text and individual list rows. Repeated API snapshots and unchanged notifications do not retransmit the same values to SIMPL Debugger. Disconnection still clears previously displayed values.

Seat rows are sorted by actual API seat number, including noncontiguous numbers. `Mic_Number[i]` identifies the seat controlled by pulses on `Mic_On[i]` and `Mic_Off[i]`. Outputs `Mic_On_fb`, `Mic_Request_fb`, `Mic_Name`, and `Mic_Role` show confirmed controller feedback. Participant names come from the running meeting and fall back to `Seat N`.

Speaker and request rows retain controller order. Set `Selected_Speaker`, `Selected_Request`, or `Selected_Meeting` to a **1-based row index**, then pulse its action. Index 0 and rows outside the displayed capacity are ignored. The selected row is resolved to its current seat number or stable meeting ID when the action is triggered.

| Inputs/actions | Feedback |
| --- | --- |
| `Turn_Off_Selected_Speaker`, `Enable_Selected_Request` | `Speaker_Number/Name`, `Request_Number/Name`, `Speaker_Count_fb`, `Request_Count_fb` |
| `Clear_Speakers`, `Clear_Requests` | Confirmed speaker/request lists |
| `Meeting_Start`, `Meeting_Stop`, `Start_Selected_Meeting` | `Meeting_Running_fb`, `Meeting_Title_fb`, `Meeting_ID_fb` |
| `Selected_Meeting` | `Scheduled_Meeting_Name/ID`, `Meeting_Count_fb` |
| `Recording_Start`, `Recording_Stop` | `Recording_Active_fb`, `Recording_State_fb` |
| `Wireless_Turn_Off` | `Status_fb` reports the API command result |
| `Discussion_Mode`, `Number_Of_Open_Mics`, `Apply_Discussion_Settings` | `Discussion_Mode_fb`, `Number_Of_Open_Mics_fb` |

Discussion mode values: 1 = Direct Speak, 2 = Request, 3 = Group, 4 = Operator, 5 = Hands Free. Set mode and open-mic limit (1–100), then pulse `Apply_Discussion_Settings`. Received mode options are preserved; switching to a mode not read in this session uses the Q-SYS plugin's defaults. The open-mic limit is a live controller setting, independent of the module's compile-time row capacities.

Commands run in order and refresh confirmed metadata. A successful mic PUT does not imply the controller admitted the seat; feedback decides its displayed state. Optional-feature errors (including meeting licensing and rejected recording commands) report through `Status_fb` without disabling working speaker monitoring. Commands are ignored until speaker connectivity is established. Connection loss clears row mappings and feedback. Authentication headers/tokens are never printed.

Speaker/request notifications supply ordered list updates. Startup, manual refresh, history discontinuity, and commands reconcile snapshots; periodic metadata refresh does not poll either list endpoint. Metadata refresh includes seats, participant names, scheduled meetings, discussion settings, and recording state. Reconciliation runs between event polls, so an idle poll can delay it by up to the 40-second event timeout plus request time. An optional feature request has a 10-second timeout.

- `Connected_fb`: high immediately after a successful authenticated speaker snapshot, without waiting for a speaker change or the first event response. Low during initialization, authentication/communication failure, and retry.
- `First_Active_Mic`: first seat number in the ordered speaker list.
- `Last_Active_Mic`: last seat number in the ordered speaker list.

Both analogs are 0 for an empty list or disconnected state, and equal for one active microphone. These are seat numbers, not positions in the list. Feedback follows SpeakersChanged events. A lost event history triggers a fresh snapshot. An idle event request reaching its 40-second client timeout triggers a speaker snapshot and renews the poll with the same event cursor. Feedback stays connected if that snapshot succeeds. Communication errors clear feedback and retry after two seconds. A failed request can take up to the 40-second HTTP timeout to be detected; startup feedback does not wait for the first long-poll response.

Run `./build.ps1` to build the provided C# project, copy its CLZ beside the USP, and compile the USP. Run `./build.ps1 -Test` to also run the local HTTP integration checks. Visual Studio 2022 Community MSBuild, the bundled NuGet SDK, and the installed Crestron SIMPL+ compiler are used.

The integration checks cover speaker ordering, empty and single-speaker states, removal/reactivation, connection failure/recovery, authentication, repeated initialization, notification cursors, and recovery from an actual 40-second idle event timeout without dropping connection feedback. The full-module HTTP fixture additionally checks noncontiguous seat mapping, independent row capacities, all commands, participant names, settings options, external queue/recording events, and optional-feature failures. Tests do not require the shared simulator. Hardware behavior, TLS, and SIMPL wiring still require validation on a Crestron processor connected to Confero.

The build normalizes the USP to CRLF because the installed SIMPL+ compiler can silently treat the rest of an LF-only file as a line comment.

The shared fake API is available in `ConferoFakeAPI-TestKit` through a Windows directory junction to the Q-SYS project's test kit. Edits through either project immediately affect the same files. The linked folder is excluded from this repository's Git tracking; commit simulator changes in the Q-SYS repository.

Run `powershell -NoProfile -ExecutionPolicy Bypass -File .\link-fake-api.ps1` to create the link after cloning, or pass `-TargetPath 'C:\path\to\ConferoFakeAPI-TestKit'` if the Q-SYS project is elsewhere. Start the simulator from this workspace with `python ./ConferoFakeAPI-TestKit/ConferoFakeAPI.py --seats 10 --token qsys-test-token`, then open `http://127.0.0.1:9080/`. For Crestron testing, set `IP` to the simulator computer's address and `API_Key` to `qsys-test-token`. The shared folder's `TESTING.md` contains further simulator instructions.
