/* Wea2WeatherReaderTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or (at
 * your option) any later version.
 *
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
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
