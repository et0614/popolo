/* VenetianBlind.cs
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
using Popolo.Core.Numerics.LinearAlgebra;

namespace Popolo.Core.Building.Envelope
{
  /// <summary>Represents a venetian blind with detailed optical property calculation.</summary>
  /// <remarks>Based on ISO 15099.</remarks>
  public class VenetianBlind : IShadingDevice
  {

    #region 定数宣言

    /// <summary>Number of slat subdivisions for the radiation calculation.</summary>
    private const int SP_N = 5;

    #endregion

    #region インスタンス変数・プロパティ

    /// <summary>Backing field for the deployed state.</summary>
    private bool pullDowned = true;

    /// <summary>Indicates whether direct irradiance properties need recalculation.</summary>
    private bool needUpdateDirectIrradianceProperties = true;

    /// <summary>Indicates whether the inverse matrix needs recalculation.</summary>
    private bool needUpdateInverseMatrix = true;

    /// <summary>Diffuse irradiance optical properties from the F side (upper/lower transmittance and reflectance).</summary>
    private double difTauF_U, difTauF_L, difRhoF_U, difRhoF_L;

    /// <summary>Diffuse irradiance optical properties from the B side (upper/lower transmittance and reflectance).</summary>
    private double difTauB_U, difTauB_L, difRhoB_U, difRhoB_L;

    /// <summary>Direct irradiance optical properties from the F side (upper/lower diffuse transmittance, reflectance, and beam transmittance).</summary>
    private double dirTauF_U, dirTauF_L, dirRhoF_U, dirRhoF_L, dirTauDir;

    /// <summary>View factor matrix between slat subdivisions.</summary>
    private double[,] vFacSlt = new double[SP_N + 2, SP_N + 2];

    /// <summary>View factor vector from F-side opening to slat subdivisions.</summary>
    private double[] vFacOF = new double[SP_N * 2 + 1];

    /// <summary>View factor vector from B-side opening to slat subdivisions.</summary>
    private double[] vFacOB = new double[SP_N * 2 + 1];

    /// <summary>Radiosity matrix.</summary>
    private IMatrix iaMatrix = new Matrix(SP_N * 2, SP_N * 2);

    /// <summary>Inverse of the radiosity matrix.</summary>
    private IMatrix iaMatrixINV = new Matrix(SP_N * 2, SP_N * 2);

    /// <summary>Normalized slat span length [-].</summary>
    private double dRspan;

    /// <summary>Profile angle [radian].</summary>
    private double pAngle;

    /// <summary>Slat angle [radian].</summary>
    private double sAngle;

    /// <summary>Gets the slat width [mm].</summary>
    public double SlatWidth { get; private set; }

    /// <summary>Gets the slat spacing [mm].</summary>
    public double SlatSpan { get; private set; }

    /// <summary>Gets or sets a value indicating whether the blind is deployed.</summary>
    public bool Pulldowned
    {
      get { return pullDowned; }
      set
      {
        HasPropertyChanged |= (pullDowned != value);
        pullDowned = value;
      }
    }

    /// <summary>Gets a value indicating whether optical properties have changed.</summary>
    public bool HasPropertyChanged { get; private set; }

    /// <summary>Gets or sets the profile angle (apparent solar altitude) [radian].</summary>
    public double ProfileAngle
    {
      get { return pAngle; }
      set
      {
        if (pAngle == value) return;
        pAngle = value;
        needUpdateDirectIrradianceProperties = true;
        HasPropertyChanged = true;
      }
    }

    /// <summary>Gets or sets the slat angle [radian] (0 = horizontal).</summary>
    public double SlatAngle
    {
      get { return sAngle; }
      set
      {
        if (sAngle == value) return;
        sAngle = value;
        needUpdateInverseMatrix = true;
        HasPropertyChanged = true;
      }
    }

    /// <summary>Gets the upper surface transmittance of the slat [-].</summary>
    public double UpsideTransmittance { get; private set; }

    /// <summary>Gets the lower surface transmittance of the slat [-].</summary>
    public double DownsideTransmittance { get; private set; }

    /// <summary>Gets the upper surface reflectance of the slat [-].</summary>
    public double UpsideReflectance { get; private set; }

    /// <summary>Gets the lower surface reflectance of the slat [-].</summary>
    public double DownsideReflectance { get; private set; }

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new venetian blind with the specified slat geometry and optical properties.</summary>
    /// <param name="slatWidth">Slat width (any consistent unit).</param>
    /// <param name="slatSpan">Slat spacing (same unit as slatWidth).</param>
    /// <param name="upsideTransmittance">Transmittance of the upper slat surface [-].</param>
    /// <param name="downsideTransmittance">Transmittance of the lower slat surface [-].</param>
    /// <param name="upsideReflectance">Reflectance of the upper slat surface [-].</param>
    /// <param name="downsideReflectance">Reflectance of the lower slat surface [-].</param>
    /// <remarks>slatWidth and slatSpan must use the same unit.</remarks>
    public VenetianBlind(double slatWidth, double slatSpan, double upsideTransmittance,
      double downsideTransmittance, double upsideReflectance, double downsideReflectance)
    {
      SlatWidth = slatWidth;
      SlatSpan = slatSpan;
      dRspan = SP_N * slatSpan / slatWidth;
      UpsideTransmittance = upsideTransmittance;
      DownsideTransmittance = downsideTransmittance;
      UpsideReflectance = upsideReflectance;
      DownsideReflectance = downsideReflectance;
    }

    #endregion

    #region 光学特性計算関連の処理

    /// <summary>Computes the optical properties of the venetian blind.</summary>
    /// <param name="isDiffuseIrradianceProperties">True for diffuse irradiance; false for direct.</param>
    /// <param name="irradianceFromSideF">True if irradiance is from the F side.</param>
    /// <param name="transmittance">Transmittance [-].</param>
    /// <param name="reflectance">Reflectance [-].</param>
    public void ComputeOpticalProperties(bool isDiffuseIrradianceProperties, bool irradianceFromSideF,
      out double transmittance, out double reflectance)
    {
      if (!Pulldowned)
      {
        transmittance = 1.0;
        reflectance = 0.0;
        return;
      }

      //必要に応じて逆行列を更新
      if (needUpdateInverseMatrix)
      {
        UpdateInverseMatrix();
        UpdateDirectIrradianceProperties();
        UpdateDiffuseIrradianceProperties();
        HasPropertyChanged = false;
      }

      //拡散日射に対する光学特性
      if (isDiffuseIrradianceProperties)
      {
        if (irradianceFromSideF)
        {
          transmittance = difTauF_U + difTauF_L;
          reflectance = difRhoF_U + difRhoF_L;
        }
        else
        {
          transmittance = difTauB_U + difTauB_L;
          reflectance = difRhoB_U + difRhoB_L;
        }
      }
      //直達日射に対する光学特性
      else
      {
        if (needUpdateDirectIrradianceProperties)
        {
          UpdateDirectIrradianceProperties();
          HasPropertyChanged = false;
        }

        if (irradianceFromSideF)
        {
          transmittance = dirTauF_U + dirTauF_L + dirTauDir;
          reflectance = dirRhoF_U + dirRhoF_L;
        }
        else
        {
          //F側と同じ値とする
          transmittance = dirTauF_U + dirTauF_L + dirTauDir;
          reflectance = dirRhoF_U + dirRhoF_L;
        }
      }
    }

    /// <summary>Computes the irradiance fractions reaching each opening from the slat surfaces.</summary>
    /// <param name="eVec">Irradiance absorbed by each slat subdivision.</param>
    /// <param name="sideF_U">Upward irradiance reaching the F-side opening.</param>
    /// <param name="sideF_L">Downward irradiance reaching the F-side opening.</param>
    /// <param name="sideB_U">Upward irradiance reaching the B-side opening.</param>
    /// <param name="sideB_L">Downward irradiance reaching the B-side opening.</param>
    private void ComputeRateToOpenings
      (IVector eVec, out double sideF_U, out double sideF_L, out double sideB_U, out double sideB_L)
    {
      double td1, td2, td3, td4, rd1, rd2, rd3, rd4;
      td1 = td2 = td3 = td4 = rd1 = rd2 = rd3 = rd4 = 0;
      for (int i = 0; i < SP_N; i++)
      {
        td1 += eVec[i] * vFacSlt[SP_N + 1, i];
        td2 += eVec[i + SP_N] * vFacSlt[SP_N + 1, i];
        td3 += eVec[i + SP_N] * vFacSlt[i, SP_N + 1];
        td4 += eVec[i] * vFacSlt[i, SP_N + 1];
        rd1 += eVec[i] * vFacSlt[SP_N, i];
        rd2 += eVec[i + SP_N] * vFacSlt[SP_N, i];
        rd3 += eVec[i + SP_N] * vFacSlt[i, SP_N];
        rd4 += eVec[i] * vFacSlt[i, SP_N];
      }
      sideB_U = td1 * UpsideTransmittance + td2 * DownsideReflectance;
      sideB_L = td3 * DownsideTransmittance + td4 * UpsideReflectance;
      sideF_U = rd1 * UpsideTransmittance + rd2 * DownsideReflectance;
      sideF_L = rd3 * DownsideTransmittance + rd4 * UpsideReflectance;
    }

    /// <summary>Computes optical properties for diffuse irradiance.</summary>
    private void UpdateDiffuseIrradianceProperties()
    {
      IVector bVec = new Vector(SP_N * 2);
      IVector eVec = new Vector(SP_N * 2);

      //F側開口からの拡散日射に対する透過率・反射率
      for (int i = 0; i < bVec.Length; i++) bVec[i] = vFacOF[i];
      LinearAlgebraOperations.Multiplicate(iaMatrixINV, bVec, eVec, 1, 0);
      ComputeRateToOpenings(eVec, out difRhoF_U, out difRhoF_L, out difTauF_U, out difTauF_L);
      difTauF_L += vFacOF[SP_N * 2];

      //B側開口からの拡散日射に対する透過率・反射率
      for (int i = 0; i < bVec.Length; i++) bVec[i] = vFacOB[i];
      LinearAlgebraOperations.Multiplicate(iaMatrixINV, bVec, eVec, 1, 0);
      ComputeRateToOpenings(eVec, out difTauB_U, out difTauB_L, out difRhoB_U, out difRhoB_L);
      difTauB_L += vFacOB[SP_N * 2];
    }

    /// <summary>Computes optical properties for direct irradiance based on the current profile angle.</summary>
    private void UpdateDirectIrradianceProperties()
    {
      //スラットに直接入射する日射を計算
      IVector bVec = new Vector(SP_N * 2);
      IVector eVec = new Vector(SP_N * 2);

      if (-SlatAngle == ProfileAngle)
      {
        dirTauDir = 1.0;
        dirTauF_U = dirTauF_L = dirRhoF_U = dirRhoF_L = 0;
        return;
      }

      double dRrad = dRspan / (Math.Cos(SlatAngle) * Math.Abs(Math.Tan(SlatAngle) + Math.Tan(ProfileAngle)));
      int bf = 0;
      if (ProfileAngle < -SlatAngle) bf = SP_N;
      for (int i = 0; i < SP_N; i++) bVec[i + bf] = Math.Min(1, Math.Max(0, dRrad - i)) / dRrad;
      LinearAlgebraOperations.Multiplicate(iaMatrixINV, bVec, eVec, 1, 0);

      //開口部へ向かう日射を計算
      ComputeRateToOpenings(eVec, out dirRhoF_U, out dirRhoF_L, out dirTauF_U, out dirTauF_L);
      dirTauDir = Math.Max(0, dRrad - SP_N) / dRrad;

      needUpdateDirectIrradianceProperties = false;
    }

    #endregion

    #region 逆行列関連の処理

    /// <summary>Updates all view factors for the current slat angle.</summary>
    private void UpdateViewFactor()
    {
      //スラット間距離を更新
      double[] dn = new double[SP_N * 2 + 1];
      double sinPsi = Math.Sin(SlatAngle);
      for (int i = 0; i < dn.Length; i++)
      {
        int bf = (i - SP_N);
        dn[i] = Math.Sqrt(dRspan * (2 * bf * sinPsi + dRspan) + bf * bf);
      }

      //スラットからみた形態係数を更新
      double sum;
      double[] ff = new double[2 * SP_N - 1];
      for (int i = 0; i < ff.Length; i++) ff[i] = 0.5 * (dn[i] + dn[i + 2]) - dn[i + 1];
      for (int i = 0; i < SP_N; i++)
      {
        sum = 0;
        for (int j = 0; j < SP_N; j++)
        {
          vFacSlt[i, j] = ff[j - i + SP_N - 1];
          sum += vFacSlt[i, j];
        }
        vFacSlt[i, SP_N] = vFacSlt[SP_N + 1, SP_N - 1 - i] = 0.5 * (dn[SP_N - i] - dn[SP_N - 1 - i] + 1);
        vFacSlt[i, SP_N + 1] = vFacSlt[SP_N, SP_N - 1 - i] = 1 - (sum + vFacSlt[i, SP_N]);
      }

      //開口からみた形態係数を更新
      sum = 0;
      for (int i = 0; i < SP_N; i++)
      {
        vFacOF[i] = vFacOB[vFacOB.Length - 2 - i] = vFacSlt[i, SP_N] / dRspan;
        vFacOF[i + SP_N] = vFacOB[SP_N - 1 - i] = vFacSlt[SP_N, i] / dRspan;
        sum += vFacOF[i] + vFacOF[i + SP_N];
      }
      vFacOF[SP_N * 2] = vFacOB[SP_N * 2] = 1 - sum;
    }

    /// <summary>Updates the radiosity inverse matrix for the current slat angle.</summary>
    private void UpdateInverseMatrix()
    {
      //形態係数を更新
      UpdateViewFactor();

      for (int i = 0; i < SP_N; i++)
      {
        for (int j = 0; j < SP_N; j++)
        {
          double dlt = 0;
          if (i == j) dlt = 1;
          iaMatrix[i, j] = dlt - (UpsideTransmittance * vFacSlt[i, j]);
          iaMatrix[i, j + SP_N] = -(DownsideReflectance * vFacSlt[i, j]);
          iaMatrix[i + SP_N, j + SP_N] = dlt - (DownsideTransmittance * vFacSlt[j, i]);
          iaMatrix[i + SP_N, j] = -(UpsideReflectance * vFacSlt[j, i]);
        }
      }
      LinearAlgebraOperations.GetInverse(iaMatrix, iaMatrixINV);

      needUpdateInverseMatrix = false;
    }

    #endregion

    #region 日照関連の処理

    /// <summary>Computes detailed optical properties for illuminance calculation.</summary>
    /// <param name="diffuseDiffuseTransmittance_U">Upper diffuse-diffuse transmittance [-].</param>
    /// <param name="diffuseDiffuseTransmittance_L">Lower diffuse-diffuse transmittance [-].</param>
    /// <param name="directDiffuseTransmittance_U">Upper direct-to-diffuse transmittance [-].</param>
    /// <param name="directDiffuseTransmittance_L">Lower direct-to-diffuse transmittance [-].</param>
    /// <param name="directDirectTransmittanse">Direct-to-direct (beam) transmittance [-].</param>
    public void ComputeOpticalProperties
      (out double diffuseDiffuseTransmittance_U, out double diffuseDiffuseTransmittance_L,
      out double directDiffuseTransmittance_U, out double directDiffuseTransmittance_L, out double directDirectTransmittanse)
    {
      //必要に応じて逆行列を更新
      if (needUpdateInverseMatrix)
      {
        UpdateInverseMatrix();
        UpdateDirectIrradianceProperties();
        UpdateDiffuseIrradianceProperties();
        HasPropertyChanged = false;
      }

      if (!Pulldowned)
      {
        diffuseDiffuseTransmittance_U = 0.3;
        diffuseDiffuseTransmittance_L = 0.7;
        directDiffuseTransmittance_U = directDiffuseTransmittance_L = 0;
        directDirectTransmittanse = 1.0;
      }
      else
      {
        diffuseDiffuseTransmittance_U = difTauF_U;
        diffuseDiffuseTransmittance_L = difTauF_L;
        directDiffuseTransmittance_U = dirTauF_U;
        directDiffuseTransmittance_L = dirTauF_L;
        directDirectTransmittanse = dirTauDir;
      }
    }

    #endregion

  }
}
