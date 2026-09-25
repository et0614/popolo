/* AdsorptionChillerTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 * Licensed under the Apache License, Version 2.0 - see the accompanying LICENSE file.
 */

using System;
using Xunit;
using Popolo.Core.HVAC.HeatSource;

namespace Popolo.Core.Tests.HVAC.HeatSource
{
  /// <summary>Unit tests for <see cref="AdsorptionChiller"/>.</summary>
  /// <remarks>
  /// Rated conditions (from adsorptionChillerTest() sample):
  ///   Chilled water:  22C -> ~19.66C, 54/60 kg/s
  ///   Cooling water:  27C -> ~30.1C,  127/60 kg/s
  ///   Hot water:      55C -> ~50.8C,  64.5/60 kg/s
  ///   Cooling load:   8.8 kW, COP ~= 0.47
  /// </remarks>
  public class AdsorptionChillerTests
  {
    #region Rated condition constants

    private static readonly double MChW = 54.0 / 60.0;
    private static readonly double MCdW = 127.0 / 60.0;
    private static readonly double MHW = 64.5 / 60.0;
    private const double QCh = 8.8;
    private const double COP0 = 0.47;
    private const double TChi = 22.0;
    private const double THi = 55.0;
    private const double TCdi = 27.0;
    private const double Cp = 4.186;

    #endregion

    #region Helpers

    private static AdsorptionChiller MakeChiller()
    {
      double qh = QCh / COP0;
      double qcd = QCh + qh;
      double tcho = TChi - QCh / (Cp * MChW);
      double tho = THi - qh / (Cp * MHW);
      double tcdo = TCdi + qcd / (Cp * MCdW);

      var c = new AdsorptionChiller(
          TChi, tcho, MChW,
          TCdi, tcdo, MCdW,
          THi, tho, MHW);
      c.ChilledWaterOutletSetpointTemperature = 0; // 成り行き運転
      return c;
    }

    #endregion

    [Fact]
    public void Constructor_NominalCapacity_IsPositive()
    {
      var c = MakeChiller();
      Assert.True(c.NominalCapacity > 0,
          $"NominalCapacity={c.NominalCapacity:F2} kW > 0");
    }

    [Fact]
    public void Constructor_NominalCOP_InRealisticRange()
    {
      var c = MakeChiller();
      Assert.InRange(c.NominalCOP, 0.2, 1.0);
    }

    [Fact]
    public void Constructor_InitialState_CoolingLoadIsZero()
    {
      var c = MakeChiller();
      Assert.Equal(0.0, c.CoolingLoad);
    }

    [Fact]
    public void Update_RatedCondition_CoolingLoadIsPositive()
    {
      var c = MakeChiller();
      c.Update(TChi, MChW, TCdi, MCdW, THi, MHW);
      Assert.True(c.CoolingLoad > 0,
          $"CoolingLoad={c.CoolingLoad:F2} kW > 0");
    }

    [Fact]
    public void Update_RatedCondition_ChilledWaterOutletCoolerThanInlet()
    {
      var c = MakeChiller();
      c.Update(TChi, MChW, TCdi, MCdW, THi, MHW);
      Assert.True(c.ChilledWaterOutletTemperature < c.ChilledWaterInletTemperature,
          $"CHW out={c.ChilledWaterOutletTemperature:F2} < in={c.ChilledWaterInletTemperature:F2}");
    }

    [Fact]
    public void Update_RatedCondition_CoolingWaterOutletHigherThanInlet()
    {
      var c = MakeChiller();
      c.Update(TChi, MChW, TCdi, MCdW, THi, MHW);
      Assert.True(c.CoolingWaterOutletTemperature > c.CoolingWaterInletTemperature,
          $"CDW out={c.CoolingWaterOutletTemperature:F2} > in={c.CoolingWaterInletTemperature:F2}");
    }

    [Fact]
    public void Update_RatedCondition_HotWaterOutletCoolerThanInlet()
    {
      var c = MakeChiller();
      c.Update(TChi, MChW, TCdi, MCdW, THi, MHW);
      Assert.True(c.HotWaterOutletTemperature < c.HotWaterInletTemperature,
          $"HW out={c.HotWaterOutletTemperature:F2} < in={c.HotWaterInletTemperature:F2}");
    }

    [Fact]
    public void Update_RatedCondition_COPInRealisticRange()
    {
      var c = MakeChiller();
      c.Update(TChi, MChW, TCdi, MCdW, THi, MHW);
      Assert.InRange(c.COP, 0.1, 1.0);
    }

    /// <summary>温水入口温度が高いほど冷凍能力が大きい（45C vs 55C）。</summary>
    [Fact]
    public void Update_HigherHotWaterTemp_HigherCoolingLoad()
    {
      var cLow = MakeChiller();
      cLow.Update(TChi, MChW, TCdi, MCdW, 45.0, MHW);
      double qLow = cLow.CoolingLoad;

      var cHigh = MakeChiller();
      cHigh.Update(TChi, MChW, TCdi, MCdW, 55.0, MHW);
      double qHigh = cHigh.CoolingLoad;

      Assert.True(qHigh > qLow,
          $"HW=55C: Q={qHigh:F2} kW > HW=45C: Q={qLow:F2} kW");
    }

    /// <summary>冷却水入口温度が低いほど冷凍能力が大きい（33C vs 24C）。</summary>
    [Fact]
    public void Update_LowerCoolingWaterTemp_HigherCoolingLoad()
    {
      var cHot = MakeChiller();
      cHot.Update(TChi, MChW, 33.0, MCdW, THi, MHW);
      double qHot = cHot.CoolingLoad;

      var cCold = MakeChiller();
      cCold.Update(TChi, MChW, 24.0, MCdW, THi, MHW);
      double qCold = cCold.CoolingLoad;

      Assert.True(qCold > qHot,
          $"CDW=24C: Q={qCold:F2} kW > CDW=33C: Q={qHot:F2} kW");
    }

    /// <summary>CyclingTimeRate を変えると冷凍能力が変化する。</summary>
    [Fact]
    public void Update_DifferentCyclingTimeRate_CoolingLoadChanges()
    {
      var c1 = MakeChiller();
      c1.CyclingTimeRatio = 1.0;
      c1.Update(TChi, MChW, TCdi, MCdW, THi, MHW);
      double q1 = c1.CoolingLoad;

      var c2 = MakeChiller();
      c2.CyclingTimeRatio = 2.0;
      c2.Update(TChi, MChW, TCdi, MCdW, THi, MHW);
      double q2 = c2.CoolingLoad;

      Assert.False(Math.Abs(q1 - q2) < 0.01,
          $"CyclingTimeRate=1: Q={q1:F3}, CyclingTimeRate=2: Q={q2:F3} should differ");
    }

    /// <summary>
    /// 低温の冷却水条件（約 9 C）で COP=0 と COP=0.8 の残差がともに正となる
    /// （COP の求根区間が挟めない）場合でも例外とならず、冷凍能力ゼロとして解を返す。
    /// </summary>
    /// <remarks>
    /// この温度域では吸着量の補正により残差関数が不連続となるため、
    /// 入力値は再現性確保のため丸めずに与えている。
    /// </remarks>
    [Fact]
    public void Update_ResidualPositiveAtZeroCOP_ReturnsZeroCoolingLoad()
    {
      var c = MakeChiller();
      c.CyclingTimeRatio = 0.37913373840450426;
      c.Update(4.3966121288931985, MChW * 0.9783220056809122,
          9.424447756458282, MCdW * 1.6059921012753584,
          30.44232354939092, MHW * 0.45109017991977285);
      Assert.Equal(0.0, c.CoolingLoad);
      Assert.Equal(c.ChilledWaterInletTemperature, c.ChilledWaterOutletTemperature);
    }

    /// <summary>
    /// COP=0 と COP=0.8 の残差がともに負となる場合、探索上限を拡張して解を求め、
    /// 例外とならずに物理的に妥当な範囲（0 &lt;= COP &lt;= 1）の結果を返す。
    /// </summary>
    [Fact]
    public void Update_ResidualNegativeAtInitialUpperBound_ExpandsBracket()
    {
      var c = MakeChiller();
      c.CyclingTimeRatio = 0.3005918916412271;
      c.Update(4.619937852779374, MChW * 0.33828143637547337,
          10.891701625609631, MCdW * 1.6833598811101913,
          14.655740272093443, MHW * 0.9446031430943883);
      Assert.True(c.CoolingLoad >= 0, $"CoolingLoad={c.CoolingLoad} >= 0");
      Assert.InRange(c.COP, 0.0, 1.0);
      Assert.True(double.IsFinite(c.ChilledWaterOutletTemperature));
    }

    [Fact]
    public void ShutOff_ZeroCoolingLoad()
    {
      var c = MakeChiller();
      c.Update(TChi, MChW, TCdi, MCdW, THi, MHW);
      Assert.True(c.CoolingLoad > 0);
      c.ShutOff();
      Assert.Equal(0.0, c.CoolingLoad);
    }
  }
}