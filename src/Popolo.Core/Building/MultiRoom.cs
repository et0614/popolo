/* MultiRoom.cs
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
using System.Collections.Generic;

using Popolo.Core.Numerics.LinearAlgebra;
using Popolo.Core.Numerics;
using Popolo.Core.Climate;
using Popolo.Core.Building.Envelope;
using Popolo.Core.Physics;

namespace Popolo.Core.Building
{
  /// <inheritdoc cref="IReadOnlyMultiRoom"/>
  /// <remarks>
  /// This is the mutable implementation of <see cref="IReadOnlyMultiRoom"/>.
  /// Use the constructor to build a multi-room, then call the <c>Add*</c> and
  /// <c>Set*</c> methods to configure zones, walls, windows, and boundary
  /// conditions. For read-only access (e.g. when passing the model to other
  /// components), use the <see cref="IReadOnlyMultiRoom"/> interface.
  /// </remarks>
  public class MultiRoom : IReadOnlyMultiRoom
  {

    #region 定数宣言

    /// <summary>Linearized radiative heat transfer coefficient [W/(m²·K)] at a reference temperature of 24°C.</summary>
    private const double RAD_COEF = 4.0 * PhysicsConstants.StefanBoltzmannConstant * 297.15 * 297.15 * 297.15;

    #endregion

    #region インスタンス変数

    /// <summary>Array of thermal zones.</summary>
    private Zone[] zones;

    /// <summary>Array of wall assemblies.</summary>
    private Wall[] walls;

    /// <summary>Array of window assemblies.</summary>
    private Window[] windows;

    /// <summary>Flag indicating whether initialization is required.</summary>
    private bool needInitialize = true;

    /// <summary>Inter-zone air flow rates [kg/s].</summary>
    private double[,] zoneVent = null!;

    /// <summary>View factor matrices for each room.</summary>
    private IMatrix[] formFactor = null!;

    /// <summary>Gebhart absorption factor matrices for long-wave (gMatL) and short-wave (gMatS) radiation.</summary>
    private double[,] gMatL = null!, gMatS = null!;

    /// <summary>Array of boundary surface elements facing the interior.</summary>
    private BoundarySurface[] surfaces = null!;

    /// <summary>Floor surfaces that preferentially receive short-wave radiation from windows.</summary>
    private Dictionary<Window, BoundarySurface> swDistFloor = null!;

    /// <summary>Fraction of window short-wave radiation distributed to the floor.</summary>
    private Dictionary<Window, double> swDistRate = null!;

    /// <summary>Flags indicating whether the reverse side of each surface is a boundary condition.</summary>
    private bool[] isSFboundary = null!;

    /// <summary>Short-wave irradiance [W/m²] incident on each interior surface (windows store the absorbed SHGC value).</summary>
    private double[] radToSurf_S = null!;

    /// <summary>Long-wave irradiance [W/m²] incident on wall surfaces for each room.</summary>
    private double[] radToSurf_L = null!;

    /// <summary>List of boundary condition surfaces (outdoor-facing or ground-contact).</summary>
    internal List<BoundarySurface> bndSurfaces = new List<BoundarySurface>();

    /// <summary>Short-wave emissivity values for each window.</summary>
    private double[] wSWEmissivity = null!;

    /// <summary>Zone indices belonging to each room.</summary>
    private List<int>[] rZones = null!;

    /// <summary>Serial indices of surfaces, organized as [room][zone][surface].</summary>
    private int[][][] wsIndex = null!;

    /// <summary>True while a sensible heat balance forecast is in progress.</summary>
    private bool forecastingHeatTransfer = false;

    /// <summary>True while a moisture balance forecast is in progress.</summary>
    private bool forecastingMoistureTransfer = false;

    /// <summary>Temporary storage for zone temperatures and humidity ratios.</summary>
    private double[] zoneTemp = null!, zoneHumid = null!;

    /// <summary>Working matrices for the sensible heat balance calculation.</summary>
    private IMatrix matA = null!, matAInv = null!, matB = null!, matD = null!, matF = null!, matI = null!, matK = null!, matBf = null!;
    private IVector vecC = null!, vecEJ = null!, vecTH = null!, vecTWS = null!;

    /// <summary>Working matrices for the moisture balance calculation.</summary>
    private IMatrix matAW = null!, matCW = null!;
    private IVector vecWH = null!;

    #endregion

    #region プロパティ

    /// <summary>Calculation time step [s].</summary>
    private double timeStep = 3600;

    /// <summary>Gets or sets the calculation time step [s].</summary>
    public double TimeStep
    {
      get { return timeStep; }
      set
      {
        for (int i = 0; i < walls.Length; i++) walls[i].TimeStep = value;
        timeStep = value;
      }
    }

    /// <summary>Gets a value indicating whether coupled heat and moisture transfer is solved.</summary>
    public bool SolveMoistureTransferSimultaneously { get; private set; }

    /// <summary>Gets or sets the current simulation date and time.</summary>
    public DateTime CurrentDateTime { get; private set; }

    /// <summary>Gets or sets the solar state.</summary>
    public IReadOnlySun Sun { get; set; } = null!;

    /// <summary>Gets the number of rooms.</summary>
    public int RoomCount { get; private set; }

    /// <summary>Gets the number of zones.</summary>
    public int ZoneCount { get; private set; }

    /// <summary>Gets the array of zones in this multi-room system.</summary>
    public IReadOnlyZone[] Zones { get { return zones; } }

    /// <summary>Gets the array of wall assemblies.</summary>
    public IReadOnlyWall[] Walls { get { return walls; } }

    /// <summary>Gets the array of window assemblies.</summary>
    public IReadOnlyWindow[] Windows { get { return windows; } }

    /// <summary>Gets the outdoor dry-bulb temperature [°C].</summary>
    public double OutdoorTemperature { get; private set; }

    /// <summary>Gets the outdoor humidity ratio [kg/kg].</summary>
    public double OutdoorHumidityRatio { get; private set; }

    /// <summary>Gets the nocturnal (long-wave) radiation [W/m²].</summary>
    public double NocturnalRadiation { get; private set; }

    /// <summary>Gets or sets the ground surface albedo [-].</summary>
    public double Albedo { get; set; } = 0.4;

    /// <summary>Gets or sets a value indicating whether tilted-surface solar irradiance is provided directly.</summary>
    public bool IsSolarIrradianceGiven { get; set; } = false;

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new multi-room thermal model.</summary>
    /// <param name="rmCount">Total number of rooms.</param>
    /// <param name="zones">Array of thermal zones.</param>
    /// <param name="walls">Array of wall assemblies.</param>
    /// <param name="windows">Array of window assemblies.</param>
    /// <remarks>
    /// All zones are initially assigned to room 0 and all walls/windows are
    /// unplaced; the caller is expected to finish model construction by
    /// calling <see cref="AddZone(int,int)"/>, <see cref="AddWall(int,int,bool)"/>
    /// (or its interior-wall / loop-wall variants), <see cref="AddWindow(int,int)"/>,
    /// and one of the boundary setters
    /// (<see cref="SetOutsideWall(int,bool,IReadOnlyIncline)"/>,
    /// <see cref="SetGroundWall(int,bool,double)"/>,
    /// <see cref="UseAdjacentSpaceFactor(int,bool,double)"/>). Call
    /// <see cref="InitializeAirState"/> after configuration and before the
    /// first solver step.
    /// </remarks>
    public MultiRoom(int rmCount, Zone[] zones, Wall[] walls, Window[] windows)
    {
      RoomCount = rmCount;
      ZoneCount = zones.Length;
      this.walls = walls;
      this.windows = windows;
      this.zones = zones;
      formFactor = new IMatrix[RoomCount];
      rZones = new List<int>[RoomCount];
      radToSurf_L = new double[RoomCount];
      wsIndex = new int[RoomCount][][];
      zoneVent = new double[ZoneCount, ZoneCount];
      wSWEmissivity = new double[windows.Length];
      zoneTemp = new double[ZoneCount];
      zoneHumid = new double[ZoneCount];
      swDistFloor = new Dictionary<Window, BoundarySurface>();
      swDistRate = new Dictionary<Window, double>();
      for (int i = 0; i < RoomCount; i++) rZones[i] = new List<int>();
      for (int i = 0; i < ZoneCount; i++)
      {
        AddZone(0, i);
        zones[i].MultiRoom = this;
        zones[i].Index = i;
      }

      //熱水分同時移動判定//水分透過壁が1つでもあれば
      foreach (Wall wl in walls)
      {
        if (wl.ComputeMoistureTransfer)
        {
          SolveMoistureTransferSimultaneously = true;
          break;
        }
      }
      int num = ZoneCount;
      if (SolveMoistureTransferSimultaneously) num *= 2;
      matD = new Matrix(num, num);
      vecEJ = new Vector(num);
      matI = new Matrix(num, num);
      matK = new Matrix(num, num);
      vecTH = new Vector(num);
      matAW = new Matrix(num, num);
      matCW = new Matrix(num, num);
      vecWH = new Vector(num);
    }

    #endregion

    #region 熱平衡（熱水分同時移動対応）に関する処理

    /// <summary>Forecasts the future sensible heat balance state.</summary>
    /// <remarks>
    /// May be called multiple times iteratively.
    /// Call <see cref="FixHeatTransfer"/> afterwards to commit the result.
    /// </remarks>
    internal void ForecastHeatTransfer()
    {
      //準備計算実行
      PrepareForHeatTransfer();

      //IJ行列作成処理
      MakeIJMatrix();

      bool[] ctrl = new bool[matK.Columns];
      for (int i = 0; i < ctrl.Length; i++)
      {
        if (i < ZoneCount) ctrl[i] = zones[i].TemperatureControlled;
        else ctrl[i] = zones[i - ZoneCount].HumidityControlled;
      }

      matK.Initialize(0);
      for (int i = 0; i < matI.Rows; i++)
      {
        //K行列を作成
        for (int j = 0; j < matI.Columns; j++)
        {
          if (!ctrl[j]) matK[i, j] = matI[i, j];
          else if (ctrl[j] && i == j) matK[i, j] = -1;
        }

        //Lベクトルを作成
        vecTH[i] = vecEJ[i];
        for (int j = 0; j < matI.Columns; j++)
        {
          if (ctrl[j])
          {
            if (j < ZoneCount) vecTH[i] -= matI[i, j] * zones[j].TemperatureSetpoint;
            else vecTH[i] -= matI[i, j] * zones[j - ZoneCount].HumidityRatioSetpoint;
          }
        }
        if (!ctrl[i])
        {
          if (i < ZoneCount) vecTH[i] += zones[i].HeatSupply;
          else vecTH[i] += zones[i - ZoneCount].MoistureSupply;
        }
      }

      //連立方程式を解く（温湿度を計算）
      LinearAlgebraOperations.SolveLinearEquations(matK, vecTH);
      for (int i = 0; i < ZoneCount; i++)
      {
        if (zones[i].TemperatureControlled)
        {
          zones[i].HeatSupply = vecTH[i];
          zones[i].Temperature = zones[i].TemperatureSetpoint;
        }
        else zones[i].Temperature = vecTH[i];
        if (SolveMoistureTransferSimultaneously)
        {
          if (zones[i].HumidityControlled)
          {
            zones[i].MoistureSupply = vecTH[i + ZoneCount];
            zones[i].HumidityRatio = zones[i].HumidityRatioSetpoint;
          }
          else zones[i].HumidityRatio = vecTH[i + ZoneCount];
        }
      }
    }

    /// <summary>Commits the forecasted heat balance state and updates surface temperatures.</summary>
    internal void FixHeatTransfer()
    {
      forecastingHeatTransfer = false;

      //壁表面温度を計算
      for (int i = 0; i < ZoneCount; i++)
      {
        vecTH[i] = zones[i].Temperature;
        if (SolveMoistureTransferSimultaneously) vecTH[i + ZoneCount] = zones[i].HumidityRatio;
      }
      LinearAlgebraOperations.Multiply(matB, vecTH, vecC, 1, 1);
      LinearAlgebraOperations.Multiply(matAInv, vecC, vecTWS, 1, 0);

      //温湿度条件を設定
      for (int i = 0; i < RoomCount; i++)
      {
        for (int j = 0; j < wsIndex[i].Length; j++)
        {
          for (int k = 0; k < wsIndex[i][j].Length; k++)
          {
            //相当温度
            BoundarySurface ws1 = surfaces[wsIndex[i][j][k]];
            ws1.SolAirTemperature = 0;
            for (int m = 0; m < wsIndex[i].Length; m++)
            {
              for (int n = 0; n < wsIndex[i][m].Length; n++)
              {
                int s2 = surfaces[wsIndex[i][m][n]].Index;
                ws1.SolAirTemperature += gMatL[ws1.Index, s2] * vecTWS[s2];
              }
            }
            ws1.SolAirTemperature *= ws1.RadiativeCoefficient;
            ws1.SolAirTemperature += zones[rZones[i][j]].Temperature * ws1.ConvectiveCoefficient;
            ws1.SolAirTemperature += radToSurf_L[i];
            if (ws1.IsWall) ws1.SolAirTemperature += radToSurf_S[ws1.Index];
            ws1.SolAirTemperature /= ws1.FilmCoefficient;

            //絶対湿度
            if (SolveMoistureTransferSimultaneously) ws1.HumidityRatio = zones[rZones[i][j]].HumidityRatio;
          }
        }
      }
    }

    /// <summary>Forecasts and commits the heat balance state in a single call.</summary>
    internal void UpdateHeatTransfer()
    {
      ForecastHeatTransfer();
      FixHeatTransfer();
    }

    /// <summary>Computes the A and B matrices for the sensible heat balance.</summary>
    private void MakeABMatrix()
    {
      //AB行列の再計算の要否を確認
      bool needUpdateAB = false;
      for (int i = 0; i < walls.Length; i++)
      {
        if (walls[i].invMatrixUpdated)
        {
          needUpdateAB = true;
          break;
        }
      }
      if (!needUpdateAB) return;

      int nS = surfaces.Length;
      matA.Initialize(0);
      matB.Initialize(0);
      for (int i = 0; i < RoomCount; i++)
      {
        for (int j = 0; j < wsIndex[i].Length; j++)
        {
          for (int k = 0; k < wsIndex[i][j].Length; k++)
          {
            int s1 = wsIndex[i][j][k];
            BoundarySurface ws1 = surfaces[s1];
            BoundarySurface ws1R = ws1.ReverseSideSurface;

            //行列Aを作成
            //同一室
            for (int m = 0; m < wsIndex[i].Length; m++)
            {
              for (int n = 0; n < wsIndex[i][m].Length; n++)
              {
                int s2 = wsIndex[i][m][n];
                if (s1 == s2)
                {
                  matA[s1, s2] += 1;
                  if (SolveMoistureTransferSimultaneously) matA[s1 + nS, s2 + nS] += 1;
                }
                else
                {
                  double bf = ws1.RadiativeFraction * gMatL[s1, s2];
                  matA[s1, s2] += -ws1.FFS2 * bf;
                  if (SolveMoistureTransferSimultaneously) matA[s1 + nS, s2] += -ws1.FFS3 * bf;
                }
              }
            }
            //裏側室
            if (!isSFboundary[s1])
            {
              int rvRm = zones[ws1R.ZoneIndex].RoomIndex;
              for (int m = 0; m < wsIndex[rvRm].Length; m++)
              {
                for (int n = 0; n < wsIndex[rvRm][m].Length; n++)
                {
                  int s2 = wsIndex[rvRm][m][n];
                  if (ws1R.Index != s2)
                  {
                    double bf = ws1R.RadiativeFraction * gMatL[ws1R.Index, s2];
                    matA[s1, s2] += -ws1.BFS2 * bf;
                    if (SolveMoistureTransferSimultaneously) matA[s1 + nS, s2] += -ws1.BFS3 * bf;
                  }
                }
              }
            }

            //行列Bを作成
            for (int q = 0; q < zones.Length; q++)
            {
              int nQ = ZoneCount;
              if (rZones[i][j] == q)
              {
                matB[s1, q] += ws1.FFS2 * ws1.ConvectiveFraction;
                if (SolveMoistureTransferSimultaneously)
                {
                  matB[s1, q + nQ] += ws1.FFL2;
                  matB[s1 + nS, q] += ws1.FFS3 * ws1.ConvectiveFraction;
                  matB[s1 + nS, q + nQ] += ws1.FFL3;
                }
              }
              if (ws1R.ZoneIndex == q && !isSFboundary[s1])
              {
                matB[s1, q] += ws1.BFS2 * ws1R.ConvectiveFraction;
                if (SolveMoistureTransferSimultaneously)
                {
                  matB[s1, q + nQ] += ws1.BFL2;
                  matB[s1 + nS, q] += ws1.BFS3 * ws1R.ConvectiveFraction;
                  matB[s1 + nS, q + nQ] += ws1.BFL3;
                }
              }
            }
          }
        }
      }

      //Aの逆行列計算
      LinearAlgebraOperations.GetInverse(matA, matAInv);
    }

    /// <summary>Computes the C vector for the sensible heat balance.</summary>
    private void MakeCVector()
    {
      int nS = surfaces.Length;
      for (int i = 0; i < RoomCount; i++)
      {
        for (int j = 0; j < wsIndex[i].Length; j++)
        {
          for (int k = 0; k < wsIndex[i][j].Length; k++)
          {
            int s1 = wsIndex[i][j][k];
            BoundarySurface ws1 = surfaces[s1];
            BoundarySurface ws1R = ws1.ReverseSideSurface;

            //ベクトルCを作成
            double rdsl;
            if (ws1.IsWall) rdsl = (radToSurf_L[i] + radToSurf_S[s1]) / ws1.FilmCoefficient;
            else rdsl = radToSurf_L[i] / ws1.FilmCoefficient;
            vecC[s1] = ws1.IF2 + ws1.FFS2 * rdsl;
            if (SolveMoistureTransferSimultaneously) vecC[s1 + nS] = ws1.IF3 + ws1.FFS3 * rdsl;
            if (!isSFboundary[s1])
            {
              rdsl = (radToSurf_L[zones[ws1R.ZoneIndex].RoomIndex] + radToSurf_S[ws1R.Index]) / ws1R.FilmCoefficient;
              vecC[s1] += ws1.BFS2 * rdsl;
              if (SolveMoistureTransferSimultaneously) vecC[s1 + nS] += ws1.BFS3 * rdsl;
            }
            else
            {
              vecC[s1] += ws1.BFS2 * ws1R.SolAirTemperature + ws1.BFL2 * ws1R.HumidityRatio;
              if (SolveMoistureTransferSimultaneously)
                vecC[s1 + nS] += ws1.BFS3 * ws1R.SolAirTemperature + ws1.BFL3 * ws1R.HumidityRatio;
            }
          }
        }
      }
    }

    /// <summary>Computes the IJ matrix for the sensible heat balance.</summary>
    private void MakeIJMatrix()
    {
      int nS = surfaces.Length;
      int nQ = zones.Length;
      matD.Initialize(0);
      matF.Initialize(0);
      for (int q1 = 0; q1 < ZoneCount; q1++)
      {
        Zone zq1 = zones[q1];
        double capSZN = (zq1.HeatCapacity + zq1.AirMass * PhysicsConstants.NominalMoistAirIsobaricSpecificHeat) / TimeStep;
        double capLZN = (zq1.MoistureCapacity + zq1.AirMass) / TimeStep;

        //行列Dを作成
        for (int q2 = 0; q2 < ZoneCount; q2++)
        {
          if (q1 == q2)
          {
            double bf = zq1.VentilationRate + zq1.SupplyAirFlowRate + zq1._supplyAirFlowRate2;
            for (int q3 = 0; q3 < ZoneCount; q3++)
              if (zoneVent[q1, q3] != 0) bf += zoneVent[q1, q3];
            matD[q1, q1] = bf * PhysicsConstants.NominalMoistAirIsobaricSpecificHeat + capSZN;
            if (SolveMoistureTransferSimultaneously) matD[q1 + nQ, q1 + nQ] = bf + capLZN;
            for (int k = 0; k < zq1.Surfaces.Count; k++)
            {
              BoundarySurface ws = zq1.Surfaces[k];
              matD[q1, q1] += ws.Area * ws.ConvectiveCoefficient;
              if (SolveMoistureTransferSimultaneously) matD[q1 + nQ, q1 + nQ] += ws.Area * ws.MoistureCoefficient;
            }
          }
          else
          {
            if (zoneVent[q1, q2] != 0)
            {
              matD[q1, q2] = -zoneVent[q1, q2] * PhysicsConstants.NominalMoistAirIsobaricSpecificHeat;
              if (SolveMoistureTransferSimultaneously) matD[q1 + nQ, q2 + nQ] = -zoneVent[q1, q2];
            }
          }
        }

        //ベクトルEを作成
        vecEJ[q1] = capSZN * zq1.Temperature + PhysicsConstants.NominalMoistAirIsobaricSpecificHeat
          * (zq1.VentilationRate * OutdoorTemperature + zq1.SupplyAirFlowRate * zq1.SupplyAirTemperature
          + zq1._supplyAirFlowRate2 * zq1._supplyAirTemperature2) + zq1.IntegrateConvectiveHeatgains();
        if (SolveMoistureTransferSimultaneously)
          vecEJ[q1 + nQ] = capLZN * zq1.HumidityRatio
            + zq1.VentilationRate * OutdoorHumidityRatio + zq1.SupplyAirFlowRate * zq1.SupplyAirHumidityRatio
            + zq1._supplyAirFlowRate2 * zq1._supplyAirHumidityRatio2 + zq1.IntegrateMoistureGains();

        //行列Fを作成
        for (int k = 0; k < zq1.Surfaces.Count; k++)
        {
          BoundarySurface ws = zq1.Surfaces[k];
          matF[q1, ws.Index] = ws.Area * ws.ConvectiveCoefficient;
          if (SolveMoistureTransferSimultaneously) matF[q1 + nQ, ws.Index + nS] = ws.Area * ws.MoistureCoefficient;
        }
      }

      //IJ行列作成処理
      LinearAlgebraOperations.Multiply(matF, matAInv, matBf);
      LinearAlgebraOperations.Multiply(matBf, matB, matI);
      LinearAlgebraOperations.Subtract(matD, matI);
      LinearAlgebraOperations.Multiply(matBf, vecC, vecEJ, 1, 1);
    }

    /// <summary>Sets the outdoor air conditions (temperature, humidity, and nocturnal radiation).</summary>
    /// <remarks>Must be called after window solar absorption has been calculated.</remarks>
    private void SetOutdoorAirState()
    {
      foreach (Window win in windows)
      {
        BoundarySurface ws = win.OutsideSurface;
        double fs = win.OutsideIncline.ConfigurationFactorToSky;
        ws.SolAirTemperature = OutdoorTemperature
          + radToSurf_S[win.InsideSurface.Index] * win.GetResistance()
          - ws.LongWaveEmissivity * fs * NocturnalRadiation / ws.FilmCoefficient;
      }

      foreach (BoundarySurface ws in bndSurfaces)
      {
        if (!ws.IsGroundWall)
        {
          if (ws.AdjacentSpaceFactor < 0)
          {
            double fs = ws.Incline!.ConfigurationFactorToSky;
            double rad;
            if (IsSolarIrradianceGiven) rad = ws.DirectSolarIrradiance + ws.DiffuseSolarIrradiance;
            else rad = ws.Incline.GetSolarIrradiance(Sun, Albedo);
            ws.SolAirTemperature = OutdoorTemperature
              + (ws.ShortWaveEmissivity * rad
              - ws.LongWaveEmissivity * fs * NocturnalRadiation) / ws.FilmCoefficient;
            ws.HumidityRatio = OutdoorHumidityRatio;
          }
          else
          {
            //隣室温度差係数を用いる場合
            double ftd = ws.AdjacentSpaceFactor;
            double tmp = zones[ws.ReverseSideSurface.ZoneIndex].Temperature;
            ws.SolAirTemperature = (1 - ftd) * tmp + ftd * OutdoorTemperature;
            ws.HumidityRatio = (1 - ftd) * tmp + ftd * OutdoorHumidityRatio;
          }
        }
      }
    }

    /// <summary>Performs preparatory calculations required before forecasting the heat balance.</summary>
    private void PrepareForHeatTransfer()
    {
      //温湿度を復旧
      if (forecastingHeatTransfer) ResetAirState();
      else
      {
        //温湿度を一時保存
        for (int i = 0; i < ZoneCount; i++)
        {
          zoneTemp[i] = zones[i].Temperature;
          zoneHumid[i] = zones[i].HumidityRatio;
        }

        //ゾーン・表面通し番号付与、形態係数行列初期化
        Initialize();

        //窓の光学特性を更新、ゲブハルト吸収率行列初期化
        UpdateWindowOpticalProperties();

        //長波長・短波長放射を分配
        for (int i = 0; i < radToSurf_S.Length; i++) radToSurf_S[i] = 0;
        for (int i = 0; i < RoomCount; i++) radToSurf_L[i] = 0;
        DistributeShortwaveRad();
        DistributeLongwaveRad();

        //屋外側相当温度を設定
        SetOutdoorAirState();

        //ABC行列を計算
        MakeABMatrix();
        MakeCVector();
        forecastingHeatTransfer = true;
      }
    }

    /// <summary>Reverts zone temperatures and humidity ratios from forecast values to current values.</summary>
    internal void ResetAirState()
    {
      if (forecastingHeatTransfer)
      {
        for (int i = 0; i < ZoneCount; i++)
        {
          zones[i].Temperature = zoneTemp[i];
          zones[i].HumidityRatio = zoneHumid[i];
        }
      }
    }

    /// <summary>Gets the breakdown of sensible heat flows into the zone (positive = inflow).</summary>
    /// <param name="zoneIndex">Zone index.</param>
    /// <param name="wallSurfaces">Heat flow from wall and window surfaces [W].</param>
    /// <param name="zoneAirChange">Inter-zone ventilation heat flow [W].</param>
    /// <param name="outdoorAir">Outdoor air heat flow [W].</param>
    /// <param name="supplyAir">Supply air heat flow [W].</param>
    /// <param name="heatGains">Internal heat gains [W].</param>
    /// <param name="heatSupply">HVAC heat supply [W].</param>
    public void GetBreakdownOfSensibleHeatFlow(
      int zoneIndex,
      out double wallSurfaces, out double zoneAirChange,
      out double outdoorAir, out double supplyAir,
      out double heatGains, out double heatSupply)
    {
      Zone zone = zones[zoneIndex];

      wallSurfaces = 0;
      foreach (BoundarySurface ws in zone.Surfaces)
        wallSurfaces += ws.Area * ws.ConvectiveCoefficient *
            (ws.SurfaceTemperature - zone.Temperature);

      zoneAirChange = 0;
      for (int q1 = 0; q1 < ZoneCount; q1++)
      {
        if (zoneVent[q1, zoneIndex] != 0)
          zoneAirChange += zoneVent[q1, zoneIndex] * PhysicsConstants.NominalMoistAirIsobaricSpecificHeat *
              (zones[q1].Temperature - zone.Temperature);
      }

      outdoorAir = zone.VentilationRate * PhysicsConstants.NominalMoistAirIsobaricSpecificHeat *
        (OutdoorTemperature - zone.Temperature);

      supplyAir = zone.SupplyAirFlowRate * PhysicsConstants.NominalMoistAirIsobaricSpecificHeat *
        (zone.SupplyAirTemperature - zone.Temperature);

      heatGains = zone.IntegrateConvectiveHeatgains();

      heatSupply = zone.HeatSupply;
    }

    /// <summary>Gets the convective heat flow from a wall surface [W].</summary>
    /// <param name="wallIndex">Wall index.</param>
    /// <param name="isSideF">True for the F side; false for the B side.</param>
    /// <returns>Convective heat flow from the wall surface [W].</returns>
    /// <remarks>Outdoor (boundary condition) surfaces are excluded from the calculation.</remarks>
    public double GetWallConvectiveHeatFlow(int wallIndex, bool isSideF)
    {
      IReadOnlyWall wall = walls[wallIndex];
      foreach (BoundarySurface ws in surfaces)
        if (ws.isSideF == isSideF && ws.Wall == wall)
          return ws.Area * ws.ConvectiveCoefficient
            * (ws.SurfaceTemperature - zones[ws.ZoneIndex].Temperature);
      return 0;
    }

    #endregion

    #region 水分平衡に関する処理

    /// <summary>Forecasts the future moisture balance state.</summary>
    /// <remarks>
    /// May be called multiple times iteratively.
    /// Call <see cref="FixMoistureTransfer"/> afterwards to commit the result.
    /// </remarks>
    internal void ForecastMoistureTransfer()
    {
      if (forecastingMoistureTransfer)
        for (int i = 0; i < ZoneCount; i++) zones[i].HumidityRatio = zoneHumid[i];
      else
      {
        for (int i = 0; i < ZoneCount; i++) zoneHumid[i] = zones[i].HumidityRatio;
        forecastingMoistureTransfer = true;
      }

      //熱平衡を計算
      for (int q1 = 0; q1 < ZoneCount; q1++)
      {
        Zone zq1 = zones[q1];
        double capLZN = (zq1.MoistureCapacity + zq1.AirMass) / TimeStep;

        //行列AWを作成
        for (int q2 = 0; q2 < ZoneCount; q2++)
        {
          if (q1 == q2)
          {
            matAW[q1, q1] = zq1.VentilationRate + zq1.SupplyAirFlowRate + zq1._supplyAirFlowRate2 + capLZN;
            for (int q3 = 0; q3 < ZoneCount; q3++) matAW[q1, q1] += zoneVent[q1, q3];
          }
          else matAW[q1, q2] = -zoneVent[q1, q2];
        }
        //ベクトルBWを作成
        vecWH[q1] = capLZN * zq1.HumidityRatio
          + zq1.VentilationRate * OutdoorHumidityRatio + zq1.SupplyAirFlowRate * zq1.SupplyAirHumidityRatio
          + zq1._supplyAirFlowRate2 * zq1._supplyAirHumidityRatio2 + zq1.IntegrateMoistureGains();
      }

      //潜熱平衡を計算
      matCW.Initialize(0);
      for (int i = 0; i < ZoneCount; i++)
      {
        //CW行列を作成
        for (int j = 0; j < ZoneCount; j++)
        {
          if (!zones[j].HumidityControlled) matCW[i, j] = matAW[i, j];
          else if (zones[j].HumidityControlled && (i == j)) matCW[i, j] = -1;
        }

        //DWベクトルを作成
        for (int j = 0; j < ZoneCount; j++)
          if (zones[j].HumidityControlled) vecWH[i] -= matAW[i, j] * zones[j].HumidityRatioSetpoint;
        if (!zones[i].HumidityControlled) vecWH[i] += zones[i].MoistureSupply;
      }

      //連立方程式を解く（湿度を計算）
      LinearAlgebraOperations.SolveLinearEquations(matCW, vecWH);
      for (int i = 0; i < ZoneCount; i++)
      {
        if (zones[i].HumidityControlled)
        {
          zones[i].MoistureSupply = vecWH[i];
          zones[i].HumidityRatio = zones[i].HumidityRatioSetpoint;
        }
        else zones[i].HumidityRatio = vecWH[i];
      }
    }

    /// <summary>Commits the forecasted moisture balance state.</summary>
    internal void FixMoistureTransfer()
    { forecastingMoistureTransfer = false; }

    /// <summary>Forecasts and commits the moisture balance state in a single call.</summary>
    internal void UpdateMoistureTransfer()
    {
      ForecastMoistureTransfer();
      FixMoistureTransfer();
    }

    /// <summary>Gets the breakdown of moisture flows into the zone (positive = inflow).</summary>
    /// <param name="zoneIndex">Zone index.</param>
    /// <param name="zoneAirChange">Inter-zone ventilation moisture flow [kg/s].</param>
    /// <param name="outdoorAir">Outdoor air moisture flow [kg/s].</param>
    /// <param name="supplyAir">Supply air moisture flow [kg/s].</param>
    /// <param name="moistureGains">Internal moisture gains [kg/s].</param>
    /// <param name="moistureSupply">HVAC moisture supply/removal [kg/s].</param>
    public void GetBreakdownOfLatentHeatFlow(
      int zoneIndex,
      out double zoneAirChange,
      out double outdoorAir, out double supplyAir,
      out double moistureGains, out double moistureSupply)
    {
      Zone zone = zones[zoneIndex];

      zoneAirChange = 0;
      for (int q1 = 0; q1 < ZoneCount; q1++)
      {
        if (zoneVent[q1, zoneIndex] != 0)
          zoneAirChange += zoneVent[q1, zoneIndex] * (zones[q1].HumidityRatio - zone.HumidityRatio);
      }

      outdoorAir = zone.VentilationRate * (OutdoorHumidityRatio - zone.HumidityRatio);

      supplyAir = zone.SupplyAirFlowRate * (zone.SupplyAirHumidityRatio - zone.HumidityRatio);

      moistureGains = zone.IntegrateMoistureGains();

      moistureSupply = zone.MoistureSupply;
    }

    #endregion

    #region 初期化処理

    /// <summary>Initializes internal data structures, form factors, and Gebhart matrices.</summary>
    private void Initialize()
    {
      if (needInitialize)
      {
        MakeSerialNumber(); //通し番号付与

        //形態係数の計算（自動推定）
        for (int i = 0; i < RoomCount; i++)
        {
          List<double> area = new List<double>();
          for (int j = 0; j < wsIndex[i].Length; j++)
            for (int k = 0; k < wsIndex[i][j].Length; k++)
              area.Add(surfaces[wsIndex[i][j][k]].Area);

          formFactor[i] = ComputeFormFactor(area.ToArray());
        }
        //ゲブハルトの吸収率行列更新
        ComputeGebhartMatrix();
        needInitialize = false;
      }
    }

    /// <summary>Assigns serial indices to all interior boundary surfaces.</summary>
    private void MakeSerialNumber()
    {
      List<BoundarySurface> sfs = new List<BoundarySurface>();
      int wsIndex = 0;
      for (int i = 0; i < RoomCount; i++)
      {
        this.wsIndex[i] = new int[rZones[i].Count][];
        for (int j = 0; j < this.wsIndex[i].Length; j++)
        {
          Zone zn = zones[rZones[i][j]];
          this.wsIndex[i][j] = new int[zn.Surfaces.Count];
          for (int k = 0; k < this.wsIndex[i][j].Length; k++)
          {
            sfs.Add(zn.Surfaces[k]);
            zn.Surfaces[k].Index = this.wsIndex[i][j][k] = wsIndex;
            wsIndex++;
          }
        }
      }
      surfaces = sfs.ToArray();
      int sNum = surfaces.Length;
      int zNum = ZoneCount;
      isSFboundary = new bool[sNum];
      for (int i = 0; i < sfs.Count; i++) isSFboundary[i] = !sfs.Contains(sfs[i].ReverseSideSurface);
      radToSurf_S = new double[sNum];
      gMatL = new double[sNum, sNum];
      gMatS = new double[sNum, sNum];
      if (SolveMoistureTransferSimultaneously)
      {
        sNum *= 2;
        zNum *= 2;
      }
      matA = new Matrix(sNum, sNum);
      matAInv = new Matrix(sNum, sNum);
      matB = new Matrix(sNum, zNum);
      vecC = new Vector(sNum);
      matF = new Matrix(zNum, sNum);
      vecTWS = new Vector(sNum);
      matBf = new Matrix(zNum, sNum);
    }

    /// <summary>Estimates the view factor matrix from surface areas.</summary>
    /// <param name="area">Array of surface areas [m²].</param>
    /// <returns>View factor matrix.</returns>
    private static IMatrix ComputeFormFactor(double[] area)
    {
      //0, 1, 2面の場合は面積関係なし
      if (area.Length == 0) return new Matrix(0, 0);

      if (area.Length == 1)
      {
        IMatrix mat = new Matrix(1, 1);
        mat[0, 0] = 1.0;
        return mat;
      }

      if (area.Length == 2)
      {
        IMatrix mat = new Matrix(2, 2);
        mat[0, 0] = mat[1, 1] = 0.0;
        mat[0, 1] = mat[1, 0] = 1.0;
        return mat;
      }

      //昇順に並べ替え
      int[] index = new int[area.Length];
      for (int i = 0; i < index.Length; i++) index[i] = i;
      Array.Sort(area, index);

      double[,] ff = new double[area.Length, area.Length];
      double sA = 0;
      int last = area.Length - 1;
      for (int i = 0; i < last; i++) sA += area[i];
      if (sA < area[last])
      {
        //最も大きい面積が他の合算よりも大きい場合
        ff[last, last] = 1.0;
        for (int i = 0; i < last; i++)
        {
          ff[i, last] = 1.0;
          ff[last, i] = area[i] / area[last];
          ff[last, last] -= ff[last, i];
        }
      }
      else
      {
        //3面の場合は半径情報は不要なので解析的に求められる
        if (area.Length == 3)
        {
          IMatrix mat = new Matrix(3, 3);
          mat[0, 0] = mat[1, 1] = mat[2, 2] = 0.0;
          mat[0, 1] = 0.5 * (area[0] + area[1] - area[2]) / area[1];
          mat[0, 2] = 0.5 * (area[0] + area[2] - area[1]) / area[2];
          mat[1, 0] = 0.5 * (area[1] + area[0] - area[2]) / area[0];
          mat[1, 2] = 0.5 * (area[1] + area[2] - area[0]) / area[2];
          mat[2, 0] = 0.5 * (area[2] + area[0] - area[1]) / area[0];
          mat[2, 1] = 0.5 * (area[2] + area[1] - area[0]) / area[1];
          return mat;
        }

        //仮想的な半径を収束計算
        double[] alpha = new double[area.Length];
        Roots.ErrorFunction eFnc = delegate (double r)
        {
          double sum = 0;
          for (int i = 0; i < area.Length; i++)
          {
            double bf = (area[i] / r);
            alpha[i] = Math.Acos(Math.Max(-1, 1 - 0.5 * bf * bf));
            sum += alpha[i];
          }
          return sum - 2 * Math.PI;
        };
        //面積和=円周と見立てて収束計算の初期値を決定
        double sumA = 0;
        for (int i = 0; i < area.Length; i++) sumA += area[i];
        double rad = Roots.NewtonBisection(eFnc, 0.5 * (sumA / Math.PI), 0.0001, 0.01, 0.01, 10);
        rad = Roots.Newton(eFnc, rad, 0.00001, 0.00001, 0.00001, 10);

        //形態係数を計算
        for (int i = 0; i < area.Length - 1; i++)
        {
          ff[i, i] = 0;
          for (int j = i + 1; j < area.Length; j++)
          {
            double a1 = alpha[i];
            double a2 = alpha[j];
            double a3 = 0;
            for (int k = i + 1; k < j; k++) a3 += alpha[k];
            double d1 = 2 * rad * Math.Sin(a3 / 2);
            double d2 = 2 * rad * Math.Sin(Math.PI - (a1 + a2 + a3) / 2);
            double d3 = 2 * rad * Math.Sin((a2 + a3) / 2);
            double d4 = 2 * rad * Math.Sin((a1 + a3) / 2);
            ff[i, j] = (d3 + d4 - d1 - d2) / (2 * area[i]);
            ff[j, i] = ff[i, j] * area[i] / area[j];
          }
        }
      }

      //順序を戻す
      IMatrix ff2 = new Matrix(area.Length, area.Length);
      for (int i = 0; i < area.Length; i++)
        for (int j = 0; j < area.Length; j++)
          ff2[index[i], index[j]] = ff[i, j];

      return ff2;
    }

    /// <summary>Computes the Gebhart absorption factor matrices for long-wave and short-wave radiation.</summary>
    private void ComputeGebhartMatrix()
    {
      for (int i = 0; i < RoomCount; i++)
      {
        //壁番号を保存
        List<int> wInd = new List<int>();
        for (int j = 0; j < rZones[i].Count; j++)
          for (int k = 0; k < wsIndex[i][j].Length; k++)
            wInd.Add(wsIndex[i][j][k]);

        int wsn = wInd.Count;
        IMatrix ffRhoL = new Matrix(wsn, wsn);
        IMatrix ffRhoS = new Matrix(wsn, wsn);
        IMatrix ffRhoInv = new Matrix(wsn, wsn);
        for (int j = 0; j < wsn; j++)
        {
          BoundarySurface ws = surfaces[wInd[j]];
          for (int k = 0; k < wsn; k++)
          {
            ffRhoL[j, k] = -(1 - ws.LongWaveEmissivity) * formFactor[i][j, k];
            ffRhoS[j, k] = -(1 - ws.ShortWaveEmissivity) * formFactor[i][j, k];
            if (j == k)
            {
              ffRhoL[j, k]++;
              ffRhoS[j, k]++;
            }
          }
        }
        LinearAlgebraOperations.GetInverse(ffRhoL, ffRhoInv);
        LinearAlgebraOperations.Multiply(formFactor[i], ffRhoInv, ffRhoL);
        LinearAlgebraOperations.GetInverse(ffRhoS, ffRhoInv);
        LinearAlgebraOperations.Multiply(formFactor[i], ffRhoInv, ffRhoS);
        for (int j = 0; j < wsn; j++)
        {
          BoundarySurface ws1 = surfaces[wInd[j]];
          for (int k = 0; k < wsn; k++)
          {
            BoundarySurface ws2 = surfaces[wInd[k]];
            gMatL[wInd[j], wInd[k]] = ffRhoL[j, k] * ws2.LongWaveEmissivity;
            gMatS[wInd[j], wInd[k]] = ffRhoS[j, k] * ws2.ShortWaveEmissivity;
          }
          //長波長は基準化して放射熱伝達率を計算
          double bf = 1 - gMatL[wInd[j], wInd[j]];
          ws1.RadiativeCoefficient = bf * ws1.LongWaveEmissivity * RAD_COEF;
          for (int k = 0; k < wsn; k++)
          {
            if (j != k) gMatL[wInd[j], wInd[k]] /= bf;
            else gMatL[wInd[j], wInd[k]] = 0;
          }
        }
      }
    }

    #endregion

    #region 放射分配処理

    /// <summary>Distributes long-wave radiation among interior surfaces.</summary>
    private void DistributeLongwaveRad()
    {
      for (int i = 0; i < RoomCount; i++)
      {
        radToSurf_L[i] = 0;
        double saSum = 0;
        for (int j = 0; j < rZones[i].Count; j++)
        {
          radToSurf_L[i] += zones[rZones[i][j]].IntegrateRadiativeHeatGains();
          for (int k = 0; k < wsIndex[i][j].Length; k++) saSum += surfaces[wsIndex[i][j][k]].Area;
        }
        radToSurf_L[i] /= saSum;
      }
    }

    /// <summary>Updates optical properties of all windows based on the current solar position.</summary>
    private void UpdateWindowOpticalProperties()
    {
      bool needUpdateGebhartMatrix = false;
      for (int i = 0; i < windows.Length; i++)
      {
        windows[i].UpdateOpticalProperties(Sun);
        if (wSWEmissivity[i] != windows[i].ShortWaveEmissivityB)
        {
          wSWEmissivity[i] = windows[i].ShortWaveEmissivityB;
          needUpdateGebhartMatrix = true;
        }
      }
      if (needUpdateGebhartMatrix) ComputeGebhartMatrix();
    }

    /// <summary>Distributes short-wave (solar) radiation among interior surfaces.</summary>
    private void DistributeShortwaveRad()
    {
      //窓面からの日射を各表面に分配
      for (int i = 0; i < RoomCount; i++)
      {
        for (int j = 0; j < wsIndex[i].Length; j++)
        {
          for (int k = 0; k < wsIndex[i][j].Length; k++)
          {
            BoundarySurface ws1 = surfaces[wsIndex[i][j][k]];
            if (!ws1.IsWall)
            {
              int indx1 = wsIndex[i][j][k];
              //窓からの透過・吸収日射を計算
              Window win = ws1.Window;
              double dir, dif;
              if (IsSolarIrradianceGiven)
              {
                dir = win.OutsideSurface.DirectSolarIrradiance;
                dif = win.OutsideSurface.DiffuseSolarIrradiance;
              }
              else
              {
                IReadOnlyIncline inc = win.OutsideIncline;
                dir = inc.GetDirectSolarIrradiance(Sun) * (1 - win.SunShade.GetShadowRatio(Sun));
                dif = inc.GetDiffuseSolarIrradiance(Sun, Albedo);
              }
              radToSurf_S[indx1] +=
                dir * win.DirectSolarIncidentAbsorptance + dif * win.DiffuseSolarIncidentAbsorptance;
              //床に優先配分される短波長を計算
              BoundarySurface? flr = null;
              double dir2 = dir * win.DirectSolarIncidentTransmittance * win.Area;
              double radFromFloor = 0.0;
              if (swDistFloor.ContainsKey(win))
              {
                flr = swDistFloor[win];
                double flRate = swDistRate[win];
                radToSurf_S[flr.Index] += dir2 * flRate * flr.ShortWaveEmissivity / flr.Area;
                radFromFloor = dir2 * flRate * (1.0 - flr.ShortWaveEmissivity);
                dir2 *= (1 - flRate);
              }
              double rad = dir2 + dif * win.DiffuseSolarIncidentTransmittance * win.Area;

              //同じ室に属する壁表面に分配
              if (0 < rad)
              {
                for (int j2 = 0; j2 < wsIndex[i].Length; j2++)
                {
                  for (int k2 = 0; k2 < wsIndex[i][j2].Length; k2++)
                  {
                    int indx2 = wsIndex[i][j2][k2];
                    BoundarySurface wsf2 = surfaces[indx2];
                    double ibsw = gMatS[indx1, indx2] * rad;
                    //床面からの放射を加算
                    if (flr != null) ibsw += gMatS[flr.Index, indx2] * radFromFloor;
                    ibsw /= wsf2.Area;
                    if (!wsf2.IsWall)
                      ibsw *= wsf2.Window.DiffuseSolarIncidentAbsorptance
                        / (1 - wsf2.Window.DiffuseSolarIncidentReflectance);
                    radToSurf_S[indx2] += ibsw;
                  }
                }
              }
            }
          }
        }
      }
    }

    #endregion

    #region 境界条件設定処理

    /// <summary>Updates outdoor air conditions from the current weather state.</summary>
    /// <param name="dTime">Current date and time.</param>
    /// <param name="sun">Solar state.</param>
    /// <param name="temperature">Outdoor dry-bulb temperature [°C].</param>
    /// <param name="humidityRatio">Outdoor humidity ratio [kg/kg].</param>
    /// <param name="nocRadiation">Nocturnal radiation [W/m²].</param>
    internal void UpdateOutdoorCondition
      (DateTime dTime, IReadOnlySun sun, double temperature, double humidityRatio, double nocRadiation)
    {
      CurrentDateTime = dTime;
      Sun = sun;
      OutdoorTemperature = temperature;
      OutdoorHumidityRatio = humidityRatio;
      NocturnalRadiation = nocRadiation;
    }

    /// <summary>Sets the ground temperature [°C] for ground-contact walls.</summary>
    /// <param name="wallIndex">Wall index.</param>
    /// <param name="isSideF">True for the F side; false for the B side.</param>
    /// <param name="groundTemperature">Ground temperature [°C].</param>
    public void SetGroundTemperature(int wallIndex, bool isSideF, double groundTemperature)
    {
      BoundarySurface ws;
      if (isSideF) ws = walls[wallIndex].SurfaceF;
      else ws = walls[wallIndex].SurfaceB;
      if (bndSurfaces.Contains(ws)) ws.SolAirTemperature = groundTemperature;
    }

    /// <summary>Sets the ground temperature [°C] for ground-contact walls.</summary>
    /// <param name="groundTemperature">Ground temperature [°C].</param>
    public void SetGroundTemperature(double groundTemperature)
    {
      foreach (BoundarySurface ws in bndSurfaces)
        if (ws.IsGroundWall) ws.SolAirTemperature = groundTemperature;
    }

    /// <summary>Sets a <b>symmetric</b> air exchange rate between two zones [kg/s].</summary>
    /// <param name="zoneIndex1">One zone.</param>
    /// <param name="zoneIndex2">The other zone.</param>
    /// <param name="airMassFlowRate">Air mass flow rate in each direction [kg/s].</param>
    /// <remarks>
    /// Cross-ventilation models a balanced two-way exchange (e.g., air moving
    /// freely back and forth through an open doorway). The same rate is
    /// recorded for both (1→2) and (2→1), which is convenient because the
    /// two zones trivially conserve mass without further bookkeeping. For an
    /// explicitly directed one-way transfer, use
    /// <see cref="SetAirFlow(int,int,double)"/> instead.
    /// </remarks>
    public void SetCrossVentilation(int zoneIndex1, int zoneIndex2, double airMassFlowRate)
    { zoneVent[zoneIndex1, zoneIndex2] = zoneVent[zoneIndex2, zoneIndex1] = airMassFlowRate; }

    /// <summary>Sets the inter-zone air flow rate [kg/s].</summary>
    /// <param name="zone1">Source zone.</param>
    /// <param name="zone2">Destination zone.</param>
    /// <param name="airMassFlowRate">Inter-zone air mass flow rate [kg/s].</param>
    public void SetCrossVentilation(IReadOnlyZone zone1, IReadOnlyZone zone2, double airMassFlowRate)
    { SetCrossVentilation(Array.IndexOf(zones, zone1), Array.IndexOf(zones, zone2), airMassFlowRate); }

    /// <summary>Sets a <b>directional</b> air flow rate from zone 1 to zone 2 [kg/s].</summary>
    /// <param name="zoneIndex1">Source zone.</param>
    /// <param name="zoneIndex2">Destination zone.</param>
    /// <param name="airMassFlowRate">Air mass flow rate [kg/s].</param>
    /// <remarks>
    /// Unlike <see cref="SetCrossVentilation(int,int,double)"/>, this records
    /// a single directed flow and does <b>not</b> populate the reverse
    /// direction. The caller is responsible for maintaining an overall air
    /// mass balance across all zones (e.g., pairing a forward flow with an
    /// outdoor-air intake or with flows to other zones).
    /// </remarks>
    public void SetAirFlow(int zoneIndex1, int zoneIndex2, double airMassFlowRate)
    { zoneVent[zoneIndex1, zoneIndex2] = airMassFlowRate; }

    /// <summary>Sets the air flow rate from zone 1 to zone 2 [kg/s].</summary>
    /// <param name="zone1">Source zone.</param>
    /// <param name="zone2">Destination zone.</param>
    /// <param name="airMassFlowRate">Air mass flow rate [kg/s].</param>
    /// <remarks>The overall air flow balance across all zones must be maintained by the caller.</remarks>
    public void SetAirFlow(IReadOnlyZone zone1, IReadOnlyZone zone2, double airMassFlowRate)
    { SetAirFlow(Array.IndexOf(zones, zone1), Array.IndexOf(zones, zone2), airMassFlowRate); }

    /// <summary>Gets the air flow rate from zone 1 to zone 2 [kg/s].</summary>
    /// <param name="zoneIndex1">Source zone index.</param>
    /// <param name="zoneIndex2">Destination zone index.</param>
    /// <returns>Air flow rate [kg/s].</returns>
    public double GetAirFlow(int zoneIndex1, int zoneIndex2)
    { return zoneVent[zoneIndex1, zoneIndex2]; }

    /// <summary>Gets the air flow rate from zone 1 to zone 2 [kg/s].</summary>
    /// <param name="zone1">Source zone.</param>
    /// <param name="zone2">Destination zone.</param>
    /// <returns>Air flow rate [kg/s].</returns>
    public double GetAirFlow(IReadOnlyZone zone1, IReadOnlyZone zone2)
    { return GetAirFlow(Array.IndexOf(zones, zone1), Array.IndexOf(zones, zone2)); }

    /// <summary>Sets the ventilation air flow rate for the specified zone [kg/s].</summary>
    /// <param name="zoneIndex">Zone index.</param>
    /// <param name="ventilationRate">Ventilation air flow rate [kg/s].</param>
    public void SetVentilationRate(int zoneIndex, double ventilationRate)
    { zones[zoneIndex].VentilationRate = ventilationRate; }

    /// <summary>Sets the ventilation air flow rate for the specified zone [kg/s].</summary>
    /// <param name="zone">Zone.</param>
    /// <param name="ventilationRate">Ventilation air flow rate [kg/s].</param>
    public void SetVentilationRate(IReadOnlyZone zone, double ventilationRate)
    { SetVentilationRate(Array.IndexOf(zones, zone), ventilationRate); }

    /// <summary>Specifies which floor receives a preferential share of direct solar radiation entering through a window.</summary>
    /// <param name="windowIndex">Window index.</param>
    /// <param name="wallIndex">Wall (floor) index.</param>
    /// <param name="isSideF">True for the F side; false for the B side of the floor.</param>
    /// <param name="distRate">Fraction of direct short-wave radiation that lands on this floor [0, 1]. The remainder is distributed to other interior surfaces by area/absorptance.</param>
    /// <remarks>
    /// By default, direct solar radiation transmitted through a window is
    /// distributed to all interior surfaces in proportion to their
    /// area-absorptance product, which is a reasonable isotropic assumption
    /// when the solar patch on the floor is not tracked explicitly. This
    /// method lets the caller override that assumption for a specific window
    /// to send a defined fraction directly to a chosen floor — useful when
    /// measured or ray-traced data are available.
    /// </remarks>
    public void SetSWDistributionRateToFloor(int windowIndex, int wallIndex, bool isSideF, double distRate)
    {
      BoundarySurface ws;
      if (isSideF) ws = walls[wallIndex].SurfaceF;
      else ws = walls[wallIndex].SurfaceB;
      swDistFloor[windows[windowIndex]] = ws;
      swDistRate[windows[windowIndex]] = distRate;
    }

    /// <summary>Sets the floor surface that preferentially receives direct short-wave radiation from the specified window.</summary>
    /// <param name="window">Window.</param>
    /// <param name="wall">Wall (floor).</param>
    /// <param name="isSideF">True for the F side; false for the B side.</param>
    /// <param name="distRate">Direct short-wave distribution ratio to the floor [-].</param>
    public void SetSWDistributionRateToFloor(IReadOnlyWindow window, IReadOnlyWall wall, bool isSideF, double distRate)
    { SetSWDistributionRateToFloor(Array.IndexOf(windows, window), Array.IndexOf(walls, wall), isSideF, distRate); }

    /// <summary>Sets the water supply conditions for a buried pipe in the specified wall.</summary>
    /// <param name="wallIndex">Wall (floor) index that contains the pipe.</param>
    /// <param name="mIndex">Node index inside the wall's layer stack where the pipe is embedded.</param>
    /// <param name="flowRate">Water mass flow rate [kg/s]; set to 0 to turn the pipe off.</param>
    /// <param name="temperature">Inlet water temperature [°C].</param>
    /// <remarks>
    /// Updates the pipe's ε-NTU effectiveness via
    /// <see cref="IReadOnlyBuriedPipe"/> and invalidates the wall's cached
    /// response-factor matrices so they are recomputed on the next solver
    /// step. The pipe must have been added to the wall beforehand (see the
    /// buried-pipe API on <see cref="Wall"/>); a node without a pipe is
    /// silently ignored.
    /// </remarks>
    public void SetBuriedPipeWaterState(int wallIndex, int mIndex, double flowRate, double temperature)
    { walls[wallIndex].SetInletWater(mIndex, flowRate, temperature); }

    /// <summary>Sets the base heat gain values for the specified zone.</summary>
    /// <param name="zone">Zone.</param>
    /// <param name="convectiveHeatGain">Convective sensible heat gain [W].</param>
    /// <param name="radiativeHeatGain">Radiative sensible heat gain [W].</param>
    /// <param name="moistureGain">Moisture generation rate [kg/s].</param>
    public void SetBaseHeatGain(IReadOnlyZone zone, double convectiveHeatGain, double radiativeHeatGain, double moistureGain)
    { SetBaseHeatGain(Array.IndexOf(zones, zone), convectiveHeatGain, radiativeHeatGain, moistureGain); }

    /// <summary>Sets the base heat gain values for the specified zone.</summary>
    /// <param name="zoneIndex">Zone index.</param>
    /// <param name="convectiveHeatGain">Convective sensible heat gain [W].</param>
    /// <param name="radiativeHeatGain">Radiative sensible heat gain [W].</param>
    /// <param name="moistureGain">Moisture generation rate [kg/s].</param>
    /// <remarks>
    /// Writes the three components into the zone's built-in
    /// <see cref="Zone.BaseHeatGain"/> slot — a convenience that avoids
    /// creating a new <see cref="IHeatGain"/> instance for steady or
    /// slowly-varying base loads. For additional heat gain elements
    /// (occupants, equipment schedules, etc.), use
    /// <see cref="AddHeatGain(int,IHeatGain)"/>; the base slot and the added
    /// elements sum into the zone balance.
    /// </remarks>
    public void SetBaseHeatGain(int zoneIndex, double convectiveHeatGain, double radiativeHeatGain, double moistureGain)
    { zones[zoneIndex].SetBaseHeatGain(convectiveHeatGain, radiativeHeatGain, moistureGain); }

    /// <summary>Supplies externally computed solar irradiance on a wall surface.</summary>
    /// <param name="wallIndex">Wall index.</param>
    /// <param name="directIrradiance">Direct solar irradiance on the surface [W/m²].</param>
    /// <param name="diffuseIrradiance">Diffuse solar irradiance on the surface [W/m²].</param>
    /// <remarks>
    /// Effective only when <see cref="IsSolarIrradianceGiven"/> is true.
    /// In that mode, the solver skips its internal solar computation and uses
    /// the values supplied here instead — useful when irradiance has been
    /// precomputed by an external ray-tracer or measured on site. When
    /// <see cref="IsSolarIrradianceGiven"/> is false (the default), values
    /// set here are ignored; irradiance is derived from the current sun
    /// state, the wall's outdoor <see cref="IReadOnlyIncline"/>, and the
    /// ground <see cref="Albedo"/>.
    /// </remarks>
    public void SetWallIrradiance(int wallIndex, double directIrradiance, double diffuseIrradiance)
    {
      BoundarySurface wsF = walls[wallIndex].SurfaceF;
      BoundarySurface wsB = walls[wallIndex].SurfaceB;
      wsF.DirectSolarIrradiance = wsB.DirectSolarIrradiance = directIrradiance;
      wsF.DiffuseSolarIrradiance = wsB.DiffuseSolarIrradiance = diffuseIrradiance;
    }

    /// <summary>Supplies externally computed solar irradiance on a window surface.</summary>
    /// <param name="windowIndex">Window index.</param>
    /// <param name="directIrradiance">Direct solar irradiance on the outdoor surface [W/m²].</param>
    /// <param name="diffuseIrradiance">Diffuse solar irradiance on the outdoor surface [W/m²].</param>
    /// <remarks>
    /// Effective only when <see cref="IsSolarIrradianceGiven"/> is true;
    /// see <see cref="SetWallIrradiance(int,double,double)"/> for the full
    /// contract. The irradiance supplied here is the value on the window's
    /// outdoor face — the glazing solver then resolves transmittance,
    /// reflectance, and absorptance through each layer and any attached
    /// shading devices.
    /// </remarks>
    public void SetWindowIrradiance(int windowIndex, double directIrradiance, double diffuseIrradiance)
    {
      BoundarySurface ws = windows[windowIndex].OutsideSurface;
      ws.DirectSolarIrradiance = directIrradiance;
      ws.DiffuseSolarIrradiance = diffuseIrradiance;
    }

    #endregion

    #region モデル作成処理

    /// <summary>Initializes wall and zone temperatures and humidity ratios to the specified values.</summary>
    /// <param name="temperature">Dry-bulb temperature [°C].</param>
    /// <param name="humidityRatio">Humidity ratio [kg/kg].</param>
    /// <remarks>
    /// Call this after the model has been fully configured and before the first
    /// solver step. It resets every zone's air state and every wall's
    /// temperature/humidity distribution to a uniform value, which provides a
    /// well-defined starting point for the response-factor solver. May also be
    /// called during a run to "re-seed" the state (e.g., at the start of a new
    /// simulation scenario).
    /// </remarks>
    public void InitializeAirState(double temperature, double humidityRatio)
    {
      foreach (Zone zn in zones) zn.InitializeAirState(temperature, humidityRatio);
      foreach (Wall wl in walls) wl.Initialize(temperature);
    }

    /// <summary>Assigns a zone to the specified room.</summary>
    /// <param name="roomIndex">Room index.</param>
    /// <param name="zoneIndex">Zone index.</param>
    /// <remarks>
    /// Each zone belongs to exactly one room. If the zone is currently assigned
    /// to another room, it is removed from that room first. The assignment
    /// marks the model for re-initialization on the next solver step (view
    /// factors and radiation-exchange matrices are rebuilt per room).
    /// </remarks>
    public void AddZone(int roomIndex, int zoneIndex)
    {
      needInitialize = true;
      for (int i = 0; i < RoomCount; i++) rZones[i].Remove(zoneIndex);
      rZones[roomIndex].Add(zoneIndex);
      zones[zoneIndex].RoomIndex = roomIndex;
    }

    /// <summary>Assigns a zone to the specified room.</summary>
    /// <param name="roomIndex">Room index.</param>
    /// <param name="zone">Zone.</param>
    public void AddZone(int roomIndex, IReadOnlyZone zone)
    { AddZone(roomIndex, Array.IndexOf(zones, zone)); }

    /// <summary>Attaches one side of a wall to the specified zone.</summary>
    /// <param name="zoneIndex">Zone index that the wall side faces.</param>
    /// <param name="wallIndex">Wall index.</param>
    /// <param name="isSideF">True for the F-side surface; false for the B side.</param>
    /// <remarks>
    /// Each wall side can be attached to at most one zone or boundary at a
    /// time. Calling this removes the side from any other zone or boundary
    /// list it was previously attached to — placement is exclusive. Typical
    /// configurations:
    /// <list type="bullet">
    ///   <item><description>Interior wall between two zones: call <see cref="AddWall(int,int,int)"/> (the 3-index overload), which attaches each side to a different zone.</description></item>
    ///   <item><description>Exterior wall: attach the indoor side with this method, then mark the outdoor side with <see cref="SetOutsideWall(int,bool,IReadOnlyIncline)"/>.</description></item>
    ///   <item><description>Partition entirely inside one zone: use <see cref="AddLoopWall(int,int)"/>.</description></item>
    /// </list>
    /// </remarks>
    public void AddWall(int zoneIndex, int wallIndex, bool isSideF)
    {
      needInitialize = true;
      BoundarySurface sf;
      if (isSideF) sf = walls[wallIndex].SurfaceF;
      else sf = walls[wallIndex].SurfaceB;

      bndSurfaces.Remove(sf);
      for (int i = 0; i < ZoneCount; i++) zones[i].Surfaces.Remove(sf);
      zones[zoneIndex].Surfaces.Add(sf);
      sf.ZoneIndex = zoneIndex;
    }

    /// <summary>Adds a wall to the specified zone.</summary>
    /// <param name="zone">Zone.</param>
    /// <param name="wall">Wall.</param>
    /// <param name="isSideF">True for the F-side wall surface; false for the B side.</param>
    public void AddWall(IReadOnlyZone zone, IReadOnlyWall wall, bool isSideF)
    { AddWall(Array.IndexOf(zones, zone), Array.IndexOf(walls, wall), isSideF); }

    /// <summary>Installs an interior wall between two zones.</summary>
    /// <param name="zoneFIndex">Zone on the F side of the wall.</param>
    /// <param name="zoneBIndex">Zone on the B side of the wall.</param>
    /// <param name="wallIndex">Wall index.</param>
    /// <remarks>
    /// Equivalent to calling <see cref="AddWall(int,int,bool)"/> twice —
    /// once for each side — so heat conduction through the wall couples
    /// the two zones. Use when both sides of the wall face conditioned
    /// zones in this multi-room; for walls facing a non-simulated space,
    /// see <see cref="UseAdjacentSpaceFactor(int,bool,double)"/> instead.
    /// </remarks>
    public void AddWall(int zoneFIndex, int zoneBIndex, int wallIndex)
    {
      AddWall(zoneFIndex, wallIndex, true);
      AddWall(zoneBIndex, wallIndex, false);
    }

    /// <summary>Adds a wall to the specified zone.</summary>
    /// <param name="zoneF">F-side zone.</param>
    /// <param name="zoneB">B-side zone.</param>
    /// <param name="wall">Wall.</param>
    public void AddWall(IReadOnlyZone zoneF, IReadOnlyZone zoneB, IReadOnlyWall wall)
    { AddWall(Array.IndexOf(zones, zoneF), Array.IndexOf(zones, zoneB), Array.IndexOf(walls, wall)); }

    /// <summary>Installs a wall whose F and B surfaces both face the same zone.</summary>
    /// <param name="zoneIndex">Zone index that both sides of the wall face.</param>
    /// <param name="wallIndex">Wall index.</param>
    /// <remarks>
    /// A loop wall contributes thermal mass to a single zone — for example,
    /// an interior partition, bookcase, or furniture mass that stores heat
    /// but does not separate two zones. Both surfaces exchange heat with
    /// the same zone air, so the solver treats the wall as a closed thermal
    /// capacitor that neither gains nor loses net heat from/to another space.
    /// </remarks>
    public void AddLoopWall(int zoneIndex, int wallIndex)
    {
      AddWall(zoneIndex, wallIndex, true);
      AddWall(zoneIndex, wallIndex, false);
    }

    /// <summary>Adds a circulating wall (both sides facing the same zone) with an adjacency factor.</summary>
    /// <param name="zone">Zone.</param>
    /// <param name="wall">Wall.</param>
    public void AddLoopWall(IReadOnlyZone zone, IReadOnlyWall wall)
    { AddLoopWall(Array.IndexOf(zones, zone), Array.IndexOf(walls, wall)); }

    /// <summary>Marks one side of a wall as outdoor-facing.</summary>
    /// <param name="wallIndex">Wall index.</param>
    /// <param name="isSideF">True for the F side; false for the B side.</param>
    /// <param name="incline">Outdoor-facing tilted surface orientation (drives solar incidence and sky/ground view factors).</param>
    /// <remarks>
    /// The three outdoor-boundary kinds are mutually exclusive per wall
    /// side: this method, <see cref="SetGroundWall(int,bool,double)"/>, and
    /// <see cref="UseAdjacentSpaceFactor(int,bool,double)"/>. Calling one
    /// replaces the previous boundary assignment for that side and removes
    /// it from any zone it was attached to.
    /// </remarks>
    public void SetOutsideWall(int wallIndex, bool isSideF, IReadOnlyIncline incline)
    {
      needInitialize = true;
      BoundarySurface ws;
      if (isSideF) ws = walls[wallIndex].SurfaceF;
      else ws = walls[wallIndex].SurfaceB;

      ws.AdjacentSpaceFactor = -1.0;
      ws.Incline = incline;
      ws.ZoneIndex = -1;
      ws.IsGroundWall = false;
      for (int i = 0; i < ZoneCount; i++) zones[i].Surfaces.Remove(ws);
      bndSurfaces.Add(ws);
    }

    /// <summary>Adds an outdoor-facing wall surface as a boundary condition.</summary>
    /// <param name="wall">Wall.</param>
    /// <param name="isSideF">True for the F side; false for the B side.</param>
    /// <param name="incline">Outdoor-facing tilted surface.</param>
    public void SetOutsideWall(IReadOnlyWall wall, bool isSideF, IReadOnlyIncline incline)
    { SetOutsideWall(Array.IndexOf(walls, wall), isSideF, incline); }

    /// <summary>Marks one side of a wall as soil-contacting (ground boundary).</summary>
    /// <param name="wallIndex">Wall index.</param>
    /// <param name="isSideF">True for the F side; false for the B side.</param>
    /// <param name="groundWallConductance">Soil-to-wall thermal conductance [W/(m²·K)].</param>
    /// <remarks>
    /// The driving temperature on this side is the ground temperature
    /// supplied by the enclosing thermal model (e.g., via
    /// <see cref="SetGroundTemperature(int,bool,double)"/> or a
    /// <see cref="Climate.Ground"/> estimate); the solar and sky radiation
    /// terms that apply to outdoor walls are skipped. Mutually exclusive
    /// with <see cref="SetOutsideWall(int,bool,IReadOnlyIncline)"/> and
    /// <see cref="UseAdjacentSpaceFactor(int,bool,double)"/> on the same
    /// wall side. The surface film resistance is typically much higher
    /// than a free-air surface, so consider bumping the outermost layer's
    /// thermal conductance when modeling a below-grade wall.
    /// </remarks>
    public void SetGroundWall(int wallIndex, bool isSideF, double groundWallConductance)
    {
      needInitialize = true;
      BoundarySurface ws;
      if (isSideF) ws = walls[wallIndex].SurfaceF;
      else ws = walls[wallIndex].SurfaceB;

      ws.ZoneIndex = -1;
      ws.IsGroundWall = true;
      ws.ConvectiveCoefficient = groundWallConductance;
      ws.RadiativeCoefficient = 0;
      for (int i = 0; i < ZoneCount; i++) zones[i].Surfaces.Remove(ws);
      bndSurfaces.Add(ws);
    }

    /// <summary>Adds a ground-contact wall with a specified ground temperature.</summary>
    /// <param name="wall">Wall.</param>
    /// <param name="isSideF">True for the F side; false for the B side.</param>
    /// <param name="groundWallConductance">Thermal conductance between soil and wall surface [W/(m²·K)].</param>
    /// <remarks>Increase the thermal conductance of the surface layer if using it as a boundary condition.</remarks>
    public void SetGroundWall(IReadOnlyWall wall, bool isSideF, double groundWallConductance)
    { SetGroundWall(Array.IndexOf(walls, wall), isSideF, groundWallConductance); }

    /// <summary>Marks one side of a wall as facing an adjacent, non-simulated space.</summary>
    /// <param name="wallIndex">Wall index.</param>
    /// <param name="isSideF">True for the F side; false for the B side.</param>
    /// <param name="adjacentSpaceFactor">Temperature-difference factor [-] (0 = behaves like indoor, 1 = behaves like outdoor, intermediate blends the two).</param>
    /// <remarks>
    /// Use this to model walls that face an un-modeled neighboring space
    /// (a tenant unit, an unconditioned shaft, an attic treated as a black
    /// box, etc.) without instantiating a second <see cref="MultiRoom"/>.
    /// The effective boundary temperature on this side is interpolated
    /// between outdoor and indoor temperatures using
    /// <paramref name="adjacentSpaceFactor"/>. Mutually exclusive with
    /// <see cref="SetOutsideWall(int,bool,IReadOnlyIncline)"/> and
    /// <see cref="SetGroundWall(int,bool,double)"/> on the same wall side.
    /// </remarks>
    public void UseAdjacentSpaceFactor(int wallIndex, bool isSideF, double adjacentSpaceFactor)
    {
      needInitialize = true;
      BoundarySurface ws;
      if (isSideF) ws = walls[wallIndex].SurfaceF;
      else ws = walls[wallIndex].SurfaceB;

      ws.AdjacentSpaceFactor = adjacentSpaceFactor;
      ws.ZoneIndex = -1;
      ws.IsGroundWall = false;
      for (int i = 0; i < ZoneCount; i++) zones[i].Surfaces.Remove(ws);
      bndSurfaces.Add(ws);
    }

    /// <summary>Sets the adjacent space temperature difference factor [-] for the specified wall surface.</summary>
    /// <param name="wall">Wall.</param>
    /// <param name="isSideF">True for the F side; false for the B side.</param>
    /// <param name="adjacentSpaceFactor">Adjacent space temperature difference factor [-].</param>
    public void UseAdjacentSpaceFactor(IReadOnlyWall wall, bool isSideF, double adjacentSpaceFactor)
    { UseAdjacentSpaceFactor(Array.IndexOf(walls, wall), isSideF, adjacentSpaceFactor); }

    /// <summary>Gets references to all outside (outdoor-facing) wall surfaces.</summary>
    /// <returns>Array of outside-wall references (wall ID, side flag, outdoor incline).</returns>
    /// <remarks>
    /// Outside walls are those added via <see cref="SetOutsideWall(int,bool,IReadOnlyIncline)"/>.
    /// Primarily used by serialization and debug tooling.
    /// </remarks>
    public OutsideWallReference[] GetOutsideWallReferences()
    {
      List<OutsideWallReference> result = new List<OutsideWallReference>();
      foreach (BoundarySurface ws in bndSurfaces)
      {
        if (!ws.IsWall) continue;
        if (ws.IsGroundWall) continue;
        if (ws.AdjacentSpaceFactor >= 0) continue;
        if (ws.Incline is null) continue; // 予防的チェック
        result.Add(new OutsideWallReference(ws.Wall.ID, ws.isSideF, ws.Incline));
      }
      return result.ToArray();
    }

    /// <summary>Gets references to all ground-contact wall surfaces.</summary>
    /// <returns>Array of ground-wall references (wall ID, side flag, conductance).</returns>
    /// <remarks>
    /// Ground walls are those added via <see cref="SetGroundWall(int,bool,double)"/>.
    /// The returned <see cref="GroundWallReference.Conductance"/> is the
    /// soil-to-wall conductance originally passed to <c>SetGroundWall</c>.
    /// </remarks>
    public GroundWallReference[] GetGroundWallReferences()
    {
      List<GroundWallReference> result = new List<GroundWallReference>();
      foreach (BoundarySurface ws in bndSurfaces)
      {
        if (!ws.IsWall) continue;
        if (!ws.IsGroundWall) continue;
        // SetGroundWall は ConvectiveCoefficient に conductance を入れる
        result.Add(new GroundWallReference(ws.Wall.ID, ws.isSideF, ws.ConvectiveCoefficient));
      }
      return result.ToArray();
    }

    /// <summary>Gets references to all adjacent-space wall surfaces.</summary>
    /// <returns>Array of adjacent-space wall references (wall ID, side flag, temperature-difference factor).</returns>
    /// <remarks>
    /// Adjacent-space walls are those added via <see cref="UseAdjacentSpaceFactor(int,bool,double)"/>.
    /// They are neither outside walls nor ground walls.
    /// </remarks>
    public AdjacentSpaceWallReference[] GetAdjacentSpaceWallReferences()
    {
      List<AdjacentSpaceWallReference> result = new List<AdjacentSpaceWallReference>();
      foreach (BoundarySurface ws in bndSurfaces)
      {
        if (!ws.IsWall) continue;
        if (ws.IsGroundWall) continue;
        if (ws.AdjacentSpaceFactor < 0) continue;
        result.Add(new AdjacentSpaceWallReference(ws.Wall.ID, ws.isSideF, ws.AdjacentSpaceFactor));
      }
      return result.ToArray();
    }

    /// <summary>Adds a window to the specified zone.</summary>
    /// <param name="zoneIndex">Zone index.</param>
    /// <param name="windowIndex">Window index.</param>
    public void AddWindow(int zoneIndex, int windowIndex)
    {
      needInitialize = true;
      Window win = windows[windowIndex];
      for (int i = 0; i < ZoneCount; i++) zones[i].Surfaces.Remove(win.InsideSurface);
      zones[zoneIndex].Surfaces.Add(win.InsideSurface);
      win.InsideSurface.ZoneIndex = zoneIndex;
    }

    /// <summary>Adds a window to the specified zone.</summary>
    /// <param name="zone">Zone.</param>
    /// <param name="window">Window.</param>
    public void AddWindow(IReadOnlyZone zone, IReadOnlyWindow window)
    { AddWindow(Array.IndexOf(zones, zone), Array.IndexOf(windows, window)); }

    /// <summary>Adds a heat gain element to the specified zone.</summary>
    /// <param name="zoneIndex">Zone index.</param>
    /// <param name="heatGain">Heat gain element.</param>
    public void AddHeatGain(int zoneIndex, IHeatGain heatGain)
    { zones[zoneIndex].AddHeatGain(heatGain); }

    /// <summary>Adds a heat gain element to the specified zone.</summary>
    /// <param name="zone">Zone.</param>
    /// <param name="heatGain">Heat gain element.</param>
    public void AddHeatGain(IReadOnlyZone zone, IHeatGain heatGain)
    { AddHeatGain(Array.IndexOf(zones, zone), heatGain); }

    /// <summary>Removes a heat gain element from the specified zone.</summary>
    /// <param name="zoneIndex">Zone index.</param>
    /// <param name="heatGain">Heat gain element.</param>
    public void RemoveHeatGain(int zoneIndex, IHeatGain heatGain)
    { zones[zoneIndex].RemoveHeatGain(heatGain); }

    /// <summary>Removes a heat gain element from the specified zone.</summary>
    /// <param name="zone">Zone.</param>
    /// <param name="heatGain">Heat gain element.</param>
    public void RemoveHeatGain(IReadOnlyZone zone, IHeatGain heatGain)
    { RemoveHeatGain(Array.IndexOf(zones, zone), heatGain); }

    /// <summary>Sets the outdoor convective heat transfer coefficient for all boundary surfaces [W/(m²·K)].</summary>
    /// <param name="convectiveCoefficient">Outdoor convective heat transfer coefficient [W/(m²·K)].</param>
    public void SetOutsideConvectiveCoefficient(double convectiveCoefficient)
    {
      needInitialize = true;
      foreach (BoundarySurface sf in bndSurfaces)
      {
        if (!sf.IsGroundWall && sf.AdjacentSpaceFactor == -1)
          sf.ConvectiveCoefficient = convectiveCoefficient;
      }
    }

    /// <summary>Sets the indoor convective heat transfer coefficient for all interior surfaces [W/(m²·K)].</summary>
    /// <param name="convectiveCoefficient">Indoor convective heat transfer coefficient [W/(m²·K)].</param>
    public void SetInsideConvectiveCoefficient(double convectiveCoefficient)
    {
      needInitialize = true;

      for (int i = 0; i < walls.Length; i++)
      {
        if (!bndSurfaces.Contains(walls[i].SurfaceF)) walls[i].ConvectiveCoefficientF = convectiveCoefficient;
        if (!bndSurfaces.Contains(walls[i].SurfaceB)) walls[i].ConvectiveCoefficientB = convectiveCoefficient;
      }
      for (int i = 0; i < windows.Length; i++)
        windows[i].InsideSurface.ConvectiveCoefficient = convectiveCoefficient;
    }

    /// <summary>Sets the convective heat transfer coefficient for the specified wall surface [W/(m²·K)].</summary>
    /// <param name="wallIndex">Wall index.</param>
    /// <param name="isSideF">True for the F side; false for the B side.</param>
    /// <param name="convectiveCoefficient">Convective heat transfer coefficient [W/(m²·K)].</param>
    public void SetConvectiveCoefficient
      (int wallIndex, bool isSideF, double convectiveCoefficient)
    {
      if (isSideF) walls[wallIndex].ConvectiveCoefficientF = convectiveCoefficient;
      else walls[wallIndex].ConvectiveCoefficientB = convectiveCoefficient;
    }

    /// <summary>Determines whether the specified surface is registered in this multi-room system.</summary>
    /// <param name="surface">Boundary surface.</param>
    /// <returns>True if the surface is registered; otherwise false.</returns>
    internal bool HasSurface(BoundarySurface surface)
    {
      foreach (BoundarySurface sf in surfaces)
        if (surface == sf) return true;
      foreach (BoundarySurface sf in bndSurfaces)
        if (surface == sf) return true;
      return false;
    }

    #endregion

    #region 制御関連の処理

    /// <summary>Switches the specified zone to <b>temperature-setpoint</b> mode.</summary>
    /// <param name="zoneIndex">Zone index.</param>
    /// <param name="setpoint">Dry-bulb temperature setpoint [°C].</param>
    /// <remarks>
    /// In this mode the solver pins the zone's dry-bulb temperature to
    /// <paramref name="setpoint"/> and reports the HVAC sensible heat supply
    /// needed to maintain it (clamped by <see cref="IReadOnlyZone.HeatingCapacity"/>
    /// / <see cref="IReadOnlyZone.CoolingCapacity"/>). Mutually exclusive
    /// with <see cref="ControlHeatSupply(int,double)"/> — calling either
    /// switches the mode.
    /// </remarks>
    public void ControlDryBulbTemperature(int zoneIndex, double setpoint)
    { zones[zoneIndex].ControlDryBulbTemperature(setpoint); }

    /// <summary>Switches the specified zone to <b>fixed heat-supply</b> mode.</summary>
    /// <param name="zoneIndex">Zone index.</param>
    /// <param name="heatSupply">Sensible heat supplied to the zone [W]. Positive = heating, negative = cooling.</param>
    /// <remarks>
    /// In this mode the solver applies the given sensible heat and lets the
    /// zone temperature float; the zone's setpoint is ignored. Use for
    /// scenarios with a prescribed HVAC load profile. Mutually exclusive
    /// with <see cref="ControlDryBulbTemperature(int,double)"/>.
    /// </remarks>
    public void ControlHeatSupply(int zoneIndex, double heatSupply)
    { zones[zoneIndex].ControlHeatSupply(heatSupply); }

    /// <summary>Switches the specified zone to <b>humidity-setpoint</b> mode.</summary>
    /// <param name="zoneIndex">Zone index.</param>
    /// <param name="setpoint">Humidity ratio setpoint [kg/kg].</param>
    /// <remarks>
    /// In this mode the solver pins the zone's humidity ratio to
    /// <paramref name="setpoint"/> and reports the moisture supply or
    /// removal needed (clamped by
    /// <see cref="IReadOnlyZone.HumidifyingCapacity"/> /
    /// <see cref="IReadOnlyZone.DehumidifyingCapacity"/>). Mutually
    /// exclusive with <see cref="ControlMoistureSupply(int,double)"/>.
    /// </remarks>
    public void ControlHumidityRatio(int zoneIndex, double setpoint)
    { zones[zoneIndex].ControlHumidityRatio(setpoint); }

    /// <summary>Switches the specified zone to <b>fixed moisture-supply</b> mode.</summary>
    /// <param name="zoneIndex">Zone index.</param>
    /// <param name="moistureSupply">Moisture supply to the zone [kg/s]. Positive = humidification, negative = dehumidification.</param>
    /// <remarks>
    /// In this mode the solver applies the given moisture flow and lets the
    /// humidity ratio float. Mutually exclusive with
    /// <see cref="ControlHumidityRatio(int,double)"/>.
    /// </remarks>
    public void ControlMoistureSupply(int zoneIndex, double moistureSupply)
    { zones[zoneIndex].ControlMoistureSupply(moistureSupply); }

    /// <summary>Sets the supply air conditions for the specified zone.</summary>
    /// <param name="zoneIndex">Zone index.</param>
    /// <param name="saTemperature">Supply air temperature [°C].</param>
    /// <param name="saHumidityRatio">Supply air humidity ratio [kg/kg].</param>
    /// <param name="saFlowRate">Supply air flow rate [kg/s].</param>
    public void SetSupplyAir(int zoneIndex, double saTemperature, double saHumidityRatio, double saFlowRate)
    {
      zones[zoneIndex].SupplyAirTemperature = saTemperature;
      zones[zoneIndex].SupplyAirHumidityRatio = saHumidityRatio;
      zones[zoneIndex].SupplyAirFlowRate = saFlowRate;
    }

    /// <summary>Sets secondary supply air conditions (from another zone) for the specified zone.</summary>
    /// <param name="zoneIndex">Zone index.</param>
    /// <param name="saTemperature">Supply air temperature [°C].</param>
    /// <param name="saHumidityRatio">Supply air humidity ratio [kg/kg].</param>
    /// <param name="saFlowRate">Supply air flow rate [kg/s].</param>
    internal void SetSupplyAir2(int zoneIndex, double saTemperature, double saHumidityRatio, double saFlowRate)
    {
      zones[zoneIndex]._supplyAirTemperature2 = saTemperature;
      zones[zoneIndex]._supplyAirHumidityRatio2 = saHumidityRatio;
      zones[zoneIndex]._supplyAirFlowRate2 = saFlowRate;
    }

    #endregion

    #region 空調能力設定関連の処理

    /// <summary>Sets the maximum heating capacity for the specified zone [W].</summary>
    /// <param name="zoneIndex">Zone index.</param>
    /// <param name="heatingCapacity">Maximum heating capacity [W].</param>
    public void SetHeatingCapacity(int zoneIndex, double heatingCapacity)
    { zones[zoneIndex].HeatingCapacity = heatingCapacity; }

    /// <summary>Sets the maximum cooling capacity for the specified zone [W].</summary>
    /// <param name="zoneIndex">Zone index.</param>
    /// <param name="coolingCapacity">Maximum cooling capacity [W].</param>
    public void SetCoolingCapacity(int zoneIndex, double coolingCapacity)
    { zones[zoneIndex].CoolingCapacity = coolingCapacity; }

    /// <summary>Sets the maximum humidifying capacity for the specified zone [kg/s].</summary>
    /// <param name="zoneIndex">Zone index.</param>
    /// <param name="humidifyingCapacity">Maximum humidifying capacity [kg/s].</param>
    public void SetHumidifyingCapacity(int zoneIndex, double humidifyingCapacity)
    { zones[zoneIndex].HumidifyingCapacity = humidifyingCapacity; }

    /// <summary>Sets the maximum dehumidifying capacity for the specified zone [kg/s].</summary>
    /// <param name="zoneIndex">Zone index.</param>
    /// <param name="dehumidifyingCapacity">Maximum dehumidifying capacity [kg/s].</param>
    public void SetDehumidifyingCapacity(int zoneIndex, double dehumidifyingCapacity)
    { zones[zoneIndex].DehumidifyingCapacity = dehumidifyingCapacity; }

    #endregion

  }

}