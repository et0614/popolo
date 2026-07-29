/* EnergyRecoveryVentilatorTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3 — see accompanying LICENSE file.
 */

using System;
using Xunit;
using Popolo.Core.HVAC.AirSide;
using Popolo.Core.HVAC.FluidCircuit;
using Popolo.Core.HVAC.HeatExchanger;

namespace Popolo.Core.Tests.HVAC.AirSide
{
  /// <summary>Unit tests for <see cref="EnergyRecoveryVentilator"/>.</summary>
  public class EnergyRecoveryVentilatorTests
  {
    //定格風量 500 m3/h
    private const double FLOW_VOL = 500.0;                    //[m3/h]
    private static readonly double FLOW_MASS = FLOW_VOL / 3600.0 * 1.2;  //[kg/s]

    //冬季条件（OA 2C/0.002, RA 22C/0.008）
    private const double OAT_W = 2.0;
    private const double OAW_W = 0.002;
    private const double RAT_W = 22.0;
    private const double RAW_W = 0.008;

    //夏季条件（OA 34C/0.019, RA 26C/0.0105）
    private const double OAT_S = 34.0;
    private const double OAW_S = 0.019;
    private const double RAT_S = 26.0;
    private const double RAW_S = 0.0105;

    private static EnergyRecoveryVentilator MakeERV()
    {
      //暖房・冷房の2条件で初期化した全熱交換器
      var hex = new AirToAirFlatPlateHeatExchanger(
          FLOW_VOL, FLOW_VOL,
          0.75, 0.65, AirToAirFlatPlateHeatExchanger.Condition.JISB8628_2003_Heating,
          0.70, 0.50, AirToAirFlatPlateHeatExchanger.Condition.JISB8628_2003_Cooling,
          AirToAirFlatPlateHeatExchanger.AirFlow.CounterFlow,
          isEnthalpyEfficiency: false);
      var saFan = new CentrifugalFan(0.15, FLOW_VOL / 3600.0, 0.15, FLOW_VOL / 3600.0, 3, false);
      var eaFan = new CentrifugalFan(0.15, FLOW_VOL / 3600.0, 0.15, FLOW_VOL / 3600.0, 3, false);
      var erv = new EnergyRecoveryVentilator(hex, saFan, eaFan);
      erv.SetAirFlowRate(FLOW_MASS, FLOW_MASS);
      return erv;
    }

    private static void SetWinter(EnergyRecoveryVentilator erv)
    {
      erv.OATemperature = OAT_W;
      erv.OAHumidityRatio = OAW_W;
      erv.RATemperature = RAT_W;
      erv.RAHumidityRatio = RAW_W;
    }

    private static void SetSummer(EnergyRecoveryVentilator erv)
    {
      erv.OATemperature = OAT_S;
      erv.OAHumidityRatio = OAW_S;
      erv.RATemperature = RAT_S;
      erv.RAHumidityRatio = RAW_S;
    }

    #region Ventilation operation

    /// <summary>冬季は外気が予熱・加湿されて給気される（+給気ファン昇温）。</summary>
    [Fact]
    public void Ventilate_Winter_RecoversHeatAndMoisture()
    {
      var erv = MakeERV();
      SetWinter(erv);
      erv.Ventilate();

      //温度はOAとRAの間+ファン昇温、湿度はOAとRAの間
      Assert.True(OAT_W < erv.SATemperature,
          $"SA temp={erv.SATemperature:F2}C > OA {OAT_W:F1}C");
      Assert.InRange(erv.SAHumidityRatio, OAW_W, RAW_W);

      //排気は逆に冷却・減湿されて捨てられる
      Assert.True(erv.EATemperature < RAT_W);
      Assert.True(erv.EAHumidityRatio < RAW_W);
    }

    /// <summary>夏季は外気が予冷・減湿されて給気される。</summary>
    [Fact]
    public void Ventilate_Summer_PrecoolsOutdoorAir()
    {
      var erv = MakeERV();
      SetSummer(erv);
      erv.Ventilate();

      //ファン昇温があっても交換分の低下が上回る
      Assert.True(erv.SATemperature < OAT_S,
          $"SA temp={erv.SATemperature:F2}C < OA {OAT_S:F1}C");
      Assert.InRange(erv.SAHumidityRatio, RAW_S, OAW_S);
    }

    /// <summary>給気ファン昇温は熱交換の後に加算される。</summary>
    [Fact]
    public void Ventilate_FanHeatAddedAfterExchange()
    {
      var erv = MakeERV();
      SetWinter(erv);
      erv.Ventilate();

      //給気温度 = 交換器SA出口温度 + ファン昇温 > 交換器SA出口温度
      Assert.True(erv.HeatExchanger.SupplyAirOutletDryBulbTemperature < erv.SATemperature);
      //湿度は交換器出口のまま（ファンは湿度を変えない）
      Assert.Equal(erv.HeatExchanger.SupplyAirOutletHumidityRatio, erv.SAHumidityRatio, precision: 9);
    }

    #endregion

    #region Bypass operation

    /// <summary>バイパス時は熱回収されず、給気=外気+ファン昇温となる。</summary>
    [Fact]
    public void Ventilate_Bypass_NoRecovery()
    {
      var erv = MakeERV();
      SetWinter(erv);
      erv.BypassHeatExchanger = true;
      erv.Ventilate();

      Assert.True(OAT_W < erv.SATemperature);  //ファン昇温のみ
      Assert.True(erv.SATemperature < OAT_W + 2.0);  //交換器の予熱(10C以上)は無い
      Assert.Equal(OAW_W, erv.SAHumidityRatio, precision: 9);
      Assert.Equal(RAT_W, erv.EATemperature, precision: 9);
    }

    /// <summary>排気風量0の場合には熱回収されない。</summary>
    [Fact]
    public void Ventilate_ZeroExhaustFlow_NoRecovery()
    {
      var erv = MakeERV();
      SetWinter(erv);
      erv.SetAirFlowRate(FLOW_MASS, 0.0);
      erv.Ventilate();

      Assert.Equal(OAW_W, erv.SAHumidityRatio, precision: 9);
      Assert.True(erv.SATemperature < OAT_W + 2.0);
    }

    #endregion

    #region Electric consumption and shut-off

    /// <summary>運転中は給気・排気ファンの消費電力が正となる。</summary>
    [Fact]
    public void GetElectricConsumption_PositiveWhileRunning()
    {
      var erv = MakeERV();
      SetWinter(erv);
      erv.Ventilate();
      Assert.True(0 < erv.GetElectricConsumption());
    }

    /// <summary>ShutOffで風量が0になり給気状態は外気と等しくなる。</summary>
    [Fact]
    public void ShutOff_ZeroState()
    {
      var erv = MakeERV();
      SetWinter(erv);
      erv.Ventilate();
      erv.ShutOff();

      Assert.Equal(0.0, erv.SAFlowRate);
      Assert.Equal(0.0, erv.EAFlowRate);
      Assert.Equal(OAT_W, erv.SATemperature, precision: 9);
      Assert.Equal(OAW_W, erv.SAHumidityRatio, precision: 9);
    }

    /// <summary>給気風量0で運転すると停止処理となる。</summary>
    [Fact]
    public void Ventilate_ZeroSupplyFlow_ShutsOff()
    {
      var erv = MakeERV();
      SetWinter(erv);
      erv.SetAirFlowRate(0.0, FLOW_MASS);
      erv.Ventilate();

      Assert.Equal(0.0, erv.SAFlowRate);
      Assert.Equal(0.0, erv.EAFlowRate);
    }

    #endregion
  }
}
