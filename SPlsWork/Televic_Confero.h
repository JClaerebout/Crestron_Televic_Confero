namespace TelevicConfero;
        // class declarations
         class MicFeedbackEventArgs;
         class ConferoController;
         class ModuleFeedbackEventArgs;
     class MicFeedbackEventArgs 
    {
        // class delegates

        // class events

        // class functions
        SIGNED_LONG_INTEGER_FUNCTION GetHashCode ();
        STRING_FUNCTION ToString ();

        // class variables
        INTEGER __class_id__;

        // class properties
        INTEGER Connected;
        INTEGER FirstActiveMic;
        INTEGER LastActiveMic;
    };

     class ConferoController 
    {
        // class delegates

        // class events
        EventHandler OnFeedback ( ConferoController sender, MicFeedbackEventArgs e );
        EventHandler OnModuleFeedback ( ConferoController sender, ModuleFeedbackEventArgs e );

        // class functions
        FUNCTION Initialize ( STRING ip , STRING apiKey );
        FUNCTION ConfigureTransport ( INTEGER https , INTEGER debug );
        FUNCTION Configure ( INTEGER microphones , INTEGER speakers , INTEGER requests , INTEGER meetings , INTEGER refresh );
        INTEGER_FUNCTION GetSeatNumber ( INTEGER row );
        INTEGER_FUNCTION GetSeatMic ( INTEGER row );
        INTEGER_FUNCTION GetSeatRequest ( INTEGER row );
        STRING_FUNCTION GetSeatRole ( INTEGER row );
        STRING_FUNCTION GetSeatName ( INTEGER row );
        INTEGER_FUNCTION GetSpeakerNumber ( INTEGER row );
        INTEGER_FUNCTION GetRequestNumber ( INTEGER row );
        STRING_FUNCTION GetSpeakerName ( INTEGER row );
        STRING_FUNCTION GetRequestName ( INTEGER row );
        STRING_FUNCTION GetMeetingId ( INTEGER row );
        STRING_FUNCTION GetMeetingName ( INTEGER row );
        FUNCTION SetSeatMic ( INTEGER row , INTEGER on );
        FUNCTION TurnOffSpeaker ( INTEGER row );
        FUNCTION EnableRequest ( INTEGER row );
        FUNCTION Refresh ();
        FUNCTION ClearSpeakers ();
        FUNCTION ClearRequests ();
        FUNCTION StartMeeting ();
        FUNCTION StopMeeting ();
        FUNCTION StartScheduledMeeting ( INTEGER row );
        FUNCTION SetRecording ( INTEGER on );
        FUNCTION WirelessTurnOff ();
        FUNCTION ApplySettings ( INTEGER mode , INTEGER openMics );
        SIGNED_LONG_INTEGER_FUNCTION GetHashCode ();
        STRING_FUNCTION ToString ();

        // class variables
        INTEGER __class_id__;

        // class properties
    };

     class ModuleFeedbackEventArgs 
    {
        // class delegates

        // class events

        // class functions
        SIGNED_LONG_INTEGER_FUNCTION GetHashCode ();
        STRING_FUNCTION ToString ();

        // class variables
        INTEGER __class_id__;

        // class properties
        INTEGER SeatCount;
        INTEGER SpeakerCount;
        INTEGER RequestCount;
        INTEGER MeetingCount;
        INTEGER MeetingRunning;
        INTEGER RecordingActive;
        INTEGER DiscussionMode;
        INTEGER OpenMics;
        STRING MeetingId[];
        STRING MeetingTitle[];
        STRING RecordingState[];
        STRING Status[];
    };

