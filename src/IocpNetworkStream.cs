using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace IocpSharp
{
    /// <summary>
    /// 使用TcpSocketAsyncEventArgs实现的IOCP异步读写的NetworkStream
    /// </summary>
    public class IocpNetworkStream : NetworkStream, ISocketBasedStream
    {
        /// <summary>
        /// 获取基础Socket
        /// </summary>
        public Socket BaseSocket => Socket;

        private class ReadWriteArgs
        {
            public TcpSocketAsyncEventArgs TcpSocketAsyncEventArgs;
            public IocpReadWriteResult AsyncResult;
            public ReadWriteArgs(TcpSocketAsyncEventArgs e, IocpReadWriteResult asyncResult)
            {
                TcpSocketAsyncEventArgs = e;
                AsyncResult = asyncResult;
            }

            ~ReadWriteArgs()
            {
                TcpSocketAsyncEventArgs = null;
                AsyncResult = null;
            }
        }
        /// <summary>
        /// 实现NetworkStream的两个构造方法
        /// </summary>
        /// <param name="baseSocket">基础Socket</param>
        public IocpNetworkStream(Socket baseSocket) : base(baseSocket) { }

        /// <summary>
        /// 实现NetworkStream的两个构造方法
        /// </summary>
        /// <param name="baseSocket">基础Socket</param>
        /// <param name="ownSocket">是否拥有Socket，为true的话，在Stream关闭的同时，关闭Socket</param>
        public IocpNetworkStream(Socket baseSocket, bool ownSocket) : base(baseSocket, ownSocket) { }

        /// <summary>
        /// 实现异步IOCP读取数据
        /// </summary>
        /// <param name="buffer">缓冲区</param>
        /// <param name="offset">数据在缓冲区的位置</param>
        /// <param name="size">准备读取的数据大小</param>
        /// <param name="callback">回调方法</param>
        /// <param name="state">用户状态</param>
        /// <returns></returns>
        public override IAsyncResult BeginRead(byte[] buffer, int offset, int size, AsyncCallback callback, object state)
        {
            //从栈中弹出一个TcpSocketAsyncEventArgs用来读取数据
            TcpSocketAsyncEventArgs e = TcpSocketAsyncEventArgs.Pop();

            //实现一个IAsyncResult
            IocpReadWriteResult asyncResult = new IocpReadWriteResult(callback, state, buffer, offset, size);

            try
            {
                //读取数据，完成后调用回调函数AfterRead
                //将TcpSocketAsyncEventArgs和IocpReadWriteResult传递给回调函数。
                //ReadWriteArgs是一个简单的封装，保存上面两个参数。
                e.ReadAsync(Socket, buffer, offset, size, AfterRead, new ReadWriteArgs(e, asyncResult));

                return asyncResult;
            }
            catch(SocketException ex)
            {
                asyncResult.SetFailed(ex);

                //回收TcpSocketAsyncEventArgs
                TcpSocketAsyncEventArgs.Push(e);
                return asyncResult;
            }
            catch
            {
                asyncResult.Dispose();
                //回收TcpSocketAsyncEventArgs
                TcpSocketAsyncEventArgs.Push(e);
                throw;
            }
        }

        /// <summary>
        /// 基础流读取到数据后
        /// </summary>
        /// <param name="errorCode"></param>
        /// <param name="bytesReceived"></param>
        /// <param name="state"></param>
        private void AfterRead(int errorCode, int bytesReceived, object state)
        {
            ReadWriteArgs args = state as ReadWriteArgs;
            IocpReadWriteResult result = args.AsyncResult;

            if (errorCode != 0)
            {
                //如果IOCP返回了错误代码，设置IAsyncResult为失败状态
                result.SetFailed(new SocketException(errorCode));
            }
            else
            {
                //成功读取数据，反馈给IAsyncResult
                result.BytesTransfered = bytesReceived;
                result.CallUserCallback();
            }

            //回收TcpSocketAsyncEventArgs
            TcpSocketAsyncEventArgs.Push(args.TcpSocketAsyncEventArgs);
        }

        /// <summary>
        /// 结束读取数据
        /// </summary>
        /// <param name="asyncResult"></param>
        /// <returns>读取数据的大小</returns>
        public override int EndRead(IAsyncResult asyncResult)
        {
            if (asyncResult is not IocpReadWriteResult result) throw new InvalidOperationException("asyncResult");

            //如果同步调用了EndRead，等待IAsyncResult完成
            //不建议同步调用。
            if (!result.IsCompleted) result.AsyncWaitHandle.WaitOne();

            //失败，抛出异常
            if (result.Exception != null) throw result.Exception;

            //返回读取到的数据
            return result.BytesTransfered;
        }

        /// <summary>
        /// 实现异步IOCP写入数据
        /// 理解逻辑跟BeginWrite一样，就不多写注释了。
        /// </summary>
        /// <param name="buffer">缓冲区</param>
        /// <param name="offset">数据在缓冲区的位置</param>
        /// <param name="size">准备写入的数据大小</param>
        /// <param name="callback">回调方法</param>
        /// <param name="state">用户状态</param>
        /// <returns></returns>
        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int size, AsyncCallback callback, object state)
        {
            TcpSocketAsyncEventArgs e = TcpSocketAsyncEventArgs.Pop();

            IocpReadWriteResult asyncResult = new IocpReadWriteResult(callback, state, buffer, offset, size);
            try
            {
                e.WriteAsync(Socket, buffer, offset, size, AfterWrite, new ReadWriteArgs(e, asyncResult));

                return asyncResult;
            }
            catch (SocketException ex)
            {
                asyncResult.SetFailed(ex);
                TcpSocketAsyncEventArgs.Push(e);
                return asyncResult;
            }
            catch
            {
                asyncResult.Dispose();
                TcpSocketAsyncEventArgs.Push(e);
                throw;
            }
        }

        /// <summary>
        /// 基础流写入后
        /// </summary>
        /// <param name="errorCode"></param>
        /// <param name="state"></param>
        private void AfterWrite(int errorCode, object state)
        {
            ReadWriteArgs args = state as ReadWriteArgs;
            IocpReadWriteResult result = args.AsyncResult;
            if (errorCode != 0)
            {
                result.SetFailed(new SocketException(errorCode));
            }
            else
            {
                result.CallUserCallback();
            }
            TcpSocketAsyncEventArgs.Push(args.TcpSocketAsyncEventArgs);
        }

        /// <summary>
        /// 结束写入数据
        /// </summary>
        /// <param name="asyncResult"></param>
        /// <returns>读取数据的大小</returns>
        public override void EndWrite(IAsyncResult asyncResult)
        {
            if (asyncResult is not IocpReadWriteResult result) throw new InvalidOperationException("asyncResult");

            if (!result.IsCompleted) result.AsyncWaitHandle.WaitOne();

            if (result.Exception != null) throw result.Exception;
        }

        /// <summary>
        /// 重写异步读方法
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
#if !NET452
            if (cancellationToken.IsCancellationRequested) return Task.FromCanceled<int>(cancellationToken);
#endif
            return Task.Factory.FromAsync(BeginRead, EndRead, buffer, offset, count, null);
        }

        /// <summary>
        /// 重写异步写方法
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
#if !NET452
            if (cancellationToken.IsCancellationRequested) return Task.FromCanceled(cancellationToken);
#endif
            return Task.Factory.FromAsync(BeginWrite, EndWrite, buffer, offset, count, null);
        }
    }
}
