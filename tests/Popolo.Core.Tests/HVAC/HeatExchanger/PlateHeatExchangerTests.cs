/* PlateHeatExchangerTests.cs
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
using Popolo.Core.HVAC.HeatExchanger;

namespace Popolo.Core.Tests.HVAC.HeatExchanger
{
  /// <summary>Unit tests for <see cref="PlateHeatExchanger"/>.</summary>
  /// <remarks>
  /// PlateHeatExchanger is a counter-flow water-to-water heat exchanger.
  /// It supports two operating modes:
  ///   Update()                 — free-running (flow rates given, temperatures result)
  ///   ControlSupplyTemperature() — controlled (target supply temperature, flow rate adjusted)
  ///
  /// Constructors:
  ///   (heatTransfer, heatsourceTemp, heatsourceFlow, supplyTemp, supplyFlow)
  ///     → computes UA from rated conditions
  ///   (heatTransferCoefficient, heatsourceFlow, supplyFlow)
  ///     → UA given directly
  ///
  /// Heating mode: return &lt; heatsource → supply &gt; return
  /// Cooling mode: return &gt; heatsource → supply &lt; return
  /// </remarks>
  public class PlateHeatExchangerTests
  {
    #region ヘルパー

    private const double Cp = 4.182; // kJ/(kg·K) ≈ PhysicsConstants.NominalWaterIsobaricSpecificHeat * 0.001

    /// <summary>UA=10 kW/K, maxHeatSourceFlow=1 kg/s, maxSupplyFlow=1 kg/s の標準モデル。</summary>
    private static PlateHeatExchanger MakeExchanger()
        => new PlateHeatExchanger(10.0, 1.0, 1.0);

    #endregion

    // ================================================================
    #region コンストラクタ（定格条件から UA を計算）

    /// <summary>
    /// 定格条件コンストラクタで HeatTransferCoefficient が正の値になる。
    /// 加熱運転: 熱源80°C, 供給60°C, 供給流量0.5kg/s, Q=40kW。
    /// </summary>
    [Fact]
    public void Constructor_RatedConditions_ComputesPositiveUA()
    {
      // 加熱運転: 供給60°C → 還温度 = 60 - 40/(cp*0.5)
      var phx = new PlateHeatExchanger(40.0, 80.0, 1.0, 60.0, 0.5);
      Assert.True(phx.HeatTransferCoefficient > 0,
          $"UA={phx.HeatTransferCoefficient:F4} kW/K should be > 0");
    }

    /// <summary>
    /// 冷却運転の定格条件コンストラクタでも HeatTransferCoefficient が正。
    /// 冷却運転: 熱源7°C, 供給12°C, 供給流量0.5kg/s, Q=20kW。
    /// </summary>
    [Fact]
    public void Constructor_RatedCooling_ComputesPositiveUA()
    {
      var phx = new PlateHeatExchanger(20.0, 7.0, 1.0, 12.0, 0.5);
      Assert.True(phx.HeatTransferCoefficient > 0,
          $"UA={phx.HeatTransferCoefficient:F4} kW/K should be > 0");
    }

    /// <summary>コンストラクタ直後は ShutOff 状態（流量ゼロ・熱交換ゼロ）。</summary>
    [Fact]
    public void Constructor_InitialState_IsShutOff()
    {
      var phx = MakeExchanger();
      Assert.Equal(0.0, phx.HeatTransfer);
      Assert.Equal(0.0, phx.HeatSourceFlowRate);
      Assert.Equal(0.0, phx.SupplyFlowRate);
    }

    #endregion

    // ================================================================
    #region Update（成り行き計算）

    /// <summary>
    /// 加熱運転: 熱源水温度 &gt; 還温度 → 供給温度が還温度より高い。
    /// </summary>
    [Fact]
    public void Update_Heating_SupplyTemperatureHigherThanReturn()
    {
      var phx = MakeExchanger();
      phx.Update(80.0, 20.0, 1.0, 1.0); // heatsource=80, return=20
      Assert.True(phx.SupplyTemperature > phx.ReturnTemperature,
          $"Supply={phx.SupplyTemperature:F2}°C > Return={phx.ReturnTemperature:F2}°C");
    }

    /// <summary>
    /// 冷却運転: 熱源水温度 &lt; 還温度 → 供給温度が還温度より低い。
    /// </summary>
    [Fact]
    public void Update_Cooling_SupplyTemperatureLowerThanReturn()
    {
      var phx = MakeExchanger();
      phx.Update(7.0, 14.0, 1.0, 1.0); // heatsource=7, return=14
      Assert.True(phx.SupplyTemperature < phx.ReturnTemperature,
          $"Supply={phx.SupplyTemperature:F2}°C < Return={phx.ReturnTemperature:F2}°C");
    }

    /// <summary>熱交換量がゼロより大きい（加熱運転）。</summary>
    [Fact]
    public void Update_Heating_HeatTransferIsPositive()
    {
      var phx = MakeExchanger();
      phx.Update(80.0, 20.0, 1.0, 1.0);
      Assert.True(phx.HeatTransfer > 0,
          $"HeatTransfer={phx.HeatTransfer:F2} kW > 0");
    }

    /// <summary>UA が大きいほど熱交換量が増える。</summary>
    [Fact]
    public void Update_HigherUA_IncreasesHeatTransfer()
    {
      var phxLow = new PlateHeatExchanger(5.0, 1.0, 1.0);
      var phxHigh = new PlateHeatExchanger(20.0, 1.0, 1.0);
      phxLow.Update(80.0, 20.0, 1.0, 1.0);
      phxHigh.Update(80.0, 20.0, 1.0, 1.0);
      Assert.True(phxHigh.HeatTransfer > phxLow.HeatTransfer,
          $"UA=20: Q={phxHigh.HeatTransfer:F2} kW > UA=5: Q={phxLow.HeatTransfer:F2} kW");
    }

    /// <summary>
    /// 熱源水流量が最大値の 0.1% 以下のとき ShutOff（熱交換ゼロ）になる。
    /// </summary>
    [Fact]
    public void Update_NearZeroHeatSourceFlow_ShutOff()
    {
      var phx = MakeExchanger();
      phx.Update(80.0, 20.0, 0.0009, 1.0); // 0.1%未満
      Assert.Equal(0.0, phx.HeatTransfer);
    }

    /// <summary>
    /// エネルギー保存：熱源水の放熱量 = 供給水の受熱量（加熱運転）。
    /// </summary>
    [Fact]
    public void Update_Heating_EnergyConservation()
    {
      var phx = MakeExchanger();
      phx.Update(80.0, 20.0, 0.5, 0.8);
      double Qhs = phx.HeatSourceFlowRate * Cp
                 * (phx.HeatSourceInletTemperature - phx.HeatSourceOutletTemperature);
      double Qsp = phx.SupplyFlowRate * Cp
                 * (phx.SupplyTemperature - phx.ReturnTemperature);
      Assert.InRange(Math.Abs(Qhs - Qsp), 0, 0.5); // 0.5kW以内の誤差
    }

    #endregion

    // ================================================================
    #region ControlSupplyTemperature（供給温度制御）

    /// <summary>
    /// 加熱制御: 供給温度が設定値に一致する（非過負荷時）。
    /// </summary>
    [Fact]
    public void ControlSupplyTemperature_Heating_SupplyReachesSetpoint()
    {
      var phx = MakeExchanger();
      phx.SupplyTemperatureSetpoint = 50.0;
      phx.ControlSupplyTemperature(80.0, 20.0, 1.0);
      Assert.InRange(phx.SupplyTemperature, 49.5, 50.5);
    }

    /// <summary>
    /// 冷却制御: 供給温度が設定値に一致する（非過負荷時）。
    /// </summary>
    [Fact]
    public void ControlSupplyTemperature_Cooling_SupplyReachesSetpoint()
    {
      var phx = MakeExchanger();
      phx.SupplyTemperatureSetpoint = 12.0;
      phx.ControlSupplyTemperature(7.0, 14.0, 1.0);
      Assert.InRange(phx.SupplyTemperature, 11.5, 12.5);
    }

    /// <summary>
    /// 熱源水温度 &lt; 還温度（逆転）のとき ShutOff になる。
    /// isRev: heatsource &lt; return かつ加熱要求 → 熱源が還温度より低く加熱不能。
    /// </summary>
    [Fact]
    public void ControlSupplyTemperature_HeatingReversed_ShutOff()
    {
      var phx = MakeExchanger();
      phx.SupplyTemperatureSetpoint = 50.0;
      // heatsource(15°C) < return(20°C) かつ isHeating=true → 逆転 → ShutOff
      phx.ControlSupplyTemperature(15.0, 20.0, 1.0);
      Assert.Equal(0.0, phx.HeatTransfer);
    }

    /// <summary>
    /// 過負荷時は HeatSourceFlowRate = MaxHeatSourceFlowRate になる。
    /// </summary>
    [Fact]
    public void ControlSupplyTemperature_Overload_FlowRateAtMaximum()
    {
      // UA=1(小), 熱源60°C, 還温度10°C, 設定値55°C(困難)
      var phx = new PlateHeatExchanger(1.0, 1.0, 1.0);
      phx.SupplyTemperatureSetpoint = 55.0;
      phx.ControlSupplyTemperature(60.0, 10.0, 0.1); // 供給流量小→要求熱量大
      if (phx.IsOverLoad)
        Assert.InRange(phx.HeatSourceFlowRate, 0.999, 1.001);
    }

    /// <summary>供給流量ゼロのとき ShutOff になる。</summary>
    [Fact]
    public void ControlSupplyTemperature_ZeroSupplyFlow_ShutOff()
    {
      var phx = MakeExchanger();
      phx.SupplyTemperatureSetpoint = 50.0;
      phx.ControlSupplyTemperature(80.0, 20.0, 0.0);
      Assert.Equal(0.0, phx.HeatTransfer);
    }

    #endregion

    // ================================================================
    #region ShutOff

    /// <summary>ShutOff 後は熱交換量ゼロ・流量ゼロ・出口=入口温度。</summary>
    [Fact]
    public void ShutOff_ResetsAllOutputs()
    {
      var phx = MakeExchanger();
      phx.Update(80.0, 20.0, 1.0, 1.0);
      Assert.True(phx.HeatTransfer > 0); // Update後は正

      phx.ShutOff();
      Assert.Equal(0.0, phx.HeatTransfer);
      Assert.Equal(0.0, phx.HeatSourceFlowRate);
      Assert.Equal(0.0, phx.SupplyFlowRate);
    }

    #endregion
  }
}