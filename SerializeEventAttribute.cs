using System;

namespace Bipolar
{
    [AttributeUsage(AttributeTargets.Event | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class SerializeEventAttribute : Attribute
    { }
}
