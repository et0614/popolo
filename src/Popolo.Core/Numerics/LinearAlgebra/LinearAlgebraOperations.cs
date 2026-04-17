/* LinearAlgebra.cs
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
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using Popolo.Core.Exceptions;
using System;

namespace Popolo.Core.Numerics.LinearAlgebra
{
  /// <summary>
  /// 線形代数処理に関する静的メソッドを提供する
  /// 参考文献:
  /// 小国力　新数値計算法
  /// 北川源四郎　時系列解析入門
  /// ニューメリカルレシピ in C
  /// </summary>
  public static class LinearAlgebraOperations
  {

    #region LU分解

    /// <summary>連立一次方程式[A][x]=[b]を解く</summary>
    /// <param name="aMatrix">係数行列[A]</param>
    /// <param name="bVector">入力：ベクトル[b]、出力：ベクトル[x]</param>
    public static void SolveLinearEquations(IMatrix aMatrix, IVector bVector)
    {
      int[] perm = new int[aMatrix.Rows];     //置換ベクトル
      IVector wArray = new Vector(aMatrix.Rows);   //作業用記憶領域
      LUDecompose(aMatrix, perm, wArray);
      FAndBSubstitute(aMatrix, perm, bVector);
    }

    /// <summary>Crout法によりLU分解（A=LU）を行う</summary>
    /// <param name="matrix">
    /// 入力：LU分解を行う正方行列
    /// 出力：上三角+対角成分-U行列、下三角成分-L行列</param>
    /// <param name="perm">pivot選択による行置換ベクトル</param>
    /// <param name="wArray">作業用記憶領域（行数）</param>
    /// <remarks>Newmerical Recipiesより移植</remarks>
    public static void LUDecompose(IMatrix matrix, int[] perm, IVector wArray)
    {
      //行列の行数・列数を取得
      int num = matrix.Rows;

      for (int i = 0; i < num; i++)
      {
        double big = 0.0;
        for (int j = 0; j < num; j++)
          big = Math.Max(big, Math.Abs(matrix[i, j]));
        if (big < 1e-30) throw new PopoloNumericalException(
          "LUDecompose",
          $"Singular matrix detected at row {i}. All elements in the row are zero.");
        wArray[i] = 1.0 / big;
      }

      for (int j = 0; j < num; j++)
      {
        double sum = 0;
        double big = 0.0d;
        int imax = 0;

        //Crout法を適用
        //i~jまでの繰り返し計算
        for (int i = 0; i < j; i++)
        {
          sum = -matrix[i, j];
          for (int k = 0; k < i; k++) sum += matrix[i, k] * matrix[k, j];
          matrix[i, j] = -sum;
        }

        //j~Nまでの繰り返し計算
        for (int i = j; i < num; i++)
        {
          sum = -matrix[i, j];
          for (int k = 0; k < j; k++) sum += matrix[i, k] * matrix[k, j];
          matrix[i, j] = -sum;

          //スケーリングを考慮して最大のpivot選択を行う
          double dum = wArray[i] * Math.Abs(sum);
          if (big <= dum)
          {
            big = dum;
            imax = i;
          }
        }

        //行交換の必要判定
        if (j != imax)
        {
          //行の交換
          for (int k = 0; k < num; k++)
          {
            double dum = matrix[imax, k];
            matrix[imax, k] = matrix[j, k];
            matrix[j, k] = dum;
          }
          wArray[imax] = wArray[j];   //スケーリング係数の交換
        }
        //行の置換を記憶
        perm[j] = imax;

        if (matrix[j, j] == 0.0) matrix[j, j] = double.MinValue;

        if (j != num)
        {
          double bf = 1d / matrix[j, j];
          for (int i = j + 1; i < num; i++) matrix[i, j] *= bf;
        }          
      }
    }

    /// <summary>LU行列にもとづき前進・後退代入処理を行う</summary>
    /// <param name="luMatrix">LU分解済の行列</param>
    /// <param name="perm">置換ベクトル</param>
    /// <param name="b">bベクトル：解が上書きされる</param>
    public static void FAndBSubstitute(IMatrix luMatrix, int[] perm, IVector b)
    {
      //行列の行数・列数を取得
      int num = luMatrix.Rows;

      //ベクトルbが0以外の数値をとる位置
      int ii = 0;

      for (int i = 0; i < num; i++)
      {
        //置換ベクトルに従ってbベクトルを入替え
        int ip = perm[i];
        double sum = b[ip];
        b[ip] = b[i];

        //前進代入処理
        if (ii != 0)
          for (int j = ii - 1; j < i; j++) sum -= luMatrix[i, j] * b[j];
        else
          if (sum != 0) ii = i + 1;
        b[i] = sum;
      }
      //後退代入処理
      for (int i = num - 1; 0 <= i; i--)
      {
        double sum = b[i];
        for (int j = i + 1; j < num; j++) sum -= luMatrix[i, j] * b[j];
        b[i] = sum / luMatrix[i, i];
      }
    }

    /// <summary>mAの逆行列を計算する</summary>
    /// <param name="mA">逆行列を求める行列</param>
    /// <param name="mB">出力:逆行列</param>
    public static void GetInverse(IMatrix mA, IMatrix mB)
    {      
      if (mA.Columns == 1)
      {
        mB[0, 0] = 1d / mA[0, 0];
        return;
      }

      int[] wA1 = new int[mA.Rows];
      IVector wA2 = new Vector(mA.Rows);
      LUDecompose(mA, wA1, wA2);
      for (int i = 0; i < mA.Rows; i++)
      {
        for (int j = 0; j < wA2.Length; j++) wA2[j] = 0;
        wA2[i] = 1;
        FAndBSubstitute(mA, wA1, wA2);
        for (int j = 0; j < wA2.Length; j++) mB[j, i] = wA2[j];
      }
    }

    #endregion

    #region 帯行列関連

    /// <summary>
    /// Thomas algorithmで三重対角行列連立一次方程式を解く
    /// abc(0,i)*nx(i-1)+abc(1,i)*nx(i)+abc(2,i)*nx(i+1)=x(i)
    /// </summary>
    /// <param name="abc">係数行列</param>
    /// <param name="x">解で上書きされる</param>
    public static void SolveTridiagonalMatrix(IMatrix abc, IVector x)
    {
      int num = abc.Columns - 1;
      abc[2, 0] /= abc[1, 0];
      x[0] /= abc[1, 0];

      for (int i = 1; i < num; i++)
      {
        abc[2, i] /= abc[1, i] - abc[0, i] * abc[2, i - 1];
        x[i] = (x[i] - abc[0, i] * x[i - 1]) / (abc[1, i] - abc[0, i] * abc[2, i - 1]);
      }

      x[num] = (x[num] - abc[0, num] * x[num - 1]) / (abc[1, num] - abc[0, num] * abc[2, num - 1]);
      for (int i = num - 1; 0 <= i; i--) x[i] -= abc[2, i] * x[i + 1];
    }

    #endregion

    #region 最小二乗法

    /// <summary>最小二乗法で回帰係数を計算する</summary>
    /// <param name="y">目的変数ベクトル</param>
    /// <param name="x">説明変数行列（1行1サンプル）</param>
    /// <returns>回帰係数ベクトル</returns>
    /// <remarks>北川源四郎　時系列解析入門</remarks>
    public static double[] LeastSquareFit(double[] y, double[,] x)
    {
      double sig, aic;
      return LeastSquareFit(y, x, out sig, out aic);
    }

    /// <summary>最小二乗法で回帰係数を計算する</summary>
    /// <param name="y">目的変数ベクトル</param>
    /// <param name="x">説明変数行列（1行1サンプル）</param>
    /// <param name="sigma2">出力：残差分散σ^2</param>
    /// <param name="aic">出力：赤池情報量</param>
    /// <returns>回帰係数ベクトル</returns>
    /// <remarks>北川源四郎　時系列解析入門</remarks>
    public static double[] LeastSquareFit
      (double[] y, double[,] x, out double sigma2, out double aic)
    {
      int col = y.Length; //データの数
      int row = x.GetLength(1); //説明変数の数

      IMatrix s = new Matrix(col, row + 1);
      for (int i = 0; i < col; i++)
      {
        s[i, row] = y[i];
        for (int j = 0; j < row; j++)
          s[i, j] = x[i, j];
      }

      MakeUpperTriangularMatrix(ref s);
      //残差分散
      sigma2 = s[row, row] * s[row, row] / col;

      double[] a = new double[row];
      for (int i = row - 1; 0 <= i; i--)
      {
        double ss = s[i, row];
        for (int j = row - 1; i < j; j--)
          ss -= s[i, j] * a[j];
        a[i] = ss / s[i, i];
      }

      //赤池情報量規準
      aic = col * (Math.Log(2 * Math.PI * sigma2) + 1) + 2 * (row + 1);
      return a;
    }

    /// <summary>ハウスホルダ変換により上三角行列を作成する</summary>
    /// <param name="mA">変換対象行列(上書きされる)</param>
    /// <remarks>小国力　新数値計算法</remarks>
    public static void MakeUpperTriangularMatrix(ref IMatrix mA)
    {
      int n = mA.Rows;
      int m = mA.Columns;
      IMatrix qi = new Matrix(n, n);
      Matrix mA2 = new Matrix(n, m);
      double[] wi = new double[n];

      for (int i = 0; i < Math.Min(n, m); i++)
      {
        double sig2 = 0;
        for (int j = 0; j < i; j++) wi[j] = 0;
        for (int j = i; j < n; j++)
        {
          sig2 += mA[j, i] * mA[j, i];
          wi[j] = mA[j, i];
        }
        double sig = Math.Sqrt(sig2);
        double sn = Math.Sign(wi[i]);
        wi[i] += sn * sig;

        double alpha = 1d / (sig2 + sn * sig * mA[i, i]);

        for (int j = 0; j < n; j++)
        {
          for (int k = 0; k < n; k++)
          {
            qi[j, k] = -alpha * wi[j] * wi[k];
            if (j == k) qi[j, k] += 1;
          }
        }

        Multiplicate(qi, mA, mA2);
        mA2.CopyTo(mA);
      }

      //下三角部分に0を代入
      for (int i = 1; i < n; i++)
        for (int j = 0; j < Math.Min(m, i); j++)
          mA[i, j] = 0;
    }

    /// <summary>Y=aX+bの単回帰係数を求める</summary>
    /// <param name="x">説明変数配列</param>
    /// <param name="y">目的変数配列</param>
    /// <param name="coefA">出力：係数a</param>
    /// <param name="coefB">出力：係数b</param>
    /// <remarks>ニューメリカルレシピより</remarks>
    public static void FitAxPlusB
      (double[] x, double[] y, out double coefA, out double coefB)
    {
      int num = x.Length;
      double sx = 0;
      double sy = 0;
      for (int i = 0; i < num; i++)
      {
        sx += x[i];
        sy += y[i];
      }
      double sxoss = sx / num;
      double st2 = 0;
      coefA = 0;
      for (int i = 0; i < num; i++)
      {
        double t = x[i] - sxoss;
        st2 += t * t;
        coefA += t * y[i];
      }
      coefA /= st2;
      coefB = (sy - sx * coefA) / num;
    }

    /// <summary>重回帰係数を求める</summary>
    /// <param name="y">目的変数ベクトル</param>
    /// <param name="x">説明変数ベクトル（1行1サンプル）</param>
    /// <returns>重回帰係数</returns>
    public static double[] EstimateMultipleRegressionCoefficients(double[] y, double[][] x)
    {
      return EstimateMultipleRegressionCoefficients(y, x, out _, out _);
    }

    /// <summary>重回帰係数を求める</summary>
    /// <param name="y">目的変数ベクトル</param>
    /// <param name="x">説明変数ベクトル（1行1サンプル）</param>
    /// <param name="sigma2">出力：残差分散σ^2</param>
    /// <param name="aic">出力：赤池情報量</param>
    /// <returns>重回帰係数</returns>
    public static double[] EstimateMultipleRegressionCoefficients(double[] y, double[][] x, out double sigma2, out double aic)
    {
      int sampleNum = y.Length;
      if (sampleNum != x.Length)
        throw new PopoloArgumentException(
          $"The number of data points in y ({sampleNum}) and x ({x.Length}) must be the same.",
        nameof(x));

      int predictorNum = x[0].Length;
      double[,] xn = new double[sampleNum, predictorNum];

      for (int i = 0; i < sampleNum; i++)
      {
        if(predictorNum != x[i].Length)
          throw new PopoloArgumentException(
            $"The number of predictors in x[{i}] ({x[i].Length}) must be {predictorNum}.",
            nameof(x));

        for (int j = 0; j < predictorNum; j++) xn[i, j] = x[i][j];
      }
      return LeastSquareFit(y, xn, out sigma2, out aic);
    }

    /// <summary>重回帰係数を求める</summary>
    /// <param name="y">目的変数ベクトル</param>
    /// <param name="x">説明変数ベクトル（1行1サンプル）</param>
    /// <param name="sigma2">出力：残差分散σ^2</param>
    /// <param name="aic">出力：赤池情報量</param>
    /// <param name="rss">出力:残差平方和（RSS: Residual Sum of Squares）</param>
    /// <returns>重回帰係数</returns>
    public static double[] EstimateMultipleRegressionCoefficients(double[] y, double[][] x, out double sigma2, out double aic, out double rss) {
      double[] weight = EstimateMultipleRegressionCoefficients(y, x, out sigma2, out aic);

      rss = 0.0;
      for (int i = 0; i < y.Length; i++)
      {
        double y_hat_i = 0.0;
        for (int j = 0; j < x[0].Length; j++) y_hat_i += x[i][j] * weight[j];
        double error_i = y[i] - y_hat_i;
        rss += error_i * error_i;
      }

      return weight;
    }

    #endregion

    #region 行列・ベクトル演算

    /// <summary>行列の積(C=AB)を計算する</summary>
    /// <param name="mA">A行列</param>
    /// <param name="mB">B行列</param>
    /// <param name="mC">C行列</param>
    public static void Multiplicate(IMatrix mA, IMatrix mB, IMatrix mC)
    {
      mC.Initialize(0);
      for (int i = 0; i < mA.Rows; i++)
      {
        for (int j = 0; j < mA.Columns; j++)
        {
          double smA = mA[i, j];
          if (smA != 0)
            for (int k = 0; k < mB.Columns; k++)
              mC[i, k] += smA * mB[j, k];
        }
      }
    }

    /// <summary>行列とベクトルの積和を計算する（vC = α mA vB + β vC）</summary>
    /// <param name="mA">行列A</param>
    /// <param name="vB">ベクトルB</param>
    /// <param name="vC">ベクトルC（解が上書きされる）</param>
    /// <param name="alpha">第一項の係数</param>
    /// <param name="beta">第二項の係数</param>
    public static void Multiplicate(IMatrix mA, IVector vB, IVector vC, double alpha, double beta)
    {
      for (int i = 0; i < mA.Rows; i++)
      {
        double buff = 0;
        for (int j = 0; j < mA.Columns; j++)
          buff += mA[i, j] * vB[j];
        vC[i] = vC[i] * beta + alpha * buff;
      }
    }

    /// <summary>行列の和を計算する（mB = cA*mA + cB*mB）</summary>
    /// <param name="mA">A行列</param>
    /// <param name="mB">B行列（解が上書きされる）</param>
    /// <param name="cA">第一項の係数</param>
    /// <param name="cB">第二項の係数</param>
    public static void Add(IMatrix mA, IMatrix mB, double cA, double cB)
    {
      for (int i = 0; i < mA.Rows; i++)
        for (int j = 0; j < mA.Columns; j++)
          mB[i, j] = mA[i, j] * cA + mB[i, j] * cB;
    }

    /// <summary>行列の和を計算する（mB = mA + mB）</summary>
    /// <param name="mA">A行列</param>
    /// <param name="mB">B行列（解が上書きされる）</param>
    public static void Add(IMatrix mA, IMatrix mB)
    {
      for (int i = 0; i < mA.Rows; i++)
        for (int j = 0; j < mA.Columns; j++)
          mB[i, j] = mA[i, j] + mB[i, j];
    }

    /// <summary>行列の差を計算する（mB = mA - mB）</summary>
    /// <param name="mA">A行列</param>
    /// <param name="mB">B行列（解が上書きされる）</param>
    public static void Subtract(IMatrix mA, IMatrix mB)
    {
      for (int i = 0; i < mA.Rows; i++)
        for (int j = 0; j < mA.Columns; j++)
          mB[i, j] = mA[i, j] - mB[i, j];
    }

    #endregion

  }
}
