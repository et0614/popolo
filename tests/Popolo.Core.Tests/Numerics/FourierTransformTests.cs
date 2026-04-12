/* FourierTransformTests.cs
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
using Popolo.Numerics;
using Popolo.Exceptions;

namespace Popolo.Core.Tests.Numerics
{
    /// <summary>FourierTransform のテスト</summary>
    public class FourierTransformTests
    {

        #region 引数チェックのテスト

        /// <summary>x が null のとき PopoloArgumentException が発生する</summary>
        [Fact]
        public void FFT_NullX_ThrowsPopoloArgumentException()
        {
            var ex = Assert.Throws<PopoloArgumentException>(
                () => FourierTransform.FFT(null!, new double[4]));
            Assert.Equal("x", ex.ParamName);
        }

        /// <summary>xi が null のとき PopoloArgumentException が発生する</summary>
        [Fact]
        public void FFT_NullXi_ThrowsPopoloArgumentException()
        {
            var ex = Assert.Throws<PopoloArgumentException>(
                () => FourierTransform.FFT(new double[4], null!));
            Assert.Equal("xi", ex.ParamName);
        }

        /// <summary>x と xi の長さが異なるとき PopoloArgumentException が発生する</summary>
        [Fact]
        public void FFT_MismatchedLength_ThrowsPopoloArgumentException()
        {
            var ex = Assert.Throws<PopoloArgumentException>(
                () => FourierTransform.FFT(new double[4], new double[8]));
            Assert.Equal("xi", ex.ParamName);
        }

        /// <summary>INV は PopoloNotImplementedException が発生する</summary>
        [Fact]
        public void INV_ThrowsPopoloNotImplementedException()
        {
            Assert.Throws<PopoloNotImplementedException>(
                () => FourierTransform.INV(new double[4]));
        }

        #endregion

        #region FFT の数学的性質のテスト

        /// <summary>定数信号のFFTはDC成分のみに集中する</summary>
        [Fact]
        public void FFT_ConstantSignal_DCComponentOnly()
        {
            // 全要素が1の信号 → FFT後は x[0]=N, 他は0
            int n = 8;
            double[] x = new double[n];
            double[] xi = new double[n];
            for (int i = 0; i < n; i++) x[i] = 1.0;

            FourierTransform.FFT(x, xi);

            Assert.Equal(n, x[0], precision: 6);
            Assert.Equal(0.0, xi[0], precision: 6);
            for (int i = 1; i < n; i++)
            {
                Assert.Equal(0.0, x[i], precision: 6);
                Assert.Equal(0.0, xi[i], precision: 6);
            }
        }

        /// <summary>インパルス信号のFFTは全周波数成分が均一になる</summary>
        [Fact]
        public void FFT_ImpulseSignal_UniformSpectrum()
        {
            // x[0]=1, 他は0 → FFT後は全成分の実部=1, 虚部=0
            int n = 8;
            double[] x = new double[n];
            double[] xi = new double[n];
            x[0] = 1.0;

            FourierTransform.FFT(x, xi);

            for (int i = 0; i < n; i++)
            {
                Assert.Equal(1.0, x[i], precision: 6);
                Assert.Equal(0.0, xi[i], precision: 6);
            }
        }

        /// <summary>パーセバルの定理：時間領域と周波数領域のエネルギーが保存される</summary>
        [Fact]
        public void FFT_ParsevalsTheorem_EnergyConserved()
        {
            // 時間領域のエネルギー = 周波数領域のエネルギー / N
            int n = 8;
            double[] x = { 1.0, 2.0, 3.0, 4.0, 3.0, 2.0, 1.0, 0.0 };
            double[] xi = new double[n];

            // 時間領域エネルギー
            double timePower = 0;
            for (int i = 0; i < n; i++) timePower += x[i] * x[i];

            FourierTransform.FFT(x, xi);

            // 周波数領域エネルギー / N
            double freqPower = 0;
            for (int i = 0; i < n; i++)
                freqPower += x[i] * x[i] + xi[i] * xi[i];
            freqPower /= n;

            Assert.Equal(timePower, freqPower, precision: 6);
        }

        /// <summary>2の冪でない長さの入力も処理できる</summary>
        [Fact]
        public void FFT_NonPowerOfTwoLength_ProcessesCorrectly()
        {
            // 長さ5の入力（内部で8にゼロパディングされる）
            double[] x = { 1.0, 2.0, 3.0, 2.0, 1.0 };
            double[] xi = new double[5];

            // 例外が出ずに完了することを確認
            FourierTransform.FFT(x, xi);

            // 元の長さ分の結果が返ることを確認
            Assert.Equal(5, x.Length);
        }

        /// <summary>正弦波のFFTは対応する周波数に成分が現れる</summary>
        [Fact]
        public void FFT_SineWave_PeakAtCorrectFrequency()
        {
            // f=1 の正弦波（1周期分、N=8点）
            int n = 8;
            double[] x = new double[n];
            double[] xi = new double[n];
            for (int i = 0; i < n; i++)
                x[i] = Math.Sin(2 * Math.PI * i / n);

            FourierTransform.FFT(x, xi);

            // 振幅スペクトルを計算
            double[] amp = new double[n];
            for (int i = 0; i < n; i++)
                amp[i] = Math.Sqrt(x[i] * x[i] + xi[i] * xi[i]);

            // f=1（インデックス1）とf=N-1（インデックス7）に最大成分が現れる
            Assert.True(amp[1] > amp[0], $"amp[1]={amp[1]} should be greater than amp[0]={amp[0]}");
            Assert.True(amp[1] > amp[2], $"amp[1]={amp[1]} should be greater than amp[2]={amp[2]}");
        }

        #endregion

    }
}
