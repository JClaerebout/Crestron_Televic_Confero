using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Linq;
using Crestron;
using Crestron.Logos.SplusLibrary;
using Crestron.Logos.SplusObjects;
using Crestron.SimplSharp;
using TelevicConfero;
using Crestron.SimplSharp.SimplSharpCloudClient;
using Crestron.SimplSharp.Python;

namespace UserModule_TELEVIC_CONFERO_CONTROL
{
    public class UserModuleClass_TELEVIC_CONFERO_CONTROL : SplusObject
    {
        static CCriticalSection g_criticalSection = new CCriticalSection();
        
        
        StringParameter IP;
        StringParameter API_KEY;
        Crestron.Logos.SplusObjects.DigitalInput INITIALIZE;
        Crestron.Logos.SplusObjects.DigitalOutput CONNECTED_FB;
        Crestron.Logos.SplusObjects.AnalogOutput LAST_ACTIVE_MIC;
        Crestron.Logos.SplusObjects.AnalogOutput FIRST_ACTIVE_MIC;
        TelevicConfero.ConferoController CONTROLLER;
        ushort READY = 0;
        public void CONTROLLER_ONFEEDBACK ( object __sender__ /*TelevicConfero.ConferoController SENDER */, TelevicConfero.MicFeedbackEventArgs E ) 
            { 
            ConferoController  SENDER  = (ConferoController )__sender__;
            try
            {
                SplusExecutionContext __context__ = SplusSimplSharpDelegateThreadStartCode();
                
                __context__.SourceCodeLine = 1;
                if ( Functions.TestForTrue  ( ( Functions.BoolToInt (CONNECTED_FB  .Value != E.Connected))  ) ) 
                    {
                    __context__.SourceCodeLine = 1;
                    CONNECTED_FB  .Value = (ushort) ( E.Connected ) ; 
                    }
                
                __context__.SourceCodeLine = 1;
                if ( Functions.TestForTrue  ( ( Functions.BoolToInt (FIRST_ACTIVE_MIC  .Value != E.FirstActiveMic))  ) ) 
                    {
                    __context__.SourceCodeLine = 1;
                    FIRST_ACTIVE_MIC  .Value = (ushort) ( E.FirstActiveMic ) ; 
                    }
                
                __context__.SourceCodeLine = 1;
                if ( Functions.TestForTrue  ( ( Functions.BoolToInt (LAST_ACTIVE_MIC  .Value != E.LastActiveMic))  ) ) 
                    {
                    __context__.SourceCodeLine = 1;
                    LAST_ACTIVE_MIC  .Value = (ushort) ( E.LastActiveMic ) ; 
                    }
                
                
                
            }
            finally { ObjectFinallyHandler(); }
            }
            
        object INITIALIZE_OnPush_0 ( Object __EventInfo__ )
        
            { 
            Crestron.Logos.SplusObjects.SignalEventArgs __SignalEventArg__ = (Crestron.Logos.SplusObjects.SignalEventArgs)__EventInfo__;
            try
            {
                SplusExecutionContext __context__ = SplusThreadStartCode(__SignalEventArg__);
                
                __context__.SourceCodeLine = 1;
                if ( Functions.TestForTrue  ( ( Functions.BoolToInt (READY == 1))  ) ) 
                    {
                    __context__.SourceCodeLine = 1;
                    CONTROLLER . Initialize ( IP  .ToString(), API_KEY  .ToString()) ; 
                    }
                
                
                
            }
            catch(Exception e) { ObjectCatchHandler(e); }
            finally { ObjectFinallyHandler( __SignalEventArg__ ); }
            return this;
            
        }
        
    public override object FunctionMain (  object __obj__ ) 
        { 
        try
        {
            SplusExecutionContext __context__ = SplusFunctionMainStartCode();
            
            __context__.SourceCodeLine = 1;
            READY = (ushort) ( 0 ) ; 
            __context__.SourceCodeLine = 1;
            CONNECTED_FB  .Value = (ushort) ( 0 ) ; 
            __context__.SourceCodeLine = 1;
            FIRST_ACTIVE_MIC  .Value = (ushort) ( 0 ) ; 
            __context__.SourceCodeLine = 1;
            LAST_ACTIVE_MIC  .Value = (ushort) ( 0 ) ; 
            __context__.SourceCodeLine = 1;
            WaitForInitializationComplete ( ) ; 
            __context__.SourceCodeLine = 1;
            // RegisterEvent( CONTROLLER , ONFEEDBACK , CONTROLLER_ONFEEDBACK ) 
            try { g_criticalSection.Enter(); CONTROLLER .OnFeedback  += CONTROLLER_ONFEEDBACK; } finally { g_criticalSection.Leave(); }
            ; 
            __context__.SourceCodeLine = 1;
            READY = (ushort) ( 1 ) ; 
            __context__.SourceCodeLine = 1;
            if ( Functions.TestForTrue  ( ( Functions.BoolToInt (INITIALIZE  .Value == 1))  ) ) 
                {
                __context__.SourceCodeLine = 1;
                CONTROLLER . Initialize ( IP  .ToString(), API_KEY  .ToString()) ; 
                }
            
            
            
        }
        catch(Exception e) { ObjectCatchHandler(e); }
        finally { ObjectFinallyHandler(); }
        return __obj__;
        }
        
    
    public override void LogosSplusInitialize()
    {
        SocketInfo __socketinfo__ = new SocketInfo( 1, this );
        InitialParametersClass.ResolveHostName = __socketinfo__.ResolveHostName;
        _SplusNVRAM = new SplusNVRAM( this );
        
        INITIALIZE = new Crestron.Logos.SplusObjects.DigitalInput( INITIALIZE__DigitalInput__, this );
        m_DigitalInputList.Add( INITIALIZE__DigitalInput__, INITIALIZE );
        
        CONNECTED_FB = new Crestron.Logos.SplusObjects.DigitalOutput( CONNECTED_FB__DigitalOutput__, this );
        m_DigitalOutputList.Add( CONNECTED_FB__DigitalOutput__, CONNECTED_FB );
        
        LAST_ACTIVE_MIC = new Crestron.Logos.SplusObjects.AnalogOutput( LAST_ACTIVE_MIC__AnalogSerialOutput__, this );
        m_AnalogOutputList.Add( LAST_ACTIVE_MIC__AnalogSerialOutput__, LAST_ACTIVE_MIC );
        
        FIRST_ACTIVE_MIC = new Crestron.Logos.SplusObjects.AnalogOutput( FIRST_ACTIVE_MIC__AnalogSerialOutput__, this );
        m_AnalogOutputList.Add( FIRST_ACTIVE_MIC__AnalogSerialOutput__, FIRST_ACTIVE_MIC );
        
        IP = new StringParameter( IP__Parameter__, this );
        m_ParameterList.Add( IP__Parameter__, IP );
        
        API_KEY = new StringParameter( API_KEY__Parameter__, this );
        m_ParameterList.Add( API_KEY__Parameter__, API_KEY );
        
        
        INITIALIZE.OnDigitalPush.Add( new InputChangeHandlerWrapper( INITIALIZE_OnPush_0, false ) );
        
        _SplusNVRAM.PopulateCustomAttributeList( true );
        
        NVRAM = _SplusNVRAM;
        
    }
    
    public override void LogosSimplSharpInitialize()
    {
        CONTROLLER  = new TelevicConfero.ConferoController();
        
        
    }
    
    public UserModuleClass_TELEVIC_CONFERO_CONTROL ( string InstanceName, string ReferenceID, Crestron.Logos.SplusObjects.CrestronStringEncoding nEncodingType ) : base( InstanceName, ReferenceID, nEncodingType ) {}
    
    
    
    
    const uint IP__Parameter__ = 10;
    const uint API_KEY__Parameter__ = 11;
    const uint INITIALIZE__DigitalInput__ = 0;
    const uint CONNECTED_FB__DigitalOutput__ = 0;
    const uint LAST_ACTIVE_MIC__AnalogSerialOutput__ = 0;
    const uint FIRST_ACTIVE_MIC__AnalogSerialOutput__ = 1;
    
    [SplusStructAttribute(-1, true, false)]
    public class SplusNVRAM : SplusStructureBase
    {
    
        public SplusNVRAM( SplusObject __caller__ ) : base( __caller__ ) {}
        
        
    }
    
    SplusNVRAM _SplusNVRAM = null;
    
    public class __CEvent__ : CEvent
    {
        public __CEvent__() {}
        public void Close() { base.Close(); }
        public int Reset() { return base.Reset() ? 1 : 0; }
        public int Set() { return base.Set() ? 1 : 0; }
        public int Wait( int timeOutInMs ) { return base.Wait( timeOutInMs ) ? 1 : 0; }
    }
    public class __CMutex__ : CMutex
    {
        public __CMutex__() {}
        public void Close() { base.Close(); }
        public void ReleaseMutex() { base.ReleaseMutex(); }
        public int WaitForMutex() { return base.WaitForMutex() ? 1 : 0; }
    }
     public int IsNull( object obj ){ return (obj == null) ? 1 : 0; }
}


}
