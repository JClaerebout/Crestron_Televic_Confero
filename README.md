# Televic Confero Control

SIMPL+ module with a SIMPL# .NET Framework 4.7.2 library, compiled for Crestron 4-Series.

Set string parameters `IP` (controller address) and `API_Key` (Bearer token, without the Bearer prefix). HTTP port 9080 is used; `host:port` is also accepted.

Pulse `Initialize` to start reading active microphones and subscribing to Discussion notifications. A repeated pulse replaces the subscription. An input already high at module startup also initializes it. Releasing the input leaves monitoring running.

- `Connected_fb`: high immediately after a successful authenticated speaker snapshot, without waiting for a speaker change or the first event response. Low during initialization, authentication/communication failure, and retry.
- `First_Active_Mic`: first seat number in the ordered speaker list.
- `Last_Active_Mic`: last seat number in the ordered speaker list.

Both analogs are 0 for an empty list or disconnected state, and equal for one active microphone. These are seat numbers, not positions in the list. Feedback follows SpeakersChanged events. A lost event history triggers a fresh snapshot. An idle event request reaching its 40-second client timeout triggers a speaker snapshot and renews the poll with the same event cursor. Feedback stays connected if that snapshot succeeds. Communication errors clear feedback and retry after two seconds. A failed request can take up to the 40-second HTTP timeout to be detected; startup feedback does not wait for the first long-poll response.

Run `./build.ps1` to build the provided C# project, copy its CLZ beside the USP, and compile the USP. Run `./build.ps1 -Test` to also run the local HTTP integration checks. Visual Studio 2022 Community MSBuild, the bundled NuGet SDK, and the installed Crestron SIMPL+ compiler are used.

The integration checks cover speaker ordering, empty and single-speaker states, removal/reactivation, connection failure/recovery, authentication, repeated initialization, notification cursors, and recovery from an actual 40-second idle event timeout without dropping connection feedback. Hardware behavior still requires validation on a Crestron processor connected to Confero.
