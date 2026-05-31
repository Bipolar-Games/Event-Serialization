using System;

namespace Bipolar
{
    [AttributeUsage(AttributeTargets.Event | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class SerializeEventAttribute : Attribute
    {
        public string CustomEventName { get; }
        
        public SerializeEventAttribute(string customEventName = null) 
        {
            CustomEventName = customEventName;
        }
    }
}
