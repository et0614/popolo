/* CO2BalanceTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3 — see accompanying LICENSE file.
 */

using System;
using Xunit;
using Popolo.Core.Building.AirQuality;
using Popolo.Core.Exceptions;

namespace Popolo.Core.Tests.Building.AirQuality
{
  /// <summary>Unit tests for <see cref="CO2Balance"/>.</summary>
  /// <remarks>
  /// 検証値は解説書26章の式26.18（発生量回帰式）、式26.20（Seidelの式）、
  /// 式26.21（必要換気量の逆算）および例題26.1にもとづく。
  /// 例題26.1: 床面積1,000m2・天井高2.7m、人員密度0.1人/m2、
  /// 外気400ppm・初期500ppmのオフィスで、1時間後に1,000ppmを
  /// 下回るための換気量は0.301m3/s。
  /// </remarks>
  public class CO2BalanceTests
  {
    #region CO2発生量（式26.18）

    /// <summary>日本人男性のオフィス作業（ADu=1.7m2, Met=1.1）で約0.02m3/(h・人)となる。</summary>
    [Fact]
    public void GetCO2GenerationRate_OfficeMale_Near002m3PerHour()
    {
      double rate = CO2Balance.GetCO2GenerationRate(1.7, 1.1, isMale: true);
      Assert.Equal(0.02, rate * 3600.0, 0.001);
    }

    /// <summary>標準発生量定数が回帰式によるオフィス作業者の値と整合する。</summary>
    [Fact]
    public void StandardGenerationRate_ConsistentWithRegression()
    {
      double rate = CO2Balance.GetCO2GenerationRate(1.7, 1.1, isMale: true);
      Assert.Equal(CO2Balance.StandardCO2GenerationRatePerPerson, rate,
        CO2Balance.StandardCO2GenerationRatePerPerson * 0.02);
    }

    /// <summary>代謝量が大きいほど発生量が増える。</summary>
    [Fact]
    public void GetCO2GenerationRate_IncreasesWithMetabolicRate()
    {
      Assert.True(CO2Balance.GetCO2GenerationRate(100.0) < CO2Balance.GetCO2GenerationRate(200.0));
    }

    #endregion

    #region Seidelの式（式26.20・例題26.1）

    /// <summary>例題26.1: 換気量0.301m3/sで1時間後の濃度が約1,078ppmとなる。</summary>
    [Fact]
    public void GetConcentration_Example26_1_FirstHour()
    {
      double c = CO2Balance.GetConcentration(
        initialCO2Level: 500e-6, outdoorCO2Level: 400e-6,
        co2Generation: 5.56e-4, ventilationRate: 0.301,
        airVolume: 2700.0, time: 3600.0);

      Assert.Equal(0.001078, c, 2e-6);
    }

    /// <summary>例題26.1: 同一換気量でさらに1時間経過すると約1,463ppmとなる。</summary>
    [Fact]
    public void GetConcentration_Example26_1_SecondHour()
    {
      double c = CO2Balance.GetConcentration(
        initialCO2Level: 0.001078, outdoorCO2Level: 400e-6,
        co2Generation: 5.56e-4, ventilationRate: 0.301,
        airVolume: 2700.0, time: 3600.0);

      Assert.Equal(0.001463, c, 3e-6);
    }

    /// <summary>換気量0の場合には発生分が線形に蓄積する。</summary>
    [Fact]
    public void GetConcentration_ZeroVentilation_LinearAccumulation()
    {
      double c = CO2Balance.GetConcentration(
        initialCO2Level: 400e-6, outdoorCO2Level: 400e-6,
        co2Generation: 5.56e-4, ventilationRate: 0.0,
        airVolume: 2700.0, time: 3600.0);

      Assert.Equal(400e-6 + 5.56e-4 * 3600.0 / 2700.0, c, precision: 9);
    }

    /// <summary>十分な時間が経過すると定常濃度（外気濃度+発生量/換気量）に収束する。</summary>
    [Fact]
    public void GetConcentration_LongTime_ConvergesToSteadyState()
    {
      double c = CO2Balance.GetConcentration(
        initialCO2Level: 500e-6, outdoorCO2Level: 400e-6,
        co2Generation: 5.56e-4, ventilationRate: 0.301,
        airVolume: 2700.0, time: 3600.0 * 100);

      Assert.Equal(400e-6 + 5.56e-4 / 0.301, c, 1e-8);
    }

    #endregion

    #region 必要換気量（式26.21・例題26.1）

    /// <summary>例題26.1: 1時間後に1,000ppmとするための換気量は約0.301m3/s。</summary>
    [Fact]
    public void GetRequiredVentilationRate_Example26_1()
    {
      double q = CO2Balance.GetRequiredVentilationRate(
        currentCO2Level: 500e-6, targetCO2Level: 1000e-6, outdoorCO2Level: 400e-6,
        co2Generation: 5.56e-4, airVolume: 2700.0, time: 3600.0);

      Assert.Equal(0.301, q, 0.002);
    }

    /// <summary>目標濃度が外気濃度以下の場合には例外を投げる。</summary>
    [Fact]
    public void GetRequiredVentilationRate_TargetBelowOutdoor_Throws()
    {
      Assert.Throws<PopoloArgumentException>(
        () => CO2Balance.GetRequiredVentilationRate(
          500e-6, 400e-6, 400e-6, 5.56e-4, 2700.0, 3600.0));
    }

    #endregion
  }
}
