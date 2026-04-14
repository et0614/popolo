/* FangerModel.cs
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

using System;

using Popolo.Core.Physics;
using Popolo.Core.Numerics;

namespace Popolo.Core.ThermalComfort
{
  /// <summary>Provides static methods for calculating thermal comfort indices based on Fanger's model (ISO 7730 / ASHRAE 55).</summary>
  public static class FangerModel
  {

    #region Constants

    /// <summary>Conversion factor from metabolic rate [met] to heat flux [W/m²].</summary>
    private const double CONVERT_MET_TO_W = 58.15;

    /// <summary>Offset for converting Celsius to Kelvin [K].</summary>
    private const double CONVERT_C_TO_K = 273;

    #endregion

    #region 列挙型

    /// <summary>Metabolic activity types with associated metabolic rates [met].</summary>
    public enum Tasks
    {
      /// <summary>Resting: sleeping (0.7 met).</summary>
      Resting_Sleeping,
      /// <summary>Resting: reclining (0.8 met).</summary>
      Resting_Reclining,
      /// <summary>Resting: seated quiet (1.0 met).</summary>
      Resting_Seated_Quiet,
      /// <summary>Resting: standing relaxed (1.2 met).</summary>
      Resting_Standing_Relaxed,
      /// <summary>Walking: slow, 0.9 m/s (2.0 met).</summary>
      Walking_Slow_09ms,
      /// <summary>Walking: normal, 1.2 m/s (2.6 met).</summary>
      Walking_Normal_12ms,
      /// <summary>Walking: fast, 1.8 m/s (3.8 met).</summary>
      Walking_Fast_18ms,
      /// <summary>Office: seated reading/writing (1.0 met).</summary>
      OfficeActivities_Seated_Reading_Writing,
      /// <summary>Office: typing (1.1 met).</summary>
      OfficeActivities_Typing,
      /// <summary>Office: filing, seated (1.2 met).</summary>
      OfficeActivities_Filing_Seated,
      /// <summary>Office: filing, standing (1.4 met).</summary>
      OfficeActivities_Filing_Standing,
      /// <summary>Office: walking (1.7 met).</summary>
      OfficeActivities_Walking,
      /// <summary>Office: lifting and packing (2.1 met).</summary>
      OfficeActivities_Lifting_Packing,
      /// <summary>Driving: automobile (1.5 met).</summary>
      Driving_Automobile,
      /// <summary>Driving: aircraft, routine (1.2 met).</summary>
      Driving_Aircraft_Routine,
      /// <summary>Driving: aircraft, instrument landing (1.8 met).</summary>
      Driving_Aircraft_Instrument_Landing,
      /// <summary>Driving: aircraft, combat (2.4 met).</summary>
      Driving_Aircraft_Combat,
      /// <summary>Driving: heavy vehicle (3.2 met).</summary>
      Driving_HeavyVehicle,
      /// <summary>Other: cooking (1.8 met).</summary>
      Other_Occupational_Cooking,
      /// <summary>Other: house cleaning (2.7 met).</summary>
      Other_Occupational_HouseCleaning,
      /// <summary>Other: seated, heavy limb movement (2.2 met).</summary>
      Other_Occupational_Seated_HeavyLimbMovement,
      /// <summary>Other: machine work, sawing (2.2 met).</summary>
      Other_Occupational_MachineWork_Sawing,
      /// <summary>Other: machine work, light (1.8 met).</summary>
      Other_Occupational_MachineWork_Light,
      /// <summary>Other: machine work, heavy (4.0 met).</summary>
      Other_Occupational_MachineWork_Heavy,
      /// <summary>Other: handling 50 kg bags (4.0 met).</summary>
      Other_Occupational_Handling50kgBags,
      /// <summary>Other: pick and shovel work (4.4 met).</summary>
      Other_Occupational_PickAndShovelWork,
      /// <summary>Other: dancing (3.4 met).</summary>
      Other_Leisure_Dancing,
      /// <summary>Other: exercise (3.5 met).</summary>
      Other_Leisure_Exercise,
      /// <summary>Other: tennis (3.8 met).</summary>
      Other_Leisure_Tennes,
      /// <summary>Other: basketball (5.8 met).</summary>
      Other_Leisure_Basketball,
      /// <summary>Other: wrestling (7.8 met).</summary>
      Other_Leisure_Wrestling
    }

    #endregion

    #region publicメソッド

    /// <summary>Computes the thermal load on the human body [W/m²] using the Fanger heat balance equation.</summary>
    /// <param name="drybulbTemperature">Dry-bulb temperature [°C].</param>
    /// <param name="meanRadiantTemperature">Mean radiant temperature [°C].</param>
    /// <param name="relativeHumidity">Relative humidity [%].</param>
    /// <param name="relativeAirVelocity">Relative air velocity [m/s].</param>
    /// <param name="clothing">Clothing insulation [clo].</param>
    /// <param name="metabolicRate">Metabolic rate [met].</param>
    /// <param name="externalWork">External work [met].</param>
    /// <returns>Thermal load on the human body [W/m²].</returns>
    public static double GetThermalLoad
      (double drybulbTemperature, double meanRadiantTemperature, double relativeHumidity,
      double relativeAirVelocity, double clothing, double metabolicRate, double externalWork)
    {
      double dbtA = CONVERT_C_TO_K + drybulbTemperature;
      double mrtA = CONVERT_C_TO_K + meanRadiantTemperature;

      //周囲の水蒸気分圧[kPa]の計算
      double pa = relativeHumidity / 100d * Water.GetSaturationPressure(drybulbTemperature);

      //代謝量[W/m2]の計算
      double m = metabolicRate * CONVERT_MET_TO_W;
      double mw = m - externalWork * CONVERT_MET_TO_W;

      //着衣の熱抵抗[m2K/W]の計算
      double rcl = 0.155 * clothing;
      //着衣面積率[-]の計算
      double fcl;
      if (rcl < 0.078) fcl = 1.0 + 1.29 * rcl;
      else fcl = 1.05 + 0.645 * rcl;

      //強制対流による対流熱伝達率[W/(m2K)]
      double hcf = 12.1 * Math.Sqrt(relativeAirVelocity);

      //着衣表面温度の反復計算
      double tcla = dbtA + (35.5 - drybulbTemperature) / (3.5 * rcl + 0.1);//初期値
      double p1 = rcl * fcl;
      double p2 = p1 * 3.96;
      double p3 = p1 * 100;
      double p4 = p1 * dbtA;
      double p5 = 308.7 - 0.028 * mw + p2 * Math.Pow(mrtA / 100.0, 4);
      double xn = tcla / 100.0;
      double xf = xn;
      double hc; //対流熱伝達率[W/(m2K)]
      int iterNum = 0;
      while (true)
      {
        if (150 < iterNum) throw new Exceptions.PopoloNumericalException("GetThermalLoad", "PMV iteration did not converge.");
        xf = (xf + xn) / 2d;
        //対流熱伝達率[W/(m2K)]を更新
        hc = Math.Max(hcf, 2.38 * Math.Pow(Math.Abs(100.0 * xn - dbtA), 0.25));
        //状態値更新
        xn = (p5 + p4 * hc - p2 * Math.Pow(xf, 4)) / (100d + p3 * hc);
        //収束判定
        if (Math.Abs(xn - xf) < 0.00015) break;
        iterNum++;
      }
      //着衣表面温度[C]
      double tcl = 100.0 * xn - CONVERT_C_TO_K;

      //人体熱負荷の計算

      double ediff = 3.05 * (5.733 - 0.00699 * mw - pa);      //皮膚表面からの潜熱損失[W/m2]
      double esw = 0.42 * Math.Max(0, mw - CONVERT_MET_TO_W); //発汗による潜熱損失[W/m2]
      double lres = 0.017 * m * (5.867 - pa);                 //呼吸による潜熱損失[W/m2]
      double dres = 0.0014 * m * (34.0 - drybulbTemperature); //呼吸による顕熱損失[W/m2]
      double r = 3.96 * fcl * (Math.Pow(xn, 4) - Math.Pow(mrtA / 100.0, 4)); //着衣表面からの放射熱損失[W/m2]      
      double c = fcl * hc * (tcl - drybulbTemperature);       //着衣表面からの対流熱損失[W/m2]

      //集計
      return mw - (ediff + esw + lres + dres + r + c);
    }

    /// <summary>Computes the Predicted Mean Vote (PMV) [-].</summary>
    /// <param name="drybulbTemperature">Dry-bulb temperature [°C].</param>
    /// <param name="meanRadiantTemperature">Mean radiant temperature [°C].</param>
    /// <param name="relativeHumidity">Relative humidity [%].</param>
    /// <param name="relativeAirVelocity">Relative air velocity [m/s].</param>
    /// <param name="clothing">Clothing insulation [clo].</param>
    /// <param name="metabolicRate">Metabolic rate [met].</param>
    /// <param name="externalWork">External work [met].</param>
    /// <returns>PMV value [-].</returns>
    public static double GetPMV
      (double drybulbTemperature, double meanRadiantTemperature, double relativeHumidity,
      double relativeAirVelocity, double clothing, double metabolicRate, double externalWork)
    {
      double thermalLoad = GetThermalLoad(drybulbTemperature, meanRadiantTemperature,
        relativeHumidity, relativeAirVelocity, clothing, metabolicRate, externalWork);
      return GetPMV(metabolicRate, thermalLoad);
    }

    /// <summary>Computes the PMV from metabolic rate and thermal load.</summary>
    /// <param name="metabolicRate">Metabolic rate [met].</param>
    /// <param name="thermalLoad">Thermal load on the human body [W/m²].</param>
    /// <returns>PMV value [-].</returns>
    public static double GetPMV(double metabolicRate, double thermalLoad)
    {
      double m = metabolicRate * CONVERT_MET_TO_W;
      return (0.303 * Math.Exp(-0.036 * m) + 0.028) * thermalLoad;
    }

    /// <summary>Computes the Predicted Percentage of Dissatisfied (PPD) [%].</summary>
    /// <param name="pmv">PMV value [-].</param>
    /// <returns>PPD value [%].</returns>
    public static double GetPPD(double pmv)
    {
      double p2 = pmv * pmv;
      return 100.0 - 95.0 * Math.Exp(-0.03353 * p2 * p2 - 0.2179 * p2);
    }

    /// <summary>Computes the dry-bulb temperature [°C] that yields the specified PMV.</summary>
    /// <param name="pmv">Target PMV value [-].</param>
    /// <param name="meanRadiantTemperature">Mean radiant temperature [°C].</param>
    /// <param name="relativeAirVelocity">Relative air velocity [m/s].</param>
    /// <param name="relativeHumidity">Relative humidity [%].</param>
    /// <param name="clothing">Clothing insulation [clo].</param>
    /// <param name="metabolicRate">Metabolic rate [met].</param>
    /// <param name="externalWork">External work [met].</param>
    /// <returns>Dry-bulb temperature [°C].</returns>
    public static double GetDrybulbTemperature
      (double pmv, double meanRadiantTemperature, double relativeHumidity, double relativeAirVelocity,
      double clothing, double metabolicRate, double externalWork)
    {
      //乾球温度上下限値
      double MAXDB = 50;
      double MINDB = -10;

      //上下限値確認
      double pmvh = GetPMV(MAXDB, meanRadiantTemperature, relativeHumidity,
        relativeAirVelocity, clothing, metabolicRate, externalWork);
      if (pmvh < pmv) return MAXDB;
      double pmvl = GetPMV(MINDB, meanRadiantTemperature, relativeHumidity,
        relativeAirVelocity, clothing, metabolicRate, externalWork);
      if (pmv < pmvl) return MINDB;

      //ニュートン・ラフソン法で収束計算
      Roots.ErrorFunction eFnc = delegate (double dbTemp)
      {
        //誤差を評価
        double pmv2 = GetPMV(dbTemp, meanRadiantTemperature, relativeHumidity,
          relativeAirVelocity, clothing, metabolicRate, externalWork);
        return pmv2 - pmv;
      };
      return Roots.Newton(eFnc, 26, 0.0001, 0.01, 0.01, 20);
    }

    /// <summary>Computes the relative humidity [%] that yields the specified PMV.</summary>
    /// <param name="pmv">Target PMV value [-].</param>
    /// <param name="dryBulbTemperature">Dry-bulb temperature [°C].</param>
    /// <param name="meanRadiantTemperature">Mean radiant temperature [°C].</param>
    /// <param name="relativeAirVelocity">Relative air velocity [m/s].</param>
    /// <param name="clothing">Clothing insulation [clo].</param>
    /// <param name="metabolicRate">Metabolic rate [met].</param>
    /// <param name="externalWork">External work [met].</param>
    /// <returns>Relative humidity [%].</returns>
    public static double GetRelativeHumidity
      (double pmv, double dryBulbTemperature, double meanRadiantTemperature, double relativeAirVelocity,
      double clothing, double metabolicRate, double externalWork)
    {
      //上下限値確認
      double pmvl = GetPMV(dryBulbTemperature, meanRadiantTemperature, 0,
        relativeAirVelocity, clothing, metabolicRate, externalWork);
      if (pmv < pmvl) return 0;
      double pmvh = GetPMV(dryBulbTemperature, meanRadiantTemperature, 100,
        relativeAirVelocity, clothing, metabolicRate, externalWork);
      if (pmvh < pmv) return 100;

      //ニュートン・ラフソン法で収束計算
      Roots.ErrorFunction eFnc = delegate (double rHumid)
      {
        //誤差を評価
        double pmv2 = GetPMV(dryBulbTemperature, meanRadiantTemperature, rHumid,
          relativeAirVelocity, clothing, metabolicRate, externalWork);
        return pmv2 - pmv;
      };
      return Roots.Newton(eFnc, 40, 0.0001, 0.01, 0.01, 20);
    }

    /// <summary>Gets the metabolic rate [met] for the specified activity.</summary>
    /// <param name="task">Activity type.</param>
    /// <returns>Metabolic rate [met].</returns>
    public static double GetMet(Tasks task)
    {
      switch (task)
      {
        case Tasks.Driving_Aircraft_Combat:
          return 2.4;
        case Tasks.Driving_Aircraft_Instrument_Landing:
          return 1.8;
        case Tasks.Driving_Aircraft_Routine:
          return 1.2;
        case Tasks.Driving_Automobile:
          return 1.5;
        case Tasks.Driving_HeavyVehicle:
          return 3.2;
        case Tasks.OfficeActivities_Filing_Seated:
          return 1.2;
        case Tasks.OfficeActivities_Filing_Standing:
          return 1.4;
        case Tasks.OfficeActivities_Lifting_Packing:
          return 2.1;
        case Tasks.OfficeActivities_Seated_Reading_Writing:
          return 1.0;
        case Tasks.OfficeActivities_Typing:
          return 1.1;
        case Tasks.OfficeActivities_Walking:
          return 1.7;
        case Tasks.Other_Leisure_Basketball:
          return 5.8;
        case Tasks.Other_Leisure_Dancing:
          return 3.4;
        case Tasks.Other_Leisure_Exercise:
          return 3.5;
        case Tasks.Other_Leisure_Tennes:
          return 3.8;
        case Tasks.Other_Leisure_Wrestling:
          return 7.8;
        case Tasks.Other_Occupational_Cooking:
          return 1.8;
        case Tasks.Other_Occupational_Handling50kgBags:
          return 4.0;
        case Tasks.Other_Occupational_HouseCleaning:
          return 2.7;
        case Tasks.Other_Occupational_MachineWork_Heavy:
          return 4.0;
        case Tasks.Other_Occupational_MachineWork_Light:
          return 1.8;
        case Tasks.Other_Occupational_MachineWork_Sawing:
          return 2.2;
        case Tasks.Other_Occupational_PickAndShovelWork:
          return 4.4;
        case Tasks.Other_Occupational_Seated_HeavyLimbMovement:
          return 2.2;
        case Tasks.Resting_Reclining:
          return 0.8;
        case Tasks.Resting_Seated_Quiet:
          return 1.0;
        case Tasks.Resting_Sleeping:
          return 0.7;
        case Tasks.Resting_Standing_Relaxed:
          return 1.2;
        case Tasks.Walking_Fast_18ms:
          return 3.8;
        case Tasks.Walking_Normal_12ms:
          return 2.6;
        case Tasks.Walking_Slow_09ms:
          return 2.0;
        default:
          throw new Exceptions.PopoloArgumentException("Unknown task type.", nameof(task));
      }
    }

    #endregion

  }
}
