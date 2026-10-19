namespace TelevicConfero;
        // class declarations
         class MicFeedbackEventArgs;
         class ConferoController;
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

        // class functions
        FUNCTION Initialize ( STRING ip , STRING apiKey );
        SIGNED_LONG_INTEGER_FUNCTION GetHashCode ();
        STRING_FUNCTION ToString ();

        // class variables
        INTEGER __class_id__;

        // class properties
    };

