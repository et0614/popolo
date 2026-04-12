/* LinearAlgebraTests.cs
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
using Popolo.Numerics.LinearAlgebra;
using Popolo.Exceptions;

namespace Popolo.Core.Tests.Numerics.Linearalgebra
{
  /// <summary>Matrix および MatrixView のテスト</summary>
  public class MatrixTests
  {
    /// <summary>行数・列数が正しく設定される</summary>
    [Fact]
    public void Matrix_Constructor_SetsRowsAndColumns()
    {
      var m = new Matrix(3, 4);
      Assert.Equal(3, m.Rows);
      Assert.Equal(4, m.Columns);
    }

    /// <summary>初期値はゼロ</summary>
    [Fact]
    public void Matrix_Constructor_InitializesToZero()
    {
      var m = new Matrix(3, 3);
      for (int i = 0; i < m.Rows; i++)
        for (int j = 0; j < m.Columns; j++)
          Assert.Equal(0.0, m[i, j]);
    }

    /// <summary>要素の読み書きが正しく動作する</summary>
    [Fact]
    public void Matrix_Indexer_GetAndSet()
    {
      var m = new Matrix(2, 2);
      m[0, 0] = 1.5;
      m[1, 1] = 3.7;
      Assert.Equal(1.5, m[0, 0]);
      Assert.Equal(3.7, m[1, 1]);
    }

    /// <summary>Initialize で全要素が指定値に設定される</summary>
    [Fact]
    public void Matrix_Initialize_SetsAllElements()
    {
      var m = new Matrix(3, 3);
      m.Initialize(5.0);
      for (int i = 0; i < m.Rows; i++)
        for (int j = 0; j < m.Columns; j++)
          Assert.Equal(5.0, m[i, j]);
    }

    /// <summary>転置行列が正しく生成される</summary>
    [Fact]
    public void Matrix_MakeTranspose_ReturnsCorrectMatrix()
    {
      var m = new Matrix(2, 3);
      m[0, 0] = 1; m[0, 1] = 2; m[0, 2] = 3;
      m[1, 0] = 4; m[1, 1] = 5; m[1, 2] = 6;

      var t = m.MakeTranspose();
      Assert.Equal(3, t.Rows);
      Assert.Equal(2, t.Columns);
      Assert.Equal(1.0, t[0, 0]);
      Assert.Equal(4.0, t[0, 1]);
      Assert.Equal(3.0, t[2, 0]);
      Assert.Equal(6.0, t[2, 1]);
    }

    /// <summary>MatrixView の行・列範囲外アクセスで例外が発生する</summary>
    [Fact]
    public void MatrixView_OutOfRangeAccess_ThrowsPopoloArgumentException()
    {
      var m = new Matrix(5, 5);
      var view = new MatrixView(m, 3, 3, 0, 0);

      Assert.Throws<PopoloArgumentException>(() => { var _ = view[3, 0]; });
      Assert.Throws<PopoloArgumentException>(() => { var _ = view[0, 3]; });
      Assert.Throws<PopoloArgumentException>(() => { var _ = view[-1, 0]; });
      Assert.Throws<PopoloArgumentException>(() => { var _ = view[0, -1]; });
    }
  }

  /// <summary>Vector および VectorView のテスト</summary>
  public class VectorTests
  {
    /// <summary>ベクトル長が正しく設定される</summary>
    [Fact]
    public void Vector_Constructor_SetsLength()
    {
      var v = new Vector(5);
      Assert.Equal(5, v.Length);
    }

    /// <summary>初期値はゼロ</summary>
    [Fact]
    public void Vector_Constructor_InitializesToZero()
    {
      var v = new Vector(4);
      for (int i = 0; i < v.Length; i++)
        Assert.Equal(0.0, v[i]);
    }

    /// <summary>配列からのコンストラクタが正しくコピーする</summary>
    [Fact]
    public void Vector_Constructor_FromArray_CopiesData()
    {
      var data = new double[] { 1.0, 2.0, 3.0 };
      var v = new Vector(data);
      Assert.Equal(1.0, v[0]);
      Assert.Equal(2.0, v[1]);
      Assert.Equal(3.0, v[2]);

      // 元配列を変更しても影響を受けないことを確認（ディープコピー）
      data[0] = 99.0;
      Assert.Equal(1.0, v[0]);
    }

    /// <summary>ユークリッドノルムが正しく計算される</summary>
    [Fact]
    public void Vector_ComputeEuclideanNorm_ReturnsCorrectValue()
    {
      // [3, 4] のノルム = 5
      var v = new Vector(new double[] { 3.0, 4.0 });
      Assert.Equal(5.0, v.ComputeEuclideanNorm(), precision: 10);
    }

    /// <summary>VectorView の範囲外アクセスで例外が発生する</summary>
    [Fact]
    public void VectorView_OutOfRangeAccess_ThrowsPopoloArgumentException()
    {
      var v = new Vector(new double[] { 1.0, 2.0, 3.0 });
      var view = new VectorView(v, 0, 3);

      Assert.Throws<PopoloArgumentException>(() => { var _ = view[3]; });
      Assert.Throws<PopoloArgumentException>(() => { var _ = view[-1]; });
    }

    /// <summary>VectorView が行列の列ベクトルとして正しく動作する</summary>
    [Fact]
    public void VectorView_FromMatrix_ColumnVector_ReturnsCorrectValues()
    {
      var m = new Matrix(3, 3);
      m[0, 1] = 10.0;
      m[1, 1] = 20.0;
      m[2, 1] = 30.0;

      // 列1のベクトルビュー
      var view = new VectorView(m, false, 0, 1, 3);
      Assert.Equal(10.0, view[0]);
      Assert.Equal(20.0, view[1]);
      Assert.Equal(30.0, view[2]);
    }
  }

  /// <summary>SparseMatrix のテスト</summary>
  public class SparseMatrixTests
  {
    /// <summary>ゼロ以外の初期化で例外が発生する</summary>
    [Fact]
    public void SparseMatrix_Initialize_WithNonZero_ThrowsPopoloArgumentException()
    {
      var m = new SparseMatrix(3, 3);
      Assert.Throws<PopoloArgumentException>(() => m.Initialize(1.0));
    }

    /// <summary>ゼロでの初期化は成功する</summary>
    [Fact]
    public void SparseMatrix_Initialize_WithZero_Succeeds()
    {
      var m = new SparseMatrix(3, 3);
      m[0, 0] = 5.0;
      m.Initialize(0.0);
      Assert.Equal(0.0, m[0, 0]);
    }

    /// <summary>要素の読み書きが正しく動作する</summary>
    [Fact]
    public void SparseMatrix_Indexer_GetAndSet()
    {
      var m = new SparseMatrix(3, 3);
      m[1, 2] = 7.5;
      Assert.Equal(7.5, m[1, 2]);
      Assert.Equal(0.0, m[0, 0]);
    }

    /// <summary>連立一次方程式が正しく解ける</summary>
    [Fact]
    public void SparseMatrix_SolveLinearEquation_ReturnsCorrectSolution()
    {
      // 対角行列 diag(2, 3, 4) の連立方程式 Ax = b
      // b = [2, 6, 8] → 解 x = [1, 2, 2]
      var m = new SparseMatrix(3, 3);
      m[0, 0] = 2.0;
      m[1, 1] = 3.0;
      m[2, 2] = 4.0;

      IVector b = new Vector(new double[] { 2.0, 6.0, 8.0 });
      IVector x = new Vector(3);
      m.SolveLinearEquation(b, ref x);

      Assert.Equal(1.0, x[0], precision: 8);
      Assert.Equal(2.0, x[1], precision: 8);
      Assert.Equal(2.0, x[2], precision: 8);
    }

    /// <summary>特異行列の逆行列変換で例外が発生する</summary>
    [Fact]
    public void SparseMatrix_ConvertToInverseMatrix_SingularMatrix_ThrowsPopoloNumericalException()
    {
      // 全要素ゼロの行列（特異行列）
      var m = new SparseMatrix(3, 3);
      Assert.Throws<PopoloNumericalException>(() => m.ConvertToInverseMatrix());
    }
  }

  /// <summary>LinearAlgebra 静的メソッドのテスト</summary>
  public class LinearAlgebraTests
  {
    private const double Tolerance = 1e-10;

    /// <summary>FitAxPlusB が既知の解を返す</summary>
    [Fact]
    public void FitAxPlusB_ReturnsCorrectCoefficients()
    {
      // 既存テストより：x=[0.1, 0.3, 1.0], y=[2, 4, 20]
      double[] x = { 0.1, 0.3, 1.0 };
      double[] y = { 2, 4, 20 };

      LinearAlgebraOperations.FitAxPlusB(x, y, out double a, out double b);

      Assert.Equal(20.746268656716417, a, precision: 10);
      Assert.Equal(-1.0149253731343275, b, precision: 10);
    }

    /// <summary>LU分解と前進後退代入で連立方程式が正しく解ける</summary>
    [Fact]
    public void LUDecompose_AndFAndBSubstitute_SolvesLinearSystem()
    {
      // 既存テストより：5x5の熱回路方程式
      IMatrix matrix = new Matrix(5, 5);
      IVector b = new Vector(5);
      int[] perm = new int[5];
      IVector wArray = new Vector(5);

      matrix[0, 0] = -28.9; matrix[0, 1] = 8.9;
      matrix[1, 0] = 8.9; matrix[1, 1] = -10.3; matrix[1, 2] = 1.4;
      matrix[2, 1] = 1.4; matrix[2, 2] = -15.7; matrix[2, 3] = 14.3;
      matrix[3, 2] = 14.3; matrix[3, 3] = -32.6; matrix[3, 4] = 18.3;
      matrix[4, 3] = 18.3; matrix[4, 4] = -27.3;
      b[0] = -700; b[4] = -234;

      LinearAlgebraOperations.LUDecompose(matrix, perm, wArray);
      LinearAlgebraOperations.FAndBSubstitute(matrix, perm, b);

      Assert.Equal(34.595444254464532, b[0], precision: 10);
      Assert.Equal(33.686330219553362, b[1], precision: 10);
      Assert.Equal(27.906962426189473, b[2], precision: 10);
      Assert.Equal(27.341150194671329, b[3], precision: 10);
      Assert.Equal(26.899012767856604, b[4], precision: 10);
    }

    /// <summary>特異行列のLU分解で例外が発生する</summary>
    [Fact]
    public void LUDecompose_SingularMatrix_ThrowsPopoloNumericalException()
    {
      IMatrix matrix = new Matrix(3, 3); // 全要素ゼロ（特異行列）
      int[] perm = new int[3];
      IVector wArray = new Vector(3);

      Assert.Throws<PopoloNumericalException>(
          () => LinearAlgebraOperations.LUDecompose(matrix, perm, wArray));
    }

    /// <summary>三重対角行列ソルバーが正しく解ける</summary>
    [Fact]
    public void SolveTridiagonalMatrix_ReturnsCorrectSolution()
    {
      // 既存テストより：LU分解と同じ熱回路方程式
      Matrix abc = new Matrix(3, 5);
      IVector x = new Vector(5);

      abc[0, 0] = 0; abc[1, 0] = -28.9; abc[2, 0] = 8.9;
      abc[0, 1] = 8.9; abc[1, 1] = -10.3; abc[2, 1] = 1.4;
      abc[0, 2] = 1.4; abc[1, 2] = -15.7; abc[2, 2] = 14.3;
      abc[0, 3] = 14.3; abc[1, 3] = -32.6; abc[2, 3] = 18.3;
      abc[0, 4] = 18.3; abc[1, 4] = -27.3; abc[2, 4] = 0;
      x[0] = -700; x[4] = -234;

      LinearAlgebraOperations.SolveTridiagonalMatrix(abc, x);

      Assert.Equal(34.595444254464525, x[0], precision: 10);
      Assert.Equal(33.686330219553355, x[1], precision: 10);
      Assert.Equal(27.906962426189473, x[2], precision: 10);
      Assert.Equal(27.341150194671329, x[3], precision: 10);
      Assert.Equal(26.899012767856604, x[4], precision: 10);
    }

    /// <summary>最小二乗法（過剰決定系）が正しい係数を返す</summary>
    [Fact]
    public void LeastSquareFit_ReturnsCorrectCoefficients()
    {
      // 既存テストより：既知の係数 [4, -2, -8] を持つデータ
      double[,] x = new double[6, 3];
      double[] y = new double[6];
      x[0, 0] = 1.5; x[1, 0] = 2.3; x[2, 0] = 3.8;
      x[3, 0] = 4.2; x[4, 0] = 5.6; x[5, 0] = 6.3;
      x[0, 1] = 1.0; x[1, 1] = 3.9; x[2, 1] = 8.6;
      x[3, 1] = 15.25; x[4, 1] = 28.6; x[5, 1] = 32.68;
      x[0, 2] = 1; x[1, 2] = 1; x[2, 2] = 1;
      x[3, 2] = 1; x[4, 2] = 1; x[5, 2] = 1;
      y[0] = -3.2; y[1] = 2.0; y[2] = 2.2;
      y[3] = 6.3; y[4] = 5.8; y[5] = 12.5;

      double[] a = LinearAlgebraOperations.LeastSquareFit(y, x, out double sig2, out double aic);

      // SIG2=3.6801, AIC=32.845となるはず
      Assert.Equal(3.6801816985972984, sig2, precision: 10);
      Assert.Equal(32.845035151940777, aic, precision: 10);
    }

    /// <summary>MakeUpperTriangularMatrix がQR分解を正しく実行する</summary>
    [Fact]
    public void MakeUpperTriangularMatrix_ReturnsCorrectResult()
    {
      // 既存テストより：5x5の対称行列
      IMatrix mA = new Matrix(5, 5);
      mA[0, 0] = 0.6273; mA[0, 1] = 0.7683; mA[0, 2] = 0.5569;
      mA[0, 3] = 0.5571; mA[0, 4] = 0.3849;
      mA[1, 0] = 0.7683; mA[1, 1] = 0.3716; mA[1, 2] = 0.4683;
      mA[1, 3] = 0.7887; mA[1, 4] = 0.6153;
      mA[2, 0] = 0.5569; mA[2, 1] = 0.4683; mA[2, 2] = 0.7764;
      mA[2, 3] = 0.6480; mA[2, 4] = 0.2756;
      mA[3, 0] = 0.5571; mA[3, 1] = 0.7887; mA[3, 2] = 0.6480;
      mA[3, 3] = 0.7036; mA[3, 4] = 0.3125;
      mA[4, 0] = 0.3849; mA[4, 1] = 0.6153; mA[4, 2] = 0.2756;
      mA[4, 3] = 0.3125; mA[4, 4] = 0.5668;

      LinearAlgebraOperations.MakeUpperTriangularMatrix(ref mA);

      double[][] ans =
      {
                new double[]{ -1.3237961361176425, -1.2875584340340815, -1.2151377512836674,
                               -1.3812965607851724, -0.95174735416211687 },
                new double[]{ 0, 0.53899109356992447, 0.15133476377364941,
                               -0.012486693790510085, 0.043076940149720341 },
                new double[]{ 0, 0, -0.35865980354487109,
                               -0.13435091875500288, 0.24491581554495295 },
                new double[]{ 0, 0, 0, -0.13724078938465178, 0.042983530405157767 },
                new double[]{ 0, 0, 0, 0, -0.22826730051106017 }
            };

      for (int i = 0; i < mA.Rows; i++)
        for (int j = 0; j < mA.Columns; j++)
          Assert.Equal(ans[i][j], mA[i, j], precision: 10);
    }

    /// <summary>EstimateMultipleRegressionCoefficients でサンプル数が不一致の場合に例外が発生する</summary>
    [Fact]
    public void EstimateMultipleRegressionCoefficients_MismatchedSampleCount_ThrowsPopoloArgumentException()
    {
      double[] y = { 1.0, 2.0, 3.0 };
      double[][] x =
      {
                new double[] { 1.0, 2.0 },
                new double[] { 3.0, 4.0 }
                // yは3要素、xは2サンプル → 不一致
            };

      Assert.Throws<PopoloArgumentException>(
          () => LinearAlgebraOperations.EstimateMultipleRegressionCoefficients(
              y, x, out _, out _));
    }

    /// <summary>EstimateMultipleRegressionCoefficients で説明変数の次元が不一致の場合に例外が発生する</summary>
    [Fact]
    public void EstimateMultipleRegressionCoefficients_MismatchedPredictorCount_ThrowsPopoloArgumentException()
    {
      double[] y = { 1.0, 2.0, 3.0 };
      double[][] x =
      {
                new double[] { 1.0, 2.0 },
                new double[] { 3.0, 4.0 },
                new double[] { 5.0 }       // 次元が異なる
            };

      Assert.Throws<PopoloArgumentException>(
          () => LinearAlgebraOperations.EstimateMultipleRegressionCoefficients(
              y, x, out _, out _));
    }
  }
}