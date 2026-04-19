/* Transform.cs
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
using System.Linq;
using Popolo.Core.Exceptions;

namespace Popolo.Core.Numerics
{
  /// <summary>Provides data transformation utilities.</summary>
  public static class Transform
  {

    #region Box-Cox変換

    /// <summary>Finds the optimal Box-Cox transformation parameter λ.</summary>
    /// <param name="data">Original data; on return, transformed with the optimal λ.</param>
    /// <param name="minLambda">Lower bound of the λ search range.</param>
    /// <param name="maxLambda">Upper bound of the λ search range.</param>
    /// <returns>Optimal λ for the Box-Cox transformation.</returns>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when <paramref name="data"/> is null or empty.
    /// </exception>
    public static double GetOptimumBoxCoxLambda(
        ref double[] data, double minLambda = -2.0, double maxLambda = 2.0)
    {
      if (data == null || data.Length == 0)
        throw new PopoloArgumentException(
            "data must not be null or empty.",
            nameof(data));

      // 同じデータ数を持つ標準正規分布の累積分布を計算
      int dNum = data.Length;
      double[] std = new double[dNum];
      for (int i = 0; i < dNum; i++)
        std[i] = NormalRandom.CumulativeDistributionInverse((i + 0.5) / dNum);
      double aveStd = std.Average();
      double[] dvStd = std.Select(_ => _ - aveStd).ToArray();
      double stdStd = Math.Sqrt(dvStd.Select(_ => _ * _).Average());

      // 正の数（1以上）に調整
      double minData = data[0];
      for (int i = 1; i < dNum; i++)
        if (data[i] < minData) minData = data[i];
      minData = minData < 0 ? 1 - minData : 0;
      for (int i = 0; i < dNum; i++)
        data[i] += minData;

      // 原データをソート
      double[] sData = new double[dNum];
      data.CopyTo(sData, 0);
      Array.Sort(sData);

      Minimization.MinimizeFunction mFnc = delegate (double lmd)
      {
        double[] bc = BoxCoxTransform(sData, lmd);
        double aveBcs = bc.Average();
        double[] dvBcs = bc.Select(_ => _ - aveBcs).ToArray();
        double stdBcs = Math.Sqrt(dvBcs.Select(_ => _ * _).Average());

        double[] prdc = new double[dNum];
        for (int i = 0; i < dNum; i++)
          prdc[i] = dvStd[i] * dvBcs[i];
        double cor = prdc.Average() / (stdStd * stdBcs);

        return 1.0 - cor;
      };

      Minimization.GoldenSection(ref minLambda, maxLambda, mFnc);
      data = BoxCoxTransform(data, minLambda);
      return minLambda;
    }

    /// <summary>Applies the Box-Cox transformation to the data.</summary>
    /// <param name="data">Original data (all elements must be positive).</param>
    /// <param name="lamda">Box-Cox transformation parameter λ.</param>
    /// <returns>Box-Cox-transformed data.</returns>
    /// <exception cref="PopoloArgumentException">
    /// Thrown when <paramref name="data"/> is null or empty.
    /// </exception>
    public static double[] BoxCoxTransform(double[] data, double lamda)
    {
      if (data == null || data.Length == 0)
        throw new PopoloArgumentException(
            "data must not be null or empty.",
            nameof(data));

      double[] bcData = new double[data.Length];
      for (int i = 0; i < data.Length; i++)
        bcData[i] = lamda == 0
            ? Math.Log(data[i])
            : (Math.Pow(data[i], lamda) - 1) / lamda;
      return bcData;
    }

    #endregion

  }
}
