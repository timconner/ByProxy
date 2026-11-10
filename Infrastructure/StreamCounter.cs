namespace ByProxy.Infrastructure {
    public sealed class StreamCounter : Stream {
        private readonly Stream _stream;
        public StreamCounter(Stream stream) => _stream = stream;
        public long BytesWritten { get; private set; }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => _stream.CanWrite;
        public override void Flush() => _stream.Flush();
        public override Task FlushAsync(CancellationToken ct) => _stream.FlushAsync(ct);

        public override void Write(byte[] buffer, int offset, int count) {
            _stream.Write(buffer, offset, count);
            BytesWritten += count;
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default) {
            await _stream.WriteAsync(buffer, ct);
            BytesWritten += buffer.Length;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public sealed class CountingStream : Stream {
            private readonly Stream _stream;

            public CountingStream(Stream inner) => _stream = inner;

            public long BytesWritten { get; private set; }

            public override bool CanRead => _stream.CanRead;
            public override bool CanSeek => _stream.CanSeek;
            public override bool CanWrite => _stream.CanWrite;
            public override long Length => _stream.Length;
            public override long Position { get => _stream.Position; set => _stream.Position = value; }

            public override void Flush() => _stream.Flush();
            public override Task FlushAsync(CancellationToken cancellationToken) => _stream.FlushAsync(cancellationToken);
            public override int Read(byte[] buffer, int offset, int count) => _stream.Read(buffer, offset, count);
            public override long Seek(long offset, SeekOrigin origin) => _stream.Seek(offset, origin);
            public override void SetLength(long value) => _stream.SetLength(value);

            public override void Write(byte[] buffer, int offset, int count) {
                _stream.Write(buffer, offset, count);
                BytesWritten += count;
            }

            public override void Write(ReadOnlySpan<byte> buffer) {
                _stream.Write(buffer);
                BytesWritten += buffer.Length;
            }

            public override void WriteByte(byte value) {
                _stream.WriteByte(value);
                BytesWritten++;
            }

            public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) {
                await _stream.WriteAsync(buffer, offset, count, cancellationToken);
                BytesWritten += count;
            }

            public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) {
                await _stream.WriteAsync(buffer, cancellationToken);
                BytesWritten += buffer.Length;
            }
        }
    }
}
