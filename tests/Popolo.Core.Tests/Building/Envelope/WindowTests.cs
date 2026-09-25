/* WindowTests.cs
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

using System;
using Xunit;
using Popolo.Core.Building.Envelope;
using Popolo.Core.Climate;

namespace Popolo.Core.Tests.Building.Envelope
{
  /// <summary>Window のテスト</summary>
  /// <remarks>
  /// 典型的な窓ガラスの光学特性（垂直入射）：
  ///   透明単板ガラス: τ=0.87, ρ=0.08, α=0.05
  ///   透明2重ガラス: τ≈0.76, ρ≈0.14（多重反射考慮）
  ///
  /// UpdateOpticalProperties 後に各プロパティが更新される。
  /// </remarks>
  public class WindowTests
  {
    #region Test helpers

    /// <summary>南向き鉛直面の傾斜面を生成する</summary>
    private static Incline MakeSouthVerticalIncline()
        => new Incline(0.0, Math.PI / 2);

    /// <summary>指定した高度・方位の太陽状態を生成する</summary>
    private static Sun MakeSun(double altitudeDeg, double orientationDeg = 0.0)
    {
      var sun = new Sun(35.7, 139.7, 135.0);
      sun.Altitude = altitudeDeg * Math.PI / 180.0;
      sun.Azimuth = orientationDeg * Math.PI / 180.0;
      return sun;
    }

    /// <summary>透明単板ガラス窓を生成する（τ=0.87, ρ=0.08）</summary>
    private static Window MakeSinglePaneWindow(double area = 1.0)
    {
      var incline = MakeSouthVerticalIncline();
      return new Window(area,
          new[] { 0.87 }, // transmittance
          new[] { 0.08 }, // reflectance
          incline);
    }

    /// <summary>透明2重ガラス窓を生成する（各層τ=0.87, ρ=0.08）</summary>
    private static Window MakeDoublePaneWindow(double area = 1.0)
    {
      var incline = MakeSouthVerticalIncline();
      return new Window(area,
          new[] { 0.87, 0.87 }, // transmittance
          new[] { 0.08, 0.08 }, // reflectance
          incline);
    }

    #endregion

    #region Constructor tests

    /// <summary>コンストラクタでプロパティが正しく設定される</summary>
    [Fact]
    public void Constructor_SetsProperties()
    {
      var window = MakeSinglePaneWindow(2.5);

      Assert.Equal(2.5, window.Area, precision: 6);
      Assert.Equal(1, window.GlazingCount);
    }

    /// <summary>2重ガラスのGlazingNumberが2</summary>
    [Fact]
    public void Constructor_DoublePane_GlazingNumberIsTwo()
    {
      var window = MakeDoublePaneWindow();
      Assert.Equal(2, window.GlazingCount);
    }

    /// <summary>熱抵抗が正の値である</summary>
    [Fact]
    public void Constructor_ThermalResistance_IsPositive()
    {
      var window = MakeSinglePaneWindow();
      Assert.True(window.GetResistance() > 0);
    }

    /// <summary>2重ガラスは単板より熱抵抗が大きい（中空層の分）</summary>
    [Fact]
    public void Constructor_DoublePane_HigherResistance()
    {
      var single = MakeSinglePaneWindow();
      var dbl = MakeDoublePaneWindow();

      Assert.True(dbl.GetResistance() > single.GetResistance(),
          $"Double ({dbl.GetResistance():F4}) should be > Single ({single.GetResistance():F4})");
    }

    #endregion

    #region Physical constraint tests for solar properties

    /// <summary>
    /// 直達日射の透過率・反射率はそれぞれ0〜1の範囲内。
    /// ※ AbsorptanceはSolar Heat Gain Coefficientの吸収成分（室内側再放射分）であり、
    ///    τ+ρ+α=1 の保存則は成立しない（αは全吸収量ではない）。
    /// </summary>
    [Fact]
    public void UpdateOpticalProperties_Direct_ValuesInRange()
    {
      var window = MakeSinglePaneWindow();
      window.UpdateOpticalProperties(MakeSun(45));

      Assert.InRange(window.DirectSolarIncidentTransmittance, 0.0, 1.0);
      Assert.InRange(window.DirectSolarIncidentReflectance, 0.0, 1.0);
      Assert.InRange(window.DirectSolarIncidentAbsorptance, 0.0, 1.0);
    }

    /// <summary>直達日射の τ + ρ ≤ 1（透過と反射の合計は全入射を超えない）</summary>
    [Fact]
    public void UpdateOpticalProperties_Direct_TransmittancePlusReflectanceLEOne()
    {
      var window = MakeSinglePaneWindow();
      window.UpdateOpticalProperties(MakeSun(45));

      double sum = window.DirectSolarIncidentTransmittance
                 + window.DirectSolarIncidentReflectance;
      Assert.InRange(sum, 0.0, 1.0);
    }

    /// <summary>拡散日射（屋外→室内）の各値が0〜1の範囲内</summary>
    [Fact]
    public void UpdateOpticalProperties_DiffuseIncident_ValuesInRange()
    {
      var window = MakeSinglePaneWindow();
      window.UpdateOpticalProperties(MakeSun(45));

      Assert.InRange(window.DiffuseSolarIncidentTransmittance, 0.0, 1.0);
      Assert.InRange(window.DiffuseSolarIncidentReflectance, 0.0, 1.0);
      Assert.InRange(window.DiffuseSolarIncidentAbsorptance, 0.0, 1.0);
    }

    /// <summary>拡散日射（室内→屋外）の各値が0〜1の範囲内</summary>
    [Fact]
    public void UpdateOpticalProperties_DiffuseLost_ValuesInRange()
    {
      var window = MakeSinglePaneWindow();
      window.UpdateOpticalProperties(MakeSun(45));

      Assert.InRange(window.DiffuseSolarLostTransmittance, 0.0, 1.0);
      Assert.InRange(window.DiffuseSolarLostReflectance, 0.0, 1.0);
      Assert.InRange(window.DiffuseSolarLostAbsorptance, 0.0, 1.0);
    }

    #endregion

    #region Property variation with solar position tests

    /// <summary>太陽高度が0以下のとき直達透過率が0・反射率が1</summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-10.0)]
    public void UpdateOpticalProperties_SunBelowHorizon_ZeroTransmittance(double altitudeDeg)
    {
      var window = MakeSinglePaneWindow();
      window.UpdateOpticalProperties(MakeSun(altitudeDeg));

      Assert.Equal(0.0, window.DirectSolarIncidentTransmittance, precision: 6);
      Assert.Equal(1.0, window.DirectSolarIncidentReflectance, precision: 6);
    }

    /// <summary>太陽が正面にあるとき直達透過率が正</summary>
    [Fact]
    public void UpdateOpticalProperties_SunFacingWindow_PositiveTransmittance()
    {
      var window = MakeSinglePaneWindow();
      window.UpdateOpticalProperties(MakeSun(45, 0)); // 南中45°

      Assert.True(window.DirectSolarIncidentTransmittance > 0,
          "Direct transmittance should be positive when sun faces window");
    }

    /// <summary>太陽方位が窓の真横（90°）のとき直達透過率がほぼ0</summary>
    [Fact]
    public void UpdateOpticalProperties_SunFromSide_NearZeroDirectTransmittance()
    {
      var window = MakeSinglePaneWindow();
      // 太陽方位90°（真東）→ 南向き窓の法線と直交 → cosθ≒0
      window.UpdateOpticalProperties(MakeSun(45, 90));

      Assert.InRange(window.DirectSolarIncidentTransmittance, 0.0, 0.05);
    }

    /// <summary>直達透過率は垂直に近いほど高い（入射角特性）</summary>
    [Fact]
    public void UpdateOpticalProperties_NormalIncidence_HigherTransmittance()
    {
      var window = MakeSinglePaneWindow();

      // 垂直入射に近い（高度45°, 南向き）
      window.UpdateOpticalProperties(MakeSun(45, 0));
      double tau45 = window.DirectSolarIncidentTransmittance;

      // 斜め入射（高度45°, 方位60°）
      window.UpdateOpticalProperties(MakeSun(45, 60));
      double tau60 = window.DirectSolarIncidentTransmittance;

      Assert.True(tau45 > tau60,
          $"Near-normal ({tau45:F4}) should be > oblique ({tau60:F4})");
    }

    #endregion

    #region Multi-pane glazing property tests

    /// <summary>2重ガラスは単板より総合透過率が低い</summary>
    [Fact]
    public void UpdateOpticalProperties_DoublePane_LowerTransmittance()
    {
      var single = MakeSinglePaneWindow();
      var dbl = MakeDoublePaneWindow();
      var sun = MakeSun(45);

      single.UpdateOpticalProperties(sun);
      dbl.UpdateOpticalProperties(sun);

      Assert.True(dbl.DirectSolarIncidentTransmittance < single.DirectSolarIncidentTransmittance,
          $"Double ({dbl.DirectSolarIncidentTransmittance:F4}) should be < Single ({single.DirectSolarIncidentTransmittance:F4})");
    }

    /// <summary>2重ガラスの拡散日射透過率も単板より低い</summary>
    [Fact]
    public void UpdateOpticalProperties_DoublePane_LowerDiffuseTransmittance()
    {
      var single = MakeSinglePaneWindow();
      var dbl = MakeDoublePaneWindow();
      var sun = MakeSun(45);

      single.UpdateOpticalProperties(sun);
      dbl.UpdateOpticalProperties(sun);

      Assert.True(dbl.DiffuseSolarIncidentTransmittance < single.DiffuseSolarIncidentTransmittance,
          $"Double ({dbl.DiffuseSolarIncidentTransmittance:F4}) should be < Single ({single.DiffuseSolarIncidentTransmittance:F4})");
    }

    #endregion

    #region Shading device tests

    /// <summary>日射遮蔽物（ブラインド）設置後に直達透過率が低下する</summary>
    [Fact]
    public void SetShadingDevice_Deployed_ReducesTransmittance()
    {
      var window = MakeSinglePaneWindow();
      var sun = MakeSun(45);

      // 遮蔽物なし
      window.UpdateOpticalProperties(sun);
      double tauBefore = window.DirectSolarIncidentTransmittance;

      // 室内側にブラインド設置（τ=0.05, ρ=0.55）
      var blind = new SimpleShadingDevice(0.05, 0.55);
      window.SetShadingDevice(1, blind); // 室内側（index=GlazingNumber）
      window.UpdateOpticalProperties(sun);
      double tauAfter = window.DirectSolarIncidentTransmittance;

      Assert.True(tauAfter < tauBefore,
          $"After blind ({tauAfter:F4}) should be < before ({tauBefore:F4})");
    }

    /// <summary>遮蔽物を展開しないとき（IsPulledDown=false）透過率が変わらない</summary>
    [Fact]
    public void SetShadingDevice_NotDeployed_NoEffect()
    {
      var window = MakeSinglePaneWindow();
      var sun = MakeSun(45);

      window.UpdateOpticalProperties(sun);
      double tauBefore = window.DirectSolarIncidentTransmittance;

      // 収納状態（IsPulledDown=false）のブラインド
      var blind = new SimpleShadingDevice(0.05, 0.55);
      blind.IsPulledDown = false;
      window.SetShadingDevice(1, blind);
      window.UpdateOpticalProperties(sun);
      double tauAfter = window.DirectSolarIncidentTransmittance;

      Assert.Equal(tauBefore, tauAfter, precision: 4);
    }

    #endregion

    #region Area tests

    /// <summary>面積が正しく設定・取得される</summary>
    [Theory]
    [InlineData(1.0)]
    [InlineData(2.5)]
    [InlineData(0.5)]
    public void Area_SetAndGet(double area)
    {
      var window = MakeSinglePaneWindow(area);
      Assert.Equal(area, window.Area, precision: 6);
    }

    /// <summary>面積0以下の設定は無視される</summary>
    [Fact]
    public void Area_SetZeroOrNegative_NotChanged()
    {
      var window = MakeSinglePaneWindow(1.0);
      window.Area = 0.0;
      Assert.Equal(1.0, window.Area, precision: 6);

      window.Area = -1.0;
      Assert.Equal(1.0, window.Area, precision: 6);
    }

    #endregion

    #region Angle-dependence preset tests

    /// <summary>
    /// 全 GlassType プリセットの入射角特性多項式が垂直入射で ≈1 に正規化されており、
    /// 任意の入射角で T+R ∈ [0,1]・吸収 1−T−R ≥ 0 を満たす。
    /// </summary>
    /// <remarks>
    /// 多項式 P(cosθ)=Σ c_j cos^(j+1)θ は垂直入射 (cosθ=1) で Σ c_j となり、
    /// τ(θ)=τ_n·P_T、ρ(θ)=1−(1−ρ_n)·P_R の関係から Σ c_j ≈ 1 でなければ
    /// 垂直入射で τ_n, ρ_n が再現されない。かつて HeatReflecting の反射率多項式に
    /// 符号誤り (−21.642、係数和 −42.285) があり、拡散反射率が 1 を超え
    /// 吸収率が負になっていた（回帰テスト）。
    /// </remarks>
    [Theory]
    [InlineData(Window.GlassType.Transparent)]
    [InlineData(Window.GlassType.HeatAbsorbing)]
    [InlineData(Window.GlassType.HeatReflecting)]
    [InlineData(Window.GlassType.LowEmissivity)]
    public void SetAngleDependence_Preset_NormalizedAndPhysical(Window.GlassType type)
    {
      const double tauN = 0.60;
      const double rhoN = 0.20;
      var window = new Window(1.0, new[] { tauN }, new[] { rhoN }, MakeSouthVerticalIncline());
      window.SetAngleDependence(0, type);

      // 拡散特性（半球積分値）は物理的範囲内
      Assert.InRange(window.DiffuseSolarIncidentTransmittance, 0.0, 1.0);
      Assert.InRange(window.DiffuseSolarIncidentReflectance, 0.0, 1.0);
      Assert.InRange(window.DiffuseSolarIncidentTransmittance
          + window.DiffuseSolarIncidentReflectance, 0.0, 1.0);
      Assert.InRange(window.DiffuseSolarLostTransmittance
          + window.DiffuseSolarLostReflectance, 0.0, 1.0);

      // ほぼ垂直入射（南向き鉛直面に太陽高度≈0・方位0）で τ_n, ρ_n を再現（多項式の係数和≈1）
      window.UpdateOpticalProperties(MakeSun(0.01, 0.0));
      Assert.Equal(tauN, window.DirectSolarIncidentTransmittance, 2);
      Assert.Equal(rhoN, window.DirectSolarIncidentReflectance, 2);

      // 入射角全域で T, R ∈ [0,1]、T+R ≤ 1（吸収 ≥ 0）
      for (int deg = 1; deg < 90; deg += 2)
      {
        window.UpdateOpticalProperties(MakeSun(deg, 0.0));
        double t = window.DirectSolarIncidentTransmittance;
        double r = window.DirectSolarIncidentReflectance;
        Assert.InRange(t, 0.0, 1.0);
        Assert.InRange(r, 0.0, 1.0);
        Assert.True(1.0 - t - r >= -1e-9, $"{type} alt={deg}°: T={t:F4}, R={r:F4}, A={1 - t - r:F4} < 0");
      }
    }

    #endregion
  }
}
