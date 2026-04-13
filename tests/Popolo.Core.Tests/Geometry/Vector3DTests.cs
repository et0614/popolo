/* Vector3DTests.cs
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

using System;
using Xunit;
using Popolo.Core.Geometry;
using Popolo.Core.Exceptions;

namespace Popolo.Core.Tests.Geometry
{
    /// <summary>Vector3D のテスト</summary>
    public class Vector3DTests
    {

        #region コンストラクタのテスト

        /// <summary>成分と長さが正しく設定される</summary>
        [Fact]
        public void Constructor_SetsComponentsAndLength()
        {
            var v = new Vector3D(3.0, 4.0, 0.0);
            Assert.Equal(3.0, v.X);
            Assert.Equal(4.0, v.Y);
            Assert.Equal(0.0, v.Z);
            Assert.Equal(5.0, v.Length, precision: 10);
        }

        /// <summary>コピーコンストラクタが正しくコピーする</summary>
        [Fact]
        public void CopyConstructor_CopiesAllComponents()
        {
            var original = new Vector3D(1.0, 2.0, 3.0);
            var copy = new Vector3D(original);
            Assert.Equal(original.X, copy.X);
            Assert.Equal(original.Y, copy.Y);
            Assert.Equal(original.Z, copy.Z);
            Assert.Equal(original.Length, copy.Length);
        }

        #endregion

        #region GetLength のテスト

        /// <summary>ゼロベクトルの長さは0</summary>
        [Fact]
        public void GetLength_ZeroVector_ReturnsZero()
        {
            Assert.Equal(0.0, Vector3D.GetLength(new Vector3D(0, 0, 0)));
        }

        /// <summary>既知の長さが正しく計算される</summary>
        [Theory]
        [InlineData(1, 0, 0, 1.0)]
        [InlineData(3, 4, 0, 5.0)]
        [InlineData(1, 1, 1, 1.7320508)]  // √3
        public void GetLength_KnownVectors_ReturnsCorrectLength(
            double x, double y, double z, double expected)
        {
            Assert.Equal(expected, Vector3D.GetLength(new Vector3D(x, y, z)), precision: 6);
        }

        #endregion

        #region GetDot のテスト

        /// <summary>平行ベクトルの内積は長さの積になる</summary>
        [Fact]
        public void GetDot_ParallelVectors_ReturnsLengthProduct()
        {
            var v1 = new Vector3D(2.0, 0.0, 0.0);
            var v2 = new Vector3D(3.0, 0.0, 0.0);
            Assert.Equal(6.0, v1.GetDot(v2), precision: 10);
        }

        /// <summary>直交ベクトルの内積は0</summary>
        [Fact]
        public void GetDot_OrthogonalVectors_ReturnsZero()
        {
            var v1 = new Vector3D(1.0, 0.0, 0.0);
            var v2 = new Vector3D(0.0, 1.0, 0.0);
            Assert.Equal(0.0, v1.GetDot(v2), precision: 10);
        }

        #endregion

        #region GetCross のテスト

        /// <summary>X×Y = Z（右手系）</summary>
        [Fact]
        public void GetCross_XCrossY_ReturnsZ()
        {
            var vx = new Vector3D(1.0, 0.0, 0.0);
            var vy = new Vector3D(0.0, 1.0, 0.0);
            var result = vx.GetCross(vy);
            Assert.Equal(0.0, result.X, precision: 10);
            Assert.Equal(0.0, result.Y, precision: 10);
            Assert.Equal(1.0, result.Z, precision: 10);
        }

        /// <summary>平行ベクトルの外積はゼロベクトル</summary>
        [Fact]
        public void GetCross_ParallelVectors_ReturnsZeroVector()
        {
            var v1 = new Vector3D(1.0, 0.0, 0.0);
            var v2 = new Vector3D(2.0, 0.0, 0.0);
            var result = v1.GetCross(v2);
            Assert.Equal(0.0, result.Length, precision: 10);
        }

        #endregion

        #region Normalize のテスト

        /// <summary>正規化後の長さは1になる</summary>
        [Fact]
        public void Normalize_NonZeroVector_LengthBecomesOne()
        {
            var v = new Vector3D(3.0, 4.0, 0.0);
            v.Normalize();
            Assert.Equal(1.0, v.Length, precision: 10);
        }

        /// <summary>正規化後の方向は変わらない</summary>
        [Fact]
        public void Normalize_PreservesDirection()
        {
            var v = new Vector3D(3.0, 4.0, 0.0);
            v.Normalize();
            Assert.Equal(0.6, v.X, precision: 10);  // 3/5
            Assert.Equal(0.8, v.Y, precision: 10);  // 4/5
        }

        /// <summary>ゼロベクトルの正規化で PopoloArgumentException が発生する</summary>
        [Fact]
        public void Normalize_ZeroVector_ThrowsPopoloArgumentException()
        {
            var v = new Vector3D(0.0, 0.0, 0.0);
            Assert.Throws<PopoloArgumentException>(() => v.Normalize());
        }

        /// <summary>GetUnitVector でゼロベクトルを渡すと PopoloArgumentException が発生する</summary>
        [Fact]
        public void GetUnitVector_ZeroVector_ThrowsPopoloArgumentException()
        {
            var ex = Assert.Throws<PopoloArgumentException>(
                () => Vector3D.GetUnitVector(new Vector3D(0, 0, 0)));
            Assert.Equal("vec", ex.ParamName);
        }

        #endregion

        #region 演算子のテスト

        /// <summary>加算が正しく動作する</summary>
        [Fact]
        public void OperatorPlus_ReturnsCorrectVector()
        {
            var v1 = new Vector3D(1.0, 2.0, 3.0);
            var v2 = new Vector3D(4.0, 5.0, 6.0);
            var result = v1 + v2;
            Assert.Equal(5.0, result.X);
            Assert.Equal(7.0, result.Y);
            Assert.Equal(9.0, result.Z);
        }

        /// <summary>減算が正しく動作する</summary>
        [Fact]
        public void OperatorMinus_ReturnsCorrectVector()
        {
            var v1 = new Vector3D(4.0, 5.0, 6.0);
            var v2 = new Vector3D(1.0, 2.0, 3.0);
            var result = v1 - v2;
            Assert.Equal(3.0, result.X);
            Assert.Equal(3.0, result.Y);
            Assert.Equal(3.0, result.Z);
        }

        /// <summary>同じ値のベクトルは等しい</summary>
        [Fact]
        public void Equals_SameComponents_ReturnsTrue()
        {
            var v1 = new Vector3D(1.0, 2.0, 3.0);
            var v2 = new Vector3D(1.0, 2.0, 3.0);
            Assert.True(v1.Equals(v2));
        }

        /// <summary>異なる値のベクトルは等しくない</summary>
        [Fact]
        public void Equals_DifferentComponents_ReturnsFalse()
        {
            var v1 = new Vector3D(1.0, 2.0, 3.0);
            var v2 = new Vector3D(1.0, 2.0, 4.0);
            Assert.False(v1.Equals(v2));
        }

        /// <summary>等しいベクトルは同じハッシュコードを返す</summary>
        [Fact]
        public void GetHashCode_EqualVectors_ReturnSameHash()
        {
            var v1 = new Vector3D(1.0, 2.0, 3.0);
            var v2 = new Vector3D(1.0, 2.0, 3.0);
            Assert.Equal(v1.GetHashCode(), v2.GetHashCode());
        }

        #endregion

    }
}
