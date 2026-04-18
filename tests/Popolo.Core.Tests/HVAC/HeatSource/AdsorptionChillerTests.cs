/* AdsorptionChillerTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3 — see accompanying LICENSE file.
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
    #region 定格条件定数

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

    #region ヘルパー

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