/* LangevinOccupantModel.cs
 *
 * Copyright (C) 2016 E.Togashi
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or (at
 * your option) any later version.
 *
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;
using Popolo.Core.Numerics;

namespace Popolo.Core.ThermalComfort
{
  /// <summary>Models occupant thermal sensation and acceptability using the Langevin model.</summary>
  /// <remarks>
  /// Based on:
  /// Langevin et al. (2013): Modeling thermal comfort holistically.
  /// <see href="https://doi.org/10.1016/j.buildenv.2013.07.017"/>
  /// Langevin et al. (2015): Simulating the human-building interaction.
  /// <see href="https://doi.org/10.1016/j.buildenv.2014.11.037"/>
  /// </remarks>
  public class LangevinOccupantModel
  {

    #region 定数宣言

    #region 申告確率に関わるパラメータ

    private readonly static double[] BETA_ASH_HVAC = new double[] { 2.30, 0.64 };

    private readonly static double[] BETA_ASH_NVNT = new double[] { 1.89, 0.62 };

    private readonly static double[] TAU_ASH_HVAC = new double[] { -999, 0.0, 0.82, 1.63, 2.83, 3.64, 4.64, 999 };

    private readonly static double[] TAU_ASH_NVNT = new double[] { -999, 0.0, 0.52, 1.13, 2.56, 3.40, 4.41, 999 };

    #endregion

    #region 受容確率に関わるパラメータ

    private readonly static double[] BETA_UA_COOL_HVAC = new double[] { 1.79, 0.84, 0.58 };

    private readonly static double[] BETA_UA_WARM_HVAC = new double[] { 2.01, -0.83, -0.13 };

    private readonly static double[] BETA_UA_COOL_NVNT = new double[] { 1.81, 0.93 };

    private readonly static double[] BETA_UA_WARM_NVNT = new double[] { 2.02, -1.28 };

    #endregion

    #endregion

    #region 列挙型定義

    /// <summary>ASHRAE seven-point thermal sensation scale.</summary>
    public enum AshraeThermalSensation
    {
      /// <summary>Cold (-3).</summary>
      Cold = -3,
      /// <summary>Cool (-2).</summary>
      Cool = -2,
      /// <summary>Slightly cool (-1).</summary>
      SlightlyCool = -1,
      /// <summary>Neutral (0).</summary>
      Neutral = 0,
      /// <summary>Slightly warm (+1).</summary>
      SlightlyWarm = 1,
      /// <summary>Warm (+2).</summary>
      Warm = 2,
      /// <summary>Hot (+3).</summary>
      Hot = 3
    }

    #endregion

    #region インスタンス変数・プロパティ

    /// <summary>Random number generator.</summary>
    private readonly MersenneTwister rnd;

    /// <summary>Gets or sets a value indicating whether it is summer.</summary>
    public bool IsSummer { get; set; } = true;

    /// <summary>Gets a value indicating whether the building has HVAC.</summary>
    public bool IsAirConditioned { get; private set; }

    /// <summary>Gets the current thermal sensation vote.</summary>
    public AshraeThermalSensation Vote { get; private set; }

    /// <summary>Gets the upper acceptable sensation threshold in summer.</summary>
    public int HighAcceptableSensationInSummer { get; private set; }

    /// <summary>Gets the lower acceptable sensation threshold in summer.</summary>
    public int LowAcceptableSensationInSummer { get; private set; }

    /// <summary>Gets the upper acceptable sensation threshold in winter.</summary>
    public int HighAcceptableSensationInWinter { get; private set; }

    /// <summary>Gets the lower acceptable sensation threshold in winter.</summary>
    public int LowAcceptableSensationInWinter { get; private set; }

    /// <summary>Gets a value indicating whether the occupant is uncomfortably cold.</summary>
    public bool UncomfortablyCold { get; private set; }

    /// <summary>Gets a value indicating whether the occupant is uncomfortably warm.</summary>
    public bool UncomfortablyWarm { get; private set; }

    /// <summary>Gets the probability of being uncomfortably cold [-].</summary>
    public double UncomfortablyColdProbability { get; private set; } = 0.0d;

    /// <summary>Gets the probability of being uncomfortably warm [-].</summary>
    public double UncomfortablyWarmProbability { get; private set; } = 0.0d;

    /// <summary>Gets the total probability of thermal discomfort [-].</summary>
    public double UncomfortableProbability { get { return UncomfortablyColdProbability + UncomfortablyWarmProbability; } }

    /// <summary>Gets a value indicating whether the occupant is thermally comfortable.</summary>
    public bool Comfortable
    { get { return !UncomfortablyCold && !UncomfortablyWarm; } }

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance and samples individual acceptable sensation thresholds.</summary>
    /// <param name="randomSeed">Random seed.</param>
    /// <param name="isAirConditioned">True if the building has HVAC; false for naturally ventilated.</param>
    public LangevinOccupantModel(uint randomSeed, bool isAirConditioned)
    {
      this.IsAirConditioned = isAirConditioned;
      rnd = new MersenneTwister(randomSeed);

      //sample initial sensation//閾値の設定は空調ありを前提とするはず
      int si = 0;
      //文献ではこのように初期化するが、これだとPMV=0付近で不満足率が上がりすぎる
      /*
      NormalRandom nRnd = new NormalRandom(rnd);
      si = (int)Math.Round(nRnd.NextDouble());
      double ucSum, ucWin;
      while (true)
      {
        ucSum = GetUnAcceptableRateFromVote(si, true, true);
        ucWin = GetUnAcceptableRateFromVote(si, true, false);
        double hld = rnd.NextDouble();
        if (ucSum < hld || ucWin < hld) break;
        si = (int)Math.Round(nRnd.NextDouble());
      }*/

      //夏季の申告許容上下限値を計算
      HighAcceptableSensationInSummer = si;
      while (HighAcceptableSensationInSummer < 3)
      {
        if (rnd.NextDouble() < GetUnAcceptableRateFromVote(HighAcceptableSensationInSummer, true, true))
          break;
        HighAcceptableSensationInSummer++;
      }
      HighAcceptableSensationInSummer--;

      LowAcceptableSensationInSummer = si;
      while (-3 < LowAcceptableSensationInSummer)
      {
        if (rnd.NextDouble() < GetUnAcceptableRateFromVote(LowAcceptableSensationInSummer, true, true))
          break;
        LowAcceptableSensationInSummer--;
      }
      LowAcceptableSensationInSummer++;

      //冬季の申告許容上下限値を計算
      HighAcceptableSensationInWinter = si;
      while (HighAcceptableSensationInWinter < 3)
      {
        if (rnd.NextDouble() < GetUnAcceptableRateFromVote(HighAcceptableSensationInWinter, true, false))
          break;
        HighAcceptableSensationInWinter++;
      }
      HighAcceptableSensationInWinter--;

      LowAcceptableSensationInWinter = si;
      while (-3 < LowAcceptableSensationInWinter)
      {
        if (rnd.NextDouble() < GetUnAcceptableRateFromVote(LowAcceptableSensationInWinter, true, false))
          break;
        LowAcceptableSensationInWinter--;
      }
      LowAcceptableSensationInWinter++;
    }

    #endregion

    #region インスタンス・メソッド

    /// <summary>Computes the thermal sensation vote distribution for the given PMV.</summary>
    /// <param name="pmv">Environmental PMV value [-].</param>
    /// <returns>Vote probability array for the seven ASHRAE scale levels (-3 to +3).</returns>
    public double[] GetVoteDistribution(double pmv)
    {
      return GetVoteDistribution(pmv, IsAirConditioned);
    }

    /// <summary>Updates the thermal sensation vote and discomfort probabilities for the given PMV.</summary>
    /// <param name="pmv">Environmental PMV value [-].</param>
    public void Update(double pmv)
    {
      AshraeThermalSensation[] VOTES = new AshraeThermalSensation[]
      { AshraeThermalSensation.Cold, AshraeThermalSensation.Cool, AshraeThermalSensation.SlightlyCool, AshraeThermalSensation.Neutral, AshraeThermalSensation.SlightlyWarm, AshraeThermalSensation.Warm };

      //温冷感申告値を更新
      double[] vDist = GetVoteDistribution(pmv);
      double cc = rnd.NextDouble();
      Vote = AshraeThermalSensation.Hot;
      for (int i = 0; i < vDist.Length - 1; i++)
      {
        if (cc < vDist[i])
        {
          Vote = VOTES[i];
          break;
        }
        cc -= vDist[i];
      }

      //快・不快感を更新
      if (IsSummer)
      {
        UncomfortablyWarm = HighAcceptableSensationInSummer < (int)Vote;
        UncomfortablyCold = (int)Vote < LowAcceptableSensationInSummer;
      }
      else
      {
        UncomfortablyWarm = HighAcceptableSensationInWinter < (int)Vote;
        UncomfortablyCold = (int)Vote < LowAcceptableSensationInWinter;
      }

      //不満確率を更新
      UncomfortablyColdProbability = UncomfortablyWarmProbability = 0;
      for (int i = 0; i < vDist.Length; i++)
      {
        if ((i - 3) < (IsSummer ? LowAcceptableSensationInSummer : LowAcceptableSensationInWinter))
          UncomfortablyColdProbability += vDist[i];
        if ((IsSummer ? HighAcceptableSensationInSummer : HighAcceptableSensationInWinter) < (i - 3))
          UncomfortablyWarmProbability += vDist[i];
      }
    }

    #endregion

    #region staticメソッド

    /// <summary>Computes the thermal sensation vote distribution for the given PMV (static version).</summary>
    /// <param name="pmv">Environmental PMV value [-].</param>
    /// <param name="useHVAC">True for HVAC building; false for naturally ventilated.</param>
    /// <returns>Vote probability array for the seven ASHRAE scale levels (-3 to +3).</returns>
    public static double[] GetVoteDistribution(double pmv, bool useHVAC)
    {
      double[] beta = useHVAC ? BETA_ASH_HVAC : BETA_ASH_NVNT;
      double[] tau = useHVAC ? TAU_ASH_HVAC : TAU_ASH_NVNT;

      double xb = beta[0] + pmv * beta[1];
      double[] voteD = new double[7];
      for (int i = 0; i < voteD.Length; i++)
        voteD[i] = NormalRandom.CumulativeDistribution(xb - tau[i])
          - NormalRandom.CumulativeDistribution(xb - tau[i + 1]);
      return voteD;
    }

    /// <summary>Computes the acceptability probability [-] for the given PMV.</summary>
    /// <param name="pmv">Environmental PMV value [-].</param>
    /// <param name="useHVAC">True for HVAC building; false for naturally ventilated.</param>
    /// <param name="isSummer">True for summer; false for winter.</param>
    /// <returns>Acceptability probability [-].</returns>
    public static double GetAcceptableRateFromPMV(double pmv, bool useHVAC, bool isSummer)
    {
      double[] dist = GetVoteDistribution(pmv, useHVAC);
      return
        GetAcceptableRateFromVote(-3.0, useHVAC, isSummer) * dist[0] +
        GetAcceptableRateFromVote(-2.0, useHVAC, isSummer) * dist[1] +
        GetAcceptableRateFromVote(-1.0, useHVAC, isSummer) * dist[2] +
        GetAcceptableRateFromVote(0.0, useHVAC, isSummer) * dist[3] +
        GetAcceptableRateFromVote(1.0, useHVAC, isSummer) * dist[4] +
        GetAcceptableRateFromVote(2.0, useHVAC, isSummer) * dist[5] +
        GetAcceptableRateFromVote(3.0, useHVAC, isSummer) * dist[6];
    }

    /// <summary>Computes the unacceptability probability [-] for the given PMV.</summary>
    /// <param name="pmv">Environmental PMV value [-].</param>
    /// <param name="useHVAC">True for HVAC building; false for naturally ventilated.</param>
    /// <param name="isSummer">True for summer; false for winter.</param>
    /// <returns>Unacceptability probability [-].</returns>
    public static double GetUnAcceptableRateFromPMV(double pmv, bool useHVAC, bool isSummer)
    {
      return 1.0 - GetAcceptableRateFromPMV(pmv, useHVAC, isSummer);
    }

    /// <summary>Computes the acceptability probability [-] for the given sensation vote.</summary>
    /// <param name="vote">Thermal sensation vote on the ASHRAE scale.</param>
    /// <param name="useHVAC">True for HVAC building; false for naturally ventilated.</param>
    /// <param name="isSummer">True for summer; false for winter.</param>
    /// <returns>Acceptability probability [-].</returns>
    public static double GetAcceptableRateFromVote(double vote, bool useHVAC, bool isSummer)
    {
      double[] beta;
      if (vote < 0)
        beta = useHVAC ? BETA_UA_COOL_HVAC : BETA_UA_COOL_NVNT;
      else
        beta = useHVAC ? BETA_UA_WARM_HVAC : BETA_UA_WARM_NVNT;

      double xb = beta[0] + vote * beta[1] + ((useHVAC && isSummer) ? beta[2] : 0);
      return NormalRandom.CumulativeDistribution(xb);
    }

    /// <summary>Computes the unacceptability probability [-] for the given sensation vote.</summary>
    /// <param name="vote">Thermal sensation vote on the ASHRAE scale.</param>
    /// <param name="useHVAC">True for HVAC building; false for naturally ventilated.</param>
    /// <param name="isSummer">True for summer; false for winter.</param>
    /// <returns>Unacceptability probability [-].</returns>
    public static double GetUnAcceptableRateFromVote(double vote, bool useHVAC, bool isSummer)
    {
      return 1.0 - GetAcceptableRateFromVote(vote, useHVAC, isSummer);
    }

    #endregion

  }
}
