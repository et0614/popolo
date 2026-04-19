using System;

namespace Popolo.Core.Numerics.LinearAlgebra
{
  /// <summary>Performs QR decomposition with optional column pivoting.</summary>
  public class QRDecomposer
  {

    /// <summary>Machine epsilon used as a stopping tolerance.</summary>
    private const double M_EPSILON = 1e-8;

    int[] iPivot;

    double[] rdiag;

    double[] acnorm;

    double[] wa;

    /// <summary>Initializes a new instance for a matrix of the specified dimensions.</summary>
    /// <param name="rowCount">Number of rows.</param>
    /// <param name="columnCount">Number of columns.</param>
    public QRDecomposer(int rowCount, int columnCount)
    {
      iPivot = new int[columnCount];
      rdiag = new double[columnCount];
      acnorm = new double[columnCount];
      wa = new double[columnCount];
    }

    /// <summary>Performs QR decomposition.</summary>
    /// <param name="matrix">Matrix to decompose (modified in place).</param>
    /// <param name="pivot">True to perform column pivoting.</param>
    public void Decompose(IMatrix matrix, bool pivot)
    {
      //各列のユークリッドノルムを計算
      for (int i = 0; i < matrix.Columns; i++)
      {
        acnorm[i] = rdiag[i] = wa[i] =
          new VectorView(matrix, false, 0, i).ComputeEuclideanNorm();
        iPivot[i] = i;
      }

      int min = Math.Min(matrix.Rows, matrix.Columns);
      for (int i = 0; i < min; i++)
      {
        //ユークリッドノルム最大の列を先頭の列と入れ替える
        if (pivot)
        {
          int iMax = i;
          for (int j = i; j < matrix.Columns; j++)
            if (acnorm[iMax] < acnorm[j]) iMax = j;
          if (iMax != i)
          {
            for (int j = 0; j < matrix.Rows; j++)
            {
              double dTmp = matrix[j, i];
              matrix[j, i] = matrix[j, iMax];
              matrix[j, iMax] = dTmp;
            }
            rdiag[iMax] = rdiag[i];
            wa[iMax] = wa[i];
            int iTmp = iPivot[i];
            iPivot[i] = iPivot[iMax];
            iPivot[iMax] = iTmp;
          }
        }

        //
        double aiNorm = new VectorView(matrix, false, i, i).ComputeEuclideanNorm();
        if (aiNorm != 0)
        {
          if (matrix[i, i] < 0.0d) aiNorm = -aiNorm;
          for (int j = i; j < matrix.Rows; j++) matrix[j, i] = matrix[j, i] / aiNorm;
          matrix[i, i] += 1.0d;

          //
          int ip1 = i + 1;
          if (ip1 <= matrix.Columns)
          {
            for (int j = ip1; j < matrix.Columns; j++)
            {
              double sum = 0.0d;
              for (int k = i; k < matrix.Rows; k++) sum += matrix[k, i] * matrix[k, j];
              double dTmp = sum / matrix[i, i];
              for (int k = i; k < matrix.Rows; k++) matrix[k, j] -= dTmp * matrix[k, i];
              if (pivot && rdiag[j] != 0.0d)
              {
                dTmp = matrix[i, j] / rdiag[j];
                rdiag[j] *= Math.Sqrt(Math.Max(0.0d, 1.0d - dTmp * dTmp));
                if (0.05 * Math.Pow(rdiag[j] / wa[j], 2) <= M_EPSILON)
                {
                  rdiag[j] = new VectorView(matrix, false, ip1, j).ComputeEuclideanNorm();
                  wa[j] = rdiag[j];
                }
              }
            }
          }
        }
        rdiag[i] = -aiNorm;
      }

    }

  }
}
