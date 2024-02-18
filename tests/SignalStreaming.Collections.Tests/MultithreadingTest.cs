using System.Threading.Tasks;
using System.Threading;
using FluentAssertions;
using Xunit;

namespace SignalStreaming.Collections.Tests
{
    public class MultithreadingTest
    {
        [Theory]
        [InlineData(4, 100)]
        [InlineData(4, 1000)]
        [InlineData(4, 10000)]
        [InlineData(4, 100000)]
        [InlineData(4, 1000000)]
        [InlineData(12, 100)]
        [InlineData(12, 1000)]
        [InlineData(12, 10000)]
        [InlineData(12, 100000)]
        [InlineData(12, 1000000)]
        public void ConcurrentRingBufferTest(uint numberOfThreads, uint numberOfItems)
        {
            // Number of threads must be even
            if (numberOfThreads % 2 != 0)
            {
                numberOfThreads++;
            }
            // Number of items must be even
            if (numberOfItems % 2 != 0)
            {
                numberOfItems++;
            }

            var ringBuffer = new ConcurrentRingBuffer<ulong>(4096);

            ulong enqueueSum = 0;
            ulong dequeueSum = 0;

            Parallel.For(0, numberOfThreads, index =>
            {
                var spinWait = new SpinWait();

                if (index % 2 == 0)
                {
                    // Enqueue
                    for (uint n = 1; n <= numberOfItems; n++)
                    {
                        while (!ringBuffer.TryEnqueue(n))
                        {
                            spinWait.SpinOnce();
                        }
                        Interlocked.Add(ref enqueueSum, (ulong)n);
                    }
                }
                else
                {
                    // Dequeue
                    for (uint n = 1; n <= numberOfItems; n++)
                    {
                        ulong value;
                        while (!ringBuffer.TryDequeue(out value))
                        {
                            spinWait.SpinOnce();
                        }
                        Interlocked.Add(ref dequeueSum, value);
                    }
                }
            });

            var expectedSum = (ulong)numberOfThreads / 2 * (numberOfItems + 1) * numberOfItems / 2;
            enqueueSum.Should().Be(expectedSum);
            dequeueSum.Should().Be(expectedSum);
        }
    }
}
