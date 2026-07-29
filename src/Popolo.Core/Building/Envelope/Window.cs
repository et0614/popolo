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

    #region Enumeration definitions

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

    #region Instance variables and properties

    /// <summary>Angle-of-incidence correction coefficients for each glazing layer.</summary>
    private double[][] tau_CF = null!, tau_CB = null!, rho_CF = null!, rho_CB = null!;

    /// <summary>Thermal resistance of each air gap layer [m²·K/W].</summary>
    private double[] agapRes = null!;

    /// <summary>Thermal resistance of each glazing layer [m²·K/W].</summary>
    private double[] glassRes = null!;

    /// <summary>Heat capacity per unit area of each glazing layer [J/(m²·K)].</summary>
    /// <remarks>
    /// Default zero (resistive-only model — Phase C-2 behavior). Set via
    /// <see cref="SetGlassHeatCapacity(int, double)"/> to introduce thermal
    /// mass on a glass layer (e.g., 3 mm clear glass: ρ × c × d ≈
    /// 2500 × 840 × 0.003 ≈ 6.3 kJ/(m²·K)). Half of the value is assigned
    /// to each of the two surface nodes of the glass (F and B faces),
    /// matching Wall's <c>HeatCapacity_F</c> / <c>HeatCapacity_B</c> split.
    /// </remarks>
    private double[] glassHeatCapacity = null!;

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
        sunShade = new SunShade(value); //Set a copy
        sunShade.Incline = OutsideIncline;
      }
    }


    /// <summary>Convective and radiative heat transfer coefficients on the F and B sides [W/(m²·K)].</summary>
    private double cCoefF, rCoefF, cCoefB, rCoefB;

    /// <inheritdoc/>
    protected override double GetConvectiveCoefficientFCore() => cCoefF;
    /// <inheritdoc/>
    protected override void SetConvectiveCoefficientFCore(double value)
    {
      cCoefF = value;
      UpdateFilmCoefficient();
      if (OutsideSurface != null) OutsideSurface.BoundaryCoefficientChanged = true;
    }

    /// <inheritdoc/>
    protected override double GetRadiativeCoefficientFCore() => rCoefF;
    /// <inheritdoc/>
    protected override void SetRadiativeCoefficientFCore(double value)
    {
      rCoefF = value;
      UpdateFilmCoefficient();
      if (OutsideSurface != null) OutsideSurface.BoundaryCoefficientChanged = true;
    }

    /// <summary>Gets the combined heat transfer coefficient on the F side (outdoor) [W/(m²·K)].</summary>
    public override double FilmCoefficientF
    { get { return 1d / (2 * agapRes[0]); } }

    /// <summary>Gets the short-wave (solar) absorptance on the F side (outdoor) [-].</summary>
    /// <remarks>
    /// Returns 0: in the matrix-based glazing model, outdoor-incident solar
    /// is absorbed per glass layer (via <c>absFDir</c> / <c>absFDif</c> and
    /// fed into the matrix as a body source), not at the outer surface. The
    /// SolAir-temperature formula at the outer face therefore applies no
    /// surface absorptance term (alpha × solar = 0). The setter is a no-op.
    /// </remarks>
    public override double ShortWaveAbsorptanceF { get => 0.0; set { } }

    /// <summary>
    /// Whether the F (outdoor) side is exposed to outdoor wind. Defaults to <c>true</c>
    /// since a window's F side is by construction the outdoor face. When <c>false</c>,
    /// the F-side convective coefficient is excluded from the wind-speed-driven
    /// dynamic update and retains its user-set value.
    /// </summary>
    public override bool IsWindExposedF { get; set; } = true;

    /// <summary>Gets the surface temperature on the F side (outdoor) [°C].</summary>
    public override double SurfaceTemperatureF { get { return OutsideSurface.SurfaceTemperature; } }

    /// <summary>
    /// Computes the outdoor-side convective heat transfer coefficient
    /// [W/(m²·K)] for a glazing surface using the MoWiTT correlation
    /// (Yazdanian–Klems 1994; <see cref="ExteriorConvection.GetMoWiTT"/>) —
    /// the smooth-glass fit appropriate for window panes, in contrast with
    /// the Walton TARP default used by opaque <see cref="Wall"/>.
    /// </summary>
    internal override double ComputeExteriorConvectiveCoefficient(
        bool isSideF, double windSpeed, double airTemperature, WindOrientation orientation)
    {
      double tSurf = isSideF ? SurfaceTemperatureF : SurfaceTemperatureB;
      double rf = isSideF ? SurfaceRoughnessMultiplierF : SurfaceRoughnessMultiplierB;
      double dT = tSurf - airTemperature;
      return ExteriorConvection.GetMoWiTT(windSpeed, dT, rf, orientation);
    }

    /// <inheritdoc/>
    protected override double GetConvectiveCoefficientBCore() => cCoefB;
    /// <inheritdoc/>
    protected override void SetConvectiveCoefficientBCore(double value)
    {
      cCoefB = value;
      UpdateFilmCoefficient();
      if (InsideSurface != null) InsideSurface.BoundaryCoefficientChanged = true;
    }

    /// <inheritdoc/>
    protected override double GetRadiativeCoefficientBCore() => rCoefB;
    /// <inheritdoc/>
    protected override void SetRadiativeCoefficientBCore(double value)
    {
      rCoefB = value;
      UpdateFilmCoefficient();
      if (InsideSurface != null) InsideSurface.BoundaryCoefficientChanged = true;
    }

    /// <summary>Gets the combined heat transfer coefficient on the B side (indoor) [W/(m²·K)].</summary>
    public override double FilmCoefficientB
    { get { return 1d / (2 * agapRes[agapRes.Length - 1]); } }

    /// <summary>Gets the short-wave (solar) absorptance on the B side (indoor) [-].</summary>
    /// <remarks>
    /// Computed as <c>1 − DiffuseSolarLostReflectance</c>: the fraction of
    /// indoor-side incident short-wave that is not reflected back to the room.
    /// The setter is a no-op since the value is derived from the optical state.
    /// </remarks>
    public override double ShortWaveAbsorptanceB { get => 1 - DiffuseSolarLostReflectance; set { } }

    /// <summary>Gets the surface temperature on the B side (indoor) [°C].</summary>
    public override double SurfaceTemperatureB { get { return InsideSurface.SurfaceTemperature; } }

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

    #region Constructors

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
      // Window glass is VerySmooth (R_f=1.00; MoWiTT basis). Same as the base-class default, but stated explicitly.
      SetSurfaceRoughnessF(SurfaceRoughness.VerySmooth);
      SetSurfaceRoughnessB(SurfaceRoughness.VerySmooth);

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
      glassHeatCapacity = new double[GlazingCount]; // Default heat capacity is 0 (Phase C-2 compatible)

      //Initialize with an empty sun shade
      sDevices = new IShadingDevice[GlazingCount + 1];
      for (int i = 0; i < sDevices.Length; i++)
      {
        sDevices[i] = new NoShadingDevice();
        int i2 = i * 2;
        opFDir[i2, 0] = opBDir[i2, 0] = opFDif[i2, 0] = opBDif[i2, 0] = 1.0;
        opFDir[i2, 1] = opBDir[i2, 1] = opFDif[i2, 1] = opBDif[i2, 1] =
          opFDir[i2, 2] = opBDir[i2, 2] = opFDif[i2, 2] = opBDif[i2, 2] = 0.0;
      }

      //Store transmittance and reflectance at normal incidence
      for (int i = 0; i < GlazingCount; i++)
      {
        taurhoF[i, 0] = transmittanceF[i];
        taurhoB[i, 0] = transmittanceB[i];
        taurhoF[i, 1] = reflectanceF[i];
        taurhoB[i, 1] = reflectanceB[i];
      }

      //Initialize thermal resistances
      RadiativeCoefficientF = RadiativeCoefficientB = 4.5;
      ConvectiveCoefficientF = 18.5;
      ConvectiveCoefficientB = 7.5;
      UpdateFilmCoefficient();
      agapRes[agapRes.Length - 1] = agapRes[agapRes.Length - 2] = 0.5 * 1 / 6.7;
      for (int i = 2; i < agapRes.Length - 2; i++) agapRes[i] = 0.5 * 1 / 6.7;
      for (int i = 0; i < glassRes.Length; i++) glassRes[i] = 0.006;

      //Allocate arrays for the matrix solver (capS all 0 = no heat capacity; resS is the glass + air-gap chain)
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
      needToUpdateUMatrix = true; //Have the first Update build the inverse matrix

      //Create the indoor and outdoor surfaces
      SurfaceF = new EnvelopeSurface(this, true);
      SurfaceB = new EnvelopeSurface(this, false);

      //Initialize incident angle characteristics as clear float glass
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

    #region Instance methods

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
      //Update optical properties of individual glass panes and shading devices//////////////////////////////
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

        //Apply the incident angle characteristics of the glass for direct solar radiation
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

        //Update the profile angle of the shading device
        double pAngle = OutsideIncline.GetProfileAngle(sun);
        for (int i = 0; i < sDevices.Length; i++) sDevices[i].ProfileAngle = pAngle;
      }

      //Update the optical properties of the shading devices
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

      //Exit if the solar position and shading device optical properties are unchanged
      if (!sunMoved && !sPropChanged) return;

      //Update overall optical properties//////////////////////////////////////////////////
      //Update characteristics for direct solar radiation
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

      //Update characteristics for diffuse solar radiation when shading device optical properties change
      if (sPropChanged) UpdateDiffuseTotalProperties();
    }

    /// <summary>
    /// Gauss-Legendre 8-point quadrature for hemispherical Lambertian
    /// integration over <c>x = cos θ ∈ [0, 1]</c>:
    /// <c>∫_0^{π/2} f(θ) · 2 cos θ sin θ dθ = ∫_0^1 f(arccos x) · 2x dx
    /// ≈ Σ_k w_k · 2x_k · f(arccos x_k)</c>.
    /// Nodes mapped from the standard [-1, 1] form and weights halved.
    /// </summary>
    private static readonly double[] _hemisQuadX = {
      0.019855071751231856, 0.101666761293186630, 0.237233795041835507, 0.408282678752175098,
      0.591717321247824902, 0.762766204958164493, 0.898333238706813370, 0.980144928248768144
    };
    private static readonly double[] _hemisQuadW = {
      0.050614268145188129, 0.111190517226687235, 0.156853322938943644, 0.181341891689180991,
      0.181341891689180991, 0.156853322938943644, 0.111190517226687235, 0.050614268145188129
    };

    /// <summary>
    /// Updates total optical properties for diffuse (hemispherical Lambertian)
    /// solar irradiance by numerically integrating the at-angle multi-layer
    /// transmittance over the hemisphere.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The straightforward "per-pane hemis → matrix combine" approach
    /// systematically underestimates the system diffuse transmittance for a
    /// multi-layer assembly: by Jensen's inequality, the matrix combination
    /// formula <c>T_dbl = T_pane² / (1 − R_pane²)</c> evaluated at the
    /// per-pane hemispherical means is <i>not</i> equal to the hemispherical
    /// mean of the at-angle <c>T_dbl(θ)</c>, because the function is
    /// non-linear in (T, R).
    /// </para>
    /// <para>
    /// For non-scattering glazing each Lambertian ray traverses every pane at
    /// the same incidence angle θ, so the physically correct integral is
    /// <c>T_dbl_hemis = ∫ T_dbl(θ) · 2 cos θ sin θ dθ</c> with
    /// <c>T_dbl(θ)</c> obtained by combining the at-angle per-pane values
    /// through the same matrix recursion used for direct solar. The 8-point
    /// Gauss-Legendre quadrature below evaluates this integral and replaces
    /// the previous hemis-then-combine shortcut.
    /// </para>
    /// <para>
    /// Shading-device layers (even-index slots) are treated as
    /// angle-independent: their pre-integrated diffuse properties from
    /// <see cref="opFDif"/> / <see cref="opBDif"/> are reused at every
    /// quadrature angle. Only glass layers (odd-index slots) are evaluated
    /// from the per-pane angular polynomial at each θ.
    /// </para>
    /// </remarks>
    private void UpdateDiffuseTotalProperties()
    {
      int rowCount = opFDif.GetLength(0);
      double[,] opFAt = new double[rowCount, 3];
      double[,] opBAt = new double[rowCount, 3];
      double[] absFAt = new double[rowCount];
      double[] absBAt = new double[rowCount];

      // Shading-device rows (even indices) are angle-independent in this model;
      // copy their pre-integrated diffuse values once.
      for (int i = 0; i < rowCount; i += 2)
      {
        opFAt[i, 0] = opFDif[i, 0]; opFAt[i, 1] = opFDif[i, 1]; opFAt[i, 2] = opFDif[i, 2];
        opBAt[i, 0] = opBDif[i, 0]; opBAt[i, 1] = opBDif[i, 1]; opBAt[i, 2] = opBDif[i, 2];
      }

      double tDifF = 0.0, rDifF = 0.0, tDifB = 0.0, rDifB = 0.0;
      for (int j = 0; j < absFDif.Length; j++) { absFDif[j] = 0.0; absBDif[j] = 0.0; }

      for (int k = 0; k < _hemisQuadX.Length; k++)
      {
        double cos = _hemisQuadX[k];
        double weight = _hemisQuadW[k] * 2.0 * cos;   // Lambertian: 2 cos θ sin θ dθ → 2x dx

        // Glass rows (odd indices): evaluate per-pane T(θ), R(θ) from the
        // angular polynomial at this quadrature point, exactly as
        // UpdateOpticalProperties does for direct solar. During the
        // constructor's initial SetAngleDependence loop the polynomials of
        // later panes have not been assigned yet (tau_CF[i] == null); fall
        // back to the per-pane diffuse values stored in opFDif / opBDif so
        // that the system calc proceeds without a NullReferenceException.
        for (int i = 0; i < GlazingCount; i++)
        {
          int row = 2 * i + 1;
          if (tau_CF[i] == null)
          {
            opFAt[row, 0] = opFDif[row, 0]; opFAt[row, 1] = opFDif[row, 1]; opFAt[row, 2] = opFDif[row, 2];
            opBAt[row, 0] = opBDif[row, 0]; opBAt[row, 1] = opBDif[row, 1]; opBAt[row, 2] = opBDif[row, 2];
            continue;
          }
          double tauF = 0, tauB = 0, rhoF = 0, rhoB = 0;
          int len = tau_CF[i].Length - 1;
          for (int n = len; 0 <= n; n--)
          {
            tauF = cos * (tauF + tau_CF[i][n]);
            tauB = cos * (tauB + tau_CB[i][n]);
            rhoF = cos * (rhoF + rho_CF[i][n]);
            rhoB = cos * (rhoB + rho_CB[i][n]);
          }
          opFAt[row, 0] = tauF * taurhoF[i, 0];
          opFAt[row, 1] = 1 - (1 - taurhoF[i, 1]) * rhoF;
          opFAt[row, 2] = 1 - (opFAt[row, 0] + opFAt[row, 1]);
          opBAt[row, 0] = tauB * taurhoB[i, 0];
          opBAt[row, 1] = 1 - (1 - taurhoB[i, 1]) * rhoB;
          opBAt[row, 2] = 1 - (opBAt[row, 0] + opBAt[row, 1]);
        }

        // ComputeTotalOProperties accumulates into the absorption arrays
        // via "+=", so they must be zeroed before each call.
        for (int j = 0; j < absFAt.Length; j++) { absFAt[j] = 0.0; absBAt[j] = 0.0; }

        // Combine via the same matrix recursion used for direct solar.
        ComputeTotalOProperties(opFAt, opBAt,
            out double ttlTF, out double ttlRF, ref absFAt,
            out double ttlTB, out double ttlRB, ref absBAt);

        // Lambertian-weighted accumulation.
        tDifF += weight * ttlTF;
        rDifF += weight * ttlRF;
        tDifB += weight * ttlTB;
        rDifB += weight * ttlRB;
        for (int j = 0; j < absFDif.Length; j++)
        {
          absFDif[j] += weight * absFAt[j];
          absBDif[j] += weight * absBAt[j];
        }
      }

      DiffuseSolarIncidentTransmittance = tDifF;
      DiffuseSolarIncidentReflectance = rDifF;
      DiffuseSolarLostTransmittance = tDifB;
      DiffuseSolarLostReflectance = rDifB;
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
      // Diffuse solar radiation: only the sky component is attenuated by a uniform SunShade factor; ground reflection is unchanged.
      //
      // Note: in the Perez (1990) anisotropic model, sky diffuse consists of
      //   (1) isotropic dome
      //   (2) circumsolar (concentrated near the sun)
      //   (3) horizon brightening (near the horizon)
      // — three components. The current approach of applying a uniform factor
      // (sky view factor ratio) to all sky diffuse is appropriate for (1), but not
      // for (2) (sun-direction dependent; shading is binary) or (3) (concentrated
      // at horizon altitude; not physically shaded by an overhang).
      // A theoretically rigorous treatment would decompose the three Perez components and attenuate them individually,
      // but for now all components are treated uniformly as an approximation (TODO: implement Perez decomposition).
      double diffuseTotal = inc.GetDiffuseSolarIrradiance(sun, albedo);
      double groundReflected = albedo * inc.ConfigurationFactorToGround
                             * sun.GlobalHorizontalRadiation;
      double skyDiffuse = diffuseTotal - groundReflected;
      if (skyDiffuse < 0) skyDiffuse = 0;
      double skyShadingRate = SunShade.GetSkyDiffuseShadingRate(inc);
      double dif = skyDiffuse * (1.0 - skyShadingRate) + groundReflected;

      // Feed the absorbed solar radiation of each layer [W/m²] to the matrix solver as per-node volumetric heat sources.
      // absFDir / absFDif have length 2N+1 (per layer boundary; odd index = glass layer center,
      // even index = air gap / shading position).
      // The absorption at each position i is split half-and-half between its two neighboring nodes
      // (only one side at the ends). This treats glass layers and shading positions uniformly.
      Array.Clear(solarAbsorption, 0, solarAbsorption.Length);
      int positions = absFDir.Length; // = 2N+1
      int last = positions - 1;
      for (int i = 0; i < positions; i++)
      {
        double q = dir * absFDir[i] + dif * absFDif[i];
        if (q == 0) continue;
        if (i == 0)
        {
          // Absorption at the outdoor end (e.g., outdoor-side ShadingDevice). Lumped into the outdoor surface node (0).
          solarAbsorption[0] += q;
        }
        else if (i == last)
        {
          // Absorption at the indoor end. Lumped into the indoor surface node (mNum-1).
          solarAbsorption[NodeCount - 1] += q;
        }
        else
        {
          // Intermediate position: half to each neighboring node.
          solarAbsorption[i - 1] += 0.5 * q;
          solarAbsorption[i] += 0.5 * q;
        }
      }

      // Short-wave radiation reaching the indoor side is handled by the matrix via the glass layer temperatures,
      // so the component absorbed at the surface (InsideAbsorbedFlux) is set to 0.
      // The transmitted portion is redistributed among the indoor surfaces as before.
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

    /// <summary>Refreshes the IF (current-state) coefficients for the glazing matrix.</summary>
    /// <remarks>
    /// Per-node body source <c>bf</c> is built from (i) the previous-step
    /// nodal state for capacity-bearing nodes (only) and (ii) the absorbed
    /// solar contribution. Capacity-zero nodes contribute purely from the
    /// absorbed solar — their <c>tempAndHumid</c> entry is overwritten each
    /// step by the matrix solve and carries no meaningful inter-step state,
    /// so it must be excluded to keep the response-coefficient
    /// decomposition consistent with <see cref="OpticalLayeredEnvelope.Update"/>.
    /// </remarks>
    public override void UpdateIFCoefficients()
    {
      int num = uxMatrix.Rows - 1;
      IF2_F = IF2_B = 0;
      for (int i = 0; i <= num; i++)
      {
        double bf = (capS[i] != 0) ? tempAndHumid[i] : 0;
        if (solarAbsorption[i] != 0) bf += qCoefS[i] * solarAbsorption[i];
        if (bf == 0) continue;
        IF2_F += uxMatrix[0, i] * bf;
        IF2_B += uxMatrix[num, i] * bf;
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

      // capS: distribute each glass layer's heat capacity to its F and B surface nodes
      // (half each, as <c>HeatCapacity_F</c> / <c>HeatCapacity_B</c> in the Wall convention).
      // Air gaps are treated as having zero heat capacity (physically negligible for an air layer as well).
      Array.Clear(capS, 0, capS.Length);
      for (int k = 0; k < N; k++)
      {
        capS[2 * k]     += 0.5 * glassHeatCapacity[k];
        capS[2 * k + 1] += 0.5 * glassHeatCapacity[k];
      }

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

    #region Model configuration methods

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

    /// <summary>Sets the heat capacity of the specified glazing layer [J/(m²·K)].</summary>
    /// <param name="glazingIndex">Glazing layer index.</param>
    /// <param name="heatCapacity">Heat capacity per unit area [J/(m²·K)].</param>
    /// <remarks>
    /// Default 0 — resistive-only model. Set a non-zero value to give the
    /// glass thermal mass; a typical 3 mm clear pane has
    /// ρ × c × d ≈ 2500 × 840 × 0.003 ≈ 6300 J/(m²·K). The value is split
    /// evenly between the layer's two surface nodes (F and B faces) when
    /// the matrix is rebuilt. Triggers a matrix rebuild on the next
    /// <see cref="OpticalLayeredEnvelope.Update"/> via
    /// <c>needToUpdateUMatrix = true</c>.
    /// </remarks>
    public void SetGlassHeatCapacity(int glazingIndex, double heatCapacity)
    {
      glassHeatCapacity[glazingIndex] = heatCapacity;
      needToUpdateUMatrix = true;
    }

    /// <summary>Gets the heat capacity per unit area of the specified glazing layer [J/(m²·K)].</summary>
    /// <param name="glazingIndex">Glazing layer index.</param>
    /// <returns>Heat capacity per unit area [J/(m²·K)].</returns>
    public double GetGlassHeatCapacity(int glazingIndex)
    { return glassHeatCapacity[glazingIndex]; }

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

    #region Incident angle characteristic methods

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

      //Compute the normalized transmittance and normalized reflectance for diffuse solar radiation
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

      // Compute hemisphere values of the diffuse transmittance and reflectance by analytical integration.
      // The polynomial normalization differs between T and R:
      //   P_T(cos) = T(θ) / T_normal             (T is a ratio)
      //     ⇒ difCTF = ∫ P_T · 2 sin θ cos θ dθ = T_hemis / T_normal
      //     ⇒ T_hemis = difCTF × T_normal                                 (line below)
      //   P_R(cos) = (1 − R(θ)) / (1 − R_normal)  (R is a ratio of (1−R))
      //     ⇒ difCRF = ∫ P_R · 2 sin θ cos θ dθ = (1 − R_hemis) / (1 − R_normal)
      //     ⇒ R_hemis = 1 − (1 − R_normal) × difCRF                       (line below)
      // (Symmetric with the same equation structure as the P_R → R(θ) recovery on the direct side, line 444)
      opFDif[2 * ln + 1, 0] = difCTF * taurhoF[ln, 0];
      opFDif[2 * ln + 1, 1] = 1 - (1 - taurhoF[ln, 1]) * difCRF;
      opFDif[2 * ln + 1, 2] = 1 - (opFDif[2 * ln + 1, 0] + opFDif[2 * ln + 1, 1]);
      opBDif[2 * ln + 1, 0] = difCTB * taurhoB[ln, 0];
      opBDif[2 * ln + 1, 1] = 1 - (1 - taurhoB[ln, 1]) * difCRB;
      opBDif[2 * ln + 1, 2] = 1 - (opBDif[2 * ln + 1, 0] + opBDif[2 * ln + 1, 1]);

      //Update overall characteristics for diffuse solar radiation
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