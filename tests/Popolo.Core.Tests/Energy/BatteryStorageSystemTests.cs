/* BatteryStorageSystemTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Xunit;
using Popolo.Core.Energy;

namespace Popolo.Core.Tests.Energy
{
  /// <summary>BatteryStorageSystem のテスト</summary>
  /// <remarks>
  /// 期待値の根拠：
  /// - 外部接続点（AC端）基準の電力で充放電を指令し、コンバータ効率で電池端に換算する
  /// - 充電: P_bat = η_conv,ch·P_sys、放電: P_bat = P_sys/η_conv,dis
  ///
  /// 標準ケース（電池E_N=10000Wh、E_max=9000Wh、Li-ion k=0.1、コンバータ定格3000W）：
  /// - 3000W充電（p=1、η_conv=0.952381）→ P_bat=2857.14W、c=0.2857、
  ///   η_ch=0.971429 → ΔE=2775.51Wh
  ///
  /// カタログ逆算の例（設計資料の数値例、丸めなし版）：
  /// E_N=1100kWh、E_eff=1000kWh、η_sys=0.8、c=0.3/0.4、鉛蓄電池、
  /// PCS定格330kW/440kW → p_test=1、η_conv=0.952381、
  /// 電池DC往復 = 0.8/0.952381² = 0.882、k=0.028713、
  /// η_bat,dis=0.988515、E_max = 1000000/(0.988515×0.952381) = 1062199Wh
  /// </remarks>
  public class BatteryStorageSystemTests
  {

    #region Test helpers

    /// <summary>標準テスト用のシステム（Li-ion 10000/9000Wh、k=0.1、コンバータ定格3000W）を作成する</summary>
    private static BatteryStorageSystem createSystem()
    {
      var battery = new StorageBattery(
          10000, 9000, StorageBattery.ChemistryType.LithiumIon)
      { LossCoefficient = 0.1 };
      var converter = new PowerConverter(3000);
      return new BatteryStorageSystem(battery, converter, converter);
    }

    #endregion

    #region Constructor tests

    /// <summary>コンストラクタでプロパティが正しく設定される</summary>
    [Fact]
    public void Constructor_SetsProperties()
    {
      var battery = new StorageBattery(
          10000, 9000, StorageBattery.ChemistryType.LithiumIon);
      var chConv = new PowerConverter(3000);
      var disConv = new PowerConverter(4000);
      var system = new BatteryStorageSystem(battery, chConv, disConv);

      Assert.Same(battery, system.Battery);
      Assert.Same(chConv, system.ChargingConverter);
      Assert.Same(disConv, system.DischargingConverter);
      Assert.Equal(0, system.StandbyPower, precision: 6);
      Assert.Equal(0, system.StateOfCharge, precision: 6);
    }

    /// <summary>充放電に同一のコンバータインスタンスを共用できる</summary>
    [Fact]
    public void Constructor_SharedConverter_Allowed()
    {
      var system = createSystem();
      Assert.Same(system.ChargingConverter, system.DischargingConverter);
    }

    /// <summary>待機電力は0以上にクランプされる</summary>
    [Fact]
    public void StandbyPower_Negative_ClampedToZero()
    {
      var system = createSystem();
      system.StandbyPower = -50;
      Assert.Equal(0, system.StandbyPower, precision: 6);
    }

    #endregion

    #region Charge tests

    /// <summary>充電時にコンバータ損失と電池損失の両方が計上される</summary>
    [Fact]
    public void Charge_AppliesConverterAndBatteryLosses()
    {
      var system = createSystem();

      //3000W×1h: η_conv(p=1)=0.952381 → P_bat=2857.14W
      //c=0.2857 → η_ch=0.971429 → ΔE=2775.51Wh
      double actual = system.Charge(3000, 1.0);

      Assert.Equal(3000, actual, precision: 6);
      Assert.Equal(2857.14 * 0.971429, system.Battery.StoredEnergy, precision: 1);
    }

    /// <summary>コンバータ定格を超える充電要求は定格に制限される</summary>
    [Fact]
    public void Charge_OverRatedPower_LimitedToRated()
    {
      var systemOver = createSystem();
      var systemRated = createSystem();

      double actualOver = systemOver.Charge(5000, 1.0);
      double actualRated = systemRated.Charge(3000, 1.0);

      Assert.Equal(actualRated, actualOver, precision: 6);
      Assert.Equal(
          systemRated.Battery.StoredEnergy,
          systemOver.Battery.StoredEnergy, precision: 6);
    }

    /// <summary>満充電到達時はAC端の実充電電力が逆算される</summary>
    [Fact]
    public void Charge_ReachesFull_BackCalculatesSystemPower()
    {
      var system = createSystem();
      system.Battery.StoredEnergy = 8900;

      //残容量100Wh: P_bat,actual = 100/0.971429 = 102.94W
      //AC端 = 102.94/0.952381 = 108.09W
      double actual = system.Charge(3000, 1.0);

      Assert.Equal(9000, system.Battery.StoredEnergy, precision: 6);
      Assert.Equal(100.0 / 0.9714286 / 0.9523810, actual, precision: 3);
    }

    #endregion

    #region Discharge tests

    /// <summary>放電時にコンバータ損失と電池損失の両方が計上される</summary>
    [Fact]
    public void Discharge_AppliesConverterAndBatteryLosses()
    {
      var system = createSystem();
      system.Battery.StoredEnergy = 5000;

      //3000W×1h: η_conv(p=1)=0.952381 → P_bat=3150W
      //c=0.315 → η_dis=0.9685 → 内部消費=3252.45Wh
      double actual = system.Discharge(3000, 1.0);

      Assert.Equal(3000, actual, precision: 6);
      Assert.Equal(5000 - 3150.0 / 0.9685, system.Battery.StoredEnergy, precision: 1);
    }

    /// <summary>放電完了時はAC端の実出力電力が逆算される</summary>
    [Fact]
    public void Discharge_ReachesEmpty_BackCalculatesSystemPower()
    {
      var system = createSystem();
      system.Battery.StoredEnergy = 1000;

      //P_bat,actual = 1000×0.9685 = 968.5W → AC端 = 968.5×0.952381 = 922.38W
      double actual = system.Discharge(3000, 1.0);

      Assert.Equal(0, system.Battery.StoredEnergy, precision: 6);
      Assert.Equal(1000 * 0.9685 * 0.9523810, actual, precision: 2);
    }

    /// <summary>AC端の往復効率が個別効率の積と整合する</summary>
    [Fact]
    public void ChargeDischarge_RoundTripEfficiency_Consistent()
    {
      var system = createSystem();

      //満充電までの投入電力量と全放電の出力電力量の比が
      //η_conv,ch×η_ch×η_dis×η_conv,dis のオーダー（0.85前後）になる
      double input = 0;
      for (int i = 0; i < 10; i++) input += system.Charge(3000, 1.0);
      double output = 0;
      for (int i = 0; i < 10; i++) output += system.Discharge(3000, 1.0);

      double roundTrip = output / input;
      Assert.InRange(roundTrip, 0.80, 0.90);
    }

    #endregion

    #region CreateFromCatalog tests

    /// <summary>設計資料の数値例（鉛蓄電池・業務用スケール）のE_maxが再現される</summary>
    [Fact]
    public void CreateFromCatalog_DesignDocumentExample_MatchesEmax()
    {
      var system = BatteryStorageSystem.CreateFromCatalog(
          nominalCapacity: 1_100_000,
          initialEffectiveCapacity: 1_000_000,
          roundTripEfficiency: 0.8,
          chargeCRate: 0.3,
          dischargeCRate: 0.4,
          chemistry: StorageBattery.ChemistryType.LeadAcid,
          ratedChargingPower: 330_000,
          ratedDischargingPower: 440_000);

      //k = 0.028713、η_bat,dis = 0.988515、η_conv,dis = 0.952381
      //E_max = 1000000/(0.988515×0.952381) = 1062199Wh
      Assert.Equal(0.028713, system.Battery.LossCoefficient, precision: 5);
      Assert.InRange(system.Battery.MaximumStorableEnergy, 1_061_000, 1_063_500);
    }

    /// <summary>逆算したE_maxで満充電から放電すると初期実効容量が回収される</summary>
    /// <remarks>
    /// E_maxの逆算はカタログ測定条件（c_dis、p_test）における効率で行われるため、
    /// 同条件で全放電すればAC端出力電力量は初期実効容量と一致するはず。
    /// </remarks>
    [Fact]
    public void CreateFromCatalog_FullDischarge_RecoversEffectiveCapacity()
    {
      double effectiveCapacity = 1_000_000;
      var system = BatteryStorageSystem.CreateFromCatalog(
          1_100_000, effectiveCapacity, 0.8, 0.3, 0.4,
          StorageBattery.ChemistryType.LeadAcid, 330_000, 440_000);

      //満充電から測定条件（0.4C相当=440kW、p=1）で全放電する
      system.Battery.StoredEnergy = system.Battery.MaximumStorableEnergy;
      double output = 0;
      for (int i = 0; i < 100; i++) output += system.Discharge(440_000, 1.0) * 1.0;

      //放電末期の効率はCレート低下により測定条件より僅かに高くなるため
      //厳密一致はしないが、1%以内で初期実効容量が回収される
      Assert.InRange(output, effectiveCapacity * 0.99, effectiveCapacity * 1.01);
    }

    /// <summary>PCS定格が小さい場合は試験Cレートが定格律速に補正される</summary>
    [Fact]
    public void CreateFromCatalog_ConverterLimited_AdjustsCRate()
    {
      //E_N=1000kWh、PCS定格150kW → 実試験Cレートは0.15に制限される
      //エラーなくE_maxが正の値で得られることを確認する
      var system = BatteryStorageSystem.CreateFromCatalog(
          1_000_000, 900_000, 0.85, 0.3, 0.4,
          StorageBattery.ChemistryType.LithiumIon, 150_000, 150_000);

      Assert.True(system.Battery.MaximumStorableEnergy > 900_000,
          "E_max should exceed E_eff (losses are stripped)");
      Assert.True(system.Battery.LossCoefficient > 0,
          "Loss coefficient should be positive");
    }

    #endregion

    #region IReadOnlyBatteryStorageSystem tests

    /// <summary>IReadOnlyBatteryStorageSystemとして参照できる</summary>
    [Fact]
    public void System_ImplementsIReadOnlyBatteryStorageSystem()
    {
      var system = createSystem();

      IReadOnlyBatteryStorageSystem readOnly = system;
      Assert.Equal(10000, readOnly.Battery.NominalCapacity, precision: 6);
      Assert.Equal(3000, readOnly.ChargingConverter.RatedPower, precision: 6);
      Assert.Equal(3000, readOnly.DischargingConverter.RatedPower, precision: 6);
      Assert.Equal(0, readOnly.StateOfCharge, precision: 6);
    }

    #endregion

  }
}
