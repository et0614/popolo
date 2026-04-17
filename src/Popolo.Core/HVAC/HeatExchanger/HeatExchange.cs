/* HeatExchange.cs
 * 
 * Copyright (C) 2013 E.Togashi
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

using System;

using Popolo.Core.Exceptions;
using Popolo.Core.Numerics;

namespace Popolo.Core.HVAC.HeatExchanger
{
  /// <summary>Provides static methods for heat exchanger calculations (effectiveness-NTU method).</summary>
  public static class HeatExchange
  {

    #region 列挙型定義

    /// <summary>Heat exchanger flow arrangement type.</summary>
    public enum FlowType
    {
      /// <summary>Counter-flow arrangement.</summary>
      CounterFlow,
      /// <summary>Parallel-flow arrangement.</summary>
      ParallelFlow,
      /// <summary>Cross-flow, both fluids mixed.</summary>
      CrossFlow_BothFluidsUnmixed,
      /// <summary>Cross-flow, the fluid with the larger heat capacity rate is mixed.</summary>
      CrossFlow_CmaxMixed,
      /// <summary>Cross-flow, the fluid with the smaller heat capacity rate is mixed.</summary>
      CrossFlow_CminMixed,
      /// <summary>Cross-flow, both fluids unmixed.</summary>
      CrossFlow_BothFluidMixed
    }

    #endregion

    #region 熱通過有効度の計算

    /// <summary>Computes the heat transfer effectiveness ε [-] from NTU and heat capacity rate ratio.</summary>
    /// <param name="ntu">Number of transfer units (NTU) [-].</param>
    /// <param name="heatCapacityRatio">Heat capacity rate ratio Cmin/Cmax [-].</param>
    /// <param name="flowType">Flow arrangement type.</param>
    /// <returns>Heat transfer effectiveness ε [-].</returns>
    public static double GetEffectiveness(double ntu, double heatCapacityRatio, FlowType flowType)
    {
      double rMC = heatCapacityRatio;

      //熱容量流量比が0の場合
      if (rMC <= 0) return 1 - Math.Exp(-ntu);

      //NTUが0の場合
      if (ntu <= 0) return 0;

      double eps;
      switch (flowType)
      {
        case FlowType.CounterFlow:
          if (rMC < 0.999) return (1 - Math.Exp((rMC - 1) * ntu)) / (1 - rMC * Math.Exp((rMC - 1) * ntu)); //2024.02.01:極端にrMCが1に近い場合のエラー回避
          else return ntu / (1 + ntu);

        case FlowType.ParallelFlow:
          if (rMC < 0.999) return (1 - Math.Exp(-ntu * (rMC + 1))) / (1 + rMC);
          else return 0.5 * (1 - Math.Exp(-2 * ntu));

        case FlowType.CrossFlow_BothFluidMixed:
          eps = ntu / (1 - Math.Exp(-ntu)) + rMC * ntu / (1 - Math.Exp(-rMC * ntu));
          return ntu / (eps - 1);

        case FlowType.CrossFlow_CminMixed:
          return 1 - Math.Exp((Math.Exp(-ntu * rMC) - 1) / rMC);

        case FlowType.CrossFlow_CmaxMixed:
          return (1 - Math.Exp((Math.Exp(-ntu) - 1) * rMC)) / rMC;

        case FlowType.CrossFlow_BothFluidsUnmixed:
          eps = (Math.Exp(-rMC * Math.Pow(ntu, 0.78)) - 1) / (rMC * Math.Pow(ntu, -0.22));
          return 1 - Math.Exp(eps);
      }
      return 0;
    }

    /// <summary>Computes the number of transfer units (NTU) [-] from effectiveness and heat capacity rate ratio.</summary>
    /// <param name="effectiveness">Heat transfer effectiveness [-].</param>
    /// <param name="heatCapacityRatio">Heat capacity rate ratio Cmin/Cmax [-].</param>
    /// <param name="flowType">Flow arrangement type.</param>
    /// <returns>Number of transfer units (NTU) [-].</returns>
    public static double GetNTU(double effectiveness, double heatCapacityRatio, FlowType flowType)
    {
      Roots.ErrorFunction eFnc = delegate (double ntu)
      { return effectiveness - GetEffectiveness(ntu, heatCapacityRatio, flowType); };
      return Roots.Newton(eFnc, 0.1, 1e-4, 1e-6, 1e-6, 20); //2024.02.01:初期値を0から0.1に変更
    }

    #endregion

    #region 平均温度差Tmの計算

    /// <summary>Computes the log mean temperature difference (LMTD) [°C] from fluid inlet and outlet temperatures.</summary>
    /// <param name="hotInletTemperature">Hot fluid inlet temperature [°C].</param>
    /// <param name="coldInletTemperature">Cold fluid inlet temperature [°C].</param>
    /// <param name="hotOutletTemperature">Hot fluid outlet temperature [°C].</param>
    /// <param name="coldOutletTemperature">Cold fluid outlet temperature [°C].</param>
    /// <param name="flowType">Flow arrangement type.</param>
    /// <returns>Log mean temperature difference (LMTD) [°C].</returns>
    public static double GetMeanTemperatureDifference
      (double hotInletTemperature, double coldInletTemperature,
      double hotOutletTemperature, double coldOutletTemperature, FlowType flowType)
    {
      //向流の場合
      if (flowType == FlowType.CounterFlow)
      {
        double dt1 = hotInletTemperature - coldOutletTemperature;
        double dt2 = hotOutletTemperature - coldInletTemperature;
        if (dt1 == dt2) return dt1;
        if (dt1 <= 0 || dt2 <= 0) return 0;
        else return (dt1 - dt2) / Math.Log(dt1 / dt2);
      }
      //並流の場合
      else if (flowType == FlowType.ParallelFlow)
      {
        double dt1 = hotInletTemperature - coldInletTemperature;
        double dt2 = hotOutletTemperature - coldOutletTemperature;
        if (dt1 <= 0 || dt2 <= 0) return 0;
        return (dt1 - dt2) / Math.Log(dt1 / dt2);
      }
      else
      {
        double p =
          (hotInletTemperature - hotOutletTemperature) / (hotInletTemperature - coldInletTemperature);
        double q =
          (coldOutletTemperature - coldInletTemperature) / (hotInletTemperature - coldInletTemperature);
        double r = 0;

        //直交流（片側混合）の場合
        if (flowType == FlowType.CrossFlow_CminMixed || flowType == FlowType.CrossFlow_CmaxMixed)
        {
          double bf = 1 - q / p * Math.Log(1 / (1 - p));
          if (bf <= 0) r = 0;
          else r = q / Math.Log(1 / bf);
        }
        //直交流（両側混合・非混合）の場合
        else if (flowType == FlowType.CrossFlow_BothFluidMixed
          || flowType == FlowType.CrossFlow_BothFluidsUnmixed)
        {
          //rの上下限値を計算して解の範囲を特定
          double rMax;  //上限値（対向流のr）
          if (Math.Abs(p - q) < 1e-8) rMax = 1;
          else rMax = (p - q) / Math.Log((1 - q) / (1 - p));
          r = Math.Max(1e-4, 1 - (p + q));  //下限値（出口温度差）

          //極小値を黄金探索
          Minimization.MinimizeFunction mFnc = delegate (double x)
          {
            if (flowType == FlowType.CrossFlow_BothFluidMixed) return Math.Abs(EFncBothMixedR(p, q, x));
            else return Math.Abs(EFncBothUnMixedR(p, q, x));
          };
          if (1e-3 < Minimization.GoldenSection(ref r, rMax, mFnc)) r = 0;
        }
        return r * (hotInletTemperature - coldInletTemperature);
      }
      throw new PopoloArgumentException(
        $"Unsupported flow type: {flowType}.", nameof(flowType));
    }

    /// <summary>Error function for cross-flow with both fluids mixed (used in NTU solver).</summary>
    /// <param name="p">p</param>
    /// <param name="q">q</param>
    /// <param name="r">r</param>
    /// <returns>Residual error for the iterative solver.</returns>
    private static double EFncBothMixedR(double p, double q, double r)
    { return (p / (1 - Math.Exp(-p / r)) + q / (1 - Math.Exp(-q / r)) - 1) - r; }

    /// <summary>Error function for cross-flow with both fluids unmixed (used in NTU solver).</summary>
    /// <param name="p">p</param>
    /// <param name="q">q</param>
    /// <param name="r">r</param>
    /// <returns>Residual error for the iterative solver.</returns>
    private static double EFncBothUnMixedR(double p, double q, double r)
    {
      const int UVMAX = 50;
      double rSum = 1;
      double pr = p / r;
      double qr = q / r;
      for (int u = 1; u <= UVMAX; u++)
      {
        double dR = 0;
        double pqr = 1;
        for (int i = 1; i <= u; i++) pqr *= pr / (i + 1);
        if (u % 2 == 0) dR += pqr;
        else dR -= pqr;

        for (int v = 1; v <= u; v++)
        {
          pqr *= qr * (u + v) / (v * (v + 1));
          if ((u + v) % 2 == 0) dR += pqr;
          else dR -= pqr;
        }

        for (int i = u; 0 < i; i--)
        {
          pqr *= i * (i + 1) / (pr * (u + i));
          if ((i + u) % 2 == 0) dR -= pqr;
          else dR += pqr;
        }

        rSum += dR;
        if (Math.Abs(dR) < 1e-6) return rSum - r;
      }
      return (1 - (p + q)) - r;
    }

    #endregion

    #region フィン効率の計算

    /// <summary>Computes the efficiency of an annular fin [-] using modified Bessel functions.</summary>
    /// <param name="tubeRadius">Tube outer radius at the fin base [m].</param>
    /// <param name="finRadius">Outer radius of the annular fin [m].</param>
    /// <param name="thickness">Fin thickness [m].</param>
    /// <param name="filmCoefficient">Convective heat transfer coefficient on the fin surface [W/(m²·K)].</param>
    /// <param name="thermalConductivity">Thermal conductivity of the fin material [W/(m·K)].</param>
    /// <returns>Annular fin efficiency [-].</returns>
    public static double GetCircularFinEfficiency
      (double tubeRadius, double finRadius, double thickness, 
      double filmCoefficient, double thermalConductivity)
    {
      double w = finRadius - tubeRadius;
      double xexb = finRadius / tubeRadius;

      double ws = w * Math.Sqrt(filmCoefficient / thermalConductivity / thickness);
      //多くの図表はwsを横軸、フィン効率を縦軸に取る
      double ub = ws / (xexb - 1);
      double ue = ub * xexb;

      //ベッセル関数の計算
      double i0ub = BesI0(ub);
      double i1ue = BesI1(ue);
      double i1ub = BesI1(ub);
      double k0ub = BesK0(ub, i0ub);
      double k1ue = BesK1(ue, i1ue);
      double k1ub = BesK1(ub, i1ub);

      double phi = (k1ue * i1ub - i1ue * k1ub) / (k1ue * i0ub + i1ue * k0ub);
      return 2 * phi / ub / (1 - Math.Pow(ue / ub, 2));
    }

    #endregion

    #region ベッセル関数の計算

    /// <summary>Computes the modified Bessel function of the first kind, order 0: I₀(u).</summary>
    /// <param name="u">Argument of the Bessel function.</param>
    /// <returns>I₀(u).</returns>
    private static double BesI0(double u)
    {

      if (u < 3.75)
      {
        double uu = u / 3.75;
        uu *= uu;
        return 1 + uu * (3.5156229 + uu * (3.0899424 + uu * (1.2067492 + uu * (0.2659732
          + uu * (0.0360768 + uu * 0.0045813)))));
      }
      else
      {
        double uu = 3.75 / u;
        return (0.398942280 + uu * (0.013285917 + uu * (0.002253187 + uu * (-0.001575649 
          + uu * (0.009162808 + uu * (-0.020577063 + uu * (0.026355372 + uu * (-0.016476329 
          + uu * 0.003923767)))))))) * (Math.Exp(u) / Math.Sqrt(u));
      }
    }

    /// <summary>Computes the modified Bessel function of the first kind, order 1: I₁(u).</summary>
    /// <param name="u">Argument of the Bessel function.</param>
    /// <returns>I₁(u).</returns>
    private static double BesI1(double u)
    {

      if (u < 3.75)
      {
        double uu = u / 3.75;
        uu *= uu;
        return (0.5 + uu * (0.87890594 + uu * (0.51498869 + uu * (0.15084934 
          + uu * (0.02658733 + uu * (0.00301532 + uu * 0.00032411)))))) * u;
      }
      else
      {
        double uu = 3.75 / u;
        return (0.398942280 + uu * (-0.039880242 + uu * (-0.003620183 + uu * (0.001638014 
          + uu * (-0.010315550 + uu * (0.022829673 + uu * (-0.028953121 + uu * (0.017876535
          + uu * (-0.004200587))))))))) * (Math.Exp(u) / Math.Sqrt(u));
      }
    }

    /// <summary>Computes the modified Bessel function of the second kind, order 0: K₀(u).</summary>
    /// <param name="u">Argument of the Bessel function.</param>
    /// <param name="i0">Value of I₀(u).</param>
    /// <returns>K₀(u).</returns>
    private static double BesK0(double u, double i0)
    {
      if (u < 2)
      {
        double uu = u / 2;
        uu *= uu;
        return -0.57721566 + uu * (0.42278420 + uu * (0.23069756 + uu * (0.03488590
          + uu * (0.00262698 + uu * (0.00010750 + uu * 0.00000740))))) - Math.Log(0.5 * u) * i0;
      }
      else
      {
        double uu = 2 / u;
        return (1.25331414 + uu * (-0.07832358 + uu * (0.02189568 + uu * (-0.01062446 + uu 
          * (0.00587872 + uu * (-0.00251540 + uu * 0.00053208)))))) / (Math.Exp(u) * Math.Sqrt(u));
      }
    }

    /// <summary>Computes the modified Bessel function of the second kind, order 0: K₀(u).</summary>
    /// <param name="u">Argument of the Bessel function.</param>
    /// <param name="i1">Value of I₁(u).</param>
    /// <returns>K₀(u).</returns>
    private static double BesK1(double u, double i1)
    {
      if (u < 2)
      {
        double uu = u / 2;
        uu *= uu;
        return (1 + uu * (0.15443144 + uu * (-0.67278579 + uu * (-0.18156897 + uu
          * (-0.01919402 + uu * (-0.00110404 + uu * (-0.00004686))))))) / u + Math.Log(0.5 * u) * i1;
      }
      else
      {
        double uu = 2 / u;
        return (1.25331414 + uu * (0.23498619 + uu * (-0.03655620 + uu * (0.01504268 + uu 
          * (-0.00780353 + uu * (0.00325614 + uu * (-0.00068245))))))) / (Math.Exp(u) * Math.Sqrt(u));
      }
    }

    #endregion

    #region 熱交換量の計算

    /// <summary>Computes the heat transfer rate [kW] using the effectiveness-NTU method.</summary>
    /// <param name="highTemperature">Hot-side temperature [°C].</param>
    /// <param name="lowTemperature">Cold-side temperature [°C].</param>
    /// <param name="highHeatCapFlow">Heat capacity rate of the hot fluid [kW/K].</param>
    /// <param name="lowHeatCapFlow">Heat capacity rate of the cold fluid [kW/K].</param>
    /// <param name="heatTransferCoefficient">Overall heat transfer coefficient UA [kW/K].</param>
    /// <param name="fType">Flow arrangement type.</param>
    /// <returns>Heat transfer rate [kW].</returns>
    public static double GetHeatTransfer(double highTemperature, double lowTemperature, 
      double highHeatCapFlow, double lowHeatCapFlow, double heatTransferCoefficient, FlowType fType)
    {
      double mcMin = Math.Min(highHeatCapFlow, lowHeatCapFlow);
      double mcMax = Math.Max(highHeatCapFlow, lowHeatCapFlow);
      double effectiveness = GetEffectiveness(heatTransferCoefficient / mcMin, mcMin / mcMax, fType);
      return effectiveness * mcMin * (highTemperature - lowTemperature);
    }

    /// <summary>Computes the overall heat transfer coefficient UA [kW/K] from inlet/outlet temperatures and flow conditions.</summary>
    /// <param name="highTemperature">Hot-side temperature [°C].</param>
    /// <param name="lowTemperature">Cold-side temperature [°C].</param>
    /// <param name="highHeatCapFlow">Heat capacity rate of the hot fluid [kW/K].</param>
    /// <param name="lowHeatCapFlow">Heat capacity rate of the cold fluid [kW/K].</param>
    /// <param name="heatTransfer">Heat transfer rate [kW].</param>
    /// <param name="fType">Flow arrangement type.</param>
    /// <returns>Overall heat transfer coefficient UA [kW/K].</returns>
    public static double GetHeatTransferCoefficient(double highTemperature, double lowTemperature,
      double highHeatCapFlow, double lowHeatCapFlow, double heatTransfer, FlowType fType)
    {
      double mcMin = Math.Min(highHeatCapFlow, lowHeatCapFlow);
      double mcMax = Math.Max(highHeatCapFlow, lowHeatCapFlow);
      double effectiveness = Math.Min(0.9999, heatTransfer / (mcMin * (highTemperature - lowTemperature)));
      return HeatExchange.GetNTU(effectiveness, mcMin / mcMax, HeatExchange.FlowType.CounterFlow) * mcMin;
    }

    #endregion

  }
}
