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

namespace CrestronModule_ANALOG_TO_SERIAL_V1_0
{
    public class CrestronModuleClass_ANALOG_TO_SERIAL_V1_0 : SplusObject
    {
        static CCriticalSection g_criticalSection = new CCriticalSection();
        
        InOutArray<Crestron.Logos.SplusObjects.AnalogInput> ANALOGIN;
        InOutArray<Crestron.Logos.SplusObjects.StringOutput> STRINGOUT;
        object ANALOGIN_OnChange_0 ( Object __EventInfo__ )
        
            { 
            Crestron.Logos.SplusObjects.SignalEventArgs __SignalEventArg__ = (Crestron.Logos.SplusObjects.SignalEventArgs)__EventInfo__;
            try
            {
                SplusExecutionContext __context__ = SplusThreadStartCode(__SignalEventArg__);
                
                __context__.SourceCodeLine = 29;
                MakeString ( STRINGOUT [ Functions.GetLastModifiedArrayIndex( __SignalEventArg__ )] , "{0}", Functions.ItoA (  (int) ( ANALOGIN[ Functions.GetLastModifiedArrayIndex( __SignalEventArg__ ) ] .UshortValue ) ) ) ; 
                
                
            }
            catch(Exception e) { ObjectCatchHandler(e); }
            finally { ObjectFinallyHandler( __SignalEventArg__ ); }
            return this;
            
        }
        
    
    public override void LogosSplusInitialize()
    {
        _SplusNVRAM = new SplusNVRAM( this );
        
        ANALOGIN = new InOutArray<AnalogInput>( 25, this );
        for( uint i = 0; i < 25; i++ )
        {
            ANALOGIN[i+1] = new Crestron.Logos.SplusObjects.AnalogInput( ANALOGIN__AnalogSerialInput__ + i, ANALOGIN__AnalogSerialInput__, this );
            m_AnalogInputList.Add( ANALOGIN__AnalogSerialInput__ + i, ANALOGIN[i+1] );
        }
        
        STRINGOUT = new InOutArray<StringOutput>( 25, this );
        for( uint i = 0; i < 25; i++ )
        {
            STRINGOUT[i+1] = new Crestron.Logos.SplusObjects.StringOutput( STRINGOUT__AnalogSerialOutput__ + i, this );
            m_StringOutputList.Add( STRINGOUT__AnalogSerialOutput__ + i, STRINGOUT[i+1] );
        }
        
        
        for( uint i = 0; i < 25; i++ )
            ANALOGIN[i+1].OnAnalogChange.Add( new InputChangeHandlerWrapper( ANALOGIN_OnChange_0, false ) );
            
        
        _SplusNVRAM.PopulateCustomAttributeList( true );
        
        NVRAM = _SplusNVRAM;
        
    }
    
    public override void LogosSimplSharpInitialize()
    {
        
        
    }
    
    public CrestronModuleClass_ANALOG_TO_SERIAL_V1_0 ( string InstanceName, string ReferenceID, Crestron.Logos.SplusObjects.CrestronStringEncoding nEncodingType ) : base( InstanceName, ReferenceID, nEncodingType ) {}
    
    
    
    
    const uint ANALOGIN__AnalogSerialInput__ = 0;
    const uint STRINGOUT__AnalogSerialOutput__ = 0;
    
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
