/* CrossFinCondensorTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3 — see accompanying LICENSE file.
 */

using System;
using Xunit;
using Popolo.Core.HVAC.HeatExchanger;

namespace Popolo.Core.Tests.HVAC.HeatExchanger
{
  /// <summary>Unit tests for <see cref="CrossFinCondensor"/>.</summary>
  /// <remarks>
  /// CrossFinCondensor models an air-cooled plate-fin-and-tube condenser.
  /// Heat is rejected from the refrigerant (at condensing temperature) to air.
  ///
  /// Constructor: (cndTemperature, heatTransfer, airFlowRate, inletAirTemp, inletAirHumidityRatio)
  ///   → computes heat transfer surface area from rated conditions
  ///
  /// Key relationships:
  ///   condensingTemp ↑ when heatTransfer ↑ or airFlowRate ↓ or inletAirTemp ↑
  ///   HeatTransfer = GetHeatTransfer(cndTemp, airFlow, Tin, Win)
  ///
  /// Water spray: reduces effective inlet air temperature, improving heat rejection.
  /// </remarks>
  public class CrossFinCondensorTests
  {
    #region ヘルパー

    /// <summary>
    /// 標準的な凝縮器を生成する。
    /// 定格: 凝縮温度50°C, 熱交換量10kW, 風量1kg/s, 外気35°C/W=0.018。
    /// </summary>
    private static CrossFinCondensor MakeCond()
        => new CrossFinCondensor(50.0, 10.0, 1.0, 35.0, 0.018);

    #endregion

    // ================================================================
    #region コンストラクタ・プロパティ

    /// <summary>SurfaceArea が正の値になる。</summary>
    [Fact]
    public void Constructor_SurfaceArea_IsPositive()
    {
      var cond = MakeCond();
      Assert.True(cond.SurfaceArea > 0,
          $"SurfaceArea={cond.SurfaceArea:F4} m² > 0");
    }

    /// <summary>NominalAirFlowRate がコンストラクタ指定値と一致する。</summary>
    [Fact]
    public void Constructor_NominalAirFlowRate_MatchesInput()
    {
      var cond = MakeCond();
      Assert.InRange(cond.NominalAirFlowRate, 0.99, 1.01);
    }

    /// <summary>初期状態では IsShutOff = true（停止状態で待機）。</summary>
    [Fact]
    public void Constructor_InitialState_IsShutOff()
    {
      var cond = MakeCond();
      Assert.True(cond.IsShutOff);
    }

    #endregion

    // ================================================================
    #region GetHeatTransfer

    /// <summary>
    /// 凝縮温度 &gt; 空気入口温度なら熱交換量が正（放熱方向）。
    /// </summary>
    [Fact]
    public void GetHeatTransfer_CondensingAboveAir_IsPositive()
    {
      var cond = MakeCond();
      double q = cond.GetHeatTransfer(50.0, 1.0, 35.0, 0.018);
      Assert.True(q > 0, $"Q={q:F3} kW > 0");
    }

    /// <summary>風量が多いほど熱交換量が増える。</summary>
    [Fact]
    public void GetHeatTransfer_HigherAirFlow_IncreasesHeatTransfer()
    {
      var cond = MakeCond();
      double qLow = cond.GetHeatTransfer(50.0, 0.5, 35.0, 0.018);
      double qHigh = cond.GetHeatTransfer(50.0, 1.5, 35.0, 0.018);
      Assert.True(qHigh > qLow,
          $"High flow Q={qHigh:F3} > Low flow Q={qLow:F3} kW");
    }

    /// <summary>空気入口温度が低いほど熱交換量が増える。</summary>
    [Fact]
    public void GetHeatTransfer_LowerInletAirTemp_IncreasesHeatTransfer()
    {
      var cond = MakeCond();
      double qHot = cond.GetHeatTransfer(50.0, 1.0, 40.0, 0.018);
      double qCold = cond.GetHeatTransfer(50.0, 1.0, 25.0, 0.018);
      Assert.True(qCold > qHot,
          $"Cold inlet Q={qCold:F3} > Hot inlet Q={qHot:F3} kW");
    }

    /// <summary>
    /// 定格条件（凝縮温度・風量・外気温）で GetHeatTransfer が
    /// 定格熱交換量に近い値を返す。
    /// </summary>
    [Fact]
    public void GetHeatTransfer_RatedCondition_MatchesRatedHeatTransfer()
    {
      var cond = MakeCond();
      double q = cond.GetHeatTransfer(50.0, 1.0, 35.0, 0.018);
      Assert.InRange(q, 9.0, 11.0); // 定格10kW ±10%
    }

    #endregion

    // ================================================================
    #region GetCondensingTemperature

    /// <summary>
    /// GetCondensingTemperature は GetHeatTransfer の逆関数。
    /// 定格条件での凝縮温度が50°Cに近い。
    /// </summary>
    [Fact]
    public void GetCondensingTemperature_RatedCondition_MatchesRatedTemp()
    {
      var cond = MakeCond();
      double tCond = cond.GetCondensingTemperature(10.0, 1.0, 35.0, 0.018);
      Assert.InRange(tCond, 48.0, 52.0);
    }

    /// <summary>熱交換量が大きいほど凝縮温度が高くなる。</summary>
    [Fact]
    public void GetCondensingTemperature_LargerHeat_HigherCondensingTemp()
    {
      var cond = MakeCond();
      double tLow = cond.GetCondensingTemperature(5.0, 1.0, 35.0, 0.018);
      double tHigh = cond.GetCondensingTemperature(15.0, 1.0, 35.0, 0.018);
      Assert.True(tHigh > tLow,
          $"High Q Tcond={tHigh:F2}°C > Low Q Tcond={tLow:F2}°C");
    }

    /// <summary>外気温が高いほど凝縮温度が高くなる。</summary>
    [Fact]
    public void GetCondensingTemperature_HigherAmbient_RaisesCondensingTemp()
    {
      var cond = MakeCond();
      double tCool = cond.GetCondensingTemperature(10.0, 1.0, 25.0, 0.010);
      double tHot = cond.GetCondensingTemperature(10.0, 1.0, 40.0, 0.020);
      Assert.True(tHot > tCool,
          $"Hot ambient Tcond={tHot:F2}°C > Cool ambient Tcond={tCool:F2}°C");
    }

    #endregion

    // ================================================================
    #region 水噴霧

    /// <summary>
    /// 水噴霧あり（UseWaterSpray=true, SprayEffectiveness>0）では
    /// 同条件の水噴霧なしより凝縮温度が低くなる（冷却効果）。
    /// </summary>
    [Fact]
    public void GetCondensingTemperature_WithSpray_LowerThanWithoutSpray()
    {
      var condNoSpray = MakeCond();
      condNoSpray.UseWaterSpray = false;
      double tNoSpray = condNoSpray.GetCondensingTemperature(10.0, 1.0, 35.0, 0.018);

      var condSpray = MakeCond();
      condSpray.UseWaterSpray = true;
      condSpray.SprayEffectiveness = 0.4;
      double tSpray = condSpray.GetCondensingTemperature(10.0, 1.0, 35.0, 0.018);

      Assert.True(tSpray < tNoSpray,
          $"Spray Tcond={tSpray:F2}°C < No-spray Tcond={tNoSpray:F2}°C");
    }

    /// <summary>水噴霧使用時は WaterSupply が正。</summary>
    [Fact]
    public void GetCondensingTemperature_WithSpray_WaterSupplyPositive()
    {
      var cond = MakeCond();
      cond.UseWaterSpray = true;
      cond.SprayEffectiveness = 0.4;
      cond.GetCondensingTemperature(10.0, 1.0, 35.0, 0.018);
      Assert.True(cond.WaterSupply > 0,
          $"WaterSupply={cond.WaterSupply:F5} kg/s > 0");
    }

    #endregion
  }
}
