/* ODESolver.cs
 *
 * Copyright (C) 2014 E.Togashi
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

namespace Popolo.Numerics
{
    /// <summary>常微分方程式ソルバ</summary>
    public static class ODESolver
    {

        #region デリゲート定義

        /// <summary>1変数の微分方程式</summary>
        /// <param name="t">時点</param>
        /// <param name="yt">現在値</param>
        /// <returns>微分値</returns>
        public delegate double DifferentialEquation(double t, double yt);

        /// <summary>連立微分方程式</summary>
        /// <param name="t">時点</param>
        /// <param name="yt">現在値の配列</param>
        /// <param name="dyt">出力：微分値の配列</param>
        public delegate void DifferentialEquations(double t, double[] yt, ref double[] dyt);

        /// <summary>強制終了判定関数</summary>
        /// <param name="t">時点</param>
        /// <param name="yt">現在値</param>
        /// <returns>強制終了する場合は true</returns>
        public delegate bool TerminateProcess(double t, double yt);

        #endregion

        #region 静的フィールドとコンストラクタ

        /// <summary>Runge-Kutta-Fehlberg法の係数</summary>
        private static readonly double[][] RKF45;

        /// <summary>静的コンストラクタ</summary>
        static ODESolver()
        {
            RKF45 = new double[6][];
            RKF45[0] = new double[] { 0 };
            RKF45[1] = new double[] { 1 / 4d, 1 / 4d };
            RKF45[2] = new double[] { 3 / 8d, 3 / 32d, 9 / 32d };
            RKF45[3] = new double[] { 12 / 13d, 1932 / 2197d, -7200 / 2197d, 7296 / 2197d };
            RKF45[4] = new double[] { 1, 439 / 216d, -8, 3680 / 513d, -845 / 4104d };
            RKF45[5] = new double[] { 0.5, -8 / 27d, 2, -3544 / 2565d, 1859 / 4104d, -11 / 40d };
        }

        #endregion

        #region Runge-Kutta法

        /// <summary>4次のRunge-Kutta法で dt 秒後の y(t) を求める</summary>
        /// <param name="dEqn">微分方程式</param>
        /// <param name="dt">タイムステップ</param>
        /// <param name="t">時点</param>
        /// <param name="yt">現在値</param>
        /// <returns>タイムステップ経過後の y(t+dt)</returns>
        public static double SolveRK4(
            DifferentialEquation dEqn, double dt, double t, double yt)
        {
            double dt2 = 0.5 * dt;
            double k1 = dt * dEqn(t, yt);
            double k2 = dt * dEqn(t + dt2, yt + 0.5 * k1);
            double k3 = dt * dEqn(t + dt2, yt + 0.5 * k2);
            double k4 = dt * dEqn(t + dt, yt + k3);
            return yt + (k1 + 2 * (k2 + k3) + k4) / 6.0;
        }

        /// <summary>Runge-Kutta-Gill法で dt 秒後の y(t) を求める</summary>
        /// <param name="dEqn">連立微分方程式</param>
        /// <param name="dt">タイムステップ</param>
        /// <param name="t">時点</param>
        /// <param name="yt0">現在値の配列</param>
        /// <param name="yt1">出力：タイムステップ経過後の y(t+dt)</param>
        /// <exception cref="PopoloArgumentException">
        /// yt0 または yt1 が null もしくは空の場合、あるいは長さが一致しない場合。
        /// </exception>
        public static void SolveRKGill(
            DifferentialEquations dEqn, double dt, double t,
            double[] yt0, ref double[] yt1)
        {
            if (yt0 == null || yt0.Length == 0)
                throw new PopoloArgumentException(
                    "yt0 must not be null or empty.", nameof(yt0));
            if (yt1 == null || yt1.Length == 0)
                throw new PopoloArgumentException(
                    "yt1 must not be null or empty.", nameof(yt1));
            if (yt0.Length != yt1.Length)
                throw new PopoloArgumentException(
                    $"yt0 and yt1 must have the same length. "
                    + $"yt0.Length={yt0.Length}, yt1.Length={yt1.Length}.",
                    nameof(yt1));

            double sq2 = Math.Sqrt(2);
            double dt2 = 0.5 * dt;
            double[] k1 = new double[yt0.Length];
            double[] k2 = new double[yt0.Length];
            double[] k3 = new double[yt0.Length];
            double[] k4 = new double[yt0.Length];

            dEqn(t, yt0, ref k1);
            for (int i = 0; i < yt0.Length; i++)
            {
                k1[i] *= dt;
                yt1[i] = yt0[i] + 0.5 * k1[i];
            }

            dEqn(t + dt2, yt1, ref k2);
            for (int i = 0; i < yt0.Length; i++)
            {
                k2[i] *= dt;
                yt1[i] = yt0[i] + 0.5 * ((sq2 - 1) * k1[i] + (2 - sq2) * k2[i]);
            }

            dEqn(t + dt2, yt1, ref k3);
            for (int i = 0; i < yt0.Length; i++)
            {
                k3[i] *= dt;
                yt1[i] = yt0[i] + 0.5 * (-sq2 * k2[i] + (2 + sq2) * k3[i]);
            }

            dEqn(t + dt, yt1, ref k4);
            for (int i = 0; i < yt0.Length; i++)
            {
                k4[i] *= dt;
                yt1[i] = yt0[i]
                    + (k1[i] + (2 - sq2) * k2[i] + (2 + sq2) * k3[i] + k4[i]) / 6.0;
            }
        }

        /// <summary>Runge-Kutta-Fehlberg法（RKF45）で解を求める</summary>
        /// <param name="dEqn">微分方程式</param>
        /// <param name="tFnc">強制終了判定関数</param>
        /// <param name="dtMax">最大タイムステップ</param>
        /// <param name="t">開始時点</param>
        /// <param name="tend">終了時点</param>
        /// <param name="yt">初期値</param>
        /// <param name="errTol">許容誤差</param>
        /// <returns>終了時点での y(tend)</returns>
        /// <remarks>
        /// http://slpr.sakura.ne.jp/qp/runge-kutta-ex
        /// </remarks>
        public static double SolveRKF45(
            DifferentialEquation dEqn, TerminateProcess tFnc,
            double dtMax, double t, double tend, double yt, double errTol)
        {
            double dt = dtMax;
            double[] k = new double[6];

            while (true)
            {
                for (int i = 0; i < k.Length; i++)
                {
                    double sm = yt;
                    for (int j = 1; j < RKF45[i].Length; j++)
                        sm += RKF45[i][j] * k[j - 1];
                    k[i] = dt * dEqn(t + dt * RKF45[i][0], sm);
                }

                double r = Math.Abs(
                    1 / 360d * k[0]
                    - 128 / 4275d * k[2]
                    - 2197 / 75240d * k[3]
                    + 1 / 50d * k[4]
                    + 2 / 55d * k[5]) / dt;

                if (r < errTol)
                {
                    yt += 25 / 216d * k[0]
                        + 1408 / 2565d * k[2]
                        + 2197 / 4104d * k[3]
                        - 1 / 5d * k[4];
                    t += dt;
                }

                if (tend <= t || tFnc(t, yt)) return yt;

                // r=0 のときゼロ除算を避ける（delta=Infinityとなりdt=4*dtに上限処理される）
                double delta = r > 0
                    ? Math.Pow(errTol / (2 * r), 0.25)
                    : 4.0;

                if (delta <= 0.1) dt = 0.1 * dt;
                else if (4 <= delta) dt = 4 * dt;
                else dt = delta * dt;

                dt = Math.Min(dtMax, dt);
                if (tend < t + dt) dt = tend - t;
            }
        }

        #endregion

    }
}
