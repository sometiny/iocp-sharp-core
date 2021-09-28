using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace IocpSharp.Server
{
    /// <summary>
    /// 直接继承SocketAsyncEventArgs，作为服务器
    /// </summary>
    public abstract class TcpIocpServer : SocketAsyncEventArgs
    {
        private Socket _socket = null;
        private IPEndPoint _localEndPoint = null;

        /// <summary>
        /// 基础Socket
        /// </summary>
        protected Socket Socket => _socket;

        /// <summary>
        /// 本地监听终结点
        /// </summary>
        public IPEndPoint LocalEndPoint => _localEndPoint;

        /// <summary>
        /// 实例化服务器
        /// </summary>
        public TcpIocpServer() : base()
        {
        }

        /// <summary>
        /// 启动服务器
        /// </summary>
        /// <returns>true成功，false失败</returns>
        protected virtual void Start()
        {
            if (_localEndPoint == null) throw new ArgumentNullException("LocalEndPoint");

            _socket = new Socket(_localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            _socket.Bind(_localEndPoint);
            _socket.Listen(256);
            _localEndPoint = _socket.LocalEndPoint as IPEndPoint;

            StartAccept();
            Started();
        }

        /// <summary>
        /// 使用本地终结点启动服务器
        /// </summary>
        /// <param name="localEndPoint">本地终结点</param>
        /// <returns></returns>
        public void Start(EndPoint localEndPoint)
        {
            _localEndPoint = localEndPoint as IPEndPoint;
            Start();
        }


        /// <summary>
        /// 使用IP地址和端口启动服务器
        /// </summary>
        /// <param name="iPAddress">ip地址</param>
        /// <param name="port">监听端口，0系统自动使用可用端口，可以在服务器启动后，通过LocalEndPoint属性获取到真正监听的端口</param>
        public void Start(IPAddress iPAddress, int port = 0)
        {
            Start(new IPEndPoint(iPAddress, port));
        }

        /// <summary>
        /// 使用IP地址和端口启动服务器
        /// </summary>
        /// <param name="iPAddress">ip地址</param>
        /// <param name="port">监听端口，0系统自动使用可用端口，可以在服务器启动后，通过LocalEndPoint属性获取到真正监听的端口</param>
        public void Start(string iPAddress, int port = 0)
        {
            Start(new IPEndPoint(IPAddress.Parse(iPAddress), port));
        }

        /// <summary>
        /// 服务器启动后调用
        /// </summary>
        protected virtual void Started()
        {

        }
        /// <summary>
        /// 服务器被停止时调用
        /// </summary>
        protected virtual void Stoped()
        {

        }

        /// <summary>
        /// 停止服务器
        /// </summary>
        public virtual void Stop()
        {
            try
            {
                _socket?.Close();
            }
            catch { }
            Stoped();
        }

        /// <summary>
        /// 服务器发生异常时调用
        /// </summary>
        /// <param name="e">异常</param>
        protected virtual void Error(Exception e)
        {

        }

        /// <summary>
        /// 开始接受客户端请求
        /// </summary>
        private void StartAccept()
        {
            AcceptSocket = null;

            try
            {
                if (!_socket.AcceptAsync(this))
                {
                    OnCompleted(this);
                }
            }
            catch (SocketException e)
            {
                Error(e);
            }
            catch (ObjectDisposedException)
            {
            }
        }

        /// <summary>
        /// 重写OnCompleted方法
        /// </summary>
        /// <param name="e"></param>
        protected sealed override void OnCompleted(SocketAsyncEventArgs e)
        {
            if (SocketError != SocketError.Success)
            {
                if (SocketError == SocketError.OperationAborted) return;
                Error(new SocketException((int)SocketError));
                StartAccept();
                return;
            }

            Socket client = AcceptSocket;
            StartAccept();
            client.NoDelay = true;
            NewClientInternal(client);
        }

        /// <summary>
        /// 重载NewClientInternal，可以不使用线程池来处理请求，默认使用线程池处理。
        /// </summary>
        /// <param name="client"></param>
        protected void NewClientInternal(Socket client)
        {
            /*
             * 使用新线程处理新的连接请求
             * 使用IOCP可一定程度上提升性能
             */
            ThreadPool.UnsafeQueueUserWorkItem(state => NewClient(state as Socket), client);
        }

        /// <summary>
        /// 接收到新客户端时调用，需要在子类实现其他业务逻辑
        /// </summary>
        /// <param name="client">客户端Socket</param>
        protected abstract void NewClient(Socket client);
    }
}
