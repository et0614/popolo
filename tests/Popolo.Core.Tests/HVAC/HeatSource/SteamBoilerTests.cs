/* SteamBoilerTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3 — see accompanying LICENSE file.
 */

using System;
using Xunit;
using Popolo.Core.HVAC.HeatSource;
using Popolo.Core.Physics;

namespace Popolo.Core.Tests.HVAC.HeatSource
{
  /// <summary>Unit tests for <see cref="SteamBoiler"/>.</summary>
  /// <remarks>
  /// SteamBoiler models a fuel-fired steam boiler.
  ///
  /// Constructor: (inletWaterTemp, steamPressure, steamFlowRate,
  ///               fuelConsumption, electricConsumption, ambientTemp,
  ///               airRatio, fuel, smokeTemperature)
  ///   → computes NominalCapacity (latent heat of vaporisation) and heat loss coefficient
  ///
  /// Update(inletWaterTemp, steamFlowRate):
  ///   Computes FuelConsumption and HeatLoad for the requested steam flow.
  ///   If fuel consumption exceeds nominal, adjusts SteamFlowRate (overload mode).
  ///   Stops if steamFlowRate ≤ 0.
  ///
  /// SteamTemperature = saturation temperature at SteamPressure.
  /// </remarks>
  public class SteamBoilerTests
  {
    #region ヘルパー

    // 定格条件: 標準大気圧 101.325 kPa ≈ 100°C, 流量0.02 kg/s
    private const double NominalPressure = 200.0;  // kPa (約120°C)
    private const double NominalFlowRate = 0.02;   // kg/s
    private const double InletWaterTemp = 20.0;   // °C

    /// <summary>標準的な蒸気ボイラを生成する。</summary>
    private static SteamBoiler MakeBoiler()
    {
      // 定格燃料消費量を推定
      double ts = Water.GetSaturationTemperature(NominalPressure);
      double hs = Water.GetSaturatedVaporEnthalpy(ts);
      double hw = Water.GetSaturatedLiquidEnthalpy(InletWaterTemp);
      double nomCap = NominalFlowRate * (hs - hw);
      double nomFuel = Boiler.GetFuelConsumption(
          nomCap, ts, 15.0, Boiler.Fuel.Gas13A, 200, 1.1, 0, 15.0, ts);

      return new SteamBoiler(
          inletWaterTemperature: InletWaterTemp,
          steamPressure: NominalPressure,
          steamFlowRate: NominalFlowRate,
          fuelConsumption: nomFuel,
          electricConsumption: 0.1,
          ambientTemperature: 15.0,
          airRatio: 1.1,
          fuel: Boiler.Fuel.Gas13A,
          smokeTemperature: 200.0);
    }

    #endregion

    // ================================================================
    #region コンストラクタ・プロパティ

    /// <summary>NominalCapacity が正（蒸発潜熱分）。</summary>
    [Fact]
    public void Constructor_NominalCapacity_IsPositive()
    {
      var boiler = MakeBoiler();
      Assert.True(boiler.NominalCapacity > 0,
          $"NominalCapacity={boiler.NominalCapacity:F2} kW > 0");
    }

    /// <summary>
    /// SteamPressure を設定すると SteamTemperature がその飽和温度になる。
    /// ShutOff() が SteamPressure=101.325 kPa（大気圧）にリセットするため、
    /// コンストラクタ直後は大気圧の飽和温度（≈100°C）になる。
    /// 明示的に SteamPressure を設定してから確認する。
    /// </summary>
    [Fact]
    public void SteamPressure_SetToNominal_MatchesSaturationTemp()
    {
      var boiler = MakeBoiler();
      boiler.SteamPressure = NominalPressure;
      double expected = Water.GetSaturationTemperature(NominalPressure);
      Assert.InRange(boiler.SteamTemperature, expected - 0.1, expected + 0.1);
    }

    /// <summary>コンストラクタ直後は ShutOff 状態。</summary>
    [Fact]
    public void Constructor_InitialState_IsShutOff()
    {
      var boiler = MakeBoiler();
      Assert.Equal(0.0, boiler.HeatLoad);
      Assert.Equal(0.0, boiler.FuelConsumption);
    }

    #endregion

    // ================================================================
    #region Update — 通常運転

    /// <summary>HeatLoad が正（蒸発に必要な熱量）。</summary>
    [Fact]
    public void Update_Normal_HeatLoadIsPositive()
    {
      var boiler = MakeBoiler();
      boiler.Update(InletWaterTemp, NominalFlowRate);
      Assert.True(boiler.HeatLoad > 0, $"HeatLoad={boiler.HeatLoad:F2} kW > 0");
    }

    /// <summary>FuelConsumption が正。</summary>
    [Fact]
    public void Update_Normal_FuelConsumptionIsPositive()
    {
      var boiler = MakeBoiler();
      boiler.Update(InletWaterTemp, NominalFlowRate);
      Assert.True(boiler.FuelConsumption > 0,
          $"FuelConsumption={boiler.FuelConsumption:F6} > 0");
    }

    /// <summary>蒸気流量が多いほど燃料消費量が多い。</summary>
    [Fact]
    public void Update_HigherSteamFlow_MoreFuelConsumption()
    {
      var boiler = MakeBoiler();
      boiler.Update(InletWaterTemp, NominalFlowRate * 0.5);
      double fcLow = boiler.FuelConsumption;

      boiler.Update(InletWaterTemp, NominalFlowRate);
      double fcHigh = boiler.FuelConsumption;

      Assert.True(fcHigh > fcLow,
          $"High flow fc={fcHigh:F6} > Low flow fc={fcLow:F6}");
    }

    /// <summary>蒸気流量ゼロのとき ShutOff（HeatLoad=0）。</summary>
    [Fact]
    public void Update_ZeroSteamFlow_ShutOff()
    {
      var boiler = MakeBoiler();
      boiler.Update(InletWaterTemp, 0.0);
      Assert.Equal(0.0, boiler.HeatLoad);
    }

    /// <summary>給水温度が高いほど燃料消費量が少ない（蒸発に必要な熱が減る）。</summary>
    [Fact]
    public void Update_HigherInletTemp_LessFuelConsumption()
    {
      var boiler = MakeBoiler();
      boiler.Update(20.0, NominalFlowRate);
      double fcCold = boiler.FuelConsumption;

      boiler.Update(60.0, NominalFlowRate);
      double fcHot = boiler.FuelConsumption;

      Assert.True(fcHot < fcCold,
          $"Hot inlet fc={fcHot:F6} < Cold inlet fc={fcCold:F6}");
    }

    #endregion

    // ================================================================
    #region ShutOff

    /// <summary>ShutOff 後は HeatLoad・FuelConsumption がゼロ。</summary>
    [Fact]
    public void ShutOff_ResetsOutputs()
    {
      var boiler = MakeBoiler();
      boiler.Update(InletWaterTemp, NominalFlowRate);
      Assert.True(boiler.HeatLoad > 0);

      boiler.ShutOff();
      Assert.Equal(0.0, boiler.HeatLoad);
      Assert.Equal(0.0, boiler.FuelConsumption);
      Assert.Equal(0.0, boiler.SteamFlowRate);
    }

    #endregion

    // ================================================================
    #region SteamPressure

    /// <summary>SteamPressure を変更すると SteamTemperature も変わる。</summary>
    [Fact]
    public void SteamPressure_Change_UpdatesSteamTemperature()
    {
      var boiler = MakeBoiler();
      double tOrig = boiler.SteamTemperature;

      boiler.SteamPressure = 400.0; // より高圧 → より高温
      Assert.True(boiler.SteamTemperature > tOrig,
          $"High pressure Tsat={boiler.SteamTemperature:F2}°C > Low pressure Tsat={tOrig:F2}°C");
    }

    #endregion
  }
}