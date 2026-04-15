/* AbsorptionRefrigerationCycleTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3 — see accompanying LICENSE file.
 */

using System;
using Xunit;
using Popolo.Core.HVAC.HeatSource;

namespace Popolo.Core.Tests.HVAC.HeatSource
{
  /// <summary>Unit tests for <see cref="AbsorptionRefrigerationCycle"/> (static methods).</summary>
  /// <remarks>
  /// Rated conditions from HotWaterAbsorptionChillerTest() sample:
  ///   new HotWaterAbsorptionChiller(12.5, 7, 274.9/60, 31, 35, 918/60, 88, 83, 432/60)
  ///   Chilled water: 12.5C -> 7C,  274.9/60 kg/s  (Q_cool ~= 105.5 kW)
  ///   Cooling water: 31C   -> 35C, 918/60   kg/s
  ///   Hot water:     88C   -> 83C, 432/60   kg/s  (Q_hot  ~= 150.7 kW)
  ///   Nominal COP ~= 0.70
  /// </remarks>
  public class AbsorptionRefrigerationCycleTests
  {
    #region 定格条件

    private static readonly double ChWM = 274.9 / 60.0;
    private static readonly double CdWM = 918.0 / 60.0;
    private static readonly double HtWM = 432.0 / 60.0;
    private const double ChWI = 12.5;
    private const double ChWO = 7.0;
    private const double CdWI = 31.0;
    private const double CdWO = 35.0;
    private const double HtWI = 88.0;
    private const double HtWO = 83.0;
    private const double Approach = 2.0;

    #endregion

    #region ヘルパー

    private static void GetRatedKA(
        out double evapKA, out double condKA, out double desorborKA,
        out double hexKA, out double solFlowRate, out double desorbHeat)
    {
      AbsorptionRefrigerationCycle.GetHeatTransferCoefficients(
          ChWI, ChWO, ChWM,
          CdWI, CdWO, CdWM,
          HtWI, HtWM, Approach,
          out evapKA, out condKA, out desorborKA,
          out hexKA, out solFlowRate, out desorbHeat);
    }

    #endregion

    // ================================================================
    #region GetHeatTransferCoefficients

    /// <summary>全 KA 値が正。</summary>
    [Fact]
    public void GetHeatTransferCoefficients_AllKAPositive()
    {
      GetRatedKA(out double evapKA, out double condKA, out double desorborKA,
                 out double hexKA, out double solFlowRate, out double desorbHeat);

      Assert.True(evapKA > 0, $"evapKA={evapKA:F4}");
      Assert.True(condKA > 0, $"condKA={condKA:F4}");
      Assert.True(desorborKA > 0, $"desorborKA={desorborKA:F4}");
      Assert.True(hexKA > 0, $"hexKA={hexKA:F4}");
      Assert.True(solFlowRate > 0, $"solFlowRate={solFlowRate:F4}");
      Assert.True(desorbHeat > 0, $"desorbHeat={desorbHeat:F2}");
    }

    /// <summary>定格 COP が現実的な範囲（0.5–0.9）。</summary>
    [Fact]
    public void GetHeatTransferCoefficients_NominalCOP_InRealisticRange()
    {
      GetRatedKA(out _, out _, out _, out _, out _, out double desorbHeat);
      const double Cp = 4.186;
      double qCool = (ChWI - ChWO) * ChWM * Cp;
      double cop = qCool / desorbHeat;
      Assert.InRange(cop, 0.5, 0.9);
    }

    #endregion

    // ================================================================
    #region GetOutletTemperatures

    /// <summary>定格条件での成り行き計算: 冷水が冷却される。</summary>
    [Fact]
    public void GetOutletTemperatures_RatedCondition_ChilledWaterCooled()
    {
      GetRatedKA(out double evapKA, out double condKA, out double desorborKA,
                 out double hexKA, out double solFlowRate, out _);

      AbsorptionRefrigerationCycle.GetOutletTemperatures(
          ChWI, ChWM, CdWI, CdWM, HtWI, HtWM,
          evapKA, condKA, desorborKA, hexKA, solFlowRate,
          out double chWO, out _, out _);

      Assert.True(chWO < ChWI,
          $"CHW outlet={chWO:F2}C < inlet={ChWI}C");
    }

    /// <summary>冷却水出口温度が入口温度より高い。</summary>
    [Fact]
    public void GetOutletTemperatures_RatedCondition_CoolingWaterHeated()
    {
      GetRatedKA(out double evapKA, out double condKA, out double desorborKA,
                 out double hexKA, out double solFlowRate, out _);

      AbsorptionRefrigerationCycle.GetOutletTemperatures(
          ChWI, ChWM, CdWI, CdWM, HtWI, HtWM,
          evapKA, condKA, desorborKA, hexKA, solFlowRate,
          out _, out double cdWO, out _);

      Assert.True(cdWO > CdWI,
          $"CDW outlet={cdWO:F2}C > inlet={CdWI}C");
    }

    /// <summary>温水出口温度が入口温度より低い。</summary>
    [Fact]
    public void GetOutletTemperatures_RatedCondition_HotWaterCooled()
    {
      GetRatedKA(out double evapKA, out double condKA, out double desorborKA,
                 out double hexKA, out double solFlowRate, out _);

      AbsorptionRefrigerationCycle.GetOutletTemperatures(
          ChWI, ChWM, CdWI, CdWM, HtWI, HtWM,
          evapKA, condKA, desorborKA, hexKA, solFlowRate,
          out _, out _, out double htWO);

      Assert.True(htWO < HtWI,
          $"HW outlet={htWO:F2}C < inlet={HtWI}C");
    }

    /// <summary>温水入口温度が高いほど冷水がより冷やされる（88C vs 75C）。</summary>
    [Fact]
    public void GetOutletTemperatures_HigherHotWaterTemp_LowerChilledOutlet()
    {
      GetRatedKA(out double evapKA, out double condKA, out double desorborKA,
                 out double hexKA, out double solFlowRate, out _);

      AbsorptionRefrigerationCycle.GetOutletTemperatures(
          ChWI, ChWM, CdWI, CdWM, 75.0, HtWM,
          evapKA, condKA, desorborKA, hexKA, solFlowRate,
          out double chWO_low, out _, out _);

      AbsorptionRefrigerationCycle.GetOutletTemperatures(
          ChWI, ChWM, CdWI, CdWM, 95.0, HtWM,
          evapKA, condKA, desorborKA, hexKA, solFlowRate,
          out double chWO_high, out _, out _);

      Assert.True(chWO_high < chWO_low,
          $"HW=95C: CHW out={chWO_high:F2}C < HW=75C: CHW out={chWO_low:F2}C");
    }

    /// <summary>冷却水入口温度が低いほど冷水がより冷やされる（31C vs 28C）。</summary>
    [Fact]
    public void GetOutletTemperatures_LowerCoolingWaterTemp_LowerChilledOutlet()
    {
      GetRatedKA(out double evapKA, out double condKA, out double desorborKA,
                 out double hexKA, out double solFlowRate, out _);

      AbsorptionRefrigerationCycle.GetOutletTemperatures(
          ChWI, ChWM, 28.0, CdWM, HtWI, HtWM,
          evapKA, condKA, desorborKA, hexKA, solFlowRate,
          out double chWO_cold, out _, out _);

      AbsorptionRefrigerationCycle.GetOutletTemperatures(
          ChWI, ChWM, 34.0, CdWM, HtWI, HtWM,
          evapKA, condKA, desorborKA, hexKA, solFlowRate,
          out double chWO_hot, out _, out _);

      Assert.True(chWO_cold < chWO_hot,
          $"CDW=28C: CHW out={chWO_cold:F2}C < CDW=34C: CHW out={chWO_hot:F2}C");
    }

    /// <summary>
    /// 負荷率50%（冷水流量を50%）でも冷水出口 &lt; 入口。
    /// サンプルコードのfor ループ条件と同様の運転確認。
    /// </summary>
    [Fact]
    public void GetOutletTemperatures_HalfFlow_StillCoolsChilledWater()
    {
      GetRatedKA(out double evapKA, out double condKA, out double desorborKA,
                 out double hexKA, out double solFlowRate, out _);

      AbsorptionRefrigerationCycle.GetOutletTemperatures(
          ChWI, ChWM * 0.5, CdWI, CdWM, HtWI, HtWM,
          evapKA, condKA, desorborKA, hexKA, solFlowRate,
          out double chWO, out _, out _);

      Assert.True(chWO < ChWI,
          $"Half-flow CHW outlet={chWO:F2}C < inlet={ChWI}C");
    }

    #endregion
  }
}