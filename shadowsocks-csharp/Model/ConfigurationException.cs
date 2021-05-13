using System;

namespace Shadowsocks.Model
{
    [Serializable]
    internal class ConfigurationException : Exception
    {
        public ConfigurationException()
        { }
        public ConfigurationException(string message) : base(message) { }
        public ConfigurationException(string message, Exception inner) : base(message, inner) { }
        protected ConfigurationException(System.Runtime.Serialization.SerializationInfo info,
                System.Runtime.Serialization.StreamingContext context)
        { }
    }
}
