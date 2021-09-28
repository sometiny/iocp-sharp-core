using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;

namespace IocpSharp
{
    //方便从Stream中拿到基础Socket的一些信息，像RemoteEndPoint等
    public interface ISocketBasedStream
    {
        public Socket BaseSocket { get; }
    }
}
