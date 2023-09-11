using System;

namespace Sniffer
{
    public class MessagePackage
    {
        public string type;
        public string from;
        public byte[] data;
        public Int64 socketHandle;

        public MessagePackage(byte[] dt, Int64 sh)
        {
            data = dt;
            socketHandle = sh;
        }

        public MessagePackage()
        {
        }

        public MessagePackage Clone()
        {
            var m = new MessagePackage((byte[])data.Clone(), socketHandle);
            m.from = from;
            m.type = type;

            return m;
        }
    }
}
