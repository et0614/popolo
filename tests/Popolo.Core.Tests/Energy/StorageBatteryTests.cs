/* StorageBatteryTests.cs
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
  /// <summary>StorageBattery のテスト</summary>
  /// <remarks>
  /// 期待値の根拠：
  /// - 充放電効率のCレート線形モデル: η_ch = η_coul·(1 − k·c)、η_dis = 1 − k·c
  /// - クーロン効率: 鉛蓄電池0.9、リチウムイオン電池1.0（充電側のみに作用）
  /// - 係数推定: 0 = k²·c_ch·c_dis − k·(c_ch + c_dis) + 1 − η_RT/η_coul の小さい方の根
  ///
  /// 標準ケース（E_N=10000Wh、E_max=9000Wh、Li-ion、k=0.1h）の手計算：
  /// - 3000W充電: c=0.3、η=0.97、1時間でΔE=2910Wh
  /// - 4000W放電: c=0.4、η=0.96、1時間で内部消費4166.67Wh
  /// </remarks>
  public class StorageBatteryTests
  {

    #region Test helpers

    /// <summary>標準テスト用の電池（E_N=10000Wh、E_max=9000Wh、Li-ion、k=0.1）を作成する</summary>
    private static StorageBattery createLithiumIonBattery()
    {
      return new StorageBattery(10000, 9000, StorageBattery.ChemistryType.LithiumIon)
      { LossCoefficient = 0.1 };
    }

    #endregion

    #region Constructor tests

    /// <summary>コンストラクタでプロパティが正しく設定される</summary>
    [Fact]
    public void Constructor_SetsProperties()
    {
      var battery = new StorageBattery(
          10000, 9000, StorageBattery.ChemistryType.LithiumIon);

      Assert.Equal(10000, battery.NominalCapacity, precision: 6);
      Assert.Equal(9000, battery.MaximumStorableEnergy, precision: 6);
      Assert.Equal(StorageBattery.ChemistryType.LithiumIon, battery.Chemistry);
      Assert.Equal(0, battery.StoredEnergy, precision: 6);
      Assert.Equal(0, battery.StateOfCharge, precision: 6);
    }

    /// <summary>クーロン効率のデフォルト値は電池種で決まる（鉛0.9、Li-ion1.0）</summary>
    [Theory]
    [InlineData(StorageBattery.ChemistryType.LeadAcid, 0.9)]
    [InlineData(StorageBattery.ChemistryType.LithiumIon, 1.0)]
    public void Constructor_CoulombicEfficiencyDefaultByChemistry(
        StorageBattery.ChemistryType chemistry, double expected)
    {
      var battery = new StorageBattery(10000, 9000, chemistry);
      Assert.Equal(expected, battery.CoulombicEfficiency, precision: 6);
    }

    /// <summary>蓄電量は[0, E_max]にクランプされる</summary>
    [Fact]
    public void StoredEnergy_Clamped()
    {
      var battery = createLithiumIonBattery();

      battery.StoredEnergy = -100;
      Assert.Equal(0, battery.StoredEnergy, precision: 6);

      battery.StoredEnergy = 12000;
      Assert.Equal(9000, battery.StoredEnergy, precision: 6);
      Assert.Equal(1.0, battery.StateOfCharge, precision: 6);
    }

    #endregion

    #region Efficiency tests

    /// <summary>充電効率のCレート線形式が手計算と一致する（Li-ion）</summary>
    [Fact]
    public void GetChargingEfficiency_LithiumIon_MatchesHandCalculation()
    {
      var battery = createLithiumIonBattery();
      //c = 3000/10000 = 0.3 → η = 1.0×(1 − 0.1×0.3) = 0.97
      Assert.Equal(0.97, battery.GetChargingEfficiency(3000), precision: 6);
    }

    /// <summary>充電効率にはクーロン効率が乗る（鉛蓄電池）</summary>
    [Fact]
    public void GetChargingEfficiency_LeadAcid_IncludesCoulombicEfficiency()
    {
      var battery = new StorageBattery(10000, 9000, StorageBattery.ChemistryType.LeadAcid)
      { LossCoefficient = 0.1 };
      //c = 0.3 → η = 0.9×(1 − 0.03) = 0.873
      Assert.Equal(0.873, battery.GetChargingEfficiency(3000), precision: 6);
    }

    /// <summary>放電効率にはクーロン効率が乗らない</summary>
    [Fact]
    public void GetDischargingEfficiency_NoCoulombicEfficiency()
    {
      var battery = new StorageBattery(10000, 9000, StorageBattery.ChemistryType.LeadAcid)
      { LossCoefficient = 0.1 };
      //c = 4000/10000 = 0.4 → η = 1 − 0.04 = 0.96（クーロン効率0.9は乗らない）
      Assert.Equal(0.96, battery.GetDischargingEfficiency(4000), precision: 6);
    }

    /// <summary>Cレートが高いほど効率が低下する</summary>
    [Fact]
    public void GetChargingEfficiency_HigherCRate_LowerEfficiency()
    {
      var battery = createLithiumIonBattery();
      Assert.True(
          battery.GetChargingEfficiency(1000) > battery.GetChargingEfficiency(5000),
          "Expected higher efficiency at lower C-rate");
    }

    #endregion

    #region Charge tests

    /// <summary>充電で蓄電量が効率込みで増加する</summary>
    [Fact]
    public void Charge_IncreasesStoredEnergy()
    {
      var battery = createLithiumIonBattery();

      //3000W×1h、η=0.97 → ΔE = 2910Wh
      double actual = battery.Charge(3000, 1.0);

      Assert.Equal(3000, actual, precision: 6);
      Assert.Equal(2910, battery.StoredEnergy, precision: 6);
    }

    /// <summary>満充電到達時は実充電電力が逆算される（エネルギー保存）</summary>
    [Fact]
    public void Charge_ReachesFull_BackCalculatesActualPower()
    {
      var battery = createLithiumIonBattery();
      battery.StoredEnergy = 8000;

      //残容量1000Whに対して2910Wh充電しようとする
      //実電力 = 1000/(0.97×1) = 1030.93W
      double actual = battery.Charge(3000, 1.0);

      Assert.Equal(9000, battery.StoredEnergy, precision: 6);
      Assert.Equal(1000.0 / 0.97, actual, precision: 4);
    }

    /// <summary>ゼロ以下の電力・時間刻みでは充電されない</summary>
    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(-500, 1.0)]
    [InlineData(3000, 0)]
    public void Charge_InvalidInput_NoChange(double power, double timeStep)
    {
      var battery = createLithiumIonBattery();
      double actual = battery.Charge(power, timeStep);

      Assert.Equal(0, actual, precision: 6);
      Assert.Equal(0, battery.StoredEnergy, precision: 6);
    }

    #endregion

    #region Discharge tests

    /// <summary>放電で蓄電量が効率込みで減少する</summary>
    [Fact]
    public void Discharge_DecreasesStoredEnergy()
    {
      var battery = createLithiumIonBattery();
      battery.StoredEnergy = 5000;

      //4000W×1h、η=0.96 → 内部消費 = 4000/0.96 = 4166.67Wh
      double actual = battery.Discharge(4000, 1.0);

      Assert.Equal(4000, actual, precision: 6);
      Assert.Equal(5000 - 4000.0 / 0.96, battery.StoredEnergy, precision: 4);
    }

    /// <summary>放電完了時は実出力電力が逆算される（エネルギー保存）</summary>
    [Fact]
    public void Discharge_ReachesEmpty_BackCalculatesActualPower()
    {
      var battery = createLithiumIonBattery();
      battery.StoredEnergy = 2000;

      //蓄電量2000Whに対して4166.67Wh引き出そうとする
      //実電力 = 2000×0.96/1 = 1920W
      double actual = battery.Discharge(4000, 1.0);

      Assert.Equal(0, battery.StoredEnergy, precision: 6);
      Assert.Equal(1920, actual, precision: 4);
    }

    /// <summary>充放電の往復でエネルギーが減少する（第二法則）</summary>
    [Fact]
    public void ChargeDischarge_RoundTrip_LosesEnergy()
    {
      var battery = createLithiumIonBattery();

      battery.Charge(3000, 1.0);
      double input = 3000.0;
      double output = battery.Discharge(5000, 1.0);

      Assert.True(output < input,
          $"Expected output ({output:F1}Wh) < input ({input:F1}Wh)");
    }

    #endregion

    #region EstimateLossCoefficient tests

    /// <summary>設計資料の数値例（鉛蓄電池）と一致する</summary>
    /// <remarks>
    /// c_ch=0.3、c_dis=0.4、電池単体DC往復効率0.882、η_coul=0.9のとき、
    /// 定数項 = 1 − 0.882/0.9 = 0.02
    /// 0 = 0.12k² − 0.7k + 0.02 → k = (0.7 − √0.4804)/0.24 = 0.028713
    /// </remarks>
    [Fact]
    public void EstimateLossCoefficient_LeadAcidExample_MatchesDesignDocument()
    {
      double k = StorageBattery.EstimateLossCoefficient(0.3, 0.4, 0.882, 0.9);
      Assert.Equal(0.028713, k, precision: 5);
    }

    /// <summary>リチウムイオン電池の代表値（DC往復93%）でk≒0.10となる</summary>
    /// <remarks>
    /// 定数項 = 1 − 0.93 = 0.07
    /// 0 = 0.12k² − 0.7k + 0.07 → k = (0.7 − √0.4564)/0.24 = 0.101776
    /// </remarks>
    [Fact]
    public void EstimateLossCoefficient_LithiumIonTypical_AboutPointOne()
    {
      double k = StorageBattery.EstimateLossCoefficient(0.3, 0.4, 0.93, 1.0);
      Assert.Equal(0.101776, k, precision: 5);
    }

    /// <summary>往復効率がクーロン効率以上の場合は損失なし（k=0）</summary>
    [Fact]
    public void EstimateLossCoefficient_EfficiencyAboveCoulombic_ReturnsZero()
    {
      double k = StorageBattery.EstimateLossCoefficient(0.3, 0.4, 0.95, 0.9);
      Assert.Equal(0, k, precision: 6);
    }

    /// <summary>推定したkで効率を再計算すると与えた往復効率が復元される</summary>
    [Fact]
    public void EstimateLossCoefficient_RoundTripConsistency()
    {
      double cch = 0.3, cdis = 0.4, rt = 0.882, coul = 0.9;
      double k = StorageBattery.EstimateLossCoefficient(cch, cdis, rt, coul);

      var battery = new StorageBattery(10000, 9000, StorageBattery.ChemistryType.LeadAcid)
      { LossCoefficient = k };
      double etaCh = battery.GetChargingEfficiency(cch * 10000);
      double etaDis = battery.GetDischargingEfficiency(cdis * 10000);

      Assert.Equal(rt, etaCh * etaDis, precision: 6);
    }

    #endregion

  }
}
