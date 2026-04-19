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
  /// Provides static methods for linear algebra operations.
  /// References:
  /// Oguni, T. "New Numerical Analysis";
  /// Kitagawa, G. "Introduction to Time Series Analysis";
  /// "Numerical Recipes in C".
  /// </summary>
  public static class LinearAlgebraOperations
  {

    #region LU分解

    /// <summary>Solves the linear system [A][x] = [b] for x.</summary>
    /// <param name="aMatrix">Coefficient matrix [A].</param>
    /// <param name="bVector">Input: vector [b]. Output: solution vector [x].</param>
    public static void SolveLinearEquations(IMatrix aMatrix, IVector bVector)
    {
      int[] perm = new int[aMatrix.Rows];     //置換ベクトル
      IVector wArray = new Vector(aMatrix.Rows);   //作業用記憶領域
      LUDecompose(aMatrix, perm, wArray);
      FAndBSubstitute(aMatrix, perm, bVector);
    }

    /// <summary>Performs LU decomposition (A = LU) using Crout's method.</summary>
    /// <param name="matrix">
    /// Input: square matrix to decompose.
    /// Output: the upper triangle and diagonal contain U; the lower triangle contains L.
    /// </param>
    /// <param name="perm">Row permutation vector produced by partial pivoting.</param>
    /// <param name="wArray">Working storage (length equal to the number of rows).</param>
    /// <remarks>Adapted from "Numerical Recipes".</remarks>
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

    /// <summary>Performs forward and back substitution using an LU-decomposed matrix.</summary>
    /// <param name="luMatrix">Matrix produced by LU decomposition.</param>
    /// <param name="perm">Row permutation vector from the LU decomposition.</param>
    /// <param name="b">Right-hand side vector; overwritten with the solution.</param>
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

    /// <summary>Computes the inverse of <paramref name="mA"/>.</summary>
    /// <param name="mA">Matrix to invert.</param>
    /// <param name="mB">Output matrix that receives the inverse.</param>
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
    /// Solves a tridiagonal linear system using the Thomas algorithm:
    /// abc(0,i)*nx(i-1) + abc(1,i)*nx(i) + abc(2,i)*nx(i+1) = x(i).
    /// </summary>
    /// <param name="abc">Coefficient matrix (sub-, main-, and super-diagonals).</param>
    /// <param name="x">Right-hand side vector; overwritten with the solution.</param>
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

    /// <summary>Computes regression coefficients by the least-squares method.</summary>
    /// <param name="y">Response variable vector.</param>
    /// <param name="x">Predictor matrix (one sample per row).</param>
    /// <returns>Vector of regression coefficients.</returns>
    /// <remarks>Kitagawa, G., "Introduction to Time Series Analysis".</remarks>
    public static double[] LeastSquareFit(double[] y, double[,] x)
    {
      double sig, aic;
      return LeastSquareFit(y, x, out sig, out aic);
    }

    /// <summary>Computes regression coefficients by the least-squares method.</summary>
    /// <param name="y">Response variable vector.</param>
    /// <param name="x">Predictor matrix (one sample per row).</param>
    /// <param name="sigma2">Output: residual variance σ².</param>
    /// <param name="aic">Output: Akaike information criterion.</param>
    /// <returns>Vector of regression coefficients.</returns>
    /// <remarks>Kitagawa, G., "Introduction to Time Series Analysis".</remarks>
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

    /// <summary>Converts the matrix to upper-triangular form by Householder transformations.</summary>
    /// <param name="mA">Matrix to transform (modified in place).</param>
    /// <remarks>Oguni, T., "New Numerical Analysis".</remarks>
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

        Multiply(qi, mA, mA2);
        mA2.CopyTo(mA);
      }

      //下三角部分に0を代入
      for (int i = 1; i < n; i++)
        for (int j = 0; j < Math.Min(m, i); j++)
          mA[i, j] = 0;
    }

    /// <summary>Fits the simple linear regression Y = aX + b.</summary>
    /// <param name="x">Predictor values.</param>
    /// <param name="y">Response values.</param>
    /// <param name="coefA">Output: slope coefficient a.</param>
    /// <param name="coefB">Output: intercept coefficient b.</param>
    /// <remarks>Adapted from "Numerical Recipes".</remarks>
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

    /// <summary>Estimates multiple regression coefficients.</summary>
    /// <param name="y">Response variable vector.</param>
    /// <param name="x">Predictor matrix (one sample per row).</param>
    /// <returns>Vector of multiple regression coefficients.</returns>
    public static double[] EstimateMultipleRegressionCoefficients(double[] y, double[][] x)
    {
      return EstimateMultipleRegressionCoefficients(y, x, out _, out _);
    }

    /// <summary>Estimates multiple regression coefficients with diagnostics.</summary>
    /// <param name="y">Response variable vector.</param>
    /// <param name="x">Predictor matrix (one sample per row).</param>
    /// <param name="sigma2">Output: residual variance σ².</param>
    /// <param name="aic">Output: Akaike information criterion.</param>
    /// <returns>Vector of multiple regression coefficients.</returns>
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

    /// <summary>Estimates multiple regression coefficients with residual sum-of-squares diagnostics.</summary>
    /// <param name="y">Response variable vector.</param>
    /// <param name="x">Predictor matrix (one sample per row).</param>
    /// <param name="sigma2">Output: residual variance σ².</param>
    /// <param name="aic">Output: Akaike information criterion.</param>
    /// <param name="rss">Output: residual sum of squares (RSS).</param>
    /// <returns>Vector of multiple regression coefficients.</returns>
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

    /// <summary>Computes the matrix product C = A * B.</summary>
    /// <param name="mA">Matrix A.</param>
    /// <param name="mB">Matrix B.</param>
    /// <param name="mC">Output matrix C.</param>
    public static void Multiply(IMatrix mA, IMatrix mB, IMatrix mC)
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

    /// <summary>Computes the matrix-vector combination vC = α * mA * vB + β * vC.</summary>
    /// <param name="mA">Matrix A.</param>
    /// <param name="vB">Vector B.</param>
    /// <param name="vC">Vector C; overwritten with the result.</param>
    /// <param name="alpha">Coefficient α of the first term.</param>
    /// <param name="beta">Coefficient β of the second term.</param>
    public static void Multiply(IMatrix mA, IVector vB, IVector vC, double alpha, double beta)
    {
      for (int i = 0; i < mA.Rows; i++)
      {
        double buff = 0;
        for (int j = 0; j < mA.Columns; j++)
          buff += mA[i, j] * vB[j];
        vC[i] = vC[i] * beta + alpha * buff;
      }
    }

    /// <summary>Computes the matrix combination mB = cA * mA + cB * mB.</summary>
    /// <param name="mA">Matrix A.</param>
    /// <param name="mB">Matrix B; overwritten with the result.</param>
    /// <param name="cA">Coefficient of the first term.</param>
    /// <param name="cB">Coefficient of the second term.</param>
    public static void Add(IMatrix mA, IMatrix mB, double cA, double cB)
    {
      for (int i = 0; i < mA.Rows; i++)
        for (int j = 0; j < mA.Columns; j++)
          mB[i, j] = mA[i, j] * cA + mB[i, j] * cB;
    }

    /// <summary>Computes the matrix sum mB = mA + mB.</summary>
    /// <param name="mA">Matrix A.</param>
    /// <param name="mB">Matrix B; overwritten with the result.</param>
    public static void Add(IMatrix mA, IMatrix mB)
    {
      for (int i = 0; i < mA.Rows; i++)
        for (int j = 0; j < mA.Columns; j++)
          mB[i, j] = mA[i, j] + mB[i, j];
    }

    /// <summary>Computes the matrix difference mB = mA - mB.</summary>
    /// <param name="mA">Matrix A.</param>
    /// <param name="mB">Matrix B; overwritten with the result.</param>
    public static void Subtract(IMatrix mA, IMatrix mB)
    {
      for (int i = 0; i < mA.Rows; i++)
        for (int j = 0; j < mA.Columns; j++)
          mB[i, j] = mA[i, j] - mB[i, j];
    }

    #endregion

  }
}
