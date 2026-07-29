/* EnergyRecoveryVentilatorLossnayTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 * GNU General Public License v3 — see accompanying LICENSE file.
 */

using System;
using Xunit;
using Popolo.Core.HVAC.AirSide;
using Popolo.Core.HVAC.FluidCircuit;
using Popolo.Core.HVAC.HeatExchanger;
using Popolo.Core.Physics;

namespace Popolo.Core.Tests.HVAC.AirSide
{
  /// <summary>
  /// Validation of <see cref="AirToAirFlatPlateHeatExchanger"/> and
  /// <see cref="EnergyRecoveryVentilator"/> against the catalog data of the
  /// Mitsubishi Electric Lossnay LGH-15RS4/15RX4 (single phase 100 V, 50 Hz,
  /// Lossnay ventilation mode).
  /// </summary>
  /// <remarks>
  /// Catalog data:
  ///   強ノッチ: 150 m³/h, 機外静圧 95 Pa, 消費電力 91 W,
  ///     温度交換効率 77 %, エンタルピ交換効率 暖房時 70 % / 冷房時 64.5 %
  ///   弱ノッチ: 100 m³/h, 機外静圧 40 Pa, 消費電力 48 W,
  ///     温度交換効率 82 %, エンタルピ交換効率 暖房時 75 % / 冷房時 71 %
  ///
  /// The exchanger model is initialized ONLY from the 強ノッチ (rated) point.
  /// The 弱ノッチ point is then used as an independent reference to verify
  /// that the ε-NTU part-flow behaviour of the model is physically sound.
  /// The Lossnay core is a cross-flow fixed-plate exchanger, so the
  /// cross-flow (both fluids unmixed) arrangement is used.
  /// </remarks>
  public class EnergyRecoveryVentilatorLossnayTests
  {
    #region Catalog values (LGH-15RS4, 100V 50Hz, Lossnay ventilation)

    //強ノッチ（定格・モデル構築に使用）
    private const double FLOW_H = 150.0;      //処理風量 [m3/h]
    private const double SENS_EFF_H = 0.77;   //温度交換効率 [-]
    private const double ENTH_EFF_HEATING_H = 0.70;   //エンタルピ交換効率（暖房時）[-]
    private const double ENTH_EFF_COOLING_H = 0.645;  //エンタルピ交換効率（冷房時）[-]
    private const double POWER_H = 0.091;     //消費電力（給気+排気ファン）[kW]

    //弱ノッチ（部分風量・検証の参照値としてのみ使用）
    private const double FLOW_L = 100.0;      //処理風量 [m3/h]
    private const double SENS_EFF_L = 0.82;   //温度交換効率 [-]
    private const double ENTH_EFF_HEATING_L = 0.75;   //エンタルピ交換効率（暖房時）[-]
    private const double ENTH_EFF_COOLING_L = 0.71;   //エンタルピ交換効率（冷房時）[-]

    //JIS B 8628:2017 試験条件の入口空気状態
    private const double JIS_H_SAT = 5.0, JIS_H_SAW = 0.00387;   //暖房・給気（外気）
    private const double JIS_H_EAT = 20.0, JIS_H_EAW = 0.00857;  //暖房・排気（室内）
    private const double JIS_C_SAT = 35.0, JIS_C_SAW = 0.02715;  //冷房・給気（外気）
    private const double JIS_C_EAT = 27.0, JIS_C_EAW = 0.01178;  //冷房・排気（室内）

    #endregion

    #region Helpers

    /// <summary>強ノッチのカタログ値のみで構築した全熱交換器（直交流）。</summary>
    private static AirToAirFlatPlateHeatExchanger MakeLossnayHex()
        => new AirToAirFlatPlateHeatExchanger(
            FLOW_H, FLOW_H,
            SENS_EFF_H, ENTH_EFF_HEATING_H,
            AirToAirFlatPlateHeatExchanger.Condition.JISB8628_2017_Heating,
            SENS_EFF_H, ENTH_EFF_COOLING_H,
            AirToAirFlatPlateHeatExchanger.Condition.JISB8628_2017_Cooling,
            AirToAirFlatPlateHeatExchanger.AirFlow.CrossFlow,
            isEnthalpyEfficiency: true);

    /// <summary>カタログ消費電力で校正したファン（91Wを給気・排気で等分）。</summary>
    private static CentrifugalFan MakeLossnayFan()
        => new CentrifugalFan(
            0.095, FLOW_H / 3600.0, 0.095, FLOW_H / 3600.0,
            3, 0.02, POWER_H / 2, false);

    #endregion

    // ================================================================
    #region Rated point reproduction (high notch)

    /// <summary>暖房定格条件でカタログの温度交換効率とエンタルピ交換効率を再現する。</summary>
    [Fact]
    public void RatedHeating_ReproducesCatalogEfficiencies()
    {
      var hex = MakeLossnayHex();
      hex.UpdateState(FLOW_H, FLOW_H, JIS_H_SAT, JIS_H_SAW, JIS_H_EAT, JIS_H_EAW);

      Assert.Equal(SENS_EFF_H, hex.SensibleEfficiency, precision: 3);
      Assert.Equal(ENTH_EFF_HEATING_H, hex.GetTotalEfficiency(), 0.01);
    }

    /// <summary>冷房定格条件でカタログのエンタルピ交換効率を再現する。</summary>
    [Fact]
    public void RatedCooling_ReproducesCatalogEfficiencies()
    {
      var hex = MakeLossnayHex();
      hex.UpdateState(FLOW_H, FLOW_H, JIS_C_SAT, JIS_C_SAW, JIS_C_EAT, JIS_C_EAW);

      Assert.Equal(SENS_EFF_H, hex.SensibleEfficiency, precision: 3);
      Assert.Equal(ENTH_EFF_COOLING_H, hex.GetTotalEfficiency(), 0.01);
    }

    #endregion

    // ================================================================
    #region Partial air flow prediction (low notch) — physical plausibility verification

    /// <summary>
    /// 強ノッチのみで構築したモデルが、弱ノッチ（100 m³/h）のカタログ
    /// 温度交換効率（82 %）を±3ポイント以内で予測する。
    /// </summary>
    [Fact]
    public void PartFlow_PredictsWeakNotchSensibleEfficiency()
    {
      var hex = MakeLossnayHex();
      hex.UpdateState(FLOW_L, FLOW_L, JIS_H_SAT, JIS_H_SAW, JIS_H_EAT, JIS_H_EAW);

      Assert.Equal(SENS_EFF_L, hex.SensibleEfficiency, 0.03);
    }

    /// <summary>
    /// 弱ノッチのエンタルピ交換効率（暖房時 75 %）を±3ポイント以内で予測する。
    /// </summary>
    [Fact]
    public void PartFlow_PredictsWeakNotchEnthalpyEfficiency_Heating()
    {
      var hex = MakeLossnayHex();
      hex.UpdateState(FLOW_L, FLOW_L, JIS_H_SAT, JIS_H_SAW, JIS_H_EAT, JIS_H_EAW);

      Assert.Equal(ENTH_EFF_HEATING_L, hex.GetTotalEfficiency(), 0.03);
    }

    /// <summary>
    /// 弱ノッチのエンタルピ交換効率（冷房時 71 %）を±3ポイント以内で予測する。
    /// </summary>
    [Fact]
    public void PartFlow_PredictsWeakNotchEnthalpyEfficiency_Cooling()
    {
      var hex = MakeLossnayHex();
      hex.UpdateState(FLOW_L, FLOW_L, JIS_C_SAT, JIS_C_SAW, JIS_C_EAT, JIS_C_EAW);

      Assert.Equal(ENTH_EFF_COOLING_L, hex.GetTotalEfficiency(), 0.03);
    }

    #endregion

    // ================================================================
    #region Characteristic curve trends

    /// <summary>
    /// 特性曲線図と同様、処理風量の増加に対して交換効率が単調に低下する
    /// （50 → 100 → 150 → 250 m³/h）。
    /// </summary>
    [Fact]
    public void EfficiencyDecreasesMonotonicallyWithFlow()
    {
      var hex = MakeLossnayHex();
      double[] flows = { 50.0, 100.0, 150.0, 250.0 };
      double prevSens = 1.0;
      double prevTotal = 1.0;
      foreach (double flow in flows)
      {
        hex.UpdateState(flow, flow, JIS_H_SAT, JIS_H_SAW, JIS_H_EAT, JIS_H_EAW);
        Assert.True(hex.SensibleEfficiency < prevSens,
            $"sensible eff at {flow} m3/h = {hex.SensibleEfficiency:F3} < {prevSens:F3}");
        Assert.True(hex.GetTotalEfficiency() < prevTotal,
            $"total eff at {flow} m3/h = {hex.GetTotalEfficiency():F3} < {prevTotal:F3}");
        prevSens = hex.SensibleEfficiency;
        prevTotal = hex.GetTotalEfficiency();
      }
    }

    /// <summary>風量250 m³/hでの温度交換効率が特性曲線図の読み取り値（約71 %）の近傍にある。</summary>
    [Fact]
    public void OverFlow_SensibleEfficiencyNearCurveValue()
    {
      var hex = MakeLossnayHex();
      hex.UpdateState(250.0, 250.0, JIS_H_SAT, JIS_H_SAW, JIS_H_EAT, JIS_H_EAW);

      //曲線図の読み取り値 71 % ± 4ポイント
      Assert.Equal(0.71, hex.SensibleEfficiency, 0.04);
    }

    #endregion

    // ================================================================
    #region Unit-level verification (including fans)

    /// <summary>定格風量におけるユニット消費電力がカタログ値（91 W）に一致する。</summary>
    [Fact]
    public void Unit_ElectricConsumptionMatchesCatalog()
    {
      var erv = new EnergyRecoveryVentilator(MakeLossnayHex(), MakeLossnayFan(), MakeLossnayFan());
      double mFlow = FLOW_H / 3600.0 * PhysicsConstants.NominalMoistAirDensity;
      erv.SetAirFlowRate(mFlow, mFlow);
      erv.OATemperature = JIS_H_SAT;
      erv.OAHumidityRatio = JIS_H_SAW;
      erv.RATemperature = JIS_H_EAT;
      erv.RAHumidityRatio = JIS_H_EAW;
      erv.Ventilate();

      Assert.Equal(POWER_H, erv.GetElectricConsumption(), 0.002);
    }

    /// <summary>
    /// 給気ファンによる昇温がカタログ消費電力に見合う値（1 K前後）に収まる。
    /// 91/2 W ÷ (0.05 kg/s × 1.006 kJ/(kg·K)) ≈ 0.9 K。
    /// </summary>
    [Fact]
    public void Unit_FanTemperatureRiseIsPlausible()
    {
      var erv = new EnergyRecoveryVentilator(MakeLossnayHex(), MakeLossnayFan(), MakeLossnayFan());
      double mFlow = FLOW_H / 3600.0 * PhysicsConstants.NominalMoistAirDensity;
      erv.SetAirFlowRate(mFlow, mFlow);
      erv.OATemperature = JIS_H_SAT;
      erv.OAHumidityRatio = JIS_H_SAW;
      erv.RATemperature = JIS_H_EAT;
      erv.RAHumidityRatio = JIS_H_EAW;
      erv.Ventilate();

      double tRise = erv.SATemperature - erv.HeatExchanger.SupplyAirOutletDryBulbTemperature;
      Assert.InRange(tRise, 0.3, 1.5);
    }

    /// <summary>
    /// 冬季の給気温度が「外気を効率77 %で予熱した温度+ファン昇温」となり、
    /// 室温（排気入口）は超えない。
    /// </summary>
    [Fact]
    public void Unit_WinterSupplyTemperatureIsPhysical()
    {
      var erv = new EnergyRecoveryVentilator(MakeLossnayHex(), MakeLossnayFan(), MakeLossnayFan());
      double mFlow = FLOW_H / 3600.0 * PhysicsConstants.NominalMoistAirDensity;
      erv.SetAirFlowRate(mFlow, mFlow);
      erv.OATemperature = JIS_H_SAT;
      erv.OAHumidityRatio = JIS_H_SAW;
      erv.RATemperature = JIS_H_EAT;
      erv.RAHumidityRatio = JIS_H_EAW;
      erv.Ventilate();

      //交換器出口 = OA + 効率 × (RA - OA)
      double expected = JIS_H_SAT + SENS_EFF_H * (JIS_H_EAT - JIS_H_SAT);
      Assert.Equal(expected, erv.HeatExchanger.SupplyAirOutletDryBulbTemperature, 0.1);
      Assert.True(erv.SATemperature < JIS_H_EAT);
    }

    #endregion
  }
}
