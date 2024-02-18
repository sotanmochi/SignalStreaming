using System;
using FluentAssertions;
using Xunit;

namespace SignalStreaming.Collections.Tests
{
    public class ConcurrentRingBufferTest
    {
        [Fact]
        public void Capacity()
        {
            var ringBuffer0 = new ConcurrentRingBuffer<int>(0);
            var ringBuffer1 = new ConcurrentRingBuffer<int>(1);
            var ringBuffer2 = new ConcurrentRingBuffer<int>(2);
            ringBuffer0.Capacity.Should().Be(2);
            ringBuffer1.Capacity.Should().Be(2);
            ringBuffer2.Capacity.Should().Be(2);

            var ringBuffer3 = new ConcurrentRingBuffer<int>(3);
            var ringBuffer4 = new ConcurrentRingBuffer<int>(4);
            ringBuffer3.Capacity.Should().Be(4);
            ringBuffer4.Capacity.Should().Be(4);

            var ringBuffer5 = new ConcurrentRingBuffer<int>(5);
            var ringBuffer6 = new ConcurrentRingBuffer<int>(6);
            var ringBuffer7 = new ConcurrentRingBuffer<int>(7);
            var ringBuffer8 = new ConcurrentRingBuffer<int>(8);
            ringBuffer5.Capacity.Should().Be(8);
            ringBuffer6.Capacity.Should().Be(8);
            ringBuffer7.Capacity.Should().Be(8);
            ringBuffer8.Capacity.Should().Be(8);

            var ringBuffer255 = new ConcurrentRingBuffer<int>(255);
            ringBuffer255.Capacity.Should().Be(256);
            var ringBuffer257 = new ConcurrentRingBuffer<int>(257);
            ringBuffer257.Capacity.Should().Be(512);

            var ringBuffer1023 = new ConcurrentRingBuffer<int>(1023);
            ringBuffer1023.Capacity.Should().Be(1024);
            var ringBuffer1025 = new ConcurrentRingBuffer<int>(1025);
            ringBuffer1025.Capacity.Should().Be(2048);
        }

        [Fact]
        public void EnqueueAndDequeue()
        {
            var ringBuffer = new ConcurrentRingBuffer<int>(8);
            ringBuffer.FillCount.Should().Be(0);
            ringBuffer.FreeCount.Should().Be(8);
            ringBuffer.EnqueuePosition.Should().Be(0);
            ringBuffer.DequeuePosition.Should().Be(0);

            for (var i = 1; i <= 8; i++) ringBuffer.TryEnqueue(i);

            ringBuffer.Buffer.ToArray().Should().Equal(1, 2, 3, 4, 5, 6, 7, 8);

            for (var i = 0; i < 4; i++) ringBuffer.TryDequeue(out _);

            ringBuffer.FillCount.Should().Be(4);
            ringBuffer.FreeCount.Should().Be(4);
            ringBuffer.EnqueuePosition.Should().Be(8);
            ringBuffer.DequeuePosition.Should().Be(4);
            ringBuffer.Buffer.ToArray().Should().Equal(0, 0, 0, 0, 5, 6, 7, 8);

            ringBuffer.TryEnqueue(9);
            ringBuffer.TryEnqueue(10);
            ringBuffer.TryDequeue(out _);

            ringBuffer.FillCount.Should().Be(5);
            ringBuffer.FreeCount.Should().Be(3);
            ringBuffer.EnqueuePosition.Should().Be(10);
            ringBuffer.DequeuePosition.Should().Be(5);
            ringBuffer.Buffer.ToArray().Should().Equal(9, 10, 0, 0, 0, 6, 7, 8);

            for (var i = 0; i < 5; i++) ringBuffer.TryDequeue(out _);

            ringBuffer.FillCount.Should().Be(0);
            ringBuffer.EnqueuePosition.Should().Be(10);
            ringBuffer.DequeuePosition.Should().Be(10);
            ringBuffer.Buffer.ToArray().Should().Equal(0, 0, 0, 0, 0, 0, 0, 0);
        }

        [Fact]
        public void TryEnqueueAndDequeue()
        {
            var success = false;
            var ringBuffer = new ConcurrentRingBuffer<int>(16);

            for (var i = 0; i < ringBuffer.Capacity; i++)
            {
                success = ringBuffer.TryEnqueue(i);
                success.Should().Be(true);
            }

            success = ringBuffer.TryEnqueue(9999);
            success.Should().Be(false);

            for (var i = 0; i < ringBuffer.Capacity; i++)
            {
                success = ringBuffer.TryDequeue(out var value);
                success.Should().Be(true);
                value.Should().Be(i);
            }

            success = ringBuffer.TryDequeue(out var defaultValue);
            success.Should().Be(false);
            defaultValue.Should().Be(0);
        }

        [Fact]
        public void Indexer()
        {
            var ringBuffer = new ConcurrentRingBuffer<int>(32);

            for (var i = 0; i < ringBuffer.Capacity / 2; i++)
            {
                ringBuffer.TryEnqueue(i);
            }

            for (var i = 0; i < ringBuffer.FillCount; i++)
            {
                ringBuffer[i].Should().Be(i);
            }

            var defaultValue = 0;
            for (var i = ringBuffer.FillCount; i < ringBuffer.Capacity; i++)
            {
                ringBuffer[i].Should().Be(defaultValue);
            }
        }

        [Fact]
        public void Slice1()
        {
            var ringBuffer = new ConcurrentRingBuffer<int>(64);

            for (var i = 1; i <= 64; i++)
            {
                ringBuffer.TryEnqueue(i);
            }
            for (var i = 0; i < 48; i++)
            {
                ringBuffer.TryDequeue(out _);
            }
            for (var i = 1; i <= 16; i++)
            {
                ringBuffer.TryEnqueue(i + 64);
            }

            ringBuffer.FillCount.Should().Be(32);
            ringBuffer.Buffer.ToArray().Should().Equal(
                65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64);

            var buffer0 = ringBuffer.Slice(0, 0);
            buffer0.IsSingleSegment.Should().Be(true);
            buffer0.IsEmpty.Should().Be(true);

            var buffer1 = ringBuffer.Slice(0, 16);
            buffer1.IsSingleSegment.Should().Be(true);
            buffer1.First.ToArray().Should().Equal(49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64);

            var buffer2 = ringBuffer.Slice(0, 32);
            buffer2.IsSingleSegment.Should().Be(false);
            buffer2.First.ToArray().Should().Equal(49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64);

            var segmentCount = 0;
            foreach (var bufferSegment in buffer2)
            {
                if (segmentCount == 0)
                {
                    bufferSegment.ToArray().Should().Equal(49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64);
                }
                else if (segmentCount == 1)
                {
                    bufferSegment.ToArray().Should().Equal(65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80);
                }
                segmentCount++;
            }
            segmentCount.Should().Be(2);
        }

        [Fact]
        public void Slice2()
        {
            var ringBuffer = new ConcurrentRingBuffer<int>(16);

            var fillCount = 8;
            for (var i = 0; i < fillCount; i++)
            {
                ringBuffer.TryEnqueue(i + 1);
            }

            ringBuffer.FillCount.Should().Be(fillCount);
            ringBuffer.Buffer.ToArray().Should().Equal(1, 2, 3, 4, 5, 6, 7, 8, 0, 0, 0, 0, 0, 0, 0, 0);

            var action1 = () => ringBuffer.Slice(0, fillCount + 1);
            action1.Should().Throw<ArgumentOutOfRangeException>();

            var action2 = () => ringBuffer.Slice(-1, fillCount / 2);
            action2.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void Slice3()
        {
            var ringBuffer = new ConcurrentRingBuffer<int>(16);

            var fillCount = 16;
            for (var i = 0; i < fillCount; i++)
            {
                ringBuffer.TryEnqueue(i + 1);
            }
            for (var i = 0; i < fillCount; i++)
            {
                ringBuffer.TryDequeue(out _);
            }

            ringBuffer.EnqueuePosition.Should().Be(16);
            ringBuffer.Buffer.ToArray().Should().Equal(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

            for (var i = 0; i < fillCount / 2; i++)
            {
                ringBuffer.TryEnqueue(fillCount + i + 1);
            }
            ringBuffer.Buffer.ToArray().Should().Equal(17, 18, 19, 20, 21, 22, 23, 24, 0, 0, 0, 0, 0, 0, 0, 0);

            var action1 = () => ringBuffer.Slice(17, fillCount / 2);
            action1.Should().Throw<ArgumentOutOfRangeException>();

            var action2 = () => ringBuffer.Slice(0, fillCount / 2);
            action2.Should().NotThrow();
            action2?.Invoke().First.ToArray().Should().Equal(17, 18, 19, 20, 21, 22, 23, 24);
        }

        [Fact]
        public void Clear()
        {
            var ringBuffer = new ConcurrentRingBuffer<int>(16);

            var enqueueCount = 16;
            for (var i = 0; i < enqueueCount; i++)
            {
                ringBuffer.TryEnqueue(i);
            }
            ringBuffer.FillCount.Should().Be(enqueueCount);

            var clearCount = 12;
            ringBuffer.Clear(clearCount);

            ringBuffer.FillCount.Should().Be(enqueueCount - clearCount);
            ringBuffer.FreeCount.Should().Be(ringBuffer.Capacity - enqueueCount + clearCount);
            ringBuffer.EnqueuePosition.Should().Be(enqueueCount);
            ringBuffer.DequeuePosition.Should().Be(clearCount);

            ringBuffer.Capacity.Should().Be(16);
            ringBuffer.FillCount.Should().Be(4);

            var count = ringBuffer.Capacity - ringBuffer.FillCount;
            for (var i = 0; i < count; i++)
            {
                ringBuffer.TryEnqueue(enqueueCount + i);
            }
            ringBuffer.Buffer.ToArray().Should().Equal(16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 12, 13, 14, 15);

            ringBuffer.Clear(8);
            ringBuffer.Buffer.ToArray().Should().Equal(0, 0, 0, 0, 20, 21, 22, 23, 24, 25, 26, 27, 0, 0, 0, 0);
        }

        [Fact]
        public void ClearAll()
        {
            var ringBuffer = new ConcurrentRingBuffer<int>(16);

            for (var i = 0; i < 16; i++)
            {
                ringBuffer.TryEnqueue(i);
            }
            ringBuffer.Clear(8);

            ringBuffer.FillCount.Should().Be(8);
            ringBuffer.FreeCount.Should().Be(8);
            ringBuffer.EnqueuePosition.Should().Be(16);
            ringBuffer.DequeuePosition.Should().Be(8);

            for (var i = 0; i < 4; i++)
            {
                ringBuffer.TryEnqueue(16 + i);
            }

            ringBuffer.Buffer.ToArray().Should().Equal(16, 17, 18, 19, 0, 0, 0, 0, 8, 9, 10, 11, 12, 13, 14, 15);

            ringBuffer.Clear();

            ringBuffer.FillCount.Should().Be(0);
            ringBuffer.FreeCount.Should().Be(16);
            ringBuffer.EnqueuePosition.Should().Be(20);
            ringBuffer.DequeuePosition.Should().Be(20);

            ringBuffer.Buffer.ToArray().Should().Equal(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        [Fact]
        public void TryBulkEnqueue()
        {
            var success = false;
            var ringBuffer = new ConcurrentRingBuffer<byte>(16);

            success = ringBuffer.TryBulkEnqueue(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            success.Should().Be(true);
            ringBuffer.EnqueuePosition.Should().Be(8);
            ringBuffer.DequeuePosition.Should().Be(0);
            ringBuffer.FillCount.Should().Be(8);
            ringBuffer.FreeCount.Should().Be(8);
            ringBuffer.Buffer.ToArray().Should().Equal(1, 2, 3, 4, 5, 6, 7, 8, 0, 0, 0, 0, 0, 0, 0, 0);

            success = ringBuffer.TryBulkEnqueue(new byte[] { 9, 10, 11, 12, 13, 14, 15, 16, 17, 18 });
            success.Should().Be(false);

            success = ringBuffer.TryBulkEnqueue(new byte[] { 9, 10, 11, 12 });
            success.Should().Be(true);
            ringBuffer.EnqueuePosition.Should().Be(12);
            ringBuffer.DequeuePosition.Should().Be(0);
            ringBuffer.FillCount.Should().Be(12);
            ringBuffer.FreeCount.Should().Be(4);

            for (var i = 0; i < 12; i++) ringBuffer.TryDequeue(out _);

            success = ringBuffer.TryBulkEnqueue(new byte[] { 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24 });
            success.Should().Be(true);
            ringBuffer.EnqueuePosition.Should().Be(24);
            ringBuffer.DequeuePosition.Should().Be(12);
            ringBuffer.FillCount.Should().Be(12);
            ringBuffer.FreeCount.Should().Be(4);

            ringBuffer.Buffer.ToArray().Should().Equal(17, 18, 19, 20, 21, 22, 23, 24, 0, 0, 0, 0, 13, 14, 15, 16);
        }

        [Fact]
        public void TryBulkDequeue()
        {
            var success = false;
            var ringBuffer = new ConcurrentRingBuffer<byte>(16);
            var destinationBuffer = new byte[4];

            success = ringBuffer.TryBulkEnqueue(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 });
            success.Should().Be(true);

            success = ringBuffer.TryBulkDequeue(destinationBuffer);
            success.Should().Be(true);
            destinationBuffer.Should().Equal(1, 2, 3, 4);
            ringBuffer.Buffer.ToArray().Should().Equal(0, 0, 0, 0, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16);

            success = ringBuffer.TryBulkDequeue(destinationBuffer);
            success.Should().Be(true);
            destinationBuffer.Should().Equal(5, 6, 7, 8);
            ringBuffer.Buffer.ToArray().Should().Equal(0, 0, 0, 0, 0, 0, 0, 0, 9, 10, 11, 12, 13, 14, 15, 16);

            success = ringBuffer.TryBulkEnqueue(new byte[] { 17, 18, 19, 20, 21, 22, 23, 24 });
            ringBuffer.Buffer.ToArray().Should().Equal(17, 18, 19, 20, 21, 22, 23, 24, 9, 10, 11, 12, 13, 14, 15, 16);

            var destinationBuffer2 = new byte[16];
            success = ringBuffer.TryBulkDequeue(destinationBuffer2);
            success.Should().Be(true);

            destinationBuffer2.Should().Equal(9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24);
            ringBuffer.Buffer.ToArray().Should().Equal(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

            ringBuffer.EnqueuePosition.Should().Be(24);
            ringBuffer.DequeuePosition.Should().Be(24);
        }
    }
}
