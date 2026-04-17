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

namespace Popolo.Core.Building.Envelope
{
  /// <summary>Represents a window assembly with multi-layer glazing and optional interior shading devices.</summary>
  public class Window : IReadOnlyWindow
  {

    #region 列挙型定義

    /// <summary>Specifies the glazing type.</summary>
    public enum GlassTypes
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
    /// 0:透過,1:反射,2:吸収
    /// F:正面,B:裏,Dir:直達,Dif:拡散</summary>
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

    /// <summary>Gets or sets the window surface area [m²].</summary>
    public double Area
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
      }
    }

    /// <summary>Gets the combined heat transfer coefficient on the F side (outdoor) [W/(m²·K)].</summary>
    public double FilmCoefficientF
    { get { return 1d / (2 * agapRes[0]); } }

    /// <summary>Gets the short-wave (solar) emissivity on the F side (outdoor) [-].</summary>
    public double ShortWaveEmissivityF
    { get { throw new PopoloNotImplementedException(
        $"{nameof(Window)}.{nameof(ShortWaveEmissivityF)}"); } }

    /// <summary>Gets or sets the long-wave (thermal) emissivity on the F side (outdoor) [-].</summary>
    public double LongWaveEmissivityF { get; set; } = 0.9;

    /// <summary>Gets or sets the sol-air temperature on the F side (outdoor) [°C].</summary>
    public double SolAirTemperatureF { get; set; }

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
      }
    }

    /// <summary>Gets the combined heat transfer coefficient on the B side (indoor) [W/(m²·K)].</summary>
    public double FilmCoefficientB
    { get { return 1d / (2 * agapRes[agapRes.Length - 1]); } }

    /// <summary>Gets the short-wave (solar) emissivity on the B side (indoor) [-].</summary>
    public double ShortWaveEmissivityB
    { get { return 1 - DiffuseSolarLostReflectance; } }

    /// <summary>Gets or sets the long-wave (thermal) emissivity on the B side (indoor) [-].</summary>
    public double LongWaveEmissivityB { get; set; } = 0.9;

    /// <summary>Gets or sets the sol-air temperature on the B side (indoor) [°C].</summary>
    public double SolAirTemperatureB { get; set; }

    /// <summary>Gets the surface temperature on the B side (indoor) [°C].</summary>
    public double SurfaceTemperatureB { get { return InsideSurface.SurfaceTemperature; } }

    /// <summary>Gets the boundary surface element on the indoor (B) side.</summary>
    internal BoundarySurface InsideSurface { get; private set; } = null!;

    /// <summary>Gets the boundary surface element on the outdoor (F) side.</summary>
    internal BoundarySurface OutsideSurface { get; private set; } = null!;

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new multi-layer glazing window assembly.</summary>
    /// <param name="area">面積[m2]</param>
    /// <param name="transmittanceF">正面側透過率リスト[-]（0=F, N-1=B）</param>
    /// <param name="reflectanceF">正面側反射率リスト[-]（0=F, N-1=B）</param>
    /// <param name="transmittanceB">裏側透過率リスト[-]（0=F, N-1=B）</param>
    /// <param name="reflectanceB">裏側反射率リスト[-]（0=F, N-1=B）</param>
    /// <param name="outsideIncline">外側傾斜面</param>
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

      //内外表面作成
      OutsideSurface = new BoundarySurface(this, true);
      InsideSurface = new BoundarySurface(this, false);

      //入射角特性を透明フロートガラスで初期化
      for (int i = 0; i < GlazingCount; i++) SetAngleDependence(i, GlassTypes.Transparent);
    }

    /// <summary>Initializes a new multi-layer glazing window assembly.</summary>
    /// <param name="area">面積[m2]</param>
    /// <param name="transmittance">透過率リスト[-]（0=F, N-1=B）</param>
    /// <param name="reflectance">反射率リスト[-]（0=F, N-1=B）</param>
    /// <param name="outsideIncline">外側傾斜面</param>
    public Window(double area, double[] transmittance, double[] reflectance, IReadOnlyIncline outsideIncline) :
      this(area, transmittance, reflectance, transmittance, reflectance, outsideIncline)
    { }

    #endregion

    #region インスタンスメソッド

    /// <summary>Updates optical properties based on the current solar position and shading device states.</summary>
    /// <param name="sun">太陽</param>
    public void UpdateOpticalProperties(IReadOnlySun sun)
    {
      //ガラス・日射遮蔽物単体の光学特性の更新処理//////////////////////////////
      double cos = OutsideIncline.GetDirectSolarRadiationRate(sun);
      if (sun.Altitude <= 0)
      {
        DirectSolarIncidentTransmittance = 0;
        DirectSolarIncidentReflectance = 1;
        DirectSolarIncidentAbsorptance = 0;
        for (int i = 0; i < sDevices.Length; i++) sDevices[i].ProfileAngle = 0;
        return;
      }
      bool sunMoved = (sun.Altitude != lstAlt || sun.Orientation != lstOri);
      if (sunMoved)
      {
        lstAlt = sun.Altitude;
        lstOri = sun.Orientation;

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

    /// <summary>Gets the total thermal resistance of the window assembly [m²·K/W].</summary>
    /// <returns>Total thermal resistance [m²·K/W].</returns>
    public double GetResistance()
    {
      double rg = 0;
      for (int i = 0; i < glassRes.Length; i++) rg += glassRes[i];
      for (int i = 0; i < agapRes.Length; i++) rg += agapRes[i];
      return rg;
    }

    /// <summary>Computes the total optical properties from the individual layer properties.</summary>
    /// <param name="opPropF">F側入射に対する光学特性</param>
    /// <param name="opPropB">B側入射に対する光学特性</param>
    /// <param name="ttlTF">出力:F側入射に対する総合透過率</param>
    /// <param name="ttlRF">出力:F側入射に対する総合反射率</param>
    /// <param name="ttlAF">出力:F側入射に対する総合吸収率</param>
    /// <param name="ttlTB">出力:B側入射に対する総合透過率</param>
    /// <param name="ttlRB">出力:B側入射に対する総合反射率</param>
    /// <param name="ttlAB">出力:B側入射に対する総合吸収率</param>
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

    /// <summary>Sets the shading device at the specified layer position.</summary>
    /// <param name="number">設定する層番号（屋外側:0,屋内側:N+1）</param>
    /// <param name="sDevice">日射遮蔽物</param>
    public void SetShadingDevice(int number, IShadingDevice sDevice) { sDevices[number] = sDevice; }

    /// <summary>Gets the shading device at the specified layer position.</summary>
    /// <param name="number">層番号（屋外側:0,屋内側:N+1）</param>
    /// <returns>The shading device.</returns>
    public IShadingDevice GetShadingDevice(int number) { return sDevices[number]; }

    /// <summary>Sets the thermal resistance of the specified glazing layer [m²·K/W].</summary>
    /// <param name="glazingIndex">ガラスの層番号</param>
    /// <param name="resistance">ガラスの熱抵抗[m2K/W]</param>
    public void SetGlassResistance(int glazingIndex, double resistance)
    {
      glassRes[glazingIndex] = resistance;
      UpdateAbsorptance();
    }

    /// <summary>Gets the thermal resistance of the specified glazing layer [m²·K/W].</summary>
    /// <param name="glazingIndex">ガラスの層番号</param>
    /// <returns>Thermal resistance [m²·K/W].</returns>
    public double GetGlassResistance(int glazingIndex)
    { return glassRes[glazingIndex]; }

    /// <summary>Sets the thermal resistance of the specified air gap layer [m²·K/W].</summary>
    /// <param name="glazingIndex">ガラスの層番号（層の右側の中空層が設定対象）</param>
    /// <param name="resistance">中空層の熱抵抗[m2K/W]</param>
    public void SetAirGapResistance(int glazingIndex, double resistance)
    {
      agapRes[2 * glazingIndex + 2] = agapRes[2 * glazingIndex + 3] = 0.5 * resistance;
      UpdateAbsorptance();
    }

    /// <summary>Gets the thermal resistance of the specified air gap layer [m²·K/W].</summary>
    /// <param name="glazingIndex">ガラスの層番号（層の右側の中空層が取得対象）</param>
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
    /// <param name="ttlA">総合吸収率リスト</param>
    /// <param name="agapResist">中空層の熱抵抗リスト</param>
    /// <param name="glassResistance">ガラスの熱抵抗リスト</param>
    /// <param name="ttlAF">F側按分量</param>
    /// <param name="ttlAB">B側按分量</param>
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
    /// <param name="glazingIndex">ガラスの番号(0,1,2...)</param>
    /// <param name="isSideF">F側か否か</param>
    /// <returns>Transmittance [-].</returns>
    public double GetGlazingTransmittance(int glazingIndex, bool isSideF)
    {
      if (isSideF) return taurhoF[glazingIndex, 0];
      else return taurhoB[glazingIndex, 0];
    }

    /// <summary>Gets the reflectance of the specified glazing layer [-].</summary>
    /// <param name="glazingIndex">ガラスの番号(0,1,2...)</param>
    /// <param name="isSideF">F側か否か</param>
    /// <returns>Reflectance [-].</returns>
    public double GetGlazingReflectance(int glazingIndex, bool isSideF)
    {
      if (isSideF) return taurhoF[glazingIndex, 1];
      else return taurhoB[glazingIndex, 1];
    }

    #endregion

    #region 入射角特性関連の処理

    /// <summary>Sets the angle-of-incidence correction coefficients for the specified glazing layer.</summary>
    /// <param name="layerIndex">層番号</param>
    /// <param name="coefTF">F側透過特性の近似係数</param>
    /// <param name="coefTB">B側透過特性の近似係数</param>
    /// <param name="coefRF">F側反射特性の近似係数</param>
    /// <param name="coefRB">B側反射特性の近似係数</param>
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

    /// <summary>Sets the angle-of-incidence correction coefficients for the specified glazing layer.</summary>
    /// <param name="layerIndex">層番号</param>
    /// <param name="type">ガラス種類</param>
    public void SetAngleDependence(int layerIndex, GlassTypes type)
    {
      switch (type)
      {
        case GlassTypes.HeatAbsorbing:
          SetAngleDependence(layerIndex,
            new double[] { 1.760, 3.770, -14.901, 16.422, -6.052 },
            new double[] { 1.760, 3.770, -14.901, 16.422, -6.052 },
            new double[] { 5.189, -12.392, 16.593, -11.851, 3.461 },
            new double[] { 5.189, -12.392, 16.593, -11.851, 3.461 }
            );
          return;
        case GlassTypes.HeatReflecting:
          SetAngleDependence(layerIndex,
            new double[] { 3.297, -1.122, -8.408, 12.206, -4.972 },
            new double[] { 3.297, -1.122, -8.408, 12.206, -4.972 },
            new double[] { 5.842, -15.264, -21.642, -15.948, 4.727 },
            new double[] { 5.842, -15.264, -21.642, -15.948, 4.727 }
            );
          return;
        case GlassTypes.LowEmissivity:
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