/* CoolingTowerTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3 — see accompanying LICENSE file.
 */

using System;
using Xunit;
using Popolo.Core.HVAC.HeatExchanger;

namespace Popolo.Core.Tests.HVAC.HeatExchanger
{
  /// <summary>Unit tests for <see cref="CoolingTower"/>.</summary>
  /// <remarks>
  /// CoolingTower models an open evaporative cooling tower using the
  /// characteristic coefficient c method (Merkel equation).
  ///
  /// Constructor:
  ///   (inletWaterTemp, outletWaterTemp, wetbulbTemp, waterFlowRate,
  ///    airFlowRate, airFlowType, powerConsumption, hasInverter)
  ///   → computes characteristic coefficient c and nominal fan power from rated conditions
  ///
  /// Key sign/direction conventions:
  ///   HeatRejection &gt; 0 always (heat is removed from the water)
  ///   OutletWaterTemperature &lt; InletWaterTemperature (water is cooled)
  ///   WaterConsumption &gt; 0 (evaporation + drift + blowdown)
  ///
  /// Update(inletWaterTemp, airFlowRate):
  ///   Computes outlet temperature, heat rejection, power, and water consumption.
  /// UpdateFromHeatRejection(heatRejection):
  ///   Adjusts fan speed to reject a given heat load.
  /// </remarks>
  public class CoolingTowerTests
  {
    #region ヘルパー

    /// <summary>
    /// 標準的な冷却塔を生成する。
    /// 定格条件: 入口37°C, 出口32°C, 外気湿球27°C, 水量10kg/s, 向流型, INVあり。
    /// </summary>
    private static CoolingTower MakeTower(bool hasInverter = true)
    {
      var ct = new CoolingTower(
          inletWaterTemperature: 37.0,
          outletWaterTemperature: 32.0,
          wetBulbTemperature: 27.0,
          waterFlowRate: 10.0,
          airFlowType: CoolingTower.AirFlowDirection.CounterFlow,
          hasInverter: hasInverter);
      ct.SetOutdoorAirState(27.0, 0.0182); // 湿球27°C相当
      ct.WaterFlowRate = 10.0;
      ct.OutletWaterSetpointTemperature = 32.0;
      return ct;
    }

    #endregion

    // ================================================================
    #region コンストラクタ・プロパティ

    /// <summary>コンストラクタで HasInverter フラグが正しく設定される。</summary>
    [Fact]
    public void Constructor_HasInverter_SetCorrectly()
    {
      var ctInv = MakeTower(hasInverter: true);
      var ctNonInv = MakeTower(hasInverter: false);
      Assert.True(ctInv.HasInverter);
      Assert.False(ctNonInv.HasInverter);
    }

    /// <summary>WaterFlowRate プロパティに値を設定できる。</summary>
    [Fact]
    public void WaterFlowRate_SetAndGet_RoundTrip()
    {
      var ct = MakeTower();
      ct.WaterFlowRate = 8.0;
      Assert.InRange(ct.WaterFlowRate, 7.99, 8.01);
    }

    /// <summary>OutletWaterSetPointTemperature を設定できる。</summary>
    [Fact]
    public void OutletWaterSetPointTemperature_SetAndGet_RoundTrip()
    {
      var ct = MakeTower();
      ct.OutletWaterSetpointTemperature = 30.0;
      Assert.InRange(ct.OutletWaterSetpointTemperature, 29.99, 30.01);
    }

    #endregion

    // ================================================================
    #region SetOutdoorAirState / ShutOff

    /// <summary>SetOutdoorAirState で外気湿球温度が更新される。</summary>
    [Fact]
    public void SetOutdoorAirState_UpdatesWetbulbTemperature()
    {
      var ct = MakeTower();
      ct.SetOutdoorAirState(25.0, 0.016);
      Assert.InRange(ct.OutdoorWetBulbTemperature, 24.5, 25.5);
    }

    /// <summary>ShutOff 後は HeatRejection = 0 かつ ElectricConsumption = 0。</summary>
    [Fact]
    public void ShutOff_ZeroHeatRejectionAndPower()
    {
      var ct = MakeTower();
      ct.Update(37.0, ct.MaxAirFlowRate);
      Assert.True(ct.HeatRejection > 0); // Update後は正

      ct.ShutOff();
      Assert.Equal(0.0, ct.HeatRejection);
      Assert.Equal(0.0, ct.ElectricConsumption);
    }

    #endregion

    // ================================================================
    #region Update

    /// <summary>
    /// Update 後に出口水温が入口水温より低い（冷却効果）。
    /// </summary>
    [Fact]
    public void Update_Normal_OutletCoolerThanInlet()
    {
      var ct = MakeTower();
      ct.Update(37.0, ct.MaxAirFlowRate);
      Assert.True(ct.OutletWaterTemperature < ct.InletWaterTemperature,
          $"Outlet={ct.OutletWaterTemperature:F2}°C < Inlet={ct.InletWaterTemperature:F2}°C");
    }

    /// <summary>除去熱量が正（常に水から熱を除去）。</summary>
    [Fact]
    public void Update_Normal_HeatRejectionIsPositive()
    {
      var ct = MakeTower();
      ct.Update(37.0, ct.MaxAirFlowRate);
      Assert.True(ct.HeatRejection > 0,
          $"HeatRejection={ct.HeatRejection:F2} kW > 0");
    }

    /// <summary>外気湿球温度が低いほど出口水温が低くなる（冷却効果大）。</summary>
    [Fact]
    public void Update_LowerWetbulb_LowerOutletTemperature()
    {
      var ctHot = MakeTower();
      ctHot.SetOutdoorAirState(27.0, 0.018);
      ctHot.Update(37.0, ctHot.MaxAirFlowRate);
      double outHot = ctHot.OutletWaterTemperature;

      var ctCold = MakeTower();
      ctCold.SetOutdoorAirState(20.0, 0.012);
      ctCold.Update(37.0, ctCold.MaxAirFlowRate);
      double outCold = ctCold.OutletWaterTemperature;

      Assert.True(outCold < outHot,
          $"Cold WB outlet={outCold:F2}°C < Hot WB outlet={outHot:F2}°C");
    }

    /// <summary>風量が多いほど除去熱量が増える。</summary>
    [Fact]
    public void Update_HigherAirFlow_IncreasesHeatRejection()
    {
      var ct = MakeTower();
      ct.Update(37.0, ct.MaxAirFlowRate * 0.5);
      double qLow = ct.HeatRejection;

      ct.Update(37.0, ct.MaxAirFlowRate);
      double qHigh = ct.HeatRejection;

      Assert.True(qHigh > qLow,
          $"Full flow Q={qHigh:F2} kW > Half flow Q={qLow:F2} kW");
    }

    /// <summary>消費電力が非負。</summary>
    [Fact]
    public void Update_ElectricConsumption_NonNegative()
    {
      var ct = MakeTower();
      ct.Update(37.0, ct.MaxAirFlowRate);
      Assert.True(ct.ElectricConsumption >= 0,
          $"ElectricConsumption={ct.ElectricConsumption:F3} kW >= 0");
    }

    /// <summary>水消費量が正（蒸発・飛散・ブロー）。</summary>
    [Fact]
    public void Update_WaterConsumption_IsPositive()
    {
      var ct = MakeTower();
      ct.Update(37.0, ct.MaxAirFlowRate);
      Assert.True(ct.WaterConsumption > 0,
          $"WaterConsumption={ct.WaterConsumption:F5} kg/s > 0");
    }

    #endregion

    // ================================================================
    #region UpdateFromHeatRejection

    /// <summary>
    /// UpdateFromHeatRejection 後に HeatRejection が指定値に近い（非過負荷時）。
    /// </summary>
    [Fact]
    public void UpdateFromHeatRejection_Normal_HeatRejectionMatchesTarget()
    {
      var ct = MakeTower();
      // 定格除去熱量を計算
      ct.Update(37.0, ct.MaxAirFlowRate);
      double rated = ct.HeatRejection;
      double target = rated * 0.7; // 70%負荷

      ct.UpdateFromHeatRejection(target);
      Assert.InRange(ct.HeatRejection, target * 0.95, target * 1.05);
    }

    /// <summary>過負荷時は IsOverLoad = true になる。</summary>
    [Fact]
    public void UpdateFromHeatRejection_Overload_IsOverLoadTrue()
    {
      var ct = MakeTower();
      // 物理的にあり得ない大きな熱量を直接指定（定格能力の推定値 ~200kW の 10倍）
      ct.UpdateFromHeatRejection(10000.0);
      Assert.True(ct.IsOverLoad);
    }

    #endregion

    // ================================================================
    #region static メソッド

    /// <summary>
    /// GetHeatRejection: 定格条件に近い値で除去熱量が正になる。
    /// </summary>
    [Fact]
    public void GetHeatRejection_RatedCondition_IsPositive()
    {
      // 特性係数を取得してから除去熱量を計算
      double c = CoolingTower.GetCoolingTowerCoefficient(
          37.0, 32.0, 27.0, 10.0, 20.0,
          CoolingTower.AirFlowDirection.CounterFlow);
      double q = CoolingTower.GetHeatRejection(
          37.0, 27.0, 10.0, 20.0, c,
          CoolingTower.AirFlowDirection.CounterFlow);
      Assert.True(q > 0, $"Q={q:F2} kW > 0");
    }

    /// <summary>
    /// GetCoolingTowerCoefficient: 定格条件から特性係数 c が正の値になる。
    /// </summary>
    [Fact]
    public void GetCoolingTowerCoefficient_RatedCondition_IsPositive()
    {
      double c = CoolingTower.GetCoolingTowerCoefficient(
          37.0, 32.0, 27.0, 10.0, 20.0,
          CoolingTower.AirFlowDirection.CounterFlow);
      Assert.True(c > 0, $"c={c:F4} > 0");
    }

    #endregion
  }
}