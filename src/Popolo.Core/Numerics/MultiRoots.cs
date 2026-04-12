/* MultiRoots.cs
 *
 * Copyright (C) 2015 E.Togashi
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
using Popolo.Exceptions;
using Popolo.Numerics.LinearAlgebra;

namespace Popolo.Numerics
{
    /// <summary>多次元非線形関数の求根を行うクラス</summary>
    public static class MultiRoots
    {
        /// <summary>誤差関数</summary>
        /// <param name="x">入力ベクトル</param>
        /// <param name="fx">出力ベクトル</param>
        public delegate void ErrorFunction(IVector x, ref IVector fx);

        /// <summary>ニュートン法で求根する</summary>
        /// <param name="eFnc">誤差関数</param>
        /// <param name="x">入力：初期値　出力：収束値</param>
        /// <param name="errorTolerance">誤差量許容値</param>
        /// <param name="collectionTolerance">入力変化率の収束条件</param>
        /// <param name="maxIteration">最大反復回数</param>
        /// <param name="iteration">出力：反復回数</param>
        /// <param name="error">出力：最終誤差</param>
        /// <returns>求根成功の場合 true</returns>
        /// <exception cref="PopoloArgumentException">
        /// x が null または空の場合。
        /// </exception>
        public static bool Newton(
            ErrorFunction eFnc, ref IVector x,
            double errorTolerance, double collectionTolerance,
            int maxIteration, out int iteration, out double error)
        {
            if (x == null || x.Length == 0)
                throw new PopoloArgumentException(
                    "x must not be null or empty.", nameof(x));

            iteration = 0;
            int num = x.Length;
            IVector fx = new Vector(num);
            IMatrix jac = new Matrix(num, num);
            eFnc(x, ref fx);
            error = 0;

            while (true)
            {
                if (maxIteration <= iteration) return false;

                ComputeJacobian(eFnc, ref x, fx, ref jac);
                for (int i = 0; i < num; i++) fx[i] = -fx[i];
                LinearAlgebraOperations.SolveLinearEquations(jac, fx);
                for (int i = 0; i < num; i++) x[i] += fx[i];

                double maxCr = 0;
                for (int i = 0; i < num; i++)
                {
                    double bf = fx[i];
                    if (1e-5 < Math.Abs(x[i])) bf /= x[i];
                    else bf /= 1e-5;
                    maxCr = Math.Max(maxCr, Math.Abs(bf));
                }
                eFnc(x, ref fx);
                error = 0;
                for (int i = 0; i < num; i++) error += Math.Abs(fx[i]);
                if (error < errorTolerance && maxCr < collectionTolerance) return true;
                iteration++;
            }
        }

        /// <summary>ニュートン法で求根する（振動防止係数付き）</summary>
        /// <param name="eFnc">誤差関数</param>
        /// <param name="x">入力：初期値　出力：収束値</param>
        /// <param name="errorTolerance">誤差量許容値</param>
        /// <param name="collectionTolerance">入力変化率の収束条件</param>
        /// <param name="maxIteration">最大反復回数</param>
        /// <param name="antiVibrationC">振動防止係数（0.0〜1.0）</param>
        /// <param name="iteration">出力：反復回数</param>
        /// <param name="error">出力：最終誤差</param>
        /// <returns>求根成功の場合 true</returns>
        /// <exception cref="PopoloArgumentException">
        /// x が null または空の場合、あるいは antiVibrationC が範囲外の場合。
        /// </exception>
        public static bool Newton(
            ErrorFunction eFnc, ref IVector x,
            double errorTolerance, double collectionTolerance,
            int maxIteration, double antiVibrationC,
            out int iteration, out double error)
        {
            if (x == null || x.Length == 0)
                throw new PopoloArgumentException(
                    "x must not be null or empty.", nameof(x));
            if (antiVibrationC <= 0.0 || antiVibrationC > 1.0)
                throw new PopoloArgumentException(
                    $"antiVibrationC must be in (0.0, 1.0]. Got: {antiVibrationC}",
                    nameof(antiVibrationC));

            iteration = 0;
            int num = x.Length;
            IVector fx = new Vector(num);
            IMatrix jac = new Matrix(num, num);
            eFnc(x, ref fx);
            error = 0;

            while (true)
            {
                if (maxIteration < iteration) return false;

                ComputeJacobian(eFnc, ref x, fx, ref jac);
                for (int i = 0; i < num; i++) fx[i] = -fx[i];
                LinearAlgebraOperations.SolveLinearEquations(jac, fx);
                for (int i = 0; i < num; i++) x[i] += fx[i] * antiVibrationC;

                double maxCr = 0;
                for (int i = 0; i < num; i++)
                {
                    double bf = fx[i];
                    if (1e-5 < Math.Abs(x[i])) bf /= x[i];
                    else bf /= 1e-5;
                    maxCr = Math.Max(maxCr, Math.Abs(bf));
                }
                eFnc(x, ref fx);
                error = 0;
                for (int i = 0; i < num; i++) error += Math.Abs(fx[i]);
                if (error < errorTolerance && maxCr < collectionTolerance) return true;
                iteration++;
            }
        }

        /// <summary>ヤコビアンを数値的に計算する</summary>
        private static void ComputeJacobian(
            ErrorFunction eFnc, ref IVector x, IVector fx, ref IMatrix jac)
        {
            IVector vec = new Vector(x.Length);
            for (int j = 0; j < x.Length; j++)
            {
                double tmp = x[j];
                double dt = 1e-7 * Math.Abs(tmp);
                if (dt < 1e-8) dt = 1e-8;
                x[j] += dt;
                eFnc(x, ref vec);
                x[j] = tmp;
                for (int i = 0; i < x.Length; i++)
                    jac[i, j] = (vec[i] - fx[i]) / dt;
            }
        }
    }
}
