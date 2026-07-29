/* HumidifierTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3 — see accompanying LICENSE file.
 */

using System;
using Xunit;
using Popolo.Core.Exceptions;
using Popolo.Core.HVAC.AirSide;
using Popolo.Core.Physics;

namespace Popolo.Core.Tests.HVAC.AirSide
{
  /// <summary>Unit tests for <see cref="Humidifier"/>.</summary>
  public class HumidifierTests
  {
    //標準的な冬季の温水コイル出口空気（30C, 0.005 kg/kg）
    private const double TIN = 30.0;
    private const double WIN = 0.005;
    private const double FLOW = 2.0;  //[kg/s]

    #region Constructors

    /// <summary>方式別のデフォルト値が設定される。</summary>
    [Theory]
    [InlineData(Humidifier.HumidifierType.Steam, 1.0, 0.9)]
    [InlineData(Humidifier.HumidifierType.WettedMedia, 0.8, 0.5)]
    [InlineData(Humidifier.HumidifierType.Ultrasonic, 0.5, 0.9)]
    [InlineData(Humidifier.HumidifierType.Atomizing, 0.3, 0.4)]
    public void Constructor_SetsDefaultEfficiencies(
      Humidifier.HumidifierType type, double maxSatEff, double wsCoef)
    {
      var hm = new Humidifier(type);
      Assert.Equal(maxSatEff, hm.MaxSaturationEfficiency);
      Assert.Equal(wsCoef, hm.WaterSupplyCoefficient);
    }

    /// <summary>範囲外の効率指定は例外を投げる。</summary>
    [Fact]
    public void Constructor_InvalidEfficiency_Throws()
    {
      Assert.Throws<PopoloArgumentException>(
        () => new Humidifier(Humidifier.HumidifierType.Steam, 0.0, 0.9));
      Assert.Throws<PopoloArgumentException>(
        () => new Humidifier(Humidifier.HumidifierType.Steam, 1.1, 0.9));
      Assert.Throws<PopoloArgumentException>(
        () => new Humidifier(Humidifier.HumidifierType.Steam, 1.0, 0.0));
      Assert.Throws<PopoloArgumentException>(
        () => new Humidifier(Humidifier.HumidifierType.Steam, 1.0, 1.1));
    }

    /// <summary>蒸気加湿のみ非断熱（乾球温度一定）である。</summary>
    [Fact]
    public void IsAdiabatic_TrueExceptSteam()
    {
      Assert.False(new Humidifier(Humidifier.HumidifierType.Steam).IsAdiabatic);
      Assert.True(new Humidifier(Humidifier.HumidifierType.WettedMedia).IsAdiabatic);
      Assert.True(new Humidifier(Humidifier.HumidifierType.Atomizing).IsAdiabatic);
      Assert.True(new Humidifier(Humidifier.HumidifierType.Ultrasonic).IsAdiabatic);
    }

    #endregion

    #region ControlOutletHumidityRatio — outlet humidity control

    /// <summary>能力範囲内なら出口絶対湿度が設定値に一致する。</summary>
    [Fact]
    public void Control_WithinCapacity_ReachesSetpoint()
    {
      var hm = new Humidifier(Humidifier.HumidifierType.WettedMedia);
      const double sp = 0.007;

      bool suc = hm.ControlOutletHumidityRatio(TIN, WIN, FLOW, sp);

      Assert.True(suc);
      Assert.Equal(sp, hm.OutletAirHumidityRatio, precision: 9);
    }

    /// <summary>水加湿は等比エンタルピー線上を移動する（式26.4の断熱加湿）。</summary>
    [Fact]
    public void Control_WaterHumidification_ConstantEnthalpy()
    {
      var hm = new Humidifier(Humidifier.HumidifierType.WettedMedia);
      hm.ControlOutletHumidityRatio(TIN, WIN, FLOW, 0.007);

      double hIn = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(TIN, WIN);
      double hOut = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(
        hm.OutletAirTemperature, hm.OutletAirHumidityRatio);

      Assert.Equal(hIn, hOut, precision: 6);
      //加湿により乾球温度は低下する
      Assert.True(hm.OutletAirTemperature < TIN);
    }

    /// <summary>蒸気加湿は乾球温度一定線上を移動する。</summary>
    [Fact]
    public void Control_SteamHumidification_ConstantDryBulbTemperature()
    {
      var hm = new Humidifier(Humidifier.HumidifierType.Steam);
      hm.ControlOutletHumidityRatio(TIN, WIN, FLOW, 0.010);

      Assert.Equal(TIN, hm.OutletAirTemperature, precision: 9);
      Assert.Equal(0.010, hm.OutletAirHumidityRatio, precision: 9);
    }

    /// <summary>飽和効率は式26.4と整合する。</summary>
    [Fact]
    public void Control_SaturationEfficiencyMatchesEq26_4()
    {
      var hm = new Humidifier(Humidifier.HumidifierType.WettedMedia);
      const double sp = 0.007;
      hm.ControlOutletHumidityRatio(TIN, WIN, FLOW, sp);

      double hIn = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(TIN, WIN);
      double satW = MoistAir.GetSaturationHumidityRatioFromEnthalpy(
        hIn, PhysicsConstants.StandardAtmosphericPressure);
      double expected = (sp - WIN) / (satW - WIN);

      Assert.Equal(expected, hm.SaturationEfficiency, precision: 9);
    }

    /// <summary>能力を超える設定値では最大飽和効率でクランプされ、falseを返す。</summary>
    [Fact]
    public void Control_BeyondCapacity_ClampsToMaxEfficiency()
    {
      var hm = new Humidifier(Humidifier.HumidifierType.Atomizing);  //最大30%
      const double sp = 0.030;  //明らかに能力超過

      bool suc = hm.ControlOutletHumidityRatio(TIN, WIN, FLOW, sp);

      Assert.False(suc);
      Assert.Equal(hm.MaxSaturationEfficiency, hm.SaturationEfficiency, precision: 9);

      double hIn = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(TIN, WIN);
      double satW = MoistAir.GetSaturationHumidityRatioFromEnthalpy(
        hIn, PhysicsConstants.StandardAtmosphericPressure);
      double wMax = (1 - hm.MaxSaturationEfficiency) * WIN + hm.MaxSaturationEfficiency * satW;
      Assert.Equal(wMax, hm.OutletAirHumidityRatio, precision: 9);
    }

    /// <summary>加湿不要（設定値が入口湿度以下）の場合には出口=入口となる。</summary>
    [Fact]
    public void Control_SetpointBelowInlet_NoHumidification()
    {
      var hm = new Humidifier(Humidifier.HumidifierType.Steam);
      bool suc = hm.ControlOutletHumidityRatio(TIN, WIN, FLOW, 0.003);

      Assert.True(suc);
      Assert.Equal(WIN, hm.OutletAirHumidityRatio, precision: 9);
      Assert.Equal(0.0, hm.SteamConsumption);
      Assert.Equal(0.0, hm.SaturationEfficiency);
    }

    #endregion

    #region UpdateOutletState — free-run operation

    /// <summary>成り行き運転では式26.5の出口湿度となる。</summary>
    [Fact]
    public void UpdateOutletState_FollowsEq26_5()
    {
      var hm = new Humidifier(Humidifier.HumidifierType.WettedMedia);
      hm.SaturationEfficiency = 0.4;
      hm.UpdateOutletState(TIN, WIN, FLOW);

      double hIn = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(TIN, WIN);
      double satW = MoistAir.GetSaturationHumidityRatioFromEnthalpy(
        hIn, PhysicsConstants.StandardAtmosphericPressure);
      double expected = (1 - 0.4) * WIN + 0.4 * satW;

      Assert.Equal(expected, hm.OutletAirHumidityRatio, precision: 9);
    }

    /// <summary>成り行き運転でも蒸気加湿が働く（分離前実装のバグ修正確認）。</summary>
    [Fact]
    public void UpdateOutletState_SteamFreeRunning_Humidifies()
    {
      var hm = new Humidifier(Humidifier.HumidifierType.Steam);
      hm.SaturationEfficiency = 0.2;
      hm.UpdateOutletState(TIN, WIN, FLOW);

      Assert.True(WIN < hm.OutletAirHumidityRatio);
      Assert.True(0 < hm.SteamConsumption);
      Assert.Equal(TIN, hm.OutletAirTemperature, precision: 9);
    }

    /// <summary>指定された飽和効率は最大飽和効率でクランプされる。</summary>
    [Fact]
    public void UpdateOutletState_EfficiencyClampedToMax()
    {
      var hm = new Humidifier(Humidifier.HumidifierType.Ultrasonic);  //最大50%
      hm.SaturationEfficiency = 0.9;
      hm.UpdateOutletState(TIN, WIN, FLOW);

      double hIn = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(TIN, WIN);
      double satW = MoistAir.GetSaturationHumidityRatioFromEnthalpy(
        hIn, PhysicsConstants.StandardAtmosphericPressure);
      double wMax = (1 - 0.5) * WIN + 0.5 * satW;
      Assert.Equal(wMax, hm.OutletAirHumidityRatio, precision: 9);
    }

    #endregion

    #region Water supply and steam consumption

    /// <summary>給水量は加湿量を給水有効利用率で除した値となる（式26.6）。</summary>
    [Fact]
    public void WaterConsumption_FollowsEq26_6()
    {
      var hm = new Humidifier(Humidifier.HumidifierType.WettedMedia);
      const double sp = 0.007;
      hm.ControlOutletHumidityRatio(TIN, WIN, FLOW, sp);

      double expected = (sp - WIN) * FLOW / hm.WaterSupplyCoefficient;
      Assert.Equal(expected, hm.WaterConsumption, precision: 9);
      Assert.Equal(0.0, hm.SteamConsumption);
    }

    /// <summary>蒸気加湿では蒸気量が計上され、給水量は0となる。</summary>
    [Fact]
    public void SteamConsumption_SteamType()
    {
      var hm = new Humidifier(Humidifier.HumidifierType.Steam);
      const double sp = 0.010;
      hm.ControlOutletHumidityRatio(TIN, WIN, FLOW, sp);

      double expected = (sp - WIN) * FLOW / hm.WaterSupplyCoefficient;
      Assert.Equal(expected, hm.SteamConsumption, precision: 9);
      Assert.Equal(0.0, hm.WaterConsumption);
    }

    #endregion

    #region ShutOff and stop methods

    /// <summary>ShutOffで風量・消費量・飽和効率が0になる。</summary>
    [Fact]
    public void ShutOff_ZeroState()
    {
      var hm = new Humidifier(Humidifier.HumidifierType.WettedMedia);
      hm.ControlOutletHumidityRatio(TIN, WIN, FLOW, 0.007);
      hm.ShutOff();

      Assert.Equal(0.0, hm.AirFlowRate);
      Assert.Equal(0.0, hm.WaterConsumption);
      Assert.Equal(0.0, hm.SteamConsumption);
      Assert.Equal(0.0, hm.SaturationEfficiency);
    }

    /// <summary>風量0で呼び出すと停止処理となる。</summary>
    [Fact]
    public void ZeroAirFlow_ShutsOff()
    {
      var hm = new Humidifier(Humidifier.HumidifierType.Steam);
      bool suc = hm.ControlOutletHumidityRatio(TIN, WIN, 0.0, 0.010);

      Assert.False(suc);
      Assert.Equal(0.0, hm.AirFlowRate);
      Assert.Equal(0.0, hm.SteamConsumption);
    }

    #endregion
  }
}
