/* MultiMinimization.cs
 *
 * Copyright (C) 2016 E.Togashi
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
using Popolo.Core.Exceptions;
using Popolo.Core.Numerics.LinearAlgebra;

namespace Popolo.Core.Numerics
{
  /// <summary>多変数非線形関数の最小化処理クラス</summary>
  public static class MultiMinimization
  {

    #region デリゲート

    /// <summary>最小化する関数</summary>
    /// <param name="vecX">入力ベクトル</param>
    /// <param name="iter">反復回数</param>
    /// <returns>評価値</returns>
    public delegate double MinimizeFunction(IVector vecX, int iter);

    #endregion

    #region プロパティ

    /// <summary>数値微分のための差分評価係数</summary>
    public static double Delta { get; set; } = 1e-7;

    #endregion

    #region ニュートン法

    /// <summary>ニュートン法で極小値を探索する</summary>
    /// <param name="vecX">初期値ベクトル　出力：収束値</param>
    /// <param name="mFnc">探索する関数</param>
    /// <param name="maxIteration">最大反復回数</param>
    /// <param name="rErrXVal">入力変化率の収束条件</param>
    /// <param name="rErrFVal">関数評価値変化率の収束条件</param>
    /// <param name="rErrDel">微分値の収束条件</param>
    /// <param name="iteration">出力：反復回数</param>
    /// <returns>探索成功の場合 true</returns>
    public static bool Newton(
        ref IVector vecX, MinimizeFunction mFnc, int maxIteration,
        double rErrXVal, double rErrFVal, double rErrDel, out int iteration)
    {
      int num = vecX.Length;
      IVector dir = new Vector(num);
      IVector dif1 = new Vector(num);
      IVector dif2 = new Vector(num);
      IMatrix hessian = new Matrix(num, num);
      int sNum = 0;
      iteration = 1;

      double lstF = mFnc(vecX, iteration);

      while (true)
      {
        if (iteration == maxIteration) return false;

        double alpha;
        double fx1 = mFnc(vecX, iteration);
        GetDiff(fx1, ref vecX, ref dif1, mFnc, iteration);

        bool isSingular = false;
        for (int i = 0; i < num; i++)
          if (dif1[i] == 0) isSingular = true;

        if (isSingular)
        {
          for (int i = 0; i < num; i++) dir[i] = -dif1[i];
          LineSearch(vecX, dir, dif1, mFnc, iteration, out alpha, out fx1);
        }
        else
        {
          for (int i = 0; i < num; i++)
          {
            double tmp = vecX[i];
            double dx = Math.Abs(tmp) * Delta;
            if (dx < 1e-8) dx = 1e-8;
            vecX[i] += dx;
            double fx2 = mFnc(vecX, iteration);
            GetDiff(fx2, ref vecX, ref dif2, mFnc, iteration);
            for (int j = 0; j < num; j++) hessian[i, j] = (dif2[j] - dif1[j]) / dx;
            vecX[i] = tmp;
          }

          for (int i = 0; i < num; i++) dir[i] = -dif1[i];
          LinearAlgebraOperations.SolveLinearEquations(hessian, dir);
          LineSearch(vecX, dir, dif1, mFnc, iteration, out alpha, out fx1);

          if (alpha == 0)
          {
            for (int i = 0; i < num; i++) dir[i] = -dif1[i];
            LineSearch(vecX, dir, dif1, mFnc, iteration, out alpha, out fx1);
          }
        }

        if (alpha == 0)
        {
          for (int i = 0; i < num; i++)
            if (i != sNum) dir[i] = 0;
          if (dir[sNum] == 0) dir[sNum] = (iteration % 2 == 1) ? 1 : -1;
          LineSearch(vecX, dir, dif1, mFnc, iteration, out alpha, out fx1);
          if (alpha == 0) sNum++;
          else sNum = 0;
          if (sNum == num) return false;
        }

        bool cnvgd = true;
        for (int i = 0; i < num; i++)
        {
          double dX = alpha * dir[i];
          vecX[i] += dX;
          if (rErrXVal < Math.Abs(vecX[i])) dX /= vecX[i];
          if (rErrXVal < Math.Abs(dX)) cnvgd = false;
          if (rErrDel < Math.Abs(dif1[i])) cnvgd = false;
        }
        double prvF = mFnc(vecX, iteration);
        if (Math.Abs(lstF - prvF) < rErrFVal && cnvgd) return true;
        lstF = prvF;
        iteration++;
      }
    }

    #endregion

    #region 準ニュートン法

    /// <summary>準ニュートン法（BFGS）で極小値を探索する</summary>
    /// <param name="vecX">初期値ベクトル　出力：収束値</param>
    /// <param name="mFnc">探索する関数</param>
    /// <param name="maxIteration">最大反復回数</param>
    /// <param name="rErrXVal">入力変化率の収束条件</param>
    /// <param name="rErrFVal">関数評価値変化率の収束条件</param>
    /// <param name="rErrDel">微分値の収束条件</param>
    /// <param name="iteration">出力：反復回数</param>
    /// <returns>探索成功の場合 true</returns>
    public static bool QuasiNewton(
        ref IVector vecX, MinimizeFunction mFnc, int maxIteration,
        double rErrXVal, double rErrFVal, double rErrDel, out int iteration)
    {
      iteration = 1;
      int num = vecX.Length;
      int nDir = 0;
      int locNum = 0;
      IVector dif = new Vector(num);
      double[] dif2 = new double[num];
      IVector dir = new Vector(num);
      IVector dir2 = new Vector(num);
      double[] qk = new double[num];
      double[] pk = new double[num];
      double[] qhk = new double[num];
      double[] hqk = new double[num];
      double[,] hINV = new double[num, num];

      double fx = mFnc(vecX, iteration);
      GetDiff(fx, ref vecX, ref dif, mFnc, iteration);
      for (int i = 0; i < num; i++) dir[i] = -dif[i];
      bool needInit = true;

      while (true)
      {
        if (maxIteration < iteration) return false;

        if (needInit || (iteration % (2 * num) == 0))
        {
          for (int i = 0; i < num; i++)
          {
            for (int j = 0; j < num; j++) hINV[i, j] = 0;
            hINV[i, i] = 1.0;
          }
          needInit = false;
        }

        for (int i = 0; i < num; i++)
        {
          double bf = 0;
          for (int j = 0; j < num; j++) bf += hINV[i, j] * dif[j];
          dir[i] = -bf;
        }

        double wk = 0;
        for (int i = 0; i < num; i++) wk += dir[i] * dif[i];
        if (0 <= wk) needInit = true;
        else
        {
          double alpha = 0;
          double fx2;
          LineSearch(vecX, dir, dif, mFnc, iteration, out alpha, out fx2);

          if (alpha == 0 && fx2 != 0)
          {
            if (dif[nDir] == 0) dir2[nDir] = (iteration % 2 == 1) ? 1 : -1;
            else dir2[nDir] = -dif[nDir];
            LineSearch(vecX, dir2, dif, mFnc, iteration, out alpha, out fx2);
            if (alpha == 0)
            {
              locNum++;
              if (locNum == num) return false;
            }
            else
            {
              locNum = 0;
              vecX[nDir] += dir2[nDir] * alpha;
              fx = fx2;
              GetDiff(fx, ref vecX, ref dif, mFnc, iteration);
            }
            dir2[nDir] = 0;
            nDir++;
            if (num == nDir) nDir = 0;
            needInit = true;
          }
          else
          {
            locNum = 0;
            for (int i = 0; i < num; i++)
            {
              pk[i] = dir[i] * alpha;
              vecX[i] += pk[i];
              dif2[i] = dif[i];
            }

            GetDiff(fx2, ref vecX, ref dif, mFnc, iteration);
            for (int i = 0; i < num; i++) qk[i] = dif[i] - dif2[i];
            double delt = Math.Abs(fx2 - fx);
            double mean = 0.5 * (Math.Abs(fx2) + Math.Abs(fx));
            if (mean * rErrFVal < delt || delt < 1e-10)
            {
              double maxDX = 0;
              double maxDF = 0;
              for (int i = 0; i < num; i++)
              {
                double bf = Math.Abs(pk[i]);
                if (rErrXVal < Math.Abs(vecX[i])) bf /= Math.Abs(vecX[i]);
                maxDX = Math.Max(maxDX, bf);
                maxDF = Math.Max(maxDF, Math.Abs(dif[i]));
              }
              if (maxDF < rErrDel && maxDX < rErrXVal) return true;
            }
            fx = fx2;

            double qhqC = 0;
            double pqC = 0;
            for (int i = 0; i < num; i++)
            {
              hqk[i] = 0;
              for (int j = 0; j < num; j++) hqk[i] += hINV[i, j] * qk[j];
              qhqC += hqk[i] * qk[i];
              pqC += pk[i] * qk[i];
            }
            double pqInv = 1.0 / pqC;
            double qhqInv = 1.0 / qhqC;
            double gm = pqC * qhqInv;
            for (int i = 0; i < num; i++)
              for (int j = 0; j < num; j++)
                hINV[i, j] = (hINV[i, j]
                    - hqk[i] * hqk[j] * qhqInv
                    + qhqC * (pk[i] * pqInv - hqk[i] * qhqInv)
                    * (pk[j] * pqInv - hqk[j] * qhqInv)) * gm
                    + pk[i] * pk[j] * pqInv;
          }
        }
        iteration++;
      }
    }

    #endregion

    #region 非公開メソッド

    /// <summary>数値微分を計算する</summary>
    private static void GetDiff(
        double fx, ref IVector vecX, ref IVector dif,
        MinimizeFunction mFnc, int iter)
    {
      int num = vecX.Length;
      for (int i = 0; i < num; i++)
      {
        double tmp = vecX[i];
        double dx = Math.Abs(tmp) * Delta;
        if (dx < 1e-8) dx = 1e-8;
        vecX[i] += dx;
        dif[i] = (mFnc(vecX, iter) - fx) / dx;
        vecX[i] = tmp;
      }
    }

    /// <summary>直線探索を行う</summary>
    private static void LineSearch(
        IVector vecX, IVector dir, IVector dif, MinimizeFunction mFnc,
        int iteration, out double alpha, out double fvecX)
    {
      const double RHO = 0.4;
      int num = dir.Length;

      alpha = 0;
      for (int i = 0; i < num; i++) alpha += dir[i] * dir[i];
      alpha = 1.0 / (3.0 * Math.Sqrt(alpha));

      double agL = 0;
      for (int i = 0; i < num; i++) agL += dir[i] * dif[i];
      double agR = agL * RHO;
      agL *= (1 - RHO);

      double fval0, fvalA;
      double dstpA = 0;
      fval0 = fvalA = EvalAlpha(0, vecX, dir, mFnc, iteration);

      double fvalC, dstpC;
      double dstpB = alpha;
      double fvalB = EvalAlpha(dstpB, vecX, dir, mFnc, iteration);
      if (fvalA < fvalB)
      {
        dstpC = dstpB;
        fvalC = fvalB;
        int iter = 0;
        while (true)
        {
          dstpB /= 4.0;
          fvalB = EvalAlpha(dstpB, vecX, dir, mFnc, iteration);
          if (fvalB < fvalA) break;
          iter++;
          if (100 < iter)
          {
            alpha = dstpA;
            fvecX = fvalA;
            return;
          }
        }
      }
      else
      {
        dstpC = dstpB * 2;
        fvalC = EvalAlpha(dstpC, vecX, dir, mFnc, iteration);
        int iter = 0;
        while (fvalC < fvalB)
        {
          dstpA = dstpB;
          fvalA = fvalB;
          dstpB = dstpC;
          fvalB = fvalC;
          dstpC = dstpB * 2;
          fvalC = EvalAlpha(dstpC, vecX, dir, mFnc, iteration);
          iter++;
          if (100 < iter)
            throw new PopoloNumericalException(
                "LineSearch",
                $"Convergence failed after {iter} iterations. "
                + $"dstpB={dstpB}, fvalB={fvalB}.");
        }
      }

      int witer = 0;
      while (true)
      {
        double bf1 = (dstpC - dstpB) * fvalA;
        double bf2 = (dstpA - dstpC) * fvalB;
        double bf3 = (dstpB - dstpA) * fvalC;
        double denom = bf1 + bf2 + bf3;
        if (denom < 1e-10) alpha = 0.5 * (dstpA + dstpC);
        else
        {
          double numen = (dstpC + dstpB) * bf1
              + (dstpA + dstpC) * bf2
              + (dstpB + dstpA) * bf3;
          alpha = 0.5 * numen / denom;
        }
        if (alpha < dstpA || dstpC < alpha)
        {
          alpha = dstpB;
          fvecX = fvalB;
          return;
        }

        fvecX = EvalAlpha(alpha, vecX, dir, mFnc, iteration);
        if (fvecX <= fval0 + agR * alpha && fval0 + agL * alpha < fvecX) return;

        if (alpha < dstpB)
        {
          if (fvecX < fvalB) { dstpC = dstpB; fvalC = fvalB; dstpB = alpha; fvalB = fvecX; }
          else { dstpA = alpha; fvalA = fvecX; }
        }
        else
        {
          if (fvecX < fvalB) { dstpA = dstpB; fvalA = fvalB; dstpB = alpha; fvalB = fvecX; }
          else { dstpC = alpha; fvalC = fvecX; }
        }
        witer++;
        if (100 < witer) return;
      }
    }

    /// <summary>直線探索用の評価関数</summary>
    private static double EvalAlpha(
        double alpha, IVector vecX, IVector dir, MinimizeFunction mFnc, int iter)
    {
      IVector vecX2 = new Vector(vecX.Length);
      for (int i = 0; i < vecX.Length; i++) vecX2[i] = vecX[i] + dir[i] * alpha;
      return mFnc(vecX2, iter);
    }

    #endregion

  }
}
