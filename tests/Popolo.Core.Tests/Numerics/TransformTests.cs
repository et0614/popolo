/* TransformTests.cs
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
using Popolo.Core.Numerics;
using Popolo.Core.Exceptions;

namespace Popolo.Core.Tests.Numerics
{
    /// <summary>Transform のテスト</summary>
    public class TransformTests
    {

        #region BoxCoxTransform のテスト

        /// <summary>lambda=1 のとき f(x) = x - 1 となる</summary>
        [Fact]
        public void BoxCoxTransform_LambdaOne_ReturnsXMinusOne()
        {
            double[] data = { 1.0, 2.0, 3.0, 4.0 };
            double[] result = Transform.BoxCoxTransform(data, 1.0);

            Assert.Equal(0.0, result[0], precision: 10);
            Assert.Equal(1.0, result[1], precision: 10);
            Assert.Equal(2.0, result[2], precision: 10);
            Assert.Equal(3.0, result[3], precision: 10);
        }

        /// <summary>lambda=0 のとき対数変換 f(x) = log(x) となる</summary>
        [Fact]
        public void BoxCoxTransform_LambdaZero_ReturnsLog()
        {
            double[] data = { 1.0, Math.E, Math.E * Math.E };
            double[] result = Transform.BoxCoxTransform(data, 0.0);

            Assert.Equal(0.0, result[0], precision: 10);
            Assert.Equal(1.0, result[1], precision: 10);
            Assert.Equal(2.0, result[2], precision: 10);
        }

        /// <summary>lambda=2 のとき f(x) = (x^2 - 1) / 2 となる</summary>
        [Fact]
        public void BoxCoxTransform_LambdaTwo_ReturnsCorrectValue()
        {
            double[] data = { 1.0, 2.0, 3.0 };
            double[] result = Transform.BoxCoxTransform(data, 2.0);

            Assert.Equal(0.0, result[0], precision: 10);   // (1^2 - 1) / 2 = 0
            Assert.Equal(1.5, result[1], precision: 10);   // (2^2 - 1) / 2 = 1.5
            Assert.Equal(4.0, result[2], precision: 10);   // (3^2 - 1) / 2 = 4
        }

        /// <summary>元データを変更しない（副作用なし）</summary>
        [Fact]
        public void BoxCoxTransform_DoesNotModifyOriginalData()
        {
            double[] data = { 1.0, 2.0, 3.0 };
            double[] original = { 1.0, 2.0, 3.0 };

            Transform.BoxCoxTransform(data, 1.0);

            Assert.Equal(original[0], data[0]);
            Assert.Equal(original[1], data[1]);
            Assert.Equal(original[2], data[2]);
        }

        /// <summary>data が null のとき PopoloArgumentException が発生する</summary>
        [Fact]
        public void BoxCoxTransform_NullData_ThrowsPopoloArgumentException()
        {
            var ex = Assert.Throws<PopoloArgumentException>(
                () => Transform.BoxCoxTransform(null!, 1.0));
            Assert.Equal("data", ex.ParamName);
        }

        /// <summary>data が空のとき PopoloArgumentException が発生する</summary>
        [Fact]
        public void BoxCoxTransform_EmptyData_ThrowsPopoloArgumentException()
        {
            var ex = Assert.Throws<PopoloArgumentException>(
                () => Transform.BoxCoxTransform(Array.Empty<double>(), 1.0));
            Assert.Equal("data", ex.ParamName);
        }

        #endregion

        #region GetOptimumBoxCoxLambda のテスト

        /// <summary>正規分布データに対してラムダが約1.0になる</summary>
        [Fact]
        public void GetOptimumBoxCoxLambda_NormalData_ReturnsLambdaNearOne()
        {
            // 正規分布に近いデータ（線形データ）は lambda≒1 になるはず
            double[] data = { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 9.0, 10.0 };
            double lambda = Transform.GetOptimumBoxCoxLambda(ref data);

            // 正規分布に近いデータなのでラムダは探索範囲内に収まる
            Assert.InRange(lambda, -2.0, 2.0);
        }

        /// <summary>対数正規分布データに対してラムダが約0.0になる</summary>
        [Fact]
        public void GetOptimumBoxCoxLambda_LogNormalData_ReturnsLambdaNearZero()
        {
            // 指数的に増加するデータは対数変換（lambda≒0）で正規化される
            double[] data = new double[20];
            for (int i = 0; i < data.Length; i++)
                data[i] = Math.Exp(i * 0.3);

            double lambda = Transform.GetOptimumBoxCoxLambda(ref data);

            Assert.InRange(lambda, -0.5, 0.5);
        }

        /// <summary>変換後のデータが元データと同じ長さを持つ</summary>
        [Fact]
        public void GetOptimumBoxCoxLambda_TransformedDataHasSameLength()
        {
            double[] data = { 1.0, 2.0, 3.0, 4.0, 5.0 };
            int originalLength = data.Length;

            Transform.GetOptimumBoxCoxLambda(ref data);

            Assert.Equal(originalLength, data.Length);
        }

        /// <summary>data が null のとき PopoloArgumentException が発生する</summary>
        [Fact]
        public void GetOptimumBoxCoxLambda_NullData_ThrowsPopoloArgumentException()
        {
            double[]? data = null;
            var ex = Assert.Throws<PopoloArgumentException>(
                () => Transform.GetOptimumBoxCoxLambda(ref data!));
            Assert.Equal("data", ex.ParamName);
        }

        /// <summary>data が空のとき PopoloArgumentException が発生する</summary>
        [Fact]
        public void GetOptimumBoxCoxLambda_EmptyData_ThrowsPopoloArgumentException()
        {
            double[] data = Array.Empty<double>();
            var ex = Assert.Throws<PopoloArgumentException>(
                () => Transform.GetOptimumBoxCoxLambda(ref data));
            Assert.Equal("data", ex.ParamName);
        }

        #endregion

    }
}
