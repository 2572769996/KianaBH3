﻿#if NEED_LINKEDLIST_SHIM
using LinkedListOfBufferItem = KcpSharp.NetstandardShim.LinkedList<KcpSharp.KcpSendReceiveBufferItem>;
using LinkedListNodeOfBufferItem = KcpSharp.NetstandardShim.LinkedListNode<KcpSharp.KcpSendReceiveBufferItem>;
#else
using LinkedListOfBufferItem =
    System.Collections.Generic.LinkedList<KianaBH.KcpSharp.Base.KcpSendReceiveBufferItem>;
using LinkedListNodeOfBufferItem =
    System.Collections.Generic.LinkedListNode<KianaBH.KcpSharp.Base.KcpSendReceiveBufferItem>;
#endif
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using KianaBH.Util;

namespace KianaBH.KcpSharp.Base;

/// <summary>
///     A reliable channel over an unreliable transport implemented in KCP protocol.
/// </summary>
public sealed partial class KcpConversation : IKcpConversation, IKcpExceptionProducer<KcpConversation>
{
    private readonly IKcpBufferPool _bufferPool;
    private readonly IKcpTransport _transport;
    private readonly IPEndPoint _remoteEndPoint;
    private readonly ulong? _id;

    private readonly int _mtu;
    private readonly int _mss;
    private readonly int _preBufferSize;
    private readonly int _postBufferSize;

    private uint _snd_una;
    private uint _snd_nxt;
    private uint _rcv_nxt;

    private uint _ssthresh;

    private int _rx_rttval;
    private int _rx_srtt;
    private uint _rx_rto;
    private readonly uint _rx_minrto;

    private readonly uint _snd_wnd;
    private readonly uint _rcv_wnd;
    private uint _rmt_wnd;
    private uint _cwnd;
    private KcpProbeType _probe;
    private SpinLock _cwndUpdateLock;

    private readonly uint _interval;
    private uint _ts_flush;

    private readonly bool _nodelay;
    private uint _ts_probe;
    private uint _probe_wait;

    private uint _incr;

    private readonly KcpSendReceiveQueueItemCache _queueItemCache;
    private readonly KcpSendQueue _sendQueue;
    private readonly KcpReceiveQueue _receiveQueue;

    private readonly LinkedListOfBufferItem _sndBuf = new();
    private readonly LinkedListOfBufferItem _rcvBuf = new();
    private KcpSendReceiveBufferItemCache _cache = KcpSendReceiveBufferItemCache.Create();

    private readonly KcpAcknowledgeList _ackList;

    private readonly int _fastresend;
    private readonly int _fastlimit;
    private readonly bool _nocwnd;

    private readonly bool _keepAliveEnabled;
    private readonly uint _keepAliveInterval;
    private readonly uint _keepAliveGracePeriod;
    private uint _lastReceiveTick;
    private uint _lastSendTick;

    private readonly KcpReceiveWindowNotificationOptions? _receiveWindowNotificationOptions;
    private uint _ts_rcv_notify;
    private uint _ts_rcv_notify_wait;

    private KcpConversationUpdateActivation? _updateActivation;
    private CancellationTokenSource? _updateLoopCts;
    private bool _disposed;

    private Func<Exception, KcpConversation, object?, bool>? _exceptionHandler;
    private object? _exceptionHandlerState;

    private const uint IKCP_RTO_MAX = 60000;
    private const int IKCP_THRESH_MIN = 2;
    private const uint IKCP_PROBE_INIT = 7000; // 7 secs to probe window size
    private const uint IKCP_PROBE_LIMIT = 120000; // up to 120 secs to probe window

    /// <summary>
    ///     Construct a reliable channel using KCP protocol.
    /// </summary>
    /// <param name="remoteEndpoint">The remote endpoint</param>
    /// <param name="transport">The underlying transport.</param>
    /// <param name="options">The options of the <see cref="KcpConversation" />.</param>
    public KcpConversation(IPEndPoint remoteEndpoint, IKcpTransport transport, KcpConversationOptions? options)
        : this(remoteEndpoint, transport, null, options)
    {
    }

    /// <summary>
    ///     Construct a reliable channel using KCP protocol.
    /// </summary>
    /// <param name="remoteEndpoint">The remote endpoint</param>
    /// <param name="transport">The underlying transport.</param>
    /// <param name="conversationId">The conversation ID.</param>
    /// <param name="options">The options of the <see cref="KcpConversation" />.</param>
    public KcpConversation(IPEndPoint remoteEndpoint, IKcpTransport transport, long conversationId,
        KcpConversationOptions? options)
        : this(remoteEndpoint, transport, (ulong)conversationId, options)
    {
    }

    private KcpConversation(IPEndPoint remoteEndpoint, IKcpTransport transport, ulong? conversationId,
        KcpConversationOptions? options)
    {
        _bufferPool = options?.BufferPool ?? DefaultArrayPoolBufferAllocator.Default;
        _transport = transport;
        _remoteEndPoint = remoteEndpoint;
        _id = conversationId;

        if (options is null)
            _mtu = KcpConversationOptions.MtuDefaultValue;
        else if (options.Mtu < 50)
            throw new ArgumentException("MTU must be at least 50.", nameof(options));
        else
            _mtu = options.Mtu;

        _preBufferSize = options?.PreBufferSize ?? 0;
        _postBufferSize = options?.PostBufferSize ?? 0;
        if (_preBufferSize < 0)
            throw new ArgumentException("PreBufferSize must be a non-negative integer.", nameof(options));
        if (_postBufferSize < 0)
            throw new ArgumentException("PostBufferSize must be a non-negative integer.", nameof(options));
        if ((uint)(_preBufferSize + _postBufferSize) >= (uint)(_mtu - KcpGlobalVars.HEADER_LENGTH_WITHOUT_CONVID))
            throw new ArgumentException(
                "The sum of PreBufferSize and PostBufferSize is too large. There is not enough space in the packet for the KCP header.",
                nameof(options));
        if (conversationId.HasValue && (uint)(_preBufferSize + _postBufferSize) >=
            (uint)(_mtu - KcpGlobalVars.HEADER_LENGTH_WITH_CONVID))
            throw new ArgumentException(
                "The sum of PreBufferSize and PostBufferSize is too large. There is not enough space in the packet for the KCP header.",
                nameof(options));

        _mss = conversationId.HasValue
            ? _mtu - KcpGlobalVars.HEADER_LENGTH_WITH_CONVID
            : _mtu - KcpGlobalVars.HEADER_LENGTH_WITHOUT_CONVID;
        _mss = _mss - _preBufferSize - _postBufferSize;

        _ssthresh = 2;

        _nodelay = options is not null && options.NoDelay;
        if (_nodelay)
        {
            _rx_minrto = 30;
        }
        else
        {
            _rx_rto = 200;
            _rx_minrto = 100;
        }

        _snd_wnd = options is null || options.SendWindow <= 0
            ? KcpConversationOptions.SendWindowDefaultValue
            : (uint)options.SendWindow;
        _rcv_wnd = options is null || options.ReceiveWindow <= 0
            ? KcpConversationOptions.ReceiveWindowDefaultValue
            : (uint)options.ReceiveWindow;
        _rmt_wnd = options is null || options.RemoteReceiveWindow <= 0
            ? KcpConversationOptions.RemoteReceiveWindowDefaultValue
            : (uint)options.RemoteReceiveWindow;
        _rcv_nxt = 0;

        _interval = options is null || options.UpdateInterval < 10
            ? KcpConversationOptions.UpdateIntervalDefaultValue
            : (uint)options.UpdateInterval;

        _fastresend = options is null ? 0 : options.FastResend;
        _fastlimit = 5;
        _nocwnd = options is not null && options.DisableCongestionControl;
        StreamMode = options is not null && options.StreamMode;

        _updateActivation = new KcpConversationUpdateActivation((int)_interval);
        _queueItemCache = new KcpSendReceiveQueueItemCache();
        _sendQueue = new KcpSendQueue(_bufferPool, _updateActivation, StreamMode,
            options is null || options.SendQueueSize <= 0
                ? KcpConversationOptions.SendQueueSizeDefaultValue
                : options.SendQueueSize, _mss, _queueItemCache);
        _receiveQueue = new KcpReceiveQueue(StreamMode,
            options is null || options.ReceiveQueueSize <= 0
                ? KcpConversationOptions.ReceiveQueueSizeDefaultValue
                : options.ReceiveQueueSize, _queueItemCache);
        _ackList = new KcpAcknowledgeList(_sendQueue, (int)_snd_wnd);

        _updateLoopCts = new CancellationTokenSource();

        _ts_flush = GetTimestamp();

        _lastSendTick = _ts_flush;
        _lastReceiveTick = _ts_flush;
        var keepAliveOptions = options?.KeepAliveOptions;
        if (keepAliveOptions is not null)
        {
            _keepAliveEnabled = true;
            _keepAliveInterval = (uint)keepAliveOptions.SendInterval;
            _keepAliveGracePeriod = (uint)keepAliveOptions.GracePeriod;
        }

        _receiveWindowNotificationOptions = options?.ReceiveWindowNotificationOptions;
        if (_receiveWindowNotificationOptions is not null)
        {
            _ts_rcv_notify_wait = 0;
            _ts_rcv_notify = _ts_flush + (uint)_receiveWindowNotificationOptions.InitialInterval;
        }

        RunUpdateOnActivation();
    }

    /// <summary>
    ///     Set the handler to invoke when exception is thrown during flushing packets to the transport. Return true in the
    ///     handler to ignore the error and continue running. Return false in the handler to abort the operation and mark the
    ///     transport as closed.
    /// </summary>
    /// <param name="handler">The exception handler.</param>
    /// <param name="state">The state object to pass into the exception handler.</param>
    public void SetExceptionHandler(Func<Exception, KcpConversation, object?, bool> handler, object? state)
    {
        if (handler is null) throw new ArgumentNullException(nameof(handler));

        _exceptionHandler = handler;
        _exceptionHandlerState = state;
    }

    /// <summary>
    ///     Get the ID of the current conversation.
    /// </summary>
    public long? ConversationId => (long?)_id;

    /// <summary>
    ///     Get whether the transport is marked as closed.
    /// </summary>
    public bool TransportClosed { get; private set; }

    /// <summary>
    ///     Get whether the conversation is in stream mode.
    /// </summary>
    public bool StreamMode { get; }

    /// <summary>
    ///     Get the available byte count and available segment count in the send queue.
    /// </summary>
    /// <param name="byteCount">The available byte count in the send queue.</param>
    /// <param name="segmentCount">The available segment count in the send queue.</param>
    /// <returns>True if the transport is not closed. Otherwise false.</returns>
    public bool TryGetSendQueueAvailableSpace(out int byteCount, out int segmentCount)
    {
        return _sendQueue.TryGetAvailableSpace(out byteCount, out segmentCount);
    }

    /// <summary>
    ///     Try to put message into the send queue.
    /// </summary>
    /// <param name="buffer">The content of the message.</param>
    /// <returns>
    ///     True if the message is put into the send queue. False if the message is too large to fit in the send queue, or
    ///     the transport is closed.
    /// </returns>
    /// <exception cref="ArgumentException">
    ///     The size of the message is larger than 256 * mtu, thus it can not be correctly
    ///     fragmented and sent. This exception is never thrown in stream mode.
    /// </exception>
    /// <exception cref="InvalidOperationException">The send or flush operation is initiated concurrently.</exception>
    public bool TrySend(ReadOnlySpan<byte> buffer)
    {
        return _sendQueue.TrySend(buffer, false, out _);
    }

    /// <summary>
    ///     Try to put message into the send queue.
    /// </summary>
    /// <param name="buffer">The content of the message.</param>
    /// <param name="allowPartialSend">
    ///     Whether partial sending is allowed in stream mode. This must not be true in non-stream
    ///     mode.
    /// </param>
    /// <param name="bytesWritten">
    ///     The number of bytes put into the send queue. This is always the same as the size of the
    ///     <paramref name="buffer" /> unless <paramref name="allowPartialSend" /> is set to true.
    /// </param>
    /// <returns>
    ///     True if the message is put into the send queue. False if the message is too large to fit in the send queue, or
    ///     the transport is closed.
    /// </returns>
    /// <exception cref="ArgumentException">
    ///     <paramref name="allowPartialSend" /> is set to true in non-stream mode. Or the size
    ///     of the message is larger than 256 * mtu, thus it can not be correctly fragmented and sent. This exception is never
    ///     thrown in stream mode.
    /// </exception>
    /// <exception cref="InvalidOperationException">The send or flush operation is initiated concurrently.</exception>
    public bool TrySend(ReadOnlySpan<byte> buffer, bool allowPartialSend, out int bytesWritten)
    {
        return _sendQueue.TrySend(buffer, allowPartialSend, out bytesWritten);
    }

    /// <summary>
    ///     Wait until the send queue contains at least <paramref name="minimumBytes" /> bytes of free space, and also
    ///     <paramref name="minimumSegments" /> available segments.
    /// </summary>
    /// <param name="minimumBytes">The number of bytes in the available space.</param>
    /// <param name="minimumSegments">The count of segments in the available space.</param>
    /// <param name="cancellationToken">The token to cancel this operation.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     <paramref name="minimumBytes" /> or <paramref name="minimumSegments" />
    ///     is larger than the total space of the send queue.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    ///     The <paramref name="cancellationToken" /> is fired before send operation
    ///     is completed. Or <see cref="CancelPendingSend(Exception?, CancellationToken)" /> is called before this operation is
    ///     completed.
    /// </exception>
    /// <returns>
    ///     A <see cref="ValueTask{Boolean}" /> that completes when there is enough space in the send queue. The result of
    ///     the task is false when the transport is closed.
    /// </returns>
    public ValueTask<bool> WaitForSendQueueAvailableSpaceAsync(int minimumBytes, int minimumSegments,
        CancellationToken cancellationToken = default)
    {
        return _sendQueue.WaitForAvailableSpaceAsync(minimumBytes, minimumSegments, cancellationToken);
    }

    /// <summary>
    ///     Put message into the send queue.
    /// </summary>
    /// <param name="buffer">The content of the message.</param>
    /// <param name="cancellationToken">The token to cancel this operation.</param>
    /// <exception cref="ArgumentException">
    ///     The size of the message is larger than 256 * mtu, thus it can not be correctly
    ///     fragmented and sent. This exception is never thrown in stream mode.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    ///     The <paramref name="cancellationToken" /> is fired before send operation
    ///     is completed. Or <see cref="CancelPendingSend(Exception?, CancellationToken)" /> is called before this operation is
    ///     completed.
    /// </exception>
    /// <exception cref="InvalidOperationException">The send or flush operation is initiated concurrently.</exception>
    /// <returns>
    ///     A <see cref="ValueTask{Boolean}" /> that completes when the entire message is put into the queue. The result
    ///     of the task is false when the transport is closed.
    /// </returns>
    public ValueTask<bool> SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return _sendQueue.SendAsync(buffer, cancellationToken);
    }

    internal ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        return _sendQueue.WriteAsync(buffer, cancellationToken);
    }

    /// <summary>
    ///     Cancel the current send operation or flush operation.
    /// </summary>
    /// <returns>True if the current operation is canceled. False if there is no active send operation.</returns>
    public bool CancelPendingSend()
    {
        return _sendQueue.CancelPendingOperation(null, default);
    }

    /// <summary>
    ///     Cancel the current send operation or flush operation.
    /// </summary>
    /// <param name="innerException">
    ///     The inner exception of the <see cref="OperationCanceledException" /> thrown by the
    ///     <see cref="SendAsync(ReadOnlyMemory{byte}, CancellationToken)" /> method or
    ///     <see cref="FlushAsync(CancellationToken)" /> method.
    /// </param>
    /// <param name="cancellationToken">
    ///     The <see cref="CancellationToken" /> in the <see cref="OperationCanceledException" />
    ///     thrown by the <see cref="SendAsync(ReadOnlyMemory{byte}, CancellationToken)" /> method or
    ///     <see cref="FlushAsync(CancellationToken)" /> method.
    /// </param>
    /// <returns>True if the current operation is canceled. False if there is no active send operation.</returns>
    public bool CancelPendingSend(Exception? innerException, CancellationToken cancellationToken)
    {
        return _sendQueue.CancelPendingOperation(innerException, cancellationToken);
    }

    /// <summary>
    ///     Gets the count of bytes not yet sent to the remote host or not acknowledged by the remote host.
    /// </summary>
    public long UnflushedBytes => _sendQueue.GetUnflushedBytes();

    /// <summary>
    ///     Wait until all messages are sent and acknowledged by the remote host, as well as all the acknowledgements are sent.
    /// </summary>
    /// <param name="cancellationToken">The token to cancel this operation.</param>
    /// <exception cref="OperationCanceledException">
    ///     The <paramref name="cancellationToken" /> is fired before send operation
    ///     is completed. Or <see cref="CancelPendingSend(Exception?, CancellationToken)" /> is called before this operation is
    ///     completed.
    /// </exception>
    /// <exception cref="InvalidOperationException">The send or flush operation is initiated concurrently.</exception>
    /// <exception cref="ObjectDisposedException">The <see cref="KcpConversation" /> instance is disposed.</exception>
    /// <returns>
    ///     A <see cref="ValueTask{Boolean}" /> that completes when the all messages are sent and acknowledged. The result
    ///     of the task is false when the transport is closed.
    /// </returns>
    public ValueTask<bool> FlushAsync(CancellationToken cancellationToken = default)
    {
        return _sendQueue.FlushAsync(cancellationToken);
    }

    internal ValueTask FlushForStreamAsync(CancellationToken cancellationToken)
    {
        return _sendQueue.FlushForStreamAsync(cancellationToken);
    }

#if !NET6_0_OR_GREATER
        private ValueTask FlushCoreAsync(CancellationToken cancellationToken)
            => new ValueTask(FlushCore2Async(cancellationToken));

        private async Task FlushCore2Async(CancellationToken cancellationToken)
#else
    private ValueTask FlushCoreAsync(CancellationToken cancellationToken)
    {
        s_currentObject = this;
        return FlushCore2Async(cancellationToken);
    }

    [AsyncMethodBuilder(typeof(KcpFlushAsyncMethodBuilder))]
    private async ValueTask FlushCore2Async(CancellationToken cancellationToken)
#endif
    {
        var preBufferSize = _preBufferSize;
        var postBufferSize = _postBufferSize;
        int packetHeaderSize = _id.HasValue
            ? KcpGlobalVars.HEADER_LENGTH_WITH_CONVID
            : KcpGlobalVars.HEADER_LENGTH_WITHOUT_CONVID;
        var sizeLimitBeforePostBuffer = _mtu - _postBufferSize;
        var anyPacketSent = false;

        var windowSize = (ushort)GetUnusedReceiveWindow();
        var unacknowledged = _rcv_nxt;

        using var bufferOwner =
            _bufferPool.Rent(new KcpBufferPoolRentOptions(_mtu + (_mtu - preBufferSize - postBufferSize), true));
        var buffer = bufferOwner.Memory;
        var size = preBufferSize;
        buffer.Span.Slice(0, size).Clear();

        // flush acknowledges
        {
            var index = 0;
            while (_ackList.TryGetAt(index++, out var serialNumber, out var timestamp))
            {
                if (size + packetHeaderSize > sizeLimitBeforePostBuffer)
                {
                    buffer.Span.Slice(size, postBufferSize).Clear();
                    await _transport
                        .SendPacketAsync(buffer.Slice(0, size + postBufferSize), _remoteEndPoint, cancellationToken)
                        .ConfigureAwait(false);
                    _lastSendTick = GetTimestamp();
                    size = preBufferSize;
                    buffer.Span.Slice(0, size).Clear();
                    anyPacketSent = true;
                }

                KcpPacketHeader header = new(KcpCommand.Ack, 0, windowSize, timestamp, serialNumber, unacknowledged);
                header.EncodeHeader(_id, 0, buffer.Span.Slice(size), out var bytesWritten);
                size += bytesWritten;
            }
        }

        var current = GetTimestamp();

        // calculate window size
        var cwnd = Math.Min(_snd_wnd, _rmt_wnd);
        if (!_nocwnd) cwnd = Math.Min(_cwnd, cwnd);

        // move data from snd_queue to snd_buf
        while (TimeDiff(_snd_nxt, _snd_una + cwnd) < 0)
        {
            if (!_sendQueue.TryDequeue(out var data, out var fragment)) break;

            lock (_sndBuf)
            {
                if (TransportClosed)
                {
                    data.Release();
                    return;
                }

                _sndBuf.AddLast(CreateSendBufferItem(in data, fragment, current, windowSize,
                    (uint)Interlocked.Increment(ref Unsafe.As<uint, int>(ref _snd_nxt)) - 1, unacknowledged, _rx_rto));
            }
        }

        // calculate resent
        var resent = _fastresend > 0 ? (uint)_fastresend : 0xffffffff;
        var rtomin = !_nodelay ? _rx_rto >> 3 : 0;

        // flush data segments
        var lost = false;
        var change = false;
        var segmentNode = _sndBuf.First;
        while (segmentNode is not null && !TransportClosed)
        {
            var nextSegmentNode = segmentNode.Next;

            var needsend = false;
            var stats = segmentNode.ValueRef.Stats;

            if (segmentNode.ValueRef.Stats.TransmitCount == 0)
            {
                needsend = true;
                segmentNode.ValueRef.Stats = new KcpSendSegmentStats(current + segmentNode.ValueRef.Stats.Rto + rtomin,
                    _rx_rto, stats.FastAck, stats.TransmitCount + 1);
            }
            else if (TimeDiff(current, stats.ResendTimestamp) >= 0)
            {
                needsend = true;
                var rto = stats.Rto;
                if (!_nodelay)
                {
                    rto += Math.Max(stats.Rto, _rx_rto);
                }
                else
                {
                    var step = rto; //_nodelay < 2 ? segment.rto : _rx_rto;
                    rto += step / 2;
                }

                segmentNode.ValueRef.Stats =
                    new KcpSendSegmentStats(current + rto, rto, stats.FastAck, stats.TransmitCount + 1);
                lost = true;
            }
            else if (stats.FastAck > resent)
            {
                if (stats.TransmitCount <= _fastlimit || _fastlimit == 0)
                {
                    needsend = true;
                    segmentNode.ValueRef.Stats =
                        new KcpSendSegmentStats(current, stats.Rto, 0, stats.TransmitCount + 1);
                    change = true;
                }
            }

            if (needsend)
            {
                var header = DeplicateHeader(ref segmentNode.ValueRef.Segment, current, windowSize, unacknowledged);

                var need = packetHeaderSize + segmentNode.ValueRef.Data.Length;
                if (size + need > sizeLimitBeforePostBuffer)
                {
                    buffer.Span.Slice(size, postBufferSize).Clear();
                    await _transport
                        .SendPacketAsync(buffer.Slice(0, size + postBufferSize), _remoteEndPoint, cancellationToken)
                        .ConfigureAwait(false);
                    _lastSendTick = GetTimestamp();
                    size = preBufferSize;
                    buffer.Span.Slice(0, size).Clear();
                    anyPacketSent = true;
                }

                lock (segmentNode)
                {
                    var data = segmentNode.ValueRef.Data;
                    if (!TransportClosed)
                    {
                        header.EncodeHeader(_id, data.Length, buffer.Span.Slice(size), out var bytesWritten);

                        size += bytesWritten;

                        if (data.Length > 0)
                        {
                            data.DataRegion.CopyTo(buffer.Slice(size));
                            size += data.Length;
                        }
                    }
                }
            }

            segmentNode = nextSegmentNode;
        }

        _ackList.Clear();

        // probe window size (if remote window size equals zero)
        if (_rmt_wnd == 0)
        {
            if (_probe_wait == 0)
            {
                _probe_wait = IKCP_PROBE_INIT;
                _ts_probe = current + _probe_wait;
            }
            else
            {
                if (TimeDiff(current, _ts_probe) >= 0)
                {
                    if (_probe_wait < IKCP_PROBE_INIT) _probe_wait = IKCP_PROBE_INIT;
                    _probe_wait += _probe_wait / 2;
                    if (_probe_wait > IKCP_PROBE_LIMIT) _probe_wait = IKCP_PROBE_LIMIT;
                    _ts_probe = current + _probe_wait;
                    _probe |= KcpProbeType.AskSend;
                }
            }
        }
        else
        {
            _ts_probe = 0;
            _probe_wait = 0;
        }

        // flush window probing command
        if ((_probe & KcpProbeType.AskSend) != 0)
        {
            if (size + packetHeaderSize > sizeLimitBeforePostBuffer)
            {
                buffer.Span.Slice(size, postBufferSize).Clear();
                await _transport
                    .SendPacketAsync(buffer.Slice(0, size + postBufferSize), _remoteEndPoint, cancellationToken)
                    .ConfigureAwait(false);
                _lastSendTick = GetTimestamp();
                size = preBufferSize;
                buffer.Span.Slice(0, size).Clear();
                anyPacketSent = true;
            }

            KcpPacketHeader header = new(KcpCommand.WindowProbe, 0, windowSize, 0, 0, unacknowledged);
            header.EncodeHeader(_id, 0, buffer.Span.Slice(size), out var bytesWritten);
            size += bytesWritten;
        }

        // flush window probing response
        if (!anyPacketSent && ShouldSendWindowSize(current))
        {
            if (size + packetHeaderSize > sizeLimitBeforePostBuffer)
            {
                buffer.Span.Slice(size, postBufferSize).Clear();
                await _transport
                    .SendPacketAsync(buffer.Slice(0, size + postBufferSize), _remoteEndPoint, cancellationToken)
                    .ConfigureAwait(false);
                _lastSendTick = GetTimestamp();
                size = preBufferSize;
                buffer.Span.Slice(0, size).Clear();
            }

            KcpPacketHeader header = new(KcpCommand.WindowSize, 0, windowSize, 0, 0, unacknowledged);
            header.EncodeHeader(_id, 0, buffer.Span.Slice(size), out var bytesWritten);
            size += bytesWritten;
        }

        _probe = KcpProbeType.None;

        // flush remaining segments
        if (size > preBufferSize)
        {
            buffer.Span.Slice(size, postBufferSize).Clear();
            try
            {
                await _transport
                    .SendPacketAsync(buffer.Slice(0, size + postBufferSize), _remoteEndPoint, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                new Logger("KcpServer").Error("transport send error", ex);
            }

            _lastSendTick = GetTimestamp();
        }

        // update window
        var lockTaken = false;
        try
        {
            _cwndUpdateLock.Enter(ref lockTaken);

            var updatedCwnd = _cwnd;
            var incr = _incr;

            // update sshthresh
            if (change)
            {
                var inflight = _snd_nxt - _snd_una;
                _ssthresh = Math.Max(inflight / 2, IKCP_THRESH_MIN);
                updatedCwnd = _ssthresh + resent;
                incr = updatedCwnd * (uint)_mss;
            }

            if (lost)
            {
                _ssthresh = Math.Max(cwnd / 2, IKCP_THRESH_MIN);
                updatedCwnd = 1;
                incr = (uint)_mss;
            }

            if (updatedCwnd < 1)
            {
                updatedCwnd = 1;
                incr = (uint)_mss;
            }

            _cwnd = updatedCwnd;
            _incr = incr;
        }
        finally
        {
            if (lockTaken) _cwndUpdateLock.Exit();
        }

        // send keep-alive
        if (_keepAliveEnabled)
            if (TimeDiff(GetTimestamp(), _lastSendTick) > _keepAliveInterval)
            {
                KcpPacketHeader header = new(KcpCommand.WindowSize, 0, windowSize, 0, 0, unacknowledged);
                header.EncodeHeader(_id, 0, buffer.Span, out var bytesWritten);
                await _transport.SendPacketAsync(buffer.Slice(0, bytesWritten), _remoteEndPoint, cancellationToken)
                    .ConfigureAwait(false);
                _lastSendTick = GetTimestamp();
            }
    }

    private bool ShouldSendWindowSize(uint current)
    {
        if ((_probe & KcpProbeType.AskTell) != 0) return true;

        var options = _receiveWindowNotificationOptions;
        if (options is null) return false;

        if (TimeDiff(current, _ts_rcv_notify) < 0) return false;

        var inital = (uint)options.InitialInterval;
        var maximum = (uint)options.MaximumInterval;
        if (_ts_rcv_notify_wait < inital)
            _ts_rcv_notify_wait = inital;
        else if (_ts_rcv_notify_wait >= maximum)
            _ts_rcv_notify_wait = maximum;
        else
            _ts_rcv_notify_wait = Math.Min(maximum, _ts_rcv_notify_wait + _ts_rcv_notify_wait / 2);
        _ts_rcv_notify = current + _ts_rcv_notify_wait;

        return true;
    }

    private LinkedListNodeOfBufferItem CreateSendBufferItem(in KcpBuffer data, byte fragment, uint current,
        ushort windowSize, uint serialNumber, uint unacknowledged, uint rto)
    {
        KcpSendReceiveBufferItem newseg = new()
        {
            Data = data,
            Segment = new KcpPacketHeader(KcpCommand.Push, fragment, windowSize, current, serialNumber, unacknowledged),
            Stats = new KcpSendSegmentStats(current, rto, 0, 0)
        };
        return _cache.Allocate(in newseg);
    }

    private static KcpPacketHeader DeplicateHeader(ref KcpPacketHeader header, uint timestamp, ushort windowSize,
        uint unacknowledged)
    {
        return new KcpPacketHeader(header.Command, header.Fragment, windowSize, timestamp, header.SerialNumber,
            unacknowledged);
    }

    private uint GetUnusedReceiveWindow()
    {
        var count = (uint)_receiveQueue.GetQueueSize();
        if (count < _rcv_wnd) return _rcv_wnd - count;
        return 0;
    }

    private async void RunUpdateOnActivation()
    {
        var cancellationToken = _updateLoopCts?.Token ?? new CancellationToken(true);
        var activation = _updateActivation;
        if (activation is null) return;

        while (!cancellationToken.IsCancellationRequested)
        {
            var update = false;
            using (var notification = await activation.WaitAsync(CancellationToken.None).ConfigureAwait(false))
            {
                if (TransportClosed) break;

                var packet = notification.Packet;
                if (!packet.IsEmpty)
                    try
                    {
                        update = SetInput(packet.Span);
                    }
                    catch (Exception ex)
                    {
                        new Logger("KcpServer").Error("Update error", ex);
                    }

                if (TransportClosed) break;

                update |= notification.TimerNotification;
            }

            try
            {
                if (update) await UpdateCoreAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (!HandleFlushException(ex)) break;
            }

            if (_keepAliveEnabled && TimeDiff(GetTimestamp(), _lastReceiveTick) > _keepAliveGracePeriod)
                SetTransportClosed();
        }
    }

    private ValueTask UpdateCoreAsync(CancellationToken cancellationToken)
    {
        var current = GetTimestamp();
        long slap = TimeDiff(current, _ts_flush);
        if (slap > 10000 || slap < -10000)
        {
            _ts_flush = current;
            slap = 0;
        }

        if (slap >= 0 || _nodelay)
        {
            _ts_flush += _interval;
            if (TimeDiff(current, _ts_flush) >= 0) _ts_flush = current + _interval;
            return FlushCoreAsync(cancellationToken);
        }

        return default;
    }

    private bool HandleFlushException(Exception ex)
    {
        var handler = _exceptionHandler;
        var state = _exceptionHandlerState;
        var result = false;
        if (handler is not null)
            try
            {
                result = handler.Invoke(ex, this, state);
            }
            catch
            {
                result = false;
            }

        if (!result) SetTransportClosed();
        return result;
    }

    /// <inheritdoc />
    public ValueTask InputPakcetAsync(UdpReceiveResult packet, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return new ValueTask(Task.FromCanceled(cancellationToken));
        int packetHeaderSize = _id.HasValue
            ? KcpGlobalVars.HEADER_LENGTH_WITH_CONVID
            : KcpGlobalVars.HEADER_LENGTH_WITHOUT_CONVID;
        if (packet.Buffer.Length < packetHeaderSize) return default;

        ReadOnlySpan<byte> packetSpan = packet.Buffer.AsSpan();
        if (_id.HasValue)
        {
            var conversationId = BinaryPrimitives.ReadUInt64BigEndian(packet.Buffer.AsSpan());
            if (conversationId != _id.GetValueOrDefault()) return default;
            packetSpan = packetSpan.Slice(8);
        }

        var length = BinaryPrimitives.ReadUInt32LittleEndian(packetSpan.Slice(16));
        if (length > (uint)(packetSpan.Length - 20)) // implicitly checked for (int)length < 0
            return default;

        var activation = _updateActivation;
        if (activation is null) return default;

        return activation.InputPacketAsync(packet.Buffer.AsMemory(), cancellationToken);
    }

    private bool SetInput(ReadOnlySpan<byte> packet)
    {
        var current = GetTimestamp();
        var packetHeaderSize = _id.HasValue ? 28 : 20;

        var prev_una = _snd_una;
        uint maxack = 0, latest_ts = 0;
        var flag = false;
        var mutated = false;

        while (true)
        {
            if (packet.Length < packetHeaderSize) break;

            if (_id.HasValue)
            {
                if (BinaryPrimitives.ReadUInt64BigEndian(packet) != _id.GetValueOrDefault()) return mutated;
                packet = packet.Slice(8);
            }

            var header = KcpPacketHeader.Parse(packet);
            var length = BinaryPrimitives.ReadInt32LittleEndian(packet.Slice(16));

            packet = packet.Slice(20);
            if ((uint)length > (uint)packet.Length) return mutated;

            if (header.Command != KcpCommand.Push &&
                header.Command != KcpCommand.Ack &&
                header.Command != KcpCommand.WindowProbe &&
                header.Command != KcpCommand.WindowSize)
                return mutated;

            _lastReceiveTick = current;
            _rmt_wnd = header.WindowSize;
            mutated = HandleUnacknowledged(header.Unacknowledged) | mutated;
            mutated = UpdateSendUnacknowledged() | mutated;

            if (header.Command == KcpCommand.Ack)
            {
                var rtt = TimeDiff(current, header.Timestamp);
                if (rtt >= 0) UpdateRto(rtt);
                mutated = HandleAck(header.SerialNumber) | mutated;
                mutated = UpdateSendUnacknowledged() | mutated;

                if (!flag)
                {
                    flag = true;
                    maxack = header.SerialNumber;
                    latest_ts = header.Timestamp;
                }
                else
                {
                    if (TimeDiff(_snd_nxt, maxack) > 0)
                    {
#if !IKCP_FASTACK_CONSERVE
                        maxack = header.SerialNumber;
                        latest_ts = header.Timestamp;
#else
                            if (TimeDiff(header.Timestamp, latest_ts) > 0) {
						        maxack = header.SerialNumber;
						        latest_ts = header.Timestamp;
					        }
#endif
                    }
                }
            }
            else if (header.Command == KcpCommand.Push)
            {
                if (TimeDiff(header.SerialNumber, _rcv_nxt + _rcv_wnd) < 0)
                {
                    AckPush(header.SerialNumber, header.Timestamp);
                    if (TimeDiff(header.SerialNumber, _rcv_nxt) >= 0)
                        mutated = HandleData(header, packet.Slice(0, length)) | mutated;

                    if (_receiveWindowNotificationOptions is not null)
                        if (_ts_rcv_notify_wait != 0)
                        {
                            _ts_rcv_notify_wait = 0;
                            _ts_rcv_notify = current + (uint)_receiveWindowNotificationOptions.InitialInterval;
                        }
                }
            }
            else if (header.Command == KcpCommand.WindowProbe)
            {
                _probe |= KcpProbeType.AskTell;
            }
            else if (header.Command == KcpCommand.WindowSize)
            {
                // do nothing
            }
            else
            {
                return mutated;
            }

            packet = packet.Slice(length);
        }

        if (flag) HandleFastAck(maxack, latest_ts);

        if (TimeDiff(_snd_una, prev_una) > 0)
        {
            var lockTaken = false;
            try
            {
                _cwndUpdateLock.Enter(ref lockTaken);

                var cwnd = _cwnd;
                var incr = _incr;

                if (cwnd < _rmt_wnd)
                {
                    var mss = (uint)_mss;
                    if (cwnd < _ssthresh)
                    {
                        cwnd++;
                        incr += mss;
                    }
                    else
                    {
                        if (incr < mss) incr = mss;
                        incr += mss * mss / incr + mss / 16;
                        cwnd = (incr + mss - 1) / (mss > 0 ? mss : 1);
                    }

                    if (cwnd > _rmt_wnd)
                    {
                        cwnd = _rmt_wnd;
                        incr = _rmt_wnd * mss;
                    }
                }

                _cwnd = cwnd;
                _incr = incr;
            }
            finally
            {
                if (lockTaken) _cwndUpdateLock.Exit();
            }
        }

        return mutated;
    }

    private bool HandleUnacknowledged(uint una)
    {
        var mutated = false;
        lock (_sndBuf)
        {
            var node = _sndBuf.First;
            while (node is not null)
            {
                var next = node.Next;

                if (TimeDiff(una, node.ValueRef.Segment.SerialNumber) > 0)
                {
                    _sndBuf.Remove(node);
                    ref var dataRef = ref node.ValueRef.Data;
                    _sendQueue.SubtractUnflushedBytes(dataRef.Length);
                    dataRef.Release();
                    dataRef = default;
                    _cache.Return(node);
                    mutated = true;
                }
                else
                {
                    break;
                }

                node = next;
            }
        }

        return mutated;
    }

    private bool UpdateSendUnacknowledged()
    {
        lock (_sndBuf)
        {
            var first = _sndBuf.First;
            var snd_una = first is null ? _snd_nxt : first.ValueRef.Segment.SerialNumber;
            var old_snd_una = (uint)Interlocked.Exchange(ref Unsafe.As<uint, int>(ref _snd_una), (int)snd_una);
            return snd_una != old_snd_una;
        }
    }

    private void UpdateRto(int rtt)
    {
        if (_rx_srtt == 0)
        {
            _rx_srtt = rtt;
            _rx_rttval = rtt / 2;
        }
        else
        {
            var delta = rtt - _rx_srtt;
            if (delta < 0) delta = -delta;
            _rx_rttval = (3 * _rx_rttval + delta) / 4;
            _rx_srtt = (7 * _rx_srtt + rtt) / 8;
            if (_rx_srtt < 1) _rx_srtt = 1;
        }

        var rto = _rx_srtt + Math.Max((int)_interval, 4 * _rx_rttval);
#if NEED_MATH_SHIM
            _rx_rto = Math.Min(Math.Max((uint)rto, _rx_minrto), IKCP_RTO_MAX);
#else
        _rx_rto = Math.Clamp((uint)rto, _rx_minrto, IKCP_RTO_MAX);
#endif
    }

    private bool HandleAck(uint serialNumber)
    {
        if (TimeDiff(serialNumber, _snd_una) < 0 || TimeDiff(serialNumber, _snd_nxt) >= 0) return false;

        lock (_sndBuf)
        {
            var node = _sndBuf.First;
            while (node is not null)
            {
                var next = node.Next;

                if (serialNumber == node.ValueRef.Segment.SerialNumber)
                {
                    _sndBuf.Remove(node);
                    ref var dataRef = ref node.ValueRef.Data;
                    _sendQueue.SubtractUnflushedBytes(dataRef.Length);
                    dataRef.Release();
                    dataRef = default;
                    _cache.Return(node);
                    return true;
                }

                if (TimeDiff(serialNumber, node.ValueRef.Segment.SerialNumber) < 0) return false;

                node = next;
            }
        }

        return false;
    }

    private bool HandleData(KcpPacketHeader header, ReadOnlySpan<byte> data)
    {
        var serialNumber = header.SerialNumber;
        if (TimeDiff(serialNumber, _rcv_nxt + _rcv_wnd) >= 0 || TimeDiff(serialNumber, _rcv_nxt) < 0) return false;

        var mutated = false;
        var repeat = false;
        LinkedListNodeOfBufferItem? node;
        lock (_rcvBuf)
        {
            if (TransportClosed) return false;
            node = _rcvBuf.Last;
            while (node is not null)
            {
                var nodeSerialNumber = node.ValueRef.Segment.SerialNumber;
                if (serialNumber == nodeSerialNumber)
                {
                    repeat = true;
                    break;
                }

                if (TimeDiff(serialNumber, nodeSerialNumber) > 0) break;

                node = node.Previous;
            }

            if (!repeat)
            {
                var buffer = _bufferPool.Rent(new KcpBufferPoolRentOptions(data.Length, false));
                KcpSendReceiveBufferItem item = new()
                {
                    Data = KcpBuffer.CreateFromSpan(buffer, data),
                    Segment = header
                };
                if (node is null)
                    _rcvBuf.AddFirst(_cache.Allocate(in item));
                else
                    _rcvBuf.AddAfter(node, _cache.Allocate(in item));
                mutated = true;
            }

            // move available data from rcv_buf -> rcv_queue
            node = _rcvBuf.First;
            while (node is not null)
            {
                var next = node.Next;

                if (node.ValueRef.Segment.SerialNumber == _rcv_nxt && _receiveQueue.GetQueueSize() < _rcv_wnd)
                {
                    _rcvBuf.Remove(node);
                    _receiveQueue.Enqueue(node.ValueRef.Data, node.ValueRef.Segment.Fragment);
                    node.ValueRef.Data = default;
                    _cache.Return(node);
                    _rcv_nxt++;
                    mutated = true;
                }
                else
                {
                    break;
                }

                node = next;
            }
        }

        return mutated;
    }

    private void AckPush(uint serialNumber, uint timestamp)
    {
        _ackList.Add(serialNumber, timestamp);
    }

    private void HandleFastAck(uint serialNumber, uint timestamp)
    {
        if (TimeDiff(serialNumber, _snd_una) < 0 || TimeDiff(serialNumber, _snd_nxt) >= 0) return;

        lock (_sndBuf)
        {
            var node = _sndBuf.First;
            while (node is not null)
            {
                var next = node.Next;
                if (TimeDiff(serialNumber, node.ValueRef.Segment.SerialNumber) < 0) break;

                if (serialNumber != node.ValueRef.Segment.SerialNumber)
                {
                    ref var stats = ref node.ValueRef.Stats;
#if !IKCP_FASTACK_CONSERVE
                    stats = new KcpSendSegmentStats(stats.ResendTimestamp, stats.Rto, stats.FastAck + 1,
                        stats.TransmitCount);
#else
                        if (TimeDiff(timestamp, node.ValueRef.Segment.Timestamp) >= 0)
                        {
                            stats =
 new KcpSendSegmentStats(stats.ResendTimestamp, stats.Rto, stats.FastAck + 1, stats.TransmitCount);
                        }
#endif
                }

                node = next;
            }
        }
    }

    private static uint GetTimestamp()
    {
        return (uint)Environment.TickCount;
    }

    private static int TimeDiff(uint later, uint earlier)
    {
        return (int)(later - earlier);
    }

    /// <summary>
    ///     Get the size of the next available message in the receive queue.
    /// </summary>
    /// <param name="result">The transport state and the size of the next available message.</param>
    /// <exception cref="InvalidOperationException">The receive or peek operation is initiated concurrently.</exception>
    /// <returns>
    ///     True if the receive queue contains at least one message. False if the receive queue is empty or the transport
    ///     is closed.
    /// </returns>
    public bool TryPeek(out KcpConversationReceiveResult result)
    {
        return _receiveQueue.TryPeek(out result);
    }

    /// <summary>
    ///     Remove the next available message in the receive queue and copy its content into <paramref name="buffer" />. When
    ///     in stream mode, move as many bytes as possible into <paramref name="buffer" />.
    /// </summary>
    /// <param name="buffer">The buffer to receive message.</param>
    /// <param name="result">The transport state and the count of bytes moved into <paramref name="buffer" />.</param>
    /// <exception cref="ArgumentException">
    ///     The size of the next available message is larger than the size of
    ///     <paramref name="buffer" />. This exception is never thrown in stream mode.
    /// </exception>
    /// <exception cref="InvalidOperationException">The receive or peek operation is initiated concurrently.</exception>
    /// <returns>
    ///     True if the next available message is moved into <paramref name="buffer" />. False if the receive queue is
    ///     empty or the transport is closed.
    /// </returns>
    public bool TryReceive(Span<byte> buffer, out KcpConversationReceiveResult result)
    {
        return _receiveQueue.TryReceive(buffer, out result);
    }

    /// <summary>
    ///     Wait until the receive queue contains at least one full message, or at least one byte in stream mode.
    /// </summary>
    /// <param name="cancellationToken">The token to cancel this operation.</param>
    /// <exception cref="OperationCanceledException">
    ///     The <paramref name="cancellationToken" /> is fired before receive
    ///     operation is completed.
    /// </exception>
    /// <exception cref="InvalidOperationException">The receive or peek operation is initiated concurrently.</exception>
    /// <returns>
    ///     A <see cref="ValueTask{KcpConversationReceiveResult}" /> that completes when the receive queue contains at
    ///     least one full message, or at least one byte in stream mode. Its result contains the transport state and the size
    ///     of the available message.
    /// </returns>
    public ValueTask<KcpConversationReceiveResult> WaitToReceiveAsync(CancellationToken cancellationToken = default)
    {
        return _receiveQueue.WaitToReceiveAsync(cancellationToken);
    }

    /// <summary>
    ///     Wait until the receive queue contains at leat <paramref name="minimumBytes" /> bytes.
    /// </summary>
    /// <param name="minimumBytes">The minimum bytes in the receive queue.</param>
    /// <param name="cancellationToken">The token to cancel this operation.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minimumBytes" /> is a negative integer.</exception>
    /// <exception cref="OperationCanceledException">
    ///     The <paramref name="cancellationToken" /> is fired before receive
    ///     operation is completed.
    /// </exception>
    /// <exception cref="InvalidOperationException">The receive or peek operation is initiated concurrently.</exception>
    /// <returns>
    ///     A <see cref="ValueTask{Boolean}" /> that completes when the receive queue contains at least
    ///     <paramref name="minimumBytes" /> bytes. The result of the task is false when the transport is closed.
    /// </returns>
    public ValueTask<bool> WaitForReceiveQueueAvailableDataAsync(int minimumBytes,
        CancellationToken cancellationToken = default)
    {
        return _receiveQueue.WaitForAvailableDataAsync(minimumBytes, 0, cancellationToken);
    }

    /// <summary>
    ///     Wait until the receive queue contains at leat <paramref name="minimumBytes" /> bytes, and also
    ///     <paramref name="minimumSegments" /> segments.
    /// </summary>
    /// <param name="minimumBytes">The minimum bytes in the receive queue.</param>
    /// <param name="minimumSegments">The minimum segments in the receive queue</param>
    /// <param name="cancellationToken">The token to cancel this operation.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Any od <paramref name="minimumBytes" /> and
    ///     <paramref name="minimumSegments" /> is a negative integer.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    ///     The <paramref name="cancellationToken" /> is fired before receive
    ///     operation is completed.
    /// </exception>
    /// <exception cref="InvalidOperationException">The receive or peek operation is initiated concurrently.</exception>
    /// <returns>
    ///     A <see cref="ValueTask{Boolean}" /> that completes when the receive queue contains at least
    ///     <paramref name="minimumBytes" /> bytes. The result of the task is false when the transport is closed.
    /// </returns>
    public ValueTask<bool> WaitForReceiveQueueAvailableDataAsync(int minimumBytes, int minimumSegments,
        CancellationToken cancellationToken = default)
    {
        return _receiveQueue.WaitForAvailableDataAsync(minimumBytes, minimumSegments, cancellationToken);
    }

    /// <summary>
    ///     Wait for the next full message to arrive if the receive queue is empty. Remove the next available message in the
    ///     receive queue and copy its content into <paramref name="buffer" />. When in stream mode, move as many bytes as
    ///     possible into <paramref name="buffer" />.
    /// </summary>
    /// <param name="buffer">The buffer to receive message.</param>
    /// <param name="cancellationToken">The token to cancel this operation.</param>
    /// <exception cref="ArgumentException">
    ///     The size of the next available message is larger than the size of
    ///     <paramref name="buffer" />. This exception is never thrown in stream mode.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    ///     The <paramref name="cancellationToken" /> is fired before send operation
    ///     is completed.
    /// </exception>
    /// <exception cref="InvalidOperationException">The receive or peek operation is initiated concurrently.</exception>
    /// <returns>
    ///     A <see cref="ValueTask{KcpConversationReceiveResult}" /> that completes when a full message is moved into
    ///     <paramref name="buffer" /> or the transport is closed. Its result contains the transport state and the count of
    ///     bytes written into <paramref name="buffer" />.
    /// </returns>
    public ValueTask<KcpConversationReceiveResult> ReceiveAsync(Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        return _receiveQueue.ReceiveAsync(buffer, cancellationToken);
    }

    internal ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        return _receiveQueue.ReadAsync(buffer, cancellationToken);
    }

    /// <summary>
    ///     Cancel the current receive operation.
    /// </summary>
    /// <returns>True if the current operation is canceled. False if there is no active send operation.</returns>
    public bool CancelPendingReceive()
    {
        return _receiveQueue.CancelPendingOperation(null, default);
    }

    /// <summary>
    ///     Cancel the current receive operation.
    /// </summary>
    /// <param name="innerException">
    ///     The inner exception of the <see cref="OperationCanceledException" /> thrown by the
    ///     <see cref="ReceiveAsync(Memory{byte}, CancellationToken)" /> method or
    ///     <see cref="WaitToReceiveAsync(CancellationToken)" /> method.
    /// </param>
    /// <param name="cancellationToken">
    ///     The <see cref="CancellationToken" /> in the <see cref="OperationCanceledException" />
    ///     thrown by the <see cref="ReceiveAsync(Memory{byte}, CancellationToken)" /> method or
    ///     <see cref="WaitToReceiveAsync(CancellationToken)" /> method.
    /// </param>
    /// <returns>True if the current operation is canceled. False if there is no active send operation.</returns>
    public bool CancelPendingReceive(Exception? innerException, CancellationToken cancellationToken)
    {
        return _receiveQueue.CancelPendingOperation(innerException, cancellationToken);
    }

    /// <inheritdoc />
    public void SetTransportClosed()
    {
        TransportClosed = true;
        Interlocked.Exchange(ref _updateActivation, null)?.Dispose();
        var updateLoopCts = Interlocked.Exchange(ref _updateLoopCts, null);
        if (updateLoopCts is not null)
        {
            updateLoopCts.Cancel();
            updateLoopCts.Dispose();
        }

        _sendQueue.SetTransportClosed();
        _receiveQueue.SetTransportClosed();
        lock (_sndBuf)
        {
            var node = _sndBuf.First;
            var next = node?.Next;
            while (node is not null)
            {
                lock (node)
                {
                    node.ValueRef.Data.Release();
                    node.ValueRef = default;
                }

                _sndBuf.Remove(node);
                node = next;
                next = node?.Next;
            }
        }

        lock (_rcvBuf)
        {
            var node = _rcvBuf.First;
            while (node is not null)
            {
                node.ValueRef.Data.Release();
                node = node.Next;
            }

            _rcvBuf.Clear();
        }

        _queueItemCache.Clear();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        var disposed = _disposed;
        _disposed = true;
        SetTransportClosed();
        if (!disposed)
        {
            _sendQueue.Dispose();
            _receiveQueue.Dispose();
        }
    }
}