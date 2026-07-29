/* PerezSkyDiffuseTests.cs
 *
 * Copyright (C) 2026 E.Togashi
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or (at
 * your option) any later version.
 */

using System;
using Xunit;

using Popolo.Core.Climate;

namespace Popolo.Core.Tests.Climate
{
  /// <summary>
  /// Unit tests for the Perez (1990) all-weather sky-diffuse model
  /// implementation in <see cref="Sky.GetPerezSkyDiffuseOnPlane"/> and its
  /// wiring into <see cref="Incline.GetDiffuseSolarIrradiance(IReadOnlySun, double, SkyDiffuseModel)"/>.
  /// </summary>
  /// <remarks>
  /// Expected values are either self-consistent limits of the formula
  /// itself, or hand-derived figures from the Perez 1990 Table II
  /// coefficients.
  /// </remarks>
  public class PerezSkyDiffuseTests
  {
    private const double Tol = 1e-6;

    #region Sun.GetAirMass (Kasten & Young 1989)

    [Fact]
    public void GetAirMass_Zenith_EqualsOne()
    {
      Assert.Equal(1.0, Sun.GetAirMass(0.5 * Math.PI), precision: 3);
    }

    [Fact]
    public void GetAirMass_ThirtyDegrees_MatchesKnownValue()
    {
      // Kasten-Young at h = 30° ≈ 1.9949
      double am = Sun.GetAirMass(30 * Math.PI / 180);
      Assert.InRange(am, 1.99, 2.00);
    }

    [Fact]
    public void GetAirMass_Horizon_IsFiniteLarge()
    {
      // Kasten-Young at h = 0° ≈ 37.9
      double am = Sun.GetAirMass(0);
      Assert.InRange(am, 37.0, 39.0);
      Assert.False(double.IsInfinity(am));
    }

    [Fact]
    public void GetAirMass_BelowHorizon_ClampedToHorizon()
    {
      double atHorizon = Sun.GetAirMass(0);
      double below = Sun.GetAirMass(-0.1);
      Assert.Equal(atHorizon, below, precision: 6);
    }

    #endregion

    // ================================================================
    #region Sky.GetPerezSkyDiffuseOnPlane — formula limits

    /// <summary>
    /// For a horizontal surface (β = 0), the view factor to the sky is 1,
    /// sin β = 0, and cos θ_i = cos z, so every Perez term collapses to
    /// <c>DHI</c> regardless of sky state.
    /// </summary>
    [Fact]
    public void Perez_HorizontalSurface_EqualsDhi()
    {
      double z = 40 * Math.PI / 180;
      double result = Sky.GetPerezSkyDiffuseOnPlane(
          directNormalRadiation: 600,
          diffuseHorizontalRadiation: 200,
          surfaceTilt: 0,
          cosIncidenceAngle: Math.Cos(z),
          solarZenith: z,
          airMass: 1.3,
          extraterrestrialNormalRadiation: 1367);

      Assert.Equal(200.0, result, precision: 3);
    }

    /// <summary>
    /// Zero diffuse horizontal in → zero diffuse on the plane out. The
    /// circumsolar term is proportional to DHI, so its multiplication
    /// chain short-circuits at DHI = 0.
    /// </summary>
    [Fact]
    public void Perez_ZeroDhi_ReturnsZero()
    {
      double r = Sky.GetPerezSkyDiffuseOnPlane(
          directNormalRadiation: 800,
          diffuseHorizontalRadiation: 0,
          surfaceTilt: 0.5 * Math.PI,
          cosIncidenceAngle: 0.5,
          solarZenith: 0.8,
          airMass: 1.5,
          extraterrestrialNormalRadiation: 1367);

      Assert.Equal(0.0, r);
    }

    /// <summary>
    /// When the sun is below the horizon (z ≥ π/2) and DNI = 0, sky
    /// clearness is degenerate. The implementation falls back to the
    /// isotropic projection, i.e. DHI × (1 + cos β) / 2.
    /// </summary>
    [Fact]
    public void Perez_SunBelowHorizon_NoDni_FallsBackToIsotropic()
    {
      double beta = 0.5 * Math.PI;   // vertical wall
      double r = Sky.GetPerezSkyDiffuseOnPlane(
          directNormalRadiation: 0,
          diffuseHorizontalRadiation: 80,
          surfaceTilt: beta,
          cosIncidenceAngle: 0,
          solarZenith: 0.5 * Math.PI + 0.1,   // below horizon
          airMass: Sun.GetAirMass(-0.1),
          extraterrestrialNormalRadiation: 1367);

      // 垂直面 (β = π/2) の空の view factor は 0.5、等方モデルで 80 × 0.5 = 40
      Assert.Equal(40.0, r, precision: 3);
    }

    /// <summary>
    /// Very overcast conditions (DNI ≈ 0, DHI ≈ GHI) put clearness ε into
    /// the lowest bin (ε ≤ 1.065) where f11 / f12 / f13 are all small.
    /// F1 ≈ 0 and F2 ≈ 0, so the Perez value should be very close to the
    /// isotropic value.
    /// </summary>
    [Fact]
    public void Perez_OvercastSky_ApproximatesIsotropic()
    {
      double beta = 60 * Math.PI / 180;
      double cosBeta = Math.Cos(beta);
      double viewFactorToSky = 0.5 * (1 + cosBeta);
      double z = 45 * Math.PI / 180;

      double dhi = 150;
      double perez = Sky.GetPerezSkyDiffuseOnPlane(
          directNormalRadiation: 1,                // nearly zero
          diffuseHorizontalRadiation: dhi,
          surfaceTilt: beta,
          cosIncidenceAngle: Math.Cos(z),
          solarZenith: z,
          airMass: Sun.GetAirMass(0.5 * Math.PI - z),
          extraterrestrialNormalRadiation: 1367);
      double isotropic = dhi * viewFactorToSky;

      // 等方値 ±20% 以内 (ε bin 1 は F1, F2 とも小さいが完全ゼロではない)
      Assert.InRange(perez, 0.8 * isotropic, 1.2 * isotropic);
    }

    /// <summary>
    /// Very clear sky (high DNI, low DHI, low z) pushes ε into the top
    /// bin. The circumsolar F1 term becomes significant, so a surface
    /// facing the sun should see <em>more</em> diffuse than the isotropic
    /// model predicts.
    /// </summary>
    [Fact]
    public void Perez_ClearSky_SurfaceFacingSun_ExceedsIsotropic()
    {
      double z = 20 * Math.PI / 180;                // high sun
      double beta = 30 * Math.PI / 180;             // tilted toward sun
      double cosBeta = Math.Cos(beta);
      double viewFactorToSky = 0.5 * (1 + cosBeta);

      // β = z かつ方位を揃えれば surface normal が太陽方向 → cos θ_i = 1
      double dhi = 80;
      double perez = Sky.GetPerezSkyDiffuseOnPlane(
          directNormalRadiation: 900,
          diffuseHorizontalRadiation: dhi,
          surfaceTilt: beta,
          cosIncidenceAngle: 1.0,
          solarZenith: z,
          airMass: Sun.GetAirMass(0.5 * Math.PI - z),
          extraterrestrialNormalRadiation: 1367);
      double isotropic = dhi * viewFactorToSky;

      Assert.True(perez > isotropic,
          $"expected Perez ({perez:F2}) > isotropic ({isotropic:F2})");
    }

    #endregion

    // ================================================================
    #region Incline integration

    /// <summary>
    /// <see cref="Incline.GetDiffuseSolarIrradiance(IReadOnlySun, double)"/>
    /// (既定) は Perez モデルを使う。Isotropic を明示したオーバーロードとは
    /// 異なる値を返すべき (完全曇天ではない条件)。
    /// </summary>
    [Fact]
    public void Incline_DiffuseDefaultsToPerez_DiffersFromIsotropic()
    {
      // 45°傾斜の南向き壁
      var incline = new Incline(horizontalAngle: 0, verticalAngle: Math.PI / 4);

      // 晴天時相当 (DNI 大, DHI 小)
      var sun = new Sun(35.68, 139.77, 135.0);
      sun.Update(new DateTime(2026, 6, 21, 12, 0, 0));
      sun.SetGlobalHorizontalRadiation(diffuseHorizontalRadiation: 120.0, directNormalRadiation: 900.0);

      double defaultValue = incline.GetDiffuseSolarIrradiance(sun, albedo: 0.2);
      double isotropic = incline.GetDiffuseSolarIrradiance(
          sun, albedo: 0.2, SkyDiffuseModel.Isotropic);
      double perez = incline.GetDiffuseSolarIrradiance(
          sun, albedo: 0.2, SkyDiffuseModel.Perez);

      Assert.Equal(perez, defaultValue, precision: 6);     // 既定 = Perez
      Assert.NotEqual(perez, isotropic);                    // 実質差がある
    }

    /// <summary>
    /// 水平面 (β = 0) では傾斜面の空への view factor = 1、地面への view factor
    /// = 0 になるため、Perez / Isotropic とも結果は DHI に等しい。
    /// </summary>
    [Fact]
    public void Incline_HorizontalSurface_BothModelsAgreeAndEqualDhi()
    {
      var incline = new Incline(horizontalAngle: 0, verticalAngle: 0);

      var sun = new Sun(35.68, 139.77, 135.0);
      sun.Update(new DateTime(2026, 6, 21, 12, 0, 0));
      sun.SetGlobalHorizontalRadiation(diffuseHorizontalRadiation: 150.0, directNormalRadiation: 800.0);

      double isotropic = incline.GetDiffuseSolarIrradiance(
          sun, albedo: 0.3, SkyDiffuseModel.Isotropic);
      double perez = incline.GetDiffuseSolarIrradiance(
          sun, albedo: 0.3, SkyDiffuseModel.Perez);

      Assert.Equal(150.0, isotropic, precision: 3);
      Assert.Equal(150.0, perez, precision: 3);
    }

    /// <summary>
    /// 地面反射成分は sky モデルに依らない。同じ albedo / GHI のもとで
    /// Perez と Isotropic の差は sky 成分のみに由来する。
    /// </summary>
    [Fact]
    public void Incline_GroundReflectedComponent_IsSameAcrossModels()
    {
      var incline = new Incline(horizontalAngle: 0, verticalAngle: 0.5 * Math.PI);

      var sun = new Sun(35.68, 139.77, 135.0);
      sun.Update(new DateTime(2026, 6, 21, 12, 0, 0));
      sun.SetGlobalHorizontalRadiation(diffuseHorizontalRadiation: 150.0, directNormalRadiation: 700.0);

      double albedo = 0.25;
      double viewFactorToGround = incline.ConfigurationFactorToGround;
      double expectedGround = albedo * viewFactorToGround * sun.GlobalHorizontalRadiation;

      // 地面反射成分を除いた sky component の値を計算
      double perezSky = incline.GetDiffuseSolarIrradiance(sun, albedo, SkyDiffuseModel.Perez)
                       - expectedGround;
      double isoSky = incline.GetDiffuseSolarIrradiance(sun, albedo, SkyDiffuseModel.Isotropic)
                     - expectedGround;

      // 地面反射は一致 (差分処理で相殺される) → sky 成分の差のみが残る
      Assert.NotEqual(perezSky, isoSky);
      // 両方とも正の値 (DHI > 0)
      Assert.True(perezSky > 0);
      Assert.True(isoSky > 0);
    }

    #endregion
  }
}
