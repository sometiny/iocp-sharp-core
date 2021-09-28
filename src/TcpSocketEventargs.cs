using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;

namespace IocpSharp
{
    /// <summary>
    /// 异步读取的委托
    /// </summary>
    /// <param name="bytesReceived">接收到的字节数</param>
    /// <param name="errorCode">错误代码</param>
    /// <param name="state">用户状态</param>
    public delegate void AsyncReadCallback(int bytesReceived, int errorCode, object state);

    /// <summary>
    /// 异步写入的委托
    /// </summary>
    /// <param name="errorCode">错误代码</param>
    /// <param name="state">用户状态</param>
    public delegate void AsyncWriteCallback(int errorCode, object state);

    /// <summary>
    /// TcpSocketAsyncEventArgs类用于数据的异步读写，不需要事件，直接内部重写OnCompleted方法。
    /// 其实就是把事件封装到了回调。
    /// </summary>
    public class TcpSocketAsyncEventArgs : SocketAsyncEventArgs
    {

        private AsyncReadCallback _asyncReadCallback = null;
        private AsyncWriteCallback _asyncWriteCallback = null;
        /// <summary>
        /// 重写SocketAsyncEventArgs的OnCompleted方法
        /// 实现我们自己的逻辑
        /// </summary>
        /// <param name="e"></param>
        protected override void OnCompleted(SocketAsyncEventArgs e)
        {
            if(e.LastOperation == SocketAsyncOperation.Receive && _asyncReadCallback != null)
            {
                _asyncReadCallback(e.BytesTransferred, (int)e.SocketError, UserToken);
                return;
            }
            if (e.LastOperation == SocketAsyncOperation.Send && _asyncWriteCallback != null)
            {
                _asyncWriteCallback((int)e.SocketError, UserToken);
                return;
            }
            base.OnCompleted(e);
        }

        /// <summary>
        /// 异步读取数据
        /// </summary>
        /// <param name="socket">基础Socket</param>
        /// <param name="buffer">缓冲区</param>
        /// <param name="offset">数据在缓冲区中的索引</param>
        /// <param name="size">准备读取的数据大小</param>
        /// <param name="callback">回调</param>
        /// <param name="state">状态</param>
        /// <returns></returns>
        public void ReadAsync(Socket socket, byte[] buffer, int offset, int size, AsyncReadCallback callback, object state)
        {
            //Console.WriteLine("TcpSocketAsyncEventArgs.ReadAsync");
            _asyncReadCallback = callback;
            UserToken = state;
            SetBuffer(buffer, offset, size);
            if (!socket.ReceiveAsync(this))
            {
                OnCompleted(this);
            }
        }

        /// <summary>
        /// 异步发送数据
        /// </summary>
        /// <param name="socket">基础Socket</param>
        /// <param name="buffer">缓冲区</param>
        /// <param name="offset">数据在缓冲区中的索引</param>
        /// <param name="size">准备读取的数据大小</param>
        /// <param name="callback">回调</param>
        /// <param name="state">状态</param>
        /// <returns></returns>
        public void WriteAsync(Socket socket, byte[] buffer, int offset, int size, AsyncWriteCallback callback, object state)
        {
            //Console.WriteLine("TcpSocketAsyncEventArgs.WriteAsync");
            _asyncWriteCallback = callback;
            UserToken = state;
            SetBuffer(buffer, offset, size);
            if (!socket.SendAsync(this))
            {
                OnCompleted(this);
            }
        }

        private static ConcurrentStack<TcpSocketAsyncEventArgs> _stacks = new ConcurrentStack<TcpSocketAsyncEventArgs>();
        /// <summary>
        /// 从栈中获取一个TcpSocketAsyncEventArgs实例
        /// 对TcpSocketAsyncEventArgs实例的重复使用
        /// </summary>
        /// <returns></returns>
        public static TcpSocketAsyncEventArgs Pop()
        {
            if (_stacks.TryPop(out TcpSocketAsyncEventArgs e)) return e;

            return new TcpSocketAsyncEventArgs();
        }

        /// <summary>
        /// 将TcpSocketAsyncEventArgs实例放入栈中
        /// </summary>
        /// <param name="e"></param>
        public static void Push(TcpSocketAsyncEventArgs e)
        {
            e._asyncReadCallback = null;
            e._asyncWriteCallback = null;
            e.SetBuffer(null, 0, 0);
            e.UserToken = null;
            _stacks.Push(e);
        }
    }
}
