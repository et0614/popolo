/* Wea2WeatherReaderTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.IO;
using Xunit;
using Popolo.Core.Exceptions;
using Popolo.IO.Climate.Weather;

namespace Popolo.IO.Tests.Climate.Weather
{
    /// <summary>
    /// Wea2WeatherReader の引数検証テスト。
    /// 実データのテストは本物のWEA2ファイルが必要なため、ここではカバー対象外。
    /// </summary>
    public class Wea2WeatherReaderTests
    {
        /// <summary>LocationIndex未設定ではPopoloInvalidOperationException</summary>
        [Fact]
        public void Read_LocationIndexNotSet_Throws()
        {
            var reader = new Wea2WeatherReader();
            using var ms = new MemoryStream(new byte[100]);

            Assert.Throws<PopoloInvalidOperationException>(() => reader.Read(ms));
        }

        /// <summary>LocationIndex範囲外(0や843以上)はPopoloInvalidOperationException</summary>
        [Fact]
        public void Read_LocationIndexOutOfRange_Throws()
        {
            var r1 = new Wea2WeatherReader { LocationIndex = 0 };
            using (var ms = new MemoryStream(new byte[100]))
                Assert.Throws<PopoloInvalidOperationException>(() => r1.Read(ms));

            var r2 = new Wea2WeatherReader { LocationIndex = 843 };
            using (var ms = new MemoryStream(new byte[100]))
                Assert.Throws<PopoloInvalidOperationException>(() => r2.Read(ms));
        }

        /// <summary>nullストリームはPopoloArgumentException</summary>
        [Fact]
        public void Read_NullStream_Throws()
        {
            var reader = new Wea2WeatherReader(1);
            Assert.Throws<PopoloArgumentException>(() => reader.Read((Stream)null!));
        }

        /// <summary>
        /// シーク不可能なストリームはPopoloArgumentException。
        /// </summary>
        [Fact]
        public void Read_NonSeekableStream_Throws()
        {
            var reader = new Wea2WeatherReader(1);
            using var nonSeekable = new NonSeekableStream();
            Assert.Throws<PopoloArgumentException>(() => reader.Read(nonSeekable));
        }

        /// <summary>コンストラクタで指定したLocationIndexが保持される</summary>
        [Fact]
        public void Constructor_PreservesLocationIndex()
        {
            var reader = new Wea2WeatherReader(42);
            Assert.Equal(42, reader.LocationIndex);
        }

        /// <summary>テスト用: シーク不可のストリーム</summary>
        private sealed class NonSeekableStream : Stream
        {
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => 0;
            public override long Position { get => 0; set => throw new System.NotSupportedException(); }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => 0;
            public override long Seek(long offset, SeekOrigin origin) => throw new System.NotSupportedException();
            public override void SetLength(long value) => throw new System.NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new System.NotSupportedException();
        }
    }
}
