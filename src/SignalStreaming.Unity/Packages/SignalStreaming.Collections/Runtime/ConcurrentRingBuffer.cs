using System;
using System.Buffers;
using System.Threading;

namespace SignalStreaming.Collections
{
    public sealed class ConcurrentRingBuffer<T> where T : struct
    {
        readonly int[] _sequence;
        readonly T[] _buffer;
        readonly int _bufferMask;
        int _enqueuePosition;
        int _dequeuePosition;

        public T this[int index]
        {
            get { return _buffer[(_dequeuePosition + index) & _bufferMask]; }
        }

        public ReadOnlySpan<T> Buffer => new ReadOnlySpan<T>(_buffer);

        public int Capacity => _buffer.Length;
        public int FillCount => _enqueuePosition - _dequeuePosition;
        public int FreeCount => Capacity - FillCount;
        public int EnqueuePosition => _enqueuePosition;
        public int DequeuePosition => _dequeuePosition;
        public int Tail => _enqueuePosition;
        public int Head => _dequeuePosition;

        public ConcurrentRingBuffer(int capacity)
        {
			// Buffer size must be a power of two.
            var bufferSize = (capacity < 2) ? 2 : (int)Math.Pow(2, (int)Math.Ceiling(Math.Log(capacity, 2)));

            _bufferMask = bufferSize - 1;

            _buffer = new T[bufferSize];
            _sequence = new int[bufferSize];

            for (var i = 0; i < bufferSize; i++)
            {
                _sequence[i] = i;
            }

            _enqueuePosition = 0;
            _dequeuePosition = 0;
        }

        public void Reset()
        {
            Clear();
            _enqueuePosition = 0;
            _dequeuePosition = 0;
        }

        public bool TryEnqueue(T value)
        {
            var spinWait = new SpinWait();

            do
            {
                var buffer = _buffer;
                var position = _enqueuePosition;
                var index = position & _bufferMask;
                var diff = _sequence[index] - position;

                if (diff == 0 && Interlocked.CompareExchange(ref _enqueuePosition, position + 1, position) == position)
                {
                    buffer[index] = value;
                    Volatile.Write(ref _sequence[index], position + 1);
                    return true;
                }

                if (diff < 0)
                {
                    return false;
                }

                spinWait.SpinOnce();
            } while (true);
        }

        public bool TryDequeue(out T value)
        {
            var spinWait = new SpinWait();

            do
            {
                var buffer = _buffer;
                var bufferMask = _bufferMask;
                var position = _dequeuePosition;
                var index = position & bufferMask;
                var diff = _sequence[index] - (position + 1);

                if (diff == 0 && Interlocked.CompareExchange(ref _dequeuePosition, position + 1, position) == position)
                {
                    value = buffer[index];
                    buffer[index] = default;
                    Volatile.Write(ref _sequence[index], position + bufferMask + 1);
                    return true;
                }

                if (diff < 0)
                {
                    value = default;
                    return false;
                }

                spinWait.SpinOnce();
            } while (true);
        }

        public bool TryBulkEnqueue(ReadOnlySpan<T> source)
        {
            var spinWait = new SpinWait();

            do
            {
                var length = source.Length;

                if (length > Capacity - FillCount)
                {
                    return false;
                }

                var buffer = _buffer;
                var bufferMask = _bufferMask;
                var position = _enqueuePosition;
                var startIndex = position & bufferMask;
                var diff = _sequence[startIndex] - position;

                if (diff == 0 && Interlocked.CompareExchange(ref _enqueuePosition, position + length, position) == position)
                {
                    for (int i = 0; i < length; i++)
                    {
                        var index = (startIndex + i) & bufferMask;
                        buffer[index] = source[i];
                        Volatile.Write(ref _sequence[index], position + 1 + i);
                    }

                    return true;
                }

                if (diff < 0)
                {
                    return false;
                }

                spinWait.SpinOnce();
            } while (true);
        }

        public bool TryBulkDequeue(Span<T> destination)
        {
            var spinWait = new SpinWait();

            do
            {
                var length = destination.Length;

                var buffer = _buffer;
                var bufferMask = _bufferMask;
                var position = _dequeuePosition;
                var startIndex = position & bufferMask;
                var diff = _sequence[startIndex] - (position + 1);

                if (diff == 0 && Interlocked.CompareExchange(ref _dequeuePosition, position + length, position) == position)
                {
                    for (int i = 0; i < length; i++)
                    {
                        var index = (startIndex + i) & bufferMask;
                        destination[i] = buffer[index];
                        buffer[index] = default;
                        Volatile.Write(ref _sequence[index], position + bufferMask + 1 + i);
                    }

                    return true;
                }

                if (diff < 0)
                {
                    for (int i = 0; i < length; i++)
                    {
                        destination[i] = default;
                    }

                    return false;
                }

                spinWait.SpinOnce();
            } while (true);
        }

        public void Clear()
        {
            Clear(FillCount);
        }

        public void Clear(int length)
        {
            var spinWait = new SpinWait();

            do
            {
                var buffer = _buffer;
                var bufferMask = _bufferMask;
                var position = _dequeuePosition;
                var startIndex = position & bufferMask;

                var fillCount = _enqueuePosition - position;
                length = (length <= fillCount) ? length : fillCount;

                if (Interlocked.CompareExchange(ref _dequeuePosition, position + length, position) == position)
                {
                    for (int i = 0; i < length; i++)
                    {
                        var index = (startIndex + i) & bufferMask;
                        buffer[index] = default;
                        Volatile.Write(ref _sequence[index], position + bufferMask + 1 + i);
                    }

                    return;
                }

                spinWait.SpinOnce();
            } while (true);
        }

        public ReadOnlySequence<T> Slice(int start)
        {
            return Slice(start, FillCount);
        }

        public ReadOnlySequence<T> Slice(int start, int length)
        {
            if ((uint)start > (uint)FillCount || (uint)length > (uint)FillCount)
            {
                throw new ArgumentOutOfRangeException();
            }

            if (FillCount == 0 || length == 0)
            {
                return ReadOnlySequence<T>.Empty;
            }

            var startIndex = (_dequeuePosition + start) & _bufferMask;
            var endIndex = (_dequeuePosition + start + length - 1) & _bufferMask;

            if (startIndex <= endIndex)
            {
                return new ReadOnlySequence<T>(_buffer, startIndex, length);
            }
            else
            {
                var firstSegmentLength = _buffer.Length - startIndex;
                var secondSegmentLength = length - firstSegmentLength;

                var firstSegmentMemory = new ReadOnlyMemory<T>(_buffer, startIndex, firstSegmentLength);
                var secondSegmentMemory = new ReadOnlyMemory<T>(_buffer, 0, secondSegmentLength);

                var firstSegment = new BufferSequenceSegment(firstSegmentMemory, secondSegmentMemory);
                var secondSegment = firstSegment.Next as BufferSequenceSegment;

                return new ReadOnlySequence<T>(firstSegment, 0, secondSegment, secondSegment.Length);
            }
        }

        sealed class BufferSequenceSegment : ReadOnlySequenceSegment<T>
        {
            public int Length => Memory.Length;

            public BufferSequenceSegment(ReadOnlyMemory<T> memory, int runningIndex)
            {
                Memory = memory;
                RunningIndex = runningIndex;
            }

            public BufferSequenceSegment(ReadOnlyMemory<T> firstSegment, ReadOnlyMemory<T> secondSegment)
            {
                Memory = firstSegment;
                RunningIndex = 0;
                Next = new BufferSequenceSegment(secondSegment, firstSegment.Length);
            }
        }
    }
}