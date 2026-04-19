/* OfficeWorker.cs
 * 
 * Copyright (C) 2017 E.Togashi
 * 
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 3 of the License, or (at
 * your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;
using System.Collections.Generic;

using Popolo.Core.Numerics;

namespace Popolo.Core.OccupantBehavior
{

  public partial class OfficeTenant
  {

    /// <summary>Represents an individual office worker whose daily schedule and presence are simulated stochastically.</summary>
    public class OfficeWorker : IReadOnlyOfficeWorker
    {

      #region 列挙型定義

      /// <summary>Job category of the office worker.</summary>
      public enum CategoryOfJob
      {
        /// <summary>General staff.</summary>
        NoTitle,
        /// <summary>Managerial staff.</summary>
        Manager,
        /// <summary>Executive officer.</summary>
        Administrator
      }

      /// <summary>Takes a lunch break.</summary>
      private enum LunchBreakTake
      {
        /// <summary>Always stays outside.</summary>
        AlwaysGoesOut = 0,
        /// <summary>Always stays in the office.</summary>
        NeverGoesOut = 1,
        /// <summary>Occasionally goes out.</summary>
        SometimeGoesOut = 2
      }

      #endregion

      #region クラス変数

      /// <summary>Arrival time model parameters for weekdays.</summary>
      private static readonly double[] sttMdl = new double[] { -40.23, 32.61, 1.101, -0.452 };

      /// <summary>Arrival time model parameters for holidays.</summary>
      private static readonly double[] sttMdl_Hol = new double[] { 24.45, 98.71, 1.273, -0.060 };

      /// <summary>Departure time model parameters for weekdays (male, 20–40s).</summary>
      private static readonly double[] endMdl_M24 = new double[] { 82.3, 109.28, 1.529, 0.438 };

      /// <summary>Departure time model parameters for weekdays (other).</summary>
      private static readonly double[] endMdl = new double[] { 40.86, 72.69, 1.309, 0.286 };

      /// <summary>Departure time model parameters for holidays (duration-based).</summary>
      private static readonly double[] endMdl_Hol = new double[] { -73.75, 203.35, 1.465, 0.683 };

      /// <summary>Lunch break start time model parameters.</summary>
      private static readonly double[] lncStMdl = new double[] { -0.337, 1.881, 0.502, 0.303 };

      /// <summary>Lunch break end time model parameters.</summary>
      private static readonly double[] lncEdMdl = new double[] { -5.661, 74.93, 0.686, 0.905 };

      /// <summary>Transition probability from out-of-office to out-of-office state.</summary>
      private static readonly double[] tProbOO = new double[] { 0.880, 0.773, 0.850, 0.765, 0.816, 0.905, 0.556, 0.417, 0.556, 0.710 };

      #endregion

      #region インスタンス変数

      /// <summary>Uniform random number generator.</summary>
      private MersenneTwister urGen;

      /// <summary>Normal random number generator.</summary>
      private NormalRandom nrGen;

      /// <summary>Worker group index encoding sex and age decade: M20,M30,M40,M50,M60,F20,F30,F40,F50,F60.</summary>
      private int group;

      /// <summary>True if this worker may work overnight.</summary>
      private bool allNightWorkable;

      /// <summary>True if this worker may work on holidays.</summary>
      private bool holidayWorkable;

      /// <summary>Lunch break pattern.</summary>
      private LunchBreakTake lunchBreak;

      /// <summary>True if the worker is currently working overnight.</summary>
      private bool allNightWorker = false;

      /// <summary>True if the worker is currently working on a holiday.</summary>
      private bool holidayWorker = false;

      /// <summary>Lunch break departure time.</summary>
      private DateTime lnchOutGoTime;

      /// <summary>Lunch break return time.</summary>
      private DateTime lnchComeBackTime;

      /// <summary>List of departure times for outside activities.</summary>
      private List<DateTime> outGoTime = new List<DateTime>();

      /// <summary>List of return times from outside activities.</summary>
      private List<DateTime> comeBackTime = new List<DateTime>();

      /// <summary>Transition probability from in-office to in-office state.</summary>
      private double tProbII;

      #endregion

      #region プロパティ定義

      /// <summary>Gets a value indicating whether the worker is male.</summary>
      public bool IsMale { get; private set; }

      /// <summary>Gets the job category.</summary>
      public CategoryOfJob Job { get; private set; }

      /// <summary>Gets a value indicating whether the worker is a non-permanent employee.</summary>
      public bool IsPermanent { get; private set; }

      /// <summary>Gets the age [years].</summary>
      public int Age { get; private set; }

      /// <summary>Gets the tenant office to which this worker belongs.</summary>
      public IReadOnlyOfficeTenant Office { get; private set; } = null!;

      /// <summary>Gets the arrival time at the office.</summary>
      public DateTime ArriveTime { get; private set; }

      /// <summary>Gets the departure time from the office.</summary>
      public DateTime LeaveTime { get; private set; }

      /// <summary>Gets a value indicating whether the worker is on leave.</summary>
      public bool IsLOA { get; private set; }

      /// <summary>Gets the average in-office occupancy rate [-].</summary>
      public double AverageIndoorRate { get; private set; }

      /// <summary>Gets a value indicating whether the worker is currently in the office.</summary>
      public bool StayInOffice { get; private set; }

      #endregion

      #region コンストラクタ

      /// <summary>Initializes a new instance of <see cref="OfficeWorker"/>.</summary>
      /// <param name="office">Tenant office to which this worker belongs.</param>
      /// <param name="isMale">True for male; false for female.</param>
      /// <param name="age">Age [years].</param>
      /// <param name="isPermanent">True for permanent employee; false for non-permanent.</param>
      /// <param name="indoorRate">Average in-office occupancy rate [-].</param>
      /// <param name="uRnd">Uniform random number generator.</param>
      public OfficeWorker(OfficeTenant office, bool isMale, int age, bool isPermanent, double indoorRate, MersenneTwister uRnd)
      {
        urGen = uRnd;
        nrGen = new NormalRandom(uRnd);

        //職業種別を確率的に決定
        CategoryOfJob job;
        double[] mngRate = new double[] { 4 / 80d, 9 / 80d, 23 / 81d, 31 / 83d, 18 / 59d, 1 / 72d, 1 / 78d, 4 / 79d, 6 / 67d, 7 / 67d };  //管理職比率
        double[] admRate = new double[] { 1 / 80d, 0 / 80d, 4 / 81d, 11 / 83d, 22 / 59d, 1 / 72d, 0 / 78d, 0 / 79d, 4 / 67d, 13 / 67d };  //役員比率
        double rnd = nrGen.NextDouble();
        if (mngRate[group] < rnd) job = CategoryOfJob.Manager;
        else if (mngRate[group] + admRate[group] < rnd) job = CategoryOfJob.Administrator;
        else job = CategoryOfJob.NoTitle;

        Initialize(office, isMale, age, isPermanent, job, indoorRate);
      }

      /// <summary>Initializes a new instance of <see cref="OfficeWorker"/>.</summary>
      /// <param name="office">Tenant office to which this worker belongs.</param>
      /// <param name="isMale">True for male; false for female.</param>
      /// <param name="age">Age [years].</param>
      /// <param name="isPermanent">True for permanent employee; false for non-permanent.</param>
      /// <param name="job">Job category.</param>
      /// <param name="indoorRate">Average in-office occupancy rate [-].</param>
      /// <param name="nRnd">Normal random number generator.</param>
      public OfficeWorker(OfficeTenant office, bool isMale, int age, bool isPermanent, CategoryOfJob job, double indoorRate, MersenneTwister nRnd)
      {
        urGen = nRnd;
        nrGen = new NormalRandom(nRnd);
        Initialize(office, isMale, age, isPermanent, job, indoorRate);
      }

      /// <summary>Initializes a new instance of <see cref="OfficeWorker"/>.</summary>
      /// <param name="office">Tenant office to which this worker belongs.</param>
      /// <param name="isMale">True for male; false for female.</param>
      /// <param name="age">Age [years].</param>
      /// <param name="isPermanent">True for permanent employee; false for non-permanent.</param>
      /// <param name="job">Job category.</param>
      /// <param name="indoorRate">Average in-office occupancy rate [-].</param>
      private void Initialize(OfficeTenant office, bool isMale, int age, bool isPermanent, CategoryOfJob job, double indoorRate)
      {
        Office = office;
        IsMale = isMale;
        Age = age;
        IsPermanent = isPermanent;
        Job = job;

        //世代と年齢によるグループ判定
        if (IsMale)
        {
          if (age < 30) group = 0;
          else if (30 <= age && age < 40) group = 1;
          else if (40 <= age && age < 50) group = 2;
          else if (50 <= age && age < 60) group = 3;
          else group = 4;
        }
        else
        {
          if (age < 30) group = 5;
          else if (30 <= age && age < 40) group = 6;
          else if (40 <= age && age < 50) group = 7;
          else if (50 <= age && age < 60) group = 8;
          else group = 9;
        }

        //平均在室率から状態推移確率を計算
        tProbII = tProbOO[group] * (1 / indoorRate - 1) + 2 - 1 / indoorRate;

        //徹夜作業と休日出勤可能性判定
        if (IsPermanent)
        {
          allNightWorkable = urGen.NextDouble() < 0.045;
          holidayWorkable = urGen.NextDouble() < 0.306;
        }
        else
        {
          switch (Job)
          {
            case CategoryOfJob.NoTitle:
              allNightWorkable = urGen.NextDouble() < 0.093;
              holidayWorkable = urGen.NextDouble() < 0.355;
              break;
            case CategoryOfJob.Manager:
              allNightWorkable = urGen.NextDouble() < 0.215;
              holidayWorkable = urGen.NextDouble() < 0.527;
              break;
            case CategoryOfJob.Administrator:
              allNightWorkable = urGen.NextDouble() < 0.088;
              holidayWorkable = urGen.NextDouble() < 0.471;
              break;
          }
        }

        //昼休みの取り方
        double[] aGoOut = new double[] { 0.176, 0.157, 0.179, 0.122, 0.221, 0.068, 0.070, 0.141, 0.179, 0.155 };  //常に外出
        double[] nGoOut = new double[] { 0.392, 0.446, 0.488, 0.451, 0.453, 0.534, 0.581, 0.500, 0.488, 0.452 };  //常に社内
        double bf = urGen.NextDouble();
        if (bf < nGoOut[group]) lunchBreak = LunchBreakTake.NeverGoesOut;
        else if (bf < nGoOut[group] + aGoOut[group]) lunchBreak = LunchBreakTake.AlwaysGoesOut;
        else lunchBreak = LunchBreakTake.SometimeGoesOut;
      }

      #endregion

      #region インスタンスメソッド

      /// <summary>Updates the daily work schedule for the specified date.</summary>
      /// <param name="dTime">Date.</param>
      public void UpdateDailySchedule(DateTime dTime)
      {
        //休日出勤判定
        IsLOA = Office.IsHoliday(dTime);
        if (!IsLOA && urGen.NextDouble() < 0.035)
        {
          IsLOA = true;
          holidayWorker = false;
        }
        else holidayWorker = IsLOA && holidayWorkable && urGen.NextDouble() < 0.188;
        //休日出勤者の34.1%は平日相当の業務時間
        if (holidayWorker && 0.341 < urGen.NextDouble()) IsLOA = false;

        //出社時刻計算****************************************
        //前日徹夜作業の場合には出社時刻=0:00,翌日は非休日
        bool spendNight = allNightWorker; //一次保存
        if (allNightWorker)
        {
          ArriveTime = new DateTime(dTime.Year, dTime.Month, dTime.Day, 0, 0, 0);
          IsLOA = false;
        }
        //休日の場合
        else if (IsLOA && !holidayWorker)
          ArriveTime = new DateTime(dTime.Year, dTime.Month, dTime.Day, 0, 0, 0).AddDays(1);
        //平日出勤・休日出勤の場合
        else
        {
          double[] par;
          if (!IsLOA) par = sttMdl; //平日出勤
          else par = sttMdl_Hol;    //休日出勤
          double min = par[0] + par[1] * Math.Sinh((nrGen.NextDouble() - par[3]) / par[2]);
          ArriveTime = new DateTime(dTime.Year, dTime.Month, dTime.Day, Office.StartHour, Office.StartMinute, 0).AddMinutes(min);
        }

        //退社時刻計算****************************************
        //当日徹夜作業の場合には退社時刻=0:00
        allNightWorker = (urGen.NextDouble() < 0.012 && allNightWorkable);
        if (allNightWorker) LeaveTime = new DateTime(dTime.Year, dTime.Month, dTime.Day, 0, 0, 0).AddDays(1);
        //休日の場合
        else if (IsLOA && !holidayWorker) LeaveTime = ArriveTime;
        else
        {
          //平日出勤
          if (!IsLOA)
          {
            double[] par;
            if (group == 0 || group == 1 || group == 2) par = endMdl_M24;
            else par = endMdl;
            double min = par[0] + par[1] * Math.Sinh((nrGen.NextDouble() - par[3]) / par[2]);
            LeaveTime = new DateTime(dTime.Year, dTime.Month, dTime.Day, Office.EndHour, Office.EndMinute, 0).AddMinutes(min);
          }
          //休日出勤
          else
          {
            double min = endMdl_Hol[0] + endMdl_Hol[1] * Math.Sinh((nrGen.NextDouble() - endMdl_Hol[3]) / endMdl_Hol[2]);
            min += 60 * (Office.EndHour - Office.StartHour) + (Office.EndMinute - Office.StartMinute);
            LeaveTime = ArriveTime.AddMinutes(min);
          }
          if (LeaveTime.CompareTo(ArriveTime) < 0) LeaveTime = ArriveTime;
        }

        //昼休み**********************************************
        //外出する場合
        if (lunchBreak == LunchBreakTake.AlwaysGoesOut || urGen.NextDouble() < 0.488)
        {
          double min = lncStMdl[0] + lncStMdl[1] * Math.Sinh((nrGen.NextDouble() - lncStMdl[3]) / lncStMdl[2]);
          lnchOutGoTime = new DateTime(dTime.Year, dTime.Month, dTime.Day, Office.LunchStartHour, Office.LunchStartMinute, 0).AddMinutes(min);

          double z = (nrGen.NextDouble() - lncEdMdl[3]) / lncEdMdl[2];
          min = (lncEdMdl[0] + lncEdMdl[1] * Math.Exp(z)) / (Math.Exp(z) + 1);
          min = Math.Max(0, 60 * (Office.LunchEndHour - Office.LunchStartHour) + (Office.LunchEndMinute - Office.LunchStartMinute) - min);
          lnchComeBackTime = lnchOutGoTime.AddMinutes(min);
        }
        //社内に留まる場合
        else lnchOutGoTime = lnchComeBackTime = ArriveTime;

        //その他一時外出**************************************
        outGoTime.Clear();
        comeBackTime.Clear();
        DateTime dtNow;
        if (spendNight) dtNow = new DateTime(dTime.Year, dTime.Month, dTime.Day, Office.StartHour, Office.StartMinute, 0); //前日泊まった執務者は定時より外出し得る
        else dtNow = ArriveTime;
        bool goOut = urGen.NextDouble() < ((1.0 - tProbII) / (2.0 - tProbOO[group] - tProbII));
        if (goOut) outGoTime.Add(dtNow);
        dtNow = dtNow.AddHours(1);
        while (dtNow.CompareTo(LeaveTime) < 0)
        {
          if (goOut && (urGen.NextDouble() < 1.0 - tProbOO[group]))
          {
            goOut = false;
            comeBackTime.Add(dtNow);
          }
          else if (!goOut && (urGen.NextDouble() < 1.0 - tProbII))
          {
            goOut = true;
            outGoTime.Add(dtNow);
          }
          dtNow = dtNow.AddHours(1);
        }
        if (goOut) comeBackTime.Add(LeaveTime);
      }

      /// <summary>Updates the in-office presence state for the current time step.</summary>
      /// <param name="dTime">Current date and time.</param>
      /// <returns>True if the worker is currently in the office.</returns>
      public void UpdateStatus(DateTime dTime)
      {
        //出社退社時間外の場合は不在
        if (dTime.CompareTo(ArriveTime) < 0 || 0 < dTime.CompareTo(LeaveTime))
        {
          StayInOffice = false;
          return;
        }

        //昼休み滞在不在確認
        if (0 < dTime.CompareTo(lnchOutGoTime) && dTime.CompareTo(lnchComeBackTime) <= 0)
        {
          StayInOffice = false;
          return;
        }

        //その他一時外出確認
        for (int i = 0; i < outGoTime.Count; i++)
        {
          if (0 < dTime.CompareTo(outGoTime[i]) && dTime.CompareTo(comeBackTime[i]) <= 0)
          {
            StayInOffice = false;
            return;
          }
        }

        StayInOffice = true;
      }

      /// <summary>Sets the work style (always in, always out, or stochastic).</summary>
      /// <param name="allNightWorkable">True if the worker may work overnight.</param>
      /// <param name="holidayWorkable">True if the worker may work on holidays.</param>
      public void SetWorkStyle(bool allNightWorkable, bool holidayWorkable)
      {
        this.allNightWorkable = allNightWorkable;
        this.holidayWorkable = holidayWorkable;
      }

      /// <summary>Computes the expected number of minutes per day spent in the office.</summary>
      /// <returns>Expected minutes per day spent in the office.</returns>
      public double CalculateStayInOfficeMinutes()
      {
        double sum = (LeaveTime - ArriveTime).TotalMinutes;
        for (int i = 0; i < outGoTime.Count; i++)
          sum -= (comeBackTime[i] - outGoTime[i]).TotalMinutes;
        sum -= (lnchComeBackTime - lnchOutGoTime).TotalMinutes;  //昼食時間が重複する可能性はある
        return Math.Max(sum, 0);
      }

      /// <summary>Resets the random seed for reproducibility.</summary>
      /// <param name="seed">Random seed.</param>
      public void ResetRandomSeed(uint seed)
      {
        urGen = new MersenneTwister(seed);
        nrGen = new NormalRandom(urGen);
      }

      #endregion

    }
  }
}
