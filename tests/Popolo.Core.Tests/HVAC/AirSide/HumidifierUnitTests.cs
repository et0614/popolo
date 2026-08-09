/* HumidifierUnitTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3 — see accompanying LICENSE file.
 */

using System;
using Xunit;
using Popolo.Core.HVAC.AirSide;
using Popolo.Core.HVAC.FluidCircuit;
using Popolo.Core.Physics;

namespace Popolo.Core.Tests.HVAC.AirSide
{
  /// <summary>Unit tests for <see cref="HumidifierUnit"/>.</summary>
  public class HumidifierUnitTests
  {
    //冬季室内空気相当（22C, 0.005 kg/kg）
    private const double TIN = 22.0;
    private const double WIN = 0.005;

    //風量 1 m3/s（約1.2 kg/s）
    private static readonly double Qa = 1.0;
    private static readonly double Ma = Qa * 1.2;

    private static HumidifierUnit MakeUnit(
      Humidifier.HumidifierType type = Humidifier.HumidifierType.WettedMedia)
    {
      var hm = new Humidifier(type);
      var fan = new CentrifugalFan(0.3, Qa, 0.3, Qa, 3, false);
      var unit = new HumidifierUnit(hm, fan);
      unit.InletAirTemperature = TIN;
      unit.InletAirHumidityRatio = WIN;
      unit.SetAirFlowRate(Ma);
      return unit;
    }

    #region Outlet humidity control

    /// <summary>能力範囲内なら出口絶対湿度が設定値に一致する。</summary>
    [Fact]
    public void Control_WithinCapacity_ReachesSetpoint()
    {
      var unit = MakeUnit();
      const double sp = 0.0065;

      bool suc = unit.ControlOutletHumidityRatio(sp);

      Assert.True(suc);
      Assert.Equal(sp, unit.OutletAirHumidityRatio, precision: 9);
    }

    /// <summary>
    /// ファン発熱で昇温した空気が加湿される：
    /// 出口比エンタルピーはファン出口空気（入口+昇温）の比エンタルピーに一致する。
    /// </summary>
    [Fact]
    public void Control_FanHeatAddedBeforeHumidification()
    {
      var unit = MakeUnit();
      unit.ControlOutletHumidityRatio(0.0065);

      //ファン昇温後の温度（加湿器入口）
      double tFanOut = unit.Humidifier.InletAirTemperature;
      Assert.True(TIN < tFanOut, $"fan outlet {tFanOut:F2}C > inlet {TIN:F2}C");

      //水加湿は断熱加湿のため、比エンタルピーはファン出口空気と一致
      double hFanOut = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(tFanOut, WIN);
      double hOut = MoistAir.GetEnthalpyFromDryBulbTemperatureAndHumidityRatio(
        unit.OutletAirTemperature, unit.OutletAirHumidityRatio);
      Assert.Equal(hFanOut, hOut, precision: 6);
    }

    /// <summary>蒸気加湿ユニットでは出口温度がファン出口温度と一致する。</summary>
    [Fact]
    public void Control_SteamUnit_OutletTemperatureEqualsFanOutlet()
    {
      var unit = MakeUnit(Humidifier.HumidifierType.Steam);
      unit.ControlOutletHumidityRatio(0.008);

      Assert.Equal(unit.Humidifier.InletAirTemperature, unit.OutletAirTemperature, precision: 9);
      Assert.True(0 < unit.SteamConsumption);
    }

    /// <summary>能力を超える設定値ではfalseを返す。</summary>
    [Fact]
    public void Control_BeyondCapacity_ReturnsFalse()
    {
      var unit = MakeUnit(Humidifier.HumidifierType.Atomizing);
      bool suc = unit.ControlOutletHumidityRatio(0.030);
      Assert.False(suc);
    }

    #endregion

    #region Free-run operation

    /// <summary>成り行き運転では現在の飽和効率で加湿される。</summary>
    [Fact]
    public void Humidify_FreeRunning_UsesCurrentEfficiency()
    {
      var unit = MakeUnit();
      var hm = (Humidifier)unit.Humidifier;
      hm.SaturationEfficiency = 0.4;
      unit.Humidify();

      Assert.True(WIN < unit.OutletAirHumidityRatio);
      Assert.True(0 < unit.WaterConsumption);
    }

    /// <summary>飽和効率0の成り行き運転ではファン昇温のみが現れる。</summary>
    [Fact]
    public void Humidify_ZeroEfficiency_OnlyFanHeat()
    {
      var unit = MakeUnit();
      var hm = (Humidifier)unit.Humidifier;
      hm.SaturationEfficiency = 0.0;
      unit.Humidify();

      Assert.True(TIN < unit.OutletAirTemperature);
      Assert.Equal(WIN, unit.OutletAirHumidityRatio, precision: 9);
      Assert.Equal(0.0, unit.WaterConsumption);
    }

    #endregion

    #region Electric consumption and shut-off

    /// <summary>運転中のファン消費電力は正となる。</summary>
    [Fact]
    public void GetElectricConsumption_PositiveWhileRunning()
    {
      var unit = MakeUnit();
      unit.ControlOutletHumidityRatio(0.0065);
      Assert.True(0 < unit.GetElectricConsumption());
    }

    /// <summary>ShutOffで風量・消費量が0になる。</summary>
    [Fact]
    public void ShutOff_ZeroState()
    {
      var unit = MakeUnit();
      unit.ControlOutletHumidityRatio(0.0065);
      unit.ShutOff();

      Assert.Equal(0.0, unit.AirFlowRate);
      Assert.Equal(0.0, unit.WaterConsumption);
      Assert.Equal(0.0, unit.SteamConsumption);
    }

    /// <summary>風量0で制御を呼ぶと停止処理となりfalseを返す。</summary>
    [Fact]
    public void ZeroAirFlow_ShutsOff()
    {
      var unit = MakeUnit();
      unit.SetAirFlowRate(0.0);
      bool suc = unit.ControlOutletHumidityRatio(0.0065);

      Assert.False(suc);
      Assert.Equal(0.0, unit.AirFlowRate);
    }

    #endregion

    #region Air flow notches

    /// <summary>ノッチ切替・階段動作・無効化が機能する（ノッチはファンに設定し、ユニットは委譲）</summary>
    [Fact]
    public void Notch_SetRaiseLowerAndInvalidate()
    {
      var hm = new Humidifier(Humidifier.HumidifierType.WettedMedia);
      var fan = new CentrifugalFan(0.3, Qa, 0.3, Qa, 3, false);
      fan.SetFlowNotches(("弱", 0.5), ("中", 0.75), ("強", 1.0)); //体積流量[m3/s]
      var unit = new HumidifierUnit(hm, fan);
      unit.InletAirTemperature = TIN;
      unit.InletAirHumidityRatio = WIN;
      Assert.Equal(3, unit.NotchCount);

      unit.SetNotch("中");
      Assert.Equal(1, unit.CurrentNotchIndex);
      Assert.Equal(0.9, unit.AirFlowRate, precision: 12);

      Assert.True(unit.RaiseNotch());
      Assert.Equal("強", unit.CurrentNotchName);
      Assert.Equal(1.2, unit.AirFlowRate, precision: 12);
      Assert.False(unit.RaiseNotch());

      Assert.True(unit.LowerNotch());
      Assert.True(unit.LowerNotch());
      Assert.False(unit.LowerNotch()); //最小で停止はしない
      Assert.Equal(0.6, unit.AirFlowRate, precision: 12);

      unit.ShutOff();
      Assert.Equal(-1, unit.CurrentNotchIndex);
      Assert.True(unit.RaiseNotch()); //未選択からのRaiseは最小ノッチで復帰
      Assert.Equal(0, unit.CurrentNotchIndex);
      Assert.Equal(0.6, unit.AirFlowRate, precision: 12);
    }

    #endregion
  }
}
