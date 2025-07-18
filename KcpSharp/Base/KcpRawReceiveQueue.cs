﻿#if NEED_LINKEDLIST_SHIM
using LinkedListOfQueueItem = KcpSharp.NetstandardShim.LinkedList<KcpSharp.KcpBuffer>;
using LinkedListNodeOfQueueItem = KcpSharp.NetstandardShim.LinkedListNode<KcpSharp.KcpBuffer>;
#else
using LinkedListOfQueueItem = System.Collections.Generic.LinkedList<KianaBH.KcpSharp.Base.KcpBuffer>;
using LinkedListNodeOfQueueItem =
    System.Collections.Generic.LinkedListNode<KianaBH.KcpSharp.Base.KcpBuffer>;
#endif
using System.Diagnostics;
using System.Threading.Tasks.Sources;

namespace KianaBH.KcpSharp.Base;

internal sealed class KcpRawReceiveQueue : IValueTaskSource<KcpConversationReceiveResult>, IDisposable
{
    private readonly IKcpBufferPool _bufferPool;
    private readonly int _capacity;
    private readonly LinkedListOfQueueItem _queue;
    private readonly LinkedListOfQueueItem _recycled;

    private bool _activeWait;
    private Memory<byte> _buffer;
    private bool _bufferProvided;
    private CancellationTokenRegistration _cancellationRegistration;
    private CancellationToken _cancellationToken;
    private bool _disposed;
    private ManualResetValueTaskSourceCore<KcpConversationReceiveResult> _mrvtsc;
    private bool _signaled;

    private bool _transportClosed;

    public KcpRawReceiveQueue(IKcpBufferPool bufferPool, int capacity)
    {
        _bufferPool = bufferPool;
        _capacity = capacity;
        _queue = new LinkedListOfQueueItem();
        _recycled = new LinkedListOfQueueItem();
    }

    public void Dispose()
    {
        lock (_queue)
        {
            if (_disposed) return;
            if (_activeWait && !_signaled)
            {
                ClearPreviousOperation();
                _mrvtsc.SetResult(default);
            }

            var node = _queue.First;
            while (node is not null)
            {
                node.ValueRef.Release();
                node = node.Next;
            }

            _queue.Clear();
            _recycled.Clear();
            _disposed = true;
            _transportClosed = true;
        }
    }

    KcpConversationReceiveResult IValueTaskSource<KcpConversationReceiveResult>.GetResult(short token)
    {
        _cancellationRegistration.Dispose();
        try
        {
            return _mrvtsc.GetResult(token);
        }
        finally
        {
            _mrvtsc.Reset();
            lock (_queue)
            {
                _activeWait = false;
                _signaled = false;
                _cancellationRegistration = default;
            }
        }
    }

    ValueTaskSourceStatus IValueTaskSource<KcpConversationReceiveResult>.GetStatus(short token)
    {
        return _mrvtsc.GetStatus(token);
    }

    void IValueTaskSource<KcpConversationReceiveResult>.OnCompleted(Action<object?> continuation, object? state,
        short token, ValueTaskSourceOnCompletedFlags flags)
    {
        _mrvtsc.OnCompleted(continuation, state, token, flags);
    }

    public bool TryPeek(out KcpConversationReceiveResult result)
    {
        lock (_queue)
        {
            if (_disposed || _transportClosed)
            {
                result = default;
                return false;
            }

            if (_activeWait) ThrowHelper.ThrowConcurrentReceiveException();
            var first = _queue.First;
            if (first is null)
            {
                result = new KcpConversationReceiveResult(0);
                return false;
            }

            result = new KcpConversationReceiveResult(first.ValueRef.Length);
            return true;
        }
    }

    public ValueTask<KcpConversationReceiveResult> WaitToReceiveAsync(CancellationToken cancellationToken)
    {
        short token;
        lock (_queue)
        {
            if (_transportClosed || _disposed) return default;
            if (_activeWait)
                return new ValueTask<KcpConversationReceiveResult>(
                    Task.FromException<KcpConversationReceiveResult>(ThrowHelper.NewConcurrentReceiveException()));
            if (cancellationToken.IsCancellationRequested)
                return new ValueTask<KcpConversationReceiveResult>(
                    Task.FromCanceled<KcpConversationReceiveResult>(cancellationToken));

            var first = _queue.First;
            if (first is not null)
                return new ValueTask<KcpConversationReceiveResult>(
                    new KcpConversationReceiveResult(first.ValueRef.Length));

            _activeWait = true;
            Debug.Assert(!_signaled);
            _bufferProvided = false;
            _buffer = default;
            _cancellationToken = cancellationToken;

            token = _mrvtsc.Version;
        }

        _cancellationRegistration =
            cancellationToken.UnsafeRegister(state => ((KcpRawReceiveQueue?)state)!.SetCanceled(), this);

        return new ValueTask<KcpConversationReceiveResult>(this, token);
    }

    public bool TryReceive(Span<byte> buffer, out KcpConversationReceiveResult result)
    {
        lock (_queue)
        {
            if (_disposed || _transportClosed)
            {
                result = default;
                return false;
            }

            if (_activeWait) ThrowHelper.ThrowConcurrentReceiveException();
            var first = _queue.First;
            if (first is null)
            {
                result = new KcpConversationReceiveResult(0);
                return false;
            }

            ref var source = ref first.ValueRef;
            if (buffer.Length < source.Length) ThrowHelper.ThrowBufferTooSmall();

            source.DataRegion.Span.CopyTo(buffer);
            result = new KcpConversationReceiveResult(source.Length);

            _queue.RemoveFirst();
            source.Release();
            source = default;
            _recycled.AddLast(first);

            return true;
        }
    }

    public ValueTask<KcpConversationReceiveResult> ReceiveAsync(Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        short token;
        lock (_queue)
        {
            if (_transportClosed || _disposed) return default;
            if (_activeWait)
                return new ValueTask<KcpConversationReceiveResult>(
                    Task.FromException<KcpConversationReceiveResult>(ThrowHelper.NewConcurrentReceiveException()));
            if (cancellationToken.IsCancellationRequested)
                return new ValueTask<KcpConversationReceiveResult>(
                    Task.FromCanceled<KcpConversationReceiveResult>(cancellationToken));

            var first = _queue.First;
            if (first is not null)
            {
                ref var source = ref first.ValueRef;
                var length = source.Length;
                if (buffer.Length < source.Length)
                    return new ValueTask<KcpConversationReceiveResult>(
                        Task.FromException<KcpConversationReceiveResult>(
                            ThrowHelper.NewBufferTooSmallForBufferArgument()));
                _queue.Remove(first);

                source.DataRegion.CopyTo(buffer);
                source.Release();
                source = default;
                _recycled.AddLast(first);

                return new ValueTask<KcpConversationReceiveResult>(new KcpConversationReceiveResult(length));
            }

            _activeWait = true;
            Debug.Assert(!_signaled);
            _bufferProvided = true;
            _buffer = buffer;
            _cancellationToken = cancellationToken;

            token = _mrvtsc.Version;
        }

        _cancellationRegistration =
            cancellationToken.UnsafeRegister(state => ((KcpRawReceiveQueue?)state)!.SetCanceled(), this);

        return new ValueTask<KcpConversationReceiveResult>(this, token);
    }

    public bool CancelPendingOperation(Exception? innerException, CancellationToken cancellationToken)
    {
        lock (_queue)
        {
            if (_activeWait && !_signaled)
            {
                ClearPreviousOperation();
                _mrvtsc.SetException(
                    ThrowHelper.NewOperationCanceledExceptionForCancelPendingReceive(innerException,
                        cancellationToken));
                return true;
            }
        }

        return false;
    }

    private void SetCanceled()
    {
        lock (_queue)
        {
            if (_activeWait && !_signaled)
            {
                var cancellationToken = _cancellationToken;
                ClearPreviousOperation();
                _mrvtsc.SetException(new OperationCanceledException(cancellationToken));
            }
        }
    }

    private void ClearPreviousOperation()
    {
        _signaled = true;
        _bufferProvided = false;
        _buffer = default;
        _cancellationToken = default;
    }

    public void Enqueue(ReadOnlySpan<byte> buffer)
    {
        lock (_queue)
        {
            if (_transportClosed || _disposed) return;

            var queueSize = _queue.Count;
            if (queueSize > 0 || !_activeWait)
            {
                if (queueSize >= _capacity) return;

                var owner = _bufferPool.Rent(new KcpBufferPoolRentOptions(buffer.Length, false));
                _queue.AddLast(AllocateNode(KcpBuffer.CreateFromSpan(owner, buffer)));
                return;
            }

            if (!_bufferProvided)
            {
                var owner = _bufferPool.Rent(new KcpBufferPoolRentOptions(buffer.Length, false));
                _queue.AddLast(AllocateNode(KcpBuffer.CreateFromSpan(owner, buffer)));

                ClearPreviousOperation();
                _mrvtsc.SetResult(new KcpConversationReceiveResult(buffer.Length));
                return;
            }

            if (buffer.Length > _buffer.Length)
            {
                var owner = _bufferPool.Rent(new KcpBufferPoolRentOptions(buffer.Length, false));
                _queue.AddLast(AllocateNode(KcpBuffer.CreateFromSpan(owner, buffer)));

                ClearPreviousOperation();
                _mrvtsc.SetException(ThrowHelper.NewBufferTooSmallForBufferArgument());
                return;
            }

            buffer.CopyTo(_buffer.Span);
            ClearPreviousOperation();
            _mrvtsc.SetResult(new KcpConversationReceiveResult(buffer.Length));
        }
    }

    private LinkedListNodeOfQueueItem AllocateNode(KcpBuffer buffer)
    {
        var node = _recycled.First;
        if (node is null)
        {
            node = new LinkedListNodeOfQueueItem(buffer);
        }
        else
        {
            node.ValueRef = buffer;
            _recycled.Remove(node);
        }

        return node;
    }

    public void SetTransportClosed()
    {
        lock (_queue)
        {
            if (_transportClosed || _disposed) return;
            if (_activeWait && !_signaled)
            {
                ClearPreviousOperation();
                _mrvtsc.SetResult(default);
            }

            _recycled.Clear();
            _transportClosed = true;
        }
    }
}