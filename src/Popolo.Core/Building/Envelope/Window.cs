/* Window.cs
 * 
 * Copyright (C) 2016 E.Togashi
 * 
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or (at
 * your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 675 Mass Ave, Cambridge, MA 02139, USA.
 */

using System;
using Popolo.Core.Climate;
using Popolo.Core.Exceptions;
using Popolo.Core.Numerics.LinearAlgebra;

namespace Popolo.Core.Building.Envelope
{
  /// <inheritdoc cref="IReadOnlyWindow"/>
  /// <remarks>
  /// <para>
  /// This is the mutable implementation of <see cref="IReadOnlyWindow"/>.
  /// Build a window by specifying the normal-incidence transmittance and
  /// reflectance arrays for each glazing layer (one entry per layer, ordered
  /// from F to B) and the outdoor <see cref="IReadOnlyIncline"/>. Asymmetric
  /// glazing (different F-side vs B-side properties) is supported by the
  /// four-array constructor overload; symmetric glazing uses the two-array
  /// overload.
  /// </para>
  /// <para>
  /// After construction, install <see cref="IShadingDevice"/> objects at
  /// specific layer positions (0 = outdoor air gap, up to <c>GlazingCount</c>
  /// for the indoor surface) via <c>SetShadingDevice</c>. Angle-dependent
  /// optical behavior for each glazing can be selected from presets
  /// (<see cref="GlassType"/>) or supplied as custom coefficient arrays with
  /// <c>SetAngleDependence</c>. The thermal resistance of individual glazing
  /// layers and air gaps is configurable through <c>SetGlassResistance</c> /
  /// <c>SetAirGapResistance</c>.
  /// </para>
  /// <para>
  /// <c>UpdateOpticalProperties</c> recomputes the solar properties given the
  /// current solar position and the state of all attached shading devices.
  /// The window caches the last solar angle it was evaluated at and skips
  /// recomputation when neither the sun nor any shading device has moved,
  /// keeping the per-step cost low for steady conditions.
  /// </para>
  /// </remarks>
  public class Window : OpticalLayeredEnvelope, IReadOnlyWindow
  {

    #region 列挙型定義

    /// <summary>Specifies the glazing type.</summary>
    public enum GlassType
    {
      /// <summary>Clear float glass.</summary>
      Transparent,
      /// <summary>Heat-absorbing glass.</summary>
      HeatAbsorbing,
      /// <summary>Heat-reflecting glass.</summary>
      HeatReflecting,
      /// <summary>Low-E</summary>
      LowEmissivity
    }

    #endregion

    #region インスタンス変数・プロパティ

    /// <summary>Angle-of-incidence correction coefficients for each glazing layer.</summary>
    private double[][] tau_CF = null!, tau_CB = null!, rho_CF = null!, rho_CB = null!;

    /// <summary>Thermal resistance of each air gap layer [m²·K/W].</summary>
    private double[] agapRes = null!;

    /// <summary>Thermal resistance of each glazing layer [m²·K/W].</summary>
    private double[] glassRes = null!;

    /// <summary>Optical properties (transmittance, reflectance, absorptance) for each layer.
    /// First index: 0=transmittance, 1=reflectance, 2=absorptance.
    /// Suffixes: F=front side, B=back side, Dir=direct, Dif=diffuse.</summary>
    private double[,] opFDir = null!, opBDir = null!, opFDif = null!, opBDif = null!;

    /// <summary>Absorptance list for each layer.</summary>
    private double[] absFDir = null!, absFDif = null!, absBDif = null!;

    /// <summary>Normal-incidence transmittance and reflectance for each glazing layer.</summary>
    private double[,] taurhoF = null!, taurhoB = null!;

    /// <summary>Solar altitude and azimuth from the previous time step (used to detect state changes).</summary>
    private double lstAlt, lstOri;

    /// <summary>List of shading devices in the air gaps (including outdoor and indoor sides).</summary>
    private IShadingDevice[] sDevices;

    /// <summary>Window surface area [m²].</summary>
    private double area = 1;

    /// <summary>Gets or sets the window surface area [m²]. Non-positive values are silently ignored.</summary>
    public override double Area
    {
      set { if (0 < value) { area = value; } }
      get { return area; }
    }

    /// <summary>Gets the tilted surface orientation of the outdoor-facing side.</summary>
    public IReadOnlyIncline OutsideIncline { get; private set; }

    /// <summary>Gets the total transmittance for direct solar irradiance from outdoors [-].</summary>
    public double DirectSolarIncidentTransmittance { get; private set; }

    /// <summary>Gets the total reflectance for direct solar irradiance from outdoors [-].</summary>
    public double DirectSolarIncidentReflectance { get; private set; }

    /// <summary>Gets the absorbed solar heat gain coefficient for direct irradiance from outdoors [-].</summary>
    public double DirectSolarIncidentAbsorptance { get; private set; }

    /// <summary>Gets the total transmittance for diffuse solar irradiance from outdoors [-].</summary>
    public double DiffuseSolarIncidentTransmittance { get; private set; }

    /// <summary>Gets the total reflectance for diffuse solar irradiance from outdoors [-].</summary>
    public double DiffuseSolarIncidentReflectance { get; private set; }

    /// <summary>Gets the absorbed solar heat gain coefficient for diffuse irradiance from outdoors [-].</summary>
    public double DiffuseSolarIncidentAbsorptance { get; private set; }

    /// <summary>Gets the total transmittance for diffuse solar irradiance from indoors [-].</summary>
    public double DiffuseSolarLostTransmittance { get; private set; }

    /// <summary>Gets the total reflectance for diffuse solar irradiance from indoors [-].</summary>
    public double DiffuseSolarLostReflectance { get; private set; }

    /// <summary>Gets the absorbed solar heat gain coefficient for diffuse irradiance from indoors [-].</summary>
    public double DiffuseSolarLostAbsorptance { get; private set; }

    /// <summary>Gets the number of glazing layers.</summary>
    public int GlazingCount { get; private set; }

    /// <summary>Exterior solar shading device.</summary>
    private SunShade sunShade = null!;

    /// <summary>Gets or sets the exterior solar shading device.</summary>
    public SunShade SunShade
    {
      get { return sunShade; }
      set
      {
        if (value == null) return;
        sunShade = new SunShade(value); //コピーして設定
        sunShade.Incline = OutsideIncline;
      }
    }


    /// <summary>Convective and radiative heat transfer coefficients on the F and B sides [W/(m²·K)].</summary>
    private double cCoefF, rCoefF, cCoefB, rCoefB;

    /// <summary>Gets or sets the convective heat transfer coefficient on the F side (outdoor) [W/(m²·K)].</summary>
    public double ConvectiveCoefficientF
    {
      get { return cCoefF; }
      set
      {
        cCoefF = value;
        UpdateFilmCoefficient();
        if (OutsideSurface != null) OutsideSurface.BoundaryCoefficientChanged = true;
      }
    }

    /// <summary>Gets or sets the radiative heat transfer coefficient on the F side (outdoor) [W/(m²·K)].</summary>
    public double RadiativeCoefficientF
    {
      get { return rCoefF; }
      set
      {
        rCoefF = value;
        UpdateFilmCoefficient();
        if (OutsideSurface != null) OutsideSurface.BoundaryCoefficientChanged = true;
      }
    }

    /// <summary>Gets the combined heat transfer coefficient on the F side (outdoor) [W/(m²·K)].</summary>
    public double FilmCoefficientF
    { get { return 1d / (2 * agapRes[0]); } }

    /// <summary>Gets the short-wave (solar) emissivity on the F side (outdoor) [-].</summary>
    /// <remarks>
    /// Returns 0: in the matrix-based glazing model, outdoor-incident solar
    /// is absorbed per glass layer (via <c>absFDir</c> / <c>absFDif</c> and
    /// fed into the matrix as a body source), not at the outer surface. The
    /// SolAir-temperature formula at the outer face therefore applies no
    /// surface absorptance term (alpha × solar = 0).
    /// </remarks>
    public double ShortWaveEmissivityF { get { return 0.0; } }

    /// <summary>
    /// Whether the F (outdoor) side is exposed to outdoor wind (default <c>true</c>).
    /// When <c>false</c>, the F-side convective coefficient is excluded from the
    /// wind-speed-driven dynamic update and retains its user-set value.
    /// </summary>
    public bool IsWindExposedF { get; set; } = true;

    /// <summary>Gets the surface temperature on the F side (outdoor) [°C].</summary>
    public double SurfaceTemperatureF { get { return OutsideSurface.SurfaceTemperature; } }

    /// <summary>Gets or sets the convective heat transfer coefficient on the B side (indoor) [W/(m²·K)].</summary>
    public double ConvectiveCoefficientB
    {
      get { return cCoefB; }
      set
      {
        cCoefB = value;
        UpdateFilmCoefficient();
        if (InsideSurface != null) InsideSurface.BoundaryCoefficientChanged = true;
      }
    }

    /// <summary>Gets or sets the radiative heat transfer coefficient on the B side (indoor) [W/(m²·K)].</summary>
    public double RadiativeCoefficientB
    {
      get { return rCoefB; }
      set
      {
        rCoefB = value;
        UpdateFilmCoefficient();
        if (InsideSurface != null) InsideSurface.BoundaryCoefficientChanged = true;
      }
    }

    /// <summary>Gets the combined heat transfer coefficient on the B side (indoor) [W/(m²·K)].</summary>
    public double FilmCoefficientB
    { get { return 1d / (2 * agapRes[agapRes.Length - 1]); } }

    /// <summary>Gets the short-wave (solar) emissivity on the B side (indoor) [-].</summary>
    public double ShortWaveEmissivityB
    { get { return 1 - DiffuseSolarLostReflectance; } }

    /// <summary>Gets the surface temperature on the B side (indoor) [°C].</summary>
    public double SurfaceTemperatureB { get { return InsideSurface.SurfaceTemperature; } }

    /// <summary>
    /// Gets the boundary surface element on the indoor (B) side.
    /// </summary>
    /// <remarks>Alias for <see cref="OpticalLayeredEnvelope.SurfaceB"/>.</remarks>
    public EnvelopeSurface InsideSurface => SurfaceB;

    /// <summary>
    /// Gets the boundary surface element on the outdoor (F) side.
    /// </summary>
    /// <remarks>Alias for <see cref="OpticalLayeredEnvelope.SurfaceF"/>.</remarks>
    public EnvelopeSurface OutsideSurface => SurfaceF;


    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new multi-layer glazing window assembly.</summary>
    /// <param name="area">Surface area [m²].</param>
    /// <param name="transmittanceF">Transmittance list for F-side incidence [-] (0 = F, N-1 = B).</param>
    /// <param name="reflectanceF">Reflectance list for F-side incidence [-] (0 = F, N-1 = B).</param>
    /// <param name="transmittanceB">Transmittance list for B-side incidence [-] (0 = F, N-1 = B).</param>
    /// <param name="reflectanceB">Reflectance list for B-side incidence [-] (0 = F, N-1 = B).</param>
    /// <param name="outsideIncline">Outside tilted surface.</param>
    public Window(double area, double[] transmittanceF, double[] reflectanceF,
      double[] transmittanceB, double[] reflectanceB, IReadOnlyIncline outsideIncline)
    {
      Area = area;
      GlazingCount = transmittanceF.Length;
      this.SunShade = SunShade.MakeEmptySunShade();
      this.OutsideIncline = outsideIncline;
      lstAlt = lstOri = -999;

      tau_CF = new double[GlazingCount][];
      tau_CB = new double[GlazingCount][];
      rho_CF = new double[GlazingCount][];
      rho_CB = new double[GlazingCount][];
      taurhoF = new double[GlazingCount, 2];
      taurhoB = new double[GlazingCount, 2];
      opFDir = new double[GlazingCount * 2 + 1, 3];
      opBDir = new double[GlazingCount * 2 + 1, 3];
      opFDif = new double[GlazingCount * 2 + 1, 3];
      opBDif = new double[GlazingCount * 2 + 1, 3];
      absFDir = new double[GlazingCount * 2 + 1];
      absFDif = new double[GlazingCount * 2 + 1];
      absBDif = new double[GlazingCount * 2 + 1];
      agapRes = new double[GlazingCount * 2 + 2];
      glassRes = new double[GlazingCount];

      //空の日射遮蔽で初期化
      sDevices = new IShadingDevice[GlazingCount + 1];
      for (int i = 0; i < sDevices.Length; i++)
      {
        sDevices[i] = new NoShadingDevice();
        int i2 = i * 2;
        opFDir[i2, 0] = opBDir[i2, 0] = opFDif[i2, 0] = opBDif[i2, 0] = 1.0;
        opFDir[i2, 1] = opBDir[i2, 1] = opFDif[i2, 1] = opBDif[i2, 1] =
          opFDir[i2, 2] = opBDir[i2, 2] = opFDif[i2, 2] = opBDif[i2, 2] = 0.0;
      }

      //垂直入射時の透過率と反射率を保存
      for (int i = 0; i < GlazingCount; i++)
      {
        taurhoF[i, 0] = transmittanceF[i];
        taurhoB[i, 0] = transmittanceB[i];
        taurhoF[i, 1] = reflectanceF[i];
        taurhoB[i, 1] = reflectanceB[i];
      }

      //熱抵抗を初期化
      RadiativeCoefficientF = RadiativeCoefficientB = 4.5;
      ConvectiveCoefficientF = 18.5;
      ConvectiveCoefficientB = 7.5;
      UpdateFilmCoefficient();
      agapRes[agapRes.Length - 1] = agapRes[agapRes.Length - 2] = 0.5 * 1 / 6.7;
      for (int i = 2; i < agapRes.Length - 2; i++) agapRes[i] = 0.5 * 1 / 6.7;
      for (int i = 0; i < glassRes.Length; i++) glassRes[i] = 0.006;

      //行列ソルバ用配列を確保 (capS は全て 0 = 熱容量なし、resS は glass+air-gap の連鎖)
      int mNum = 2 * GlazingCount;
      capS = new double[mNum];
      resS = new double[mNum + 1];
      solarAbsorption = new double[mNum];
      qCoefS = new double[mNum];
      uSF = new double[mNum];
      uSB = new double[mNum];
      tempAndHumid = new Vector(mNum);
      uMatrix = new Matrix(mNum, mNum);
      uxMatrix = new Matrix(mNum, mNum);
      needToUpdateUMatrix = true; //初回 Update で逆行列を構築させる

      //内外表面作成
      SurfaceF = new EnvelopeSurface(this, true);
      SurfaceB = new EnvelopeSurface(this, false);

      //入射角特性を透明フロートガラスで初期化
      for (int i = 0; i < GlazingCount; i++) SetAngleDependence(i, GlassType.Transparent);
    }

    /// <summary>Initializes a new multi-layer glazing window assembly.</summary>
    /// <param name="area">Surface area [m²].</param>
    /// <param name="transmittance">Transmittance list [-] (0 = F, N-1 = B).</param>
    /// <param name="reflectance">Reflectance list [-] (0 = F, N-1 = B).</param>
    /// <param name="outsideIncline">Outside tilted surface.</param>
    public Window(double area, double[] transmittance, double[] reflectance, IReadOnlyIncline outsideIncline) :
      this(area, transmittance, reflectance, transmittance, reflectance, outsideIncline)
    { }

    #endregion

    #region インスタンスメソッド

    /// <summary>Recomputes the window's direct-solar optical properties for the current sun position.</summary>
    /// <param name="sun">Solar state (position is read; irradiance values are not used by this method).</param>
    /// <remarks>
    /// Per call, <see cref="UpdateOpticalProperties"/>:
    /// <list type="bullet">
    ///   <item><description>zeros the direct-solar transmittance / reflectance / absorptance when the sun is below the horizon (<c>Altitude ≤ 0</c>) and returns early;</description></item>
    ///   <item><description>caches the last evaluated solar altitude and azimuth, so a call with an unchanged sun and unchanged shading-device states skips the full recomputation and short-circuits to the cached result;</description></item>
    ///   <item><description>updates each interior <see cref="IShadingDevice"/> with the current profile angle, which lets slat-angle-dependent devices (<see cref="VenetianBlind"/>) refresh their own optics;</description></item>
    ///   <item><description>rebuilds the layer-by-layer direct optical matrix through the glazing/shading stack using the angle-dependence coefficients set by <see cref="SetAngleDependence(int, GlassType)"/>.</description></item>
    /// </list>
    /// The diffuse properties are independent of sun position and are set once
    /// by <see cref="SetAngleDependence(int, double[], double[], double[], double[])"/>;
    /// they are not recomputed here. Called internally by
    /// <see cref="MultiRoom"/> during each solver step; external callers
    /// usually do not need to invoke it directly.
    /// </remarks>
    public override void UpdateOpticalProperties(IReadOnlySun sun)
    {
      //ガラス・日射遮蔽物単体の光学特性の更新処理//////////////////////////////
      double cos = OutsideIncline.GetDirectSolarRadiationRatio(sun);
      if (sun.Altitude <= 0)
      {
        DirectSolarIncidentTransmittance = 0;
        DirectSolarIncidentReflectance = 1;
        DirectSolarIncidentAbsorptance = 0;
        for (int i = 0; i < sDevices.Length; i++) sDevices[i].ProfileAngle = 0;
        return;
      }
      bool sunMoved = (sun.Altitude != lstAlt || sun.Azimuth != lstOri);
      if (sunMoved)
      {
        lstAlt = sun.Altitude;
        lstOri = sun.Azimuth;

        //ガラスの直達日射入射角特性を反映
        if (0 < cos)
        {
          for (int i = 0; i < GlazingCount; i++)
          {
            double tauF, tauB, rhoF, rhoB;
            tauF = tauB = rhoF = rhoB = 0;
            int lenth = tau_CF[i].Length - 1;
            for (int j = lenth; 0 <= j; j--)
            {
              tauF = cos * (tauF + tau_CF[i][j]);
              tauB = cos * (tauB + tau_CB[i][j]);
              rhoF = cos * (rhoF + rho_CF[i][j]);
              rhoB = cos * (rhoB + rho_CB[i][j]);
            }
            opFDir[2 * i + 1, 0] = tauF * taurhoF[i, 0];
            opFDir[2 * i + 1, 1] = 1 - (1 - taurhoF[i, 1]) * rhoF;
            opFDir[2 * i + 1, 2] = 1 - (opFDir[2 * i + 1, 0] + opFDir[2 * i + 1, 1]);
            opBDir[2 * i + 1, 0] = tauB * taurhoB[i, 0];
            opBDir[2 * i + 1, 1] = 1 - (1 - taurhoB[i, 1]) * rhoB;
            opBDir[2 * i + 1, 2] = 1 - (opBDir[2 * i + 1, 0] + opBDir[2 * i + 1, 1]);
          }
        }

        //日射遮蔽物のプロファイル角を更新
        double pAngle = OutsideIncline.GetProfileAngle(sun);
        for (int i = 0; i < sDevices.Length; i++) sDevices[i].ProfileAngle = pAngle;
      }

      //日射遮蔽物の光学特性を更新
      bool sPropChanged = false;
      for (int i = 0; i < sDevices.Length; i++)
      {
        int i2 = i * 2;
        if (sDevices[i].HasPropertyChanged)
        {
          sDevices[i].ComputeOpticalProperties(false, true, out opFDir[i2, 0], out opFDir[i2, 1]);
          sDevices[i].ComputeOpticalProperties(false, false, out opBDir[i2, 0], out opBDir[i2, 1]);
          sDevices[i].ComputeOpticalProperties(true, true, out opFDif[i2, 0], out opFDif[i2, 1]);
          sDevices[i].ComputeOpticalProperties(true, false, out opBDif[i2, 0], out opBDif[i2, 1]);
          opFDir[i2, 2] = 1 - (opFDir[i2, 0] + opFDir[i2, 1]);
          opBDir[i2, 2] = 1 - (opBDir[i2, 0] + opBDir[i2, 1]);
          opFDif[i2, 2] = 1 - (opFDif[i2, 0] + opFDif[i2, 1]);
          opBDif[i2, 2] = 1 - (opBDif[i2, 0] + opBDif[i2, 1]);
          sPropChanged = true;
        }
      }

      //太陽位置と日射遮蔽物光学特性不変の場合は終了
      if (!sunMoved && !sPropChanged) return;

      //総合光学特性の更新処理//////////////////////////////////////////////////
      //直達日射に関する特性を更新
      if (0 < cos)
      {
        double[] bf = new double[absFDir.Length];
        ComputeTotalOProperties
          (opFDir, opBDir, out double ttlTF, out double ttlRF, ref absFDir, out _, out _, ref bf);
        DirectSolarIncidentTransmittance = ttlTF;
        DirectSolarIncidentReflectance = ttlRF;
        IntegrateAbsorption(absFDir, agapRes, glassRes, out _, out double adB);
        DirectSolarIncidentAbsorptance = adB;
      }
      else
      {
        DirectSolarIncidentTransmittance = 0;
        DirectSolarIncidentReflectance = 1;
        DirectSolarIncidentAbsorptance = 0;
      }

      //遮蔽物光学特性特性変化時には拡散日射に関する特性を更新
      if (sPropChanged) UpdateDiffuseTotalProperties();
    }

    /// <summary>Updates total optical properties for diffuse solar irradiance.</summary>
    private void UpdateDiffuseTotalProperties()
    {
      ComputeTotalOProperties
        (opFDif, opBDif, out double ttlTF, out double ttlRF, ref absFDif, out _, out _, ref absBDif);
      DiffuseSolarIncidentTransmittance = ttlTF;
      DiffuseSolarIncidentReflectance = ttlRF;
      DiffuseSolarLostTransmittance = ttlTF;
      DiffuseSolarLostReflectance = ttlRF;
      IntegrateAbsorption(absFDif, agapRes, glassRes, out _, out double adB);
      DiffuseSolarIncidentAbsorptance = adB;
      IntegrateAbsorption(absBDif, agapRes, glassRes, out _, out adB);
      DiffuseSolarLostAbsorptance = adB;
    }

    /// <summary>Gets the total thermal resistance of the window assembly, including surface films [m²·K/W].</summary>
    /// <returns>Total thermal resistance [m²·K/W].</returns>
    /// <remarks>
    /// Sum of all glazing-layer resistances plus all air-gap resistances
    /// <i>including</i> the two outer "gap" entries that encode the indoor
    /// and outdoor film resistances (1 / film_coefficient). The reciprocal is
    /// therefore the window's overall U-value.
    /// </remarks>
    public double GetResistance()
    {
      double rg = 0;
      for (int i = 0; i < glassRes.Length; i++) rg += glassRes[i];
      for (int i = 0; i < agapRes.Length; i++) rg += agapRes[i];
      return rg;
    }

    /// <summary>
    /// Computes the window's short-wave radiation contribution to the indoor
    /// space, derived from its current optical properties (incidence-angle
    /// dependent absorptances / transmittances) and the solar state at its
    /// outdoor-facing surface, with sky-diffuse attenuation by any attached
    /// <see cref="SunShade"/>.
    /// </summary>
    /// <param name="indoorSurface">Ignored — windows hardcode F = outdoor, B = indoor.</param>
    /// <param name="sun">Solar geometry / radiation source.</param>
    /// <param name="albedo">Ground albedo [-].</param>
    /// <returns>Indoor-side absorbed flux [W/m²] and transmitted power [W].</returns>
    public override ShortWaveEmission EmitShortWaveToIndoor(
      EnvelopeSurface indoorSurface,
      Climate.IReadOnlySun sun,
      double albedo)
    {
      Climate.IReadOnlyIncline inc = OutsideIncline;
      double dir = inc.GetDirectSolarIrradiance(sun) * (1 - SunShade.GetDirectShadingRate(sun, inc));
      // 拡散日射: 天空成分のみ SunShade で一律係数で減衰させ、地面反射は不変。
      //
      // Note: Perez (1990) 異方性モデルでは sky diffuse は
      //   ① isotropic dome
      //   ② circumsolar (太陽近傍に集中)
      //   ③ horizon brightening (地平線近傍)
      // の 3 成分から成る。一律係数 (sky view factor 比) で全 sky diffuse に
      // 掛ける現在の方式は ① には適切だが、② (太陽方向依存・遮蔽は二値的) と
      // ③ (地平線高度に集中・庇では物理的に遮蔽されない) には不適切。
      // 理論的に厳密化するには Perez 3 成分を分解し個別に減衰させる必要があるが、
      // 現状は近似として全成分一律で扱う (TODO: Perez 分解の実装)。
      double diffuseTotal = inc.GetDiffuseSolarIrradiance(sun, albedo);
      double groundReflected = albedo * inc.ConfigurationFactorToGround
                             * sun.GlobalHorizontalRadiation;
      double skyDiffuse = diffuseTotal - groundReflected;
      if (skyDiffuse < 0) skyDiffuse = 0;
      double skyShadingRate = SunShade.GetSkyDiffuseShadingRate(inc);
      double dif = skyDiffuse * (1.0 - skyShadingRate) + groundReflected;

      // 各層の吸収日射 [W/m²] を per-node 体積熱源として行列ソルバに供給。
      // absFDir / absFDif は 2N+1 長 (層境界毎、奇数 index = ガラス層中央、
      // 偶数 index = エアギャップ / シェーディング位置)。
      // 各 absorption 位置 i の吸収量を、その両隣の節点に半分ずつ分配する
      // (端は片側のみ)。これによりガラス層・遮蔽位置の双方を統一的に扱う。
      Array.Clear(solarAbsorption, 0, solarAbsorption.Length);
      int positions = absFDir.Length; // = 2N+1
      int last = positions - 1;
      for (int i = 0; i < positions; i++)
      {
        double q = dir * absFDir[i] + dif * absFDif[i];
        if (q == 0) continue;
        if (i == 0)
        {
          // 屋外側端の吸収 (例: 屋外側 ShadingDevice)。屋外面節点 (0) に集約。
          solarAbsorption[0] += q;
        }
        else if (i == last)
        {
          // 室内側端の吸収。室内面節点 (mNum-1) に集約。
          solarAbsorption[NodeCount - 1] += q;
        }
        else
        {
          // 中間位置: 両隣の節点に半分ずつ。
          solarAbsorption[i - 1] += 0.5 * q;
          solarAbsorption[i] += 0.5 * q;
        }
      }

      // 室内側に到達する短波長は matrix が ガラス層温度を介して扱うため、
      // 表面で吸収する成分 (InsideAbsorbedFlux) は 0 とする。
      // 透過分は従来通り室内面間で再分配される。
      double transmittedDirect = dir * DirectSolarIncidentTransmittance * Area;
      double transmittedDiffuse = dif * DiffuseSolarIncidentTransmittance * Area;
      return new ShortWaveEmission(0.0, transmittedDirect, transmittedDiffuse);
    }

    /// <summary>
    /// Returns 1.0: the raw indoor-incident diffuse flux is accumulated into
    /// the surface's <c>radToSurf_S</c> entry without re-weighting, and is
    /// then redistributed onto the per-glass-layer absorption inputs by
    /// <see cref="OnIncidentSolarFlux"/> using the indoor-side per-layer
    /// diffuse absorptances (<c>absBDif</c>).
    /// </summary>
    public override double IndoorDiffuseAbsorptanceFactor => 1.0;

    /// <summary>
    /// Distributes indoor-side incident diffuse short-wave (from interior
    /// inter-reflection) onto the per-glass-layer absorption inputs.
    /// </summary>
    /// <param name="surface">Source surface; only the indoor (B) side is processed.</param>
    /// <param name="incidentShortWaveFlux">Indoor-incident diffuse flux at the surface [W/m²].</param>
    public override void OnIncidentSolarFlux(EnvelopeSurface surface, double incidentShortWaveFlux)
    {
      if (incidentShortWaveFlux == 0) return;
      if (surface != InsideSurface) return; // outdoor-incident is set by EmitShortWaveToIndoor

      int positions = absBDif.Length;
      int last = positions - 1;
      for (int i = 0; i < positions; i++)
      {
        double q = incidentShortWaveFlux * absBDif[i];
        if (q == 0) continue;
        if (i == 0) solarAbsorption[0] += q;
        else if (i == last) solarAbsorption[NodeCount - 1] += q;
        else
        {
          solarAbsorption[i - 1] += 0.5 * q;
          solarAbsorption[i] += 0.5 * q;
        }
      }
    }

    /// <summary>
    /// Number of nodes in the glass / air-gap finite-difference network:
    /// <c>2 * GlazingCount</c> (one node on each side of every glazing layer).
    /// </summary>
    public override int NodeCount => 2 * GlazingCount;

    /// <summary>Refreshes <see cref="UpdateIFCoefficients"/>'s IF coefficients.</summary>
    /// <remarks>
    /// All glass nodes have zero heat capacity in the current model, so the
    /// previous-step nodal state carries no meaningful information across
    /// time steps and is excluded from the IF formula. Only the per-glass
    /// absorbed solar (a body source on each node) contributes.
    /// </remarks>
    public override void UpdateIFCoefficients()
    {
      int num = uxMatrix.Rows - 1;
      IF2_F = IF2_B = 0;
      for (int i = 0; i <= num; i++)
      {
        if (solarAbsorption[i] != 0)
        {
          double bf = qCoefS[i] * solarAbsorption[i];
          IF2_F += uxMatrix[0, i] * bf;
          IF2_B += uxMatrix[num, i] * bf;
        }
      }
    }

    /// <summary>
    /// Populates the base-class <c>capS</c> (all zeros) and <c>resS</c> (the
    /// outer-film + glass + inter-glass-gap + inner-film resistance chain)
    /// arrays from the window's glass / air-gap configuration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For an N-pane window the layer stack is: outer film → glass[0] →
    /// air-gap[0] → glass[1] → … → glass[N-1] → inner film. This produces
    /// 2N nodes (one each on the F and B faces of every glass layer) and
    /// 2N+1 inter-node resistances. Glass <c>i</c> sits between nodes
    /// <c>2i</c> and <c>2i+1</c>; the air gap to its indoor side sits
    /// between nodes <c>2i+1</c> and <c>2(i+1)</c>.
    /// </para>
    /// <para>
    /// The <c>agapRes</c> internal array stores each "physical" gap (and the
    /// outer / inner film) as two equal halves — the full gap resistance
    /// between two adjacent glass nodes is therefore the sum of two
    /// successive <c>agapRes</c> entries.
    /// </para>
    /// </remarks>
    protected override void PopulateSensibleProperties()
    {
      int N = GlazingCount;
      // Glass nodes: zero heat capacity (Phase C-2 — to be replaced with real
      // per-layer capacity in a future phase).
      Array.Clear(capS, 0, capS.Length);

      // resS[0] = outer film; resS[2N] = inner film
      // resS[2k+1] = glass[k]; resS[2k+2] = air gap between glass[k] and glass[k+1]
      resS[0] = agapRes[0] + agapRes[1];
      for (int k = 0; k < N; k++)
      {
        resS[2 * k + 1] = glassRes[k];
        if (k < N - 1) resS[2 * k + 2] = agapRes[2 * k + 2] + agapRes[2 * k + 3];
      }
      resS[2 * N] = agapRes[2 * N] + agapRes[2 * N + 1];
    }

    /// <summary>Computes the total optical properties from the individual layer properties.</summary>
    /// <param name="opPropF">Layer optical properties for F-side incidence.</param>
    /// <param name="opPropB">Layer optical properties for B-side incidence.</param>
    /// <param name="ttlTF">Output: total transmittance for F-side incidence.</param>
    /// <param name="ttlRF">Output: total reflectance for F-side incidence.</param>
    /// <param name="ttlAF">Output: total absorptance for F-side incidence.</param>
    /// <param name="ttlTB">Output: total transmittance for B-side incidence.</param>
    /// <param name="ttlRB">Output: total reflectance for B-side incidence.</param>
    /// <param name="ttlAB">Output: total absorptance for B-side incidence.</param>
    private static void ComputeTotalOProperties
      (double[,] opPropF, double[,] opPropB, out double ttlTF, out double ttlRF, ref double[] ttlAF,
      out double ttlTB, out double ttlRB, ref double[] ttlAB)
    {
      int ln = opPropF.GetLength(0);
      double[] ttlTBi = new double[ln];
      double[] ttlRFi = new double[ln];

      ttlTF = opPropF[ln - 1, 0];
      ttlRFi[ln - 1] = opPropF[ln - 1, 1];
      ttlTBi[ln - 1] = opPropB[ln - 1, 0];
      ttlRB = opPropB[ln - 1, 1];
      for (int i = ln - 2; 0 <= i; i--)
      {
        double xr = 1 / (1 - ttlRB * opPropF[i, 1]);
        ttlRFi[i] = ttlRFi[i + 1] + ttlTF * xr * opPropF[i, 1] * ttlTBi[i + 1];
        ttlRB = opPropB[i, 1] + opPropB[i, 0] * xr * ttlRB * opPropF[i, 0];
        ttlTF = ttlTF * xr * opPropF[i, 0];
        ttlTBi[i] = opPropB[i, 0] * xr * ttlTBi[i + 1];
      }

      ttlTF = opPropF[0, 0];
      ttlRF = opPropF[0, 1];
      ttlTB = opPropB[0, 0];
      ttlRB = opPropB[0, 1];
      ttlAF[0] = opPropF[0, 2];
      ttlAB[0] = 0;
      for (int j = 1; j < ln; j++)
      {
        double tfm1 = ttlTF;
        double tbm1 = ttlTB;
        double rbm1 = ttlRB;
        double xr = 1 / (1 - ttlRB * opPropF[j, 1]);
        ttlRF = ttlRF + ttlTF * xr * opPropF[j, 1] * ttlTB;
        ttlRB = opPropB[j, 1] + opPropB[j, 0] * xr * ttlRB * opPropF[j, 0];
        ttlTF = ttlTF * xr * opPropF[j, 0];
        ttlTB = opPropB[j, 0] * xr * ttlTB;
        double bf = 1 / (1 - rbm1 * ttlRFi[j]);
        ttlAF[j] = tfm1 * opPropF[j, 2] * bf;
        ttlAF[j - 1] += tfm1 * ttlRFi[j] * opPropB[j - 1, 2] * bf;
        ttlAB[j - 1] += ttlTBi[j] * opPropB[j - 1, 2] * bf;
        ttlAB[j] = ttlTBi[j] * rbm1 * opPropF[j, 2] * bf;
      }
    }

    #endregion

    #region モデル設定関連の処理

    /// <summary>Installs an interior shading device at a specific slot in the glazing stack.</summary>
    /// <param name="number">Slot index: 0 = outdoor air gap, <c>GlazingCount</c> = indoor air gap; intermediate values are gaps between glazing layers.</param>
    /// <param name="sDevice">Shading device to install. Pass a new <see cref="NoShadingDevice"/> to clear the slot.</param>
    /// <remarks>
    /// Every slot starts out holding a <see cref="NoShadingDevice"/>
    /// (transmittance = 1, reflectance = 0), so only slots you explicitly
    /// populate contribute to attenuation. For exterior shading (overhangs,
    /// fins) use the window's <see cref="SunShade"/> property instead.
    /// </remarks>
    public void SetShadingDevice(int number, IShadingDevice sDevice) { sDevices[number] = sDevice; }

    /// <summary>Gets the shading device at the specified layer position.</summary>
    /// <param name="number">Layer index (0 = outdoor side, N+1 = indoor side).</param>
    /// <returns>The shading device.</returns>
    public IShadingDevice GetShadingDevice(int number) { return sDevices[number]; }

    /// <summary>Sets the thermal resistance of the specified glazing layer [m²·K/W].</summary>
    /// <param name="glazingIndex">Glazing layer index.</param>
    /// <param name="resistance">Thermal resistance of the glazing [m²·K/W].</param>
    /// <remarks>
    /// Triggers a re-integration of the absorbed-solar-heat-gain coefficients:
    /// the fraction of absorbed solar energy that is redirected inward vs
    /// outward depends on the relative thermal resistances of the layers
    /// around the absorption point, so changing one glass resistance
    /// propagates to the whole assembly's SHGC.
    /// </remarks>
    public void SetGlassResistance(int glazingIndex, double resistance)
    {
      glassRes[glazingIndex] = resistance;
      UpdateAbsorptance();
      needToUpdateUMatrix = true;
    }

    /// <summary>Gets the thermal resistance of the specified glazing layer [m²·K/W].</summary>
    /// <param name="glazingIndex">Glazing layer index.</param>
    /// <returns>Thermal resistance [m²·K/W].</returns>
    public double GetGlassResistance(int glazingIndex)
    { return glassRes[glazingIndex]; }

    /// <summary>Sets the thermal resistance of the air gap to the indoor side of the specified glazing layer [m²·K/W].</summary>
    /// <param name="glazingIndex">Glazing layer index; the air gap <b>between this layer and the next glazing toward the indoor side</b> is targeted.</param>
    /// <param name="resistance">Thermal resistance of the air gap [m²·K/W].</param>
    /// <remarks>
    /// Internally the gap is split evenly between two virtual nodes (to let
    /// absorbed heat in the two adjacent surfaces be redirected separately);
    /// callers pass the total gap resistance and the split is handled
    /// transparently. Like <see cref="SetGlassResistance"/>, this triggers a
    /// full re-integration of the absorbed-solar-heat-gain coefficients.
    /// </remarks>
    public void SetAirGapResistance(int glazingIndex, double resistance)
    {
      agapRes[2 * glazingIndex + 2] = agapRes[2 * glazingIndex + 3] = 0.5 * resistance;
      UpdateAbsorptance();
      needToUpdateUMatrix = true;
    }

    /// <summary>Gets the thermal resistance of the specified air gap layer [m²·K/W].</summary>
    /// <param name="glazingIndex">Glazing layer index (the air gap on the right side of this layer is returned).</param>
    /// <returns>Thermal resistance [m²·K/W].</returns>
    public double GetAirGapResistance(int glazingIndex)
    { return 2 * agapRes[2 * glazingIndex + 2]; }

    /// <summary>Updates the combined heat transfer coefficients on both sides.</summary>
    private void UpdateFilmCoefficient()
    {
      agapRes[0] = agapRes[1] = 0.5 / (ConvectiveCoefficientF + RadiativeCoefficientF);
      agapRes[agapRes.Length - 1] =
        agapRes[agapRes.Length - 2] = 0.5 / (ConvectiveCoefficientB + RadiativeCoefficientB);
      UpdateAbsorptance();
      // Film resistance changed → matrix's resS array stale; flag a rebuild.
      needToUpdateUMatrix = true;
    }

    /// <summary>Updates the absorbed solar heat gain coefficients for each glazing layer.</summary>
    private void UpdateAbsorptance()
    {
      double adB;
      IntegrateAbsorption(absFDir, agapRes, glassRes, out _, out adB);
      DirectSolarIncidentAbsorptance = adB;
      IntegrateAbsorption(absFDif, agapRes, glassRes, out _, out adB);
      DiffuseSolarIncidentAbsorptance = adB;
      IntegrateAbsorption(absBDif, agapRes, glassRes, out _, out adB);
      DiffuseSolarLostAbsorptance = adB;
    }

    /// <summary>Distributes the total absorptance between the F and B sides.</summary>
    /// <param name="ttlA">List of total absorptance values.</param>
    /// <param name="agapResist">List of air-gap thermal resistances.</param>
    /// <param name="glassResistance">List of glazing thermal resistances.</param>
    /// <param name="ttlAF">Apportioned amount on the F side.</param>
    /// <param name="ttlAB">Apportioned amount on the B side.</param>
    private static void IntegrateAbsorption
      (double[] ttlA, double[] agapResist, double[] glassResistance, out double ttlAF, out double ttlAB)
    {
      double rSum1, rSum2;
      rSum1 = rSum2 = ttlAF = ttlAB = 0;
      for (int i = 0; i < agapResist.Length; i++) rSum1 += agapResist[i];
      for (int i = 0; i < glassResistance.Length; i++) rSum1 += glassResistance[i];
      for (int i = 0; i < ttlA.Length; i++)
      {
        rSum2 += agapResist[i];
        if (i % 2 == 1) rSum2 += 0.5 * glassResistance[(i - 1) / 2];
        else if (i != 0) rSum2 += 0.5 * glassResistance[i / 2 - 1];
        double fRate = rSum2 / rSum1;
        ttlAB += ttlA[i] * fRate;
        ttlAF += ttlA[i] * (1 - fRate);
      }
    }

    /// <summary>Gets the transmittance of the specified glazing layer [-].</summary>
    /// <param name="glazingIndex">Glazing index (0, 1, 2, ...).</param>
    /// <param name="isSideF">True for the F (front) side; false for the B (back) side.</param>
    /// <returns>Transmittance [-].</returns>
    public double GetGlazingTransmittance(int glazingIndex, bool isSideF)
    {
      if (isSideF) return taurhoF[glazingIndex, 0];
      else return taurhoB[glazingIndex, 0];
    }

    /// <summary>Gets the reflectance of the specified glazing layer [-].</summary>
    /// <param name="glazingIndex">Glazing index (0, 1, 2, ...).</param>
    /// <param name="isSideF">True for the F (front) side; false for the B (back) side.</param>
    /// <returns>Reflectance [-].</returns>
    public double GetGlazingReflectance(int glazingIndex, bool isSideF)
    {
      if (isSideF) return taurhoF[glazingIndex, 1];
      else return taurhoB[glazingIndex, 1];
    }

    #endregion

    #region 入射角特性関連の処理

    /// <summary>Supplies custom angle-of-incidence polynomial coefficients for a glazing layer.</summary>
    /// <param name="layerIndex">Glazing layer index.</param>
    /// <param name="coefTF">Polynomial coefficients for F-side transmittance vs cos(θ).</param>
    /// <param name="coefTB">Polynomial coefficients for B-side transmittance vs cos(θ).</param>
    /// <param name="coefRF">Polynomial coefficients for F-side reflectance vs cos(θ).</param>
    /// <param name="coefRB">Polynomial coefficients for B-side reflectance vs cos(θ).</param>
    /// <remarks>
    /// The direct-beam transmittance/reflectance at a given incidence angle
    /// is modeled as a polynomial in cos(θ); this method sets those
    /// coefficients for one layer. Diffuse transmittance and reflectance
    /// are <b>re-derived from the same coefficients</b> (analytically
    /// integrated over the hemisphere), so calling this method refreshes
    /// both the direct and diffuse optics in one step. For standard glass
    /// types, prefer the preset overload
    /// <see cref="SetAngleDependence(int, GlassType)"/>.
    /// </remarks>
    public void SetAngleDependence
      (int layerIndex, double[] coefTF, double[] coefTB, double[] coefRF, double[] coefRB)
    {
      int ln = layerIndex;
      tau_CF[ln] = coefTF;
      tau_CB[ln] = coefTB;
      rho_CF[ln] = coefRF;
      rho_CB[ln] = coefRB;

      //拡散日射の規準化透過率と規準化反射率の計算
      double difCTF = 0;
      double difCTB = 0;
      double difCRF = 0;
      double difCRB = 0;
      for (int j = 0; j < tau_CF[ln].Length; j++)
      {
        difCTF += tau_CF[ln][j] / (j + 3);
        difCTB += tau_CB[ln][j] / (j + 3);
        difCRF += rho_CF[ln][j] / (j + 3);
        difCRB += rho_CB[ln][j] / (j + 3);
      }
      difCTF *= 2;
      difCTB *= 2;
      difCRF *= 2;
      difCRB *= 2;

      //拡散日射の透過率と反射率を計算
      opFDif[2 * ln + 1, 0] = difCTF * taurhoF[ln, 0];
      opFDif[2 * ln + 1, 1] = difCRF * taurhoF[ln, 1];
      opFDif[2 * ln + 1, 2] = 1 - (opFDif[2 * ln + 1, 0] + opFDif[2 * ln + 1, 1]);
      opBDif[2 * ln + 1, 0] = difCTB * taurhoB[ln, 0];
      opBDif[2 * ln + 1, 1] = difCRB * taurhoB[ln, 1];
      opBDif[2 * ln + 1, 2] = 1 - (opBDif[2 * ln + 1, 0] + opBDif[2 * ln + 1, 1]);

      //拡散日射に関する総合特性を更新
      UpdateDiffuseTotalProperties();
    }

    /// <summary>Applies preset angle-of-incidence coefficients for a standard glass type.</summary>
    /// <param name="layerIndex">Glazing layer index.</param>
    /// <param name="type">Standard glass category (transparent, heat-absorbing, heat-reflecting, low-E).</param>
    /// <remarks>
    /// Convenience over
    /// <see cref="SetAngleDependence(int, double[], double[], double[], double[])"/>:
    /// looks up representative polynomial coefficients for the selected
    /// <see cref="GlassType"/> and delegates to the general overload.
    /// For <see cref="GlassType.LowEmissivity"/>, the F-side and B-side
    /// coefficients differ — Low-E coatings are asymmetric by design.
    /// </remarks>
    public void SetAngleDependence(int layerIndex, GlassType type)
    {
      switch (type)
      {
        case GlassType.HeatAbsorbing:
          SetAngleDependence(layerIndex,
            new double[] { 1.760, 3.770, -14.901, 16.422, -6.052 },
            new double[] { 1.760, 3.770, -14.901, 16.422, -6.052 },
            new double[] { 5.189, -12.392, 16.593, -11.851, 3.461 },
            new double[] { 5.189, -12.392, 16.593, -11.851, 3.461 }
            );
          return;
        case GlassType.HeatReflecting:
          SetAngleDependence(layerIndex,
            new double[] { 3.297, -1.122, -8.408, 12.206, -4.972 },
            new double[] { 3.297, -1.122, -8.408, 12.206, -4.972 },
            new double[] { 5.842, -15.264, -21.642, -15.948, 4.727 },
            new double[] { 5.842, -15.264, -21.642, -15.948, 4.727 }
            );
          return;
        case GlassType.LowEmissivity:
          SetAngleDependence(layerIndex,
            new double[] { 2.273, 1.631, -10.358, 11.769, -4.316 },
            new double[] { 2.273, 1.631, -10.358, 11.769, -4.316 },
            new double[] { 5.084, -12.646, 18.213, -13.967, 4.316 },
            new double[] { 4.387, -9.175, 11.152, -7.416, 2.052 }
            );
          return;
        default:
          SetAngleDependence(layerIndex,
            new double[] { 2.552, 1.364, -11.388, 13.617, -5.146 },
            new double[] { 2.552, 1.364, -11.388, 13.617, -5.146 },
            new double[] { 5.189, -12.392, 16.593, -11.851, 3.461 },
            new double[] { 5.189, -12.392, 16.593, -11.851, 3.461 }
            );
          return;
      }
    }

    #endregion

  }

}