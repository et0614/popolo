/* OfficeTenant.cs
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
  /// <summary>Represents an office tenant with a simulated workforce and daily schedule.</summary>
  public partial class OfficeTenant: IReadOnlyOfficeTenant
  {

    #region 列挙型定義

    /// <summary>Industry category of the tenant.</summary>
    public enum CategoryOfIndustry
    {
      /// <summary>Construction.</summary>
      Construction,
      /// <summary>Manufacturing.</summary>
      Manufacturing,
      /// <summary>Utilities (electricity, gas, water, etc.).</summary>
      Infrastructure,
      /// <summary>Information and communications.</summary>
      InformationAndCommunications,
      /// <summary>Transportation and postal services.</summary>
      TrafficOrPostalService,
      /// <summary>Wholesale and retail trade.</summary>
      WholesaleOrRetailing,
      /// <summary>Finance and insurance.</summary>
      FinanceOrInsurance,
      /// <summary>Real estate.</summary>
      RealEstate
    }

    /// <summary>Job category.</summary>
    public enum CategoryOfJob
    {
      /// <summary>Managerial staff.</summary>
      Management,
      /// <summary>Technical staff.</summary>
      Technical,
      /// <summary>Clerical staff.</summary>
      Clerical,
      /// <summary>Sales staff.</summary>
      Selling
    }

    /// <summary>Day of the week.</summary>
    [Flags]
    public enum DaysOfWeek
    {
      /// <summary>None (not applicable).</summary>
      None = 0,
      /// <summary>Sunday.</summary>
      Sunday = 1,
      /// <summary>Monday.</summary>
      Monday = 2,
      /// <summary>Tuesday.</summary>
      Tuesday = 4,
      /// <summary>Wednesday.</summary>
      Wednesday = 8,
      /// <summary>Thursday.</summary>
      Thursday = 16,
      /// <summary>Friday.</summary>
      Friday = 32,
      /// <summary>Saturday.</summary>
      Saturday = 64
    }

    #endregion

    #region クラス変数

    /// <summary>Job type distribution ratios [-] by sex: male/female × managerial/technical(perm)/technical(non-perm)/clerical(perm)/clerical(non-perm)/sales(perm)/sales(non-perm).</summary>
    private static readonly Dictionary<CategoryOfIndustry, double[][]> jobRates = new Dictionary<CategoryOfIndustry, double[][]>();

    /// <summary>Age distribution ratios [-] by sex and job type: male/female × managerial/technical(perm)/technical(non-perm)/clerical(perm)/clerical(non-perm)/sales(perm)/sales(non-perm).</summary>
    private static readonly Dictionary<CategoryOfIndustry, double[][]> ageRates = new Dictionary<CategoryOfIndustry, double[][]>();

    /// <summary>Business start hour.</summary>
    private static readonly int[] startTimes_Hour = new int[] { 9, 8, 8, 8, 9, 10, 8, 8, 8, 9, 8, 7, 8, 8 };

    /// <summary>Business start minute.</summary>
    private static readonly int[] startTimes_Minute = new int[] { 0, 30, 0, 45, 30, 0, 15, 40, 50, 15, 10, 0, 20, 25 };

    /// <summary>Probability distribution for business start time.</summary>
    private static readonly double[] startTimeDists = new double[] { 0.365, 0.600, 0.705, 0.768, 0.827, 0.872, 0.907, 0.930, 0.951, 0.967, 0.976, 0.984, 0.992, 1.000 };

    /// <summary>Lunch break start hour.</summary>
    private static readonly int[] lunchTimes_Hour = new int[] { 12, 12, 13, 12, 11, 11, 12, 11, 13, 14 };

    /// <summary>Lunch break start minute.</summary>
    private static readonly int[] lunchTimes_Minute = new int[] { 0, 30, 0, 15, 30, 45, 10, 50, 30, 0 };

    /// <summary>Probability distribution for lunch break start time.</summary>
    private static readonly double[] lunchTimeDists = new double[] { 0.855, 0.887, 0.918, 0.944, 0.958, 0.971, 0.980, 0.988, 0.994, 1.000 };

    /// <summary>Lunch break duration [min].</summary>
    private static readonly int[] lunchLengths = new int[] { 60, 45, 50, 40, 30 };

    /// <summary>Probability distribution for lunch break duration.</summary>
    private static readonly double[] lunchLengthDists = new double[] { 0.796, 0.938, 0.969, 0.988, 1.000 };

    #endregion

    #region staticメソッド

    /// <summary>Gets the male ratio among workers [-].</summary>
    /// <param name="ind">Industry category.</param>
    /// <returns>Male ratio among workers [-].</returns>
    public static double GetMaleRate(CategoryOfIndustry ind)
    {
      switch (ind)
      {
        case CategoryOfIndustry.Construction:
          return 0.582;
        case CategoryOfIndustry.Manufacturing:
          return 0.702;
        case CategoryOfIndustry.Infrastructure:
          return 0.813;
        case CategoryOfIndustry.InformationAndCommunications:
          return 0.746;
        case CategoryOfIndustry.TrafficOrPostalService:
          return 0.655;
        case CategoryOfIndustry.WholesaleOrRetailing:
          return 0.571;
        case CategoryOfIndustry.FinanceOrInsurance:
          return 0.454;
        default:
          return 0.595;
      }
    }

    /// <summary>Static constructor: initializes industry and job parameter tables.</summary>
    static OfficeTenant()
    {
      //職業比率初期化***********************************************************
      //建設業
      double[][] jRates = new double[2][];
      jRates[0] = new double[] { 0.185, 0.455, 0.511, 0.691, 0.728, 0.953, 1.000 };
      jRates[1] = new double[] { 0.030, 0.052, 0.060, 0.729, 0.970, 0.992, 1.000 };
      jobRates.Add(CategoryOfIndustry.Construction, jRates);
      //製造業
      jRates = new double[2][];
      jRates[0] = new double[] { 0.119, 0.389, 0.446, 0.746, 0.809, 0.967, 1.000 };
      jRates[1] = new double[] { 0.020, 0.063, 0.109, 0.520, 0.961, 0.980, 1.000 };
      jobRates.Add(CategoryOfIndustry.Manufacturing, jRates);
      //電気・ガス・水道・インフラ
      jRates = new double[2][];
      jRates[0] = new double[] { 0.000, 0.212, 0.230, 0.868, 0.923, 0.994, 1.000 };
      jRates[1] = new double[] { 0.000, 0.000, 0.000, 0.750, 1.000, 1.000, 1.000 };
      jobRates.Add(CategoryOfIndustry.Infrastructure, jRates);
      //情報通信業
      jRates = new double[2][];
      jRates[0] = new double[] { 0.027, 0.642, 0.714, 0.880, 0.900, 0.990, 1.000 };
      jRates[1] = new double[] { 0.000, 0.266, 0.415, 0.755, 0.944, 0.980, 1.000 };
      jobRates.Add(CategoryOfIndustry.InformationAndCommunications, jRates);
      //運輸業・郵便業
      jRates = new double[2][];
      jRates[0] = new double[] { 0.105, 0.131, 0.140, 0.706, 0.895, 0.974, 1.000 };
      jRates[1] = new double[] { 0.033, 0.033, 0.033, 0.339, 1.000, 1.000, 1.000 };
      jobRates.Add(CategoryOfIndustry.TrafficOrPostalService, jRates);
      //卸売業・小売業
      jRates = new double[2][];
      jRates[0] = new double[] { 0.115, 0.163, 0.179, 0.376, 0.442, 0.860, 1.000 };
      jRates[1] = new double[] { 0.025, 0.055, 0.118, 0.378, 0.932, 0.954, 1.000 };
      jobRates.Add(CategoryOfIndustry.WholesaleOrRetailing, jRates);
      //金融業・保険業
      jRates = new double[2][];
      jRates[0] = new double[] { 0.072, 0.111, 0.115, 0.588, 0.638, 0.966, 1.000 };
      jRates[1] = new double[] { 0.000, 0.008, 0.012, 0.453, 0.695, 0.892, 1.000 };
      jobRates.Add(CategoryOfIndustry.FinanceOrInsurance, jRates);
      //不動産業 他
      jRates = new double[2][];
      jRates[0] = new double[] { 0.149, 0.164, 0.170, 0.537, 0.680, 0.910, 1.000 };
      jRates[1] = new double[] { 0.061, 0.061, 0.061, 0.570, 0.909, 0.964, 1.000 };
      jobRates.Add(CategoryOfIndustry.RealEstate, jRates);

      //年代比率初期化***********************************************************
      //建設業
      double[][] aRates = new double[2][];
      aRates[0] = new double[] { 0.113, 0.306, 0.562, 0.762 };
      aRates[1] = new double[] { 0.081, 0.257, 0.554, 0.757 };
      ageRates.Add(CategoryOfIndustry.Construction, aRates);
      //製造業
      aRates = new double[2][];
      aRates[0] = new double[] { 0.159, 0.381, 0.651, 0.857 };
      aRates[1] = new double[] { 0.144, 0.345, 0.620, 0.831 };
      ageRates.Add(CategoryOfIndustry.Manufacturing, aRates);
      //電気・ガス・水道・インフラ
      aRates = new double[2][];
      aRates[0] = new double[] { 0.125, 0.292, 0.583, 0.875 };
      aRates[1] = new double[] { 0.000, 0.333, 1.000, 1.000 };
      ageRates.Add(CategoryOfIndustry.Infrastructure, aRates);
      //情報通信業
      aRates = new double[2][];
      aRates[0] = new double[] { 0.162, 0.461, 0.760, 0.942 };
      aRates[1] = new double[] { 0.273, 0.582, 0.855, 0.964 };
      ageRates.Add(CategoryOfIndustry.InformationAndCommunications, aRates);
      //運輸業・郵便業
      aRates = new double[2][];
      aRates[0] = new double[] { 0.096, 0.284, 0.565, 0.790 };
      aRates[1] = new double[] { 0.143, 0.349, 0.667, 0.889 };
      ageRates.Add(CategoryOfIndustry.TrafficOrPostalService, aRates);
      //卸売業・小売業
      aRates = new double[2][];
      aRates[0] = new double[] { 0.180, 0.388, 0.623, 0.801 };
      aRates[1] = new double[] { 0.198, 0.381, 0.622, 0.822 };
      ageRates.Add(CategoryOfIndustry.WholesaleOrRetailing, aRates);
      //金融業・保険業
      aRates = new double[2][];
      aRates[0] = new double[] { 0.143, 0.300, 0.586, 0.857 };
      aRates[1] = new double[] { 0.183, 0.390, 0.695, 0.915 };
      ageRates.Add(CategoryOfIndustry.FinanceOrInsurance, aRates);
      //不動産業 他
      aRates = new double[2][];
      aRates[0] = new double[] { 0.107, 0.267, 0.467, 0.640 };
      aRates[1] = new double[] { 0.136, 0.318, 0.523, 0.705 };
      ageRates.Add(CategoryOfIndustry.RealEstate, aRates);
    }

    #endregion

    #region インスタンス変数・プロパティ

    /// <summary>Uniform random number generator.</summary>
    private MersenneTwister uRnd = new MersenneTwister(1);

    /// <summary>List of special holidays added by the user.</summary>
    /// <remarks>Default holiday dates do not account for the Happy Monday system.</remarks>
    private List<DateTime> specialHolidays = new List<DateTime>()
    {
      new DateTime(1999,1,1), //元日
      new DateTime(1999,1,2), //正月休暇
      new DateTime(1999,1,3), //正月休暇
      new DateTime(1999,1,15), //成人の日
      new DateTime(1999,2,11), //建国記念日
      new DateTime(1999,4,29), //昭和の日
      new DateTime(1999,5,3), //憲法記念日
      new DateTime(1999,5,4), //みどりの日
      new DateTime(1999,5,5), //こどもの日
      new DateTime(1999,7,20), //海の日
      new DateTime(1999,8,11), //山の日
      new DateTime(1999,9,15), //敬老の日
      new DateTime(1999,10,10), //体育の日
      new DateTime(1999,11,3), //文化の日
      new DateTime(1999,11,23), //勤労感謝の日
      new DateTime(1999,12,23), //天皇誕生日
      new DateTime(1999,12,29), //年末休暇
      new DateTime(1999,12,30), //年末休暇
      new DateTime(1999,12,31) //年末休暇
    };

    /// <summary>Gets the industry category.</summary>
    public CategoryOfIndustry Industry { get; private set; }

    /// <summary>Gets the floor area [m²].</summary>
    public double FloorArea { get; private set; }

    /// <summary>Office workers belonging to this tenant.</summary>
    private OfficeWorker[] workers = new OfficeWorker[0];

    /// <summary>Gets the array of office workers.</summary>
    public IReadOnlyOfficeWorker[] OfficeWorkers { get { return workers; } }

    /// <summary>Gets the total number of workers.</summary>
    public uint OfficeWorkerNumber { get { return (uint)OfficeWorkers.Length; } }

    /// <summary>Gets the number of workers currently in the office.</summary>
    public uint StayWorkerNumber { get; private set; }

    /// <summary>Worker count array [sex × employment × job]: male/female × permanent/non-permanent × managerial/technical/clerical/sales.</summary>
    private Dictionary<bool, Dictionary<bool, uint[]>> owNumbers = new Dictionary<bool, Dictionary<bool, uint[]>>();

    /// <summary>Gets the regular holiday days of the week.</summary>
    public DaysOfWeek Holidays { get; private set; }

    /// <summary>Gets the business start hour.</summary>
    public int StartHour { get; private set; }

    /// <summary>Gets the business start minute.</summary>
    public int StartMinute { get; private set; }

    /// <summary>Gets the business end hour.</summary>
    public int EndHour { get; private set; }

    /// <summary>Gets the business end minute.</summary>
    public int EndMinute { get; private set; }

    /// <summary>Gets the lunch break start hour.</summary>
    public int LunchStartHour { get; private set; }

    /// <summary>Gets the lunch break start minute.</summary>
    public int LunchStartMinute { get; private set; }

    /// <summary>Gets the lunch break end hour.</summary>
    public int LunchEndHour { get; private set; }

    /// <summary>Gets the lunch break end minute.</summary>
    public int LunchEndMinute { get; private set; }

    #endregion

    #region コンストラクタ

    /// <summary>Initializes a new instance of <see cref="OfficeTenant"/>.</summary>
    /// <param name="cInd">Industry category.</param>
    /// <param name="floorArea">Floor area [m²].</param>
    /// <param name="holidays">Days of the week designated as regular holidays.</param>
    /// <param name="seed">Random seed.</param>
    public OfficeTenant(CategoryOfIndustry cInd, double floorArea, DaysOfWeek holidays, uint seed)
    {
      Industry = cInd;
      FloorArea = floorArea;
      Holidays = holidays;

      Initialize(seed);
    }

    /// <summary>Initializes the random seed.</summary>
    /// <param name="seed">Random seed.</param>
    public void Initialize(uint seed)
    {
      uRnd = new MersenneTwister(seed);

      //就業規則上の定時を設定*****************************************************
      //始業時刻
      double rnd = uRnd.NextDouble();
      int index = 0;
      while (startTimeDists[index] < rnd) index++;
      DateTime sttTime = new DateTime(1999, 1, 1, startTimes_Hour[index], startTimes_Minute[index], 0);
      //昼休み開始時刻
      rnd = uRnd.NextDouble();
      index = 0;
      while (lunchTimeDists[index] < rnd) index++;
      DateTime lnchSttTime = new DateTime(1999, 1, 1, lunchTimes_Hour[index], lunchTimes_Minute[index], 0);
      //昼休み終了時刻
      rnd = uRnd.NextDouble();
      index = 0;
      while (lunchLengthDists[index] < rnd) index++;
      LunchEndHour = lnchSttTime.AddMinutes(lunchLengths[index]).Hour;
      LunchEndMinute = lnchSttTime.AddMinutes(lunchLengths[index]).Minute;
      //終業時刻
      DateTime endTime = sttTime.AddHours(8).AddMinutes(lunchLengths[index]);

      StartHour = sttTime.Hour;
      StartMinute = sttTime.Minute;
      EndHour = endTime.Hour;
      EndMinute = endTime.Minute;
      LunchStartHour = lnchSttTime.Hour;
      LunchStartMinute = lnchSttTime.Minute;

      MakeOfficeWorkers();
    }

    /// <summary>Initializes a new instance of <see cref="OfficeTenant"/>.</summary>
    /// <param name="cInd">Industry category.</param>
    /// <param name="floorArea">Floor area [m²].</param>
    /// <param name="holidays">Days of the week designated as regular holidays.</param>
    /// <param name="seed">Random seed.</param>
    /// <param name="sttHour">Business start hour.</param>
    /// <param name="sttMinute">Business start minute.</param>
    /// <param name="endHour">Business end hour.</param>
    /// <param name="endMinute">Business end minute.</param>
    /// <param name="lnchSttHour">Lunch break start hour.</param>
    /// <param name="lnchSttMinute">Lunch break start minute.</param>
    /// <param name="lnchEndHour">Lunch break end hour.</param>
    /// <param name="lnchEndMinute">Lunch break end minute.</param>
    public OfficeTenant(CategoryOfIndustry cInd, double floorArea, DaysOfWeek holidays, uint seed,
      int sttHour, int sttMinute, int endHour, int endMinute, int lnchSttHour, int lnchSttMinute, int lnchEndHour, int lnchEndMinute)
    {
      Industry = cInd;
      FloorArea = floorArea;
      Holidays = holidays;
      uRnd = new MersenneTwister(seed);

      StartHour = sttHour;
      StartMinute = sttMinute;
      EndHour = endHour;
      EndMinute = endMinute;
      LunchStartHour = lnchSttHour;
      LunchStartMinute = lnchSttMinute;
      LunchEndHour = lnchEndHour;
      LunchEndMinute = lnchEndMinute;

      MakeOfficeWorkers();
    }

    /// <summary>Builds the behavioral model for all office workers.</summary>
    private void MakeOfficeWorkers()
    {
      //執務者を生成***************************************************************
      owNumbers[true] = new Dictionary<bool, uint[]>();
      owNumbers[false] = new Dictionary<bool, uint[]>();
      owNumbers[true][true] = new uint[4];
      owNumbers[true][false] = new uint[4];
      owNumbers[false][true] = new uint[4];
      owNumbers[false][false] = new uint[4];
      NormalRandom nRnd = new NormalRandom(uRnd);
      List<OfficeWorker> wks = new List<OfficeWorker>();
      double rateRnd = nRnd.NextDouble_Standard();  //在籍率用乱数
      int np0, np1, np2, np3;
      np0 = np1 = np2 = np3 = 0;
      while (true)
      {
        //性別決定
        bool isMale = uRnd.NextDouble() < GetMaleRate(Industry);

        //職業決定
        OfficeWorker.CategoryOfJob job;
        bool isPermanent;
        double oRate;  //在籍者密度[人/m2], 在室率[-]
        double[] jRates;
        if (isMale) jRates = jobRates[Industry][0];
        else jRates = jobRates[Industry][1];
        double rnd = uRnd.NextDouble();
        if (rnd < jRates[0])  //管理
        {
          isPermanent = false;
          job = OfficeWorker.CategoryOfJob.Manager; //役員無し
          oRate = nRnd.NextDouble_Standard() * 0.115 + 0.694;
          owNumbers[isMale][isPermanent][0]++;
          np0++;
        }
        else if (rnd < jRates[2]) //技術
        {
          isPermanent = jRates[1] < rnd;  //正規・非正規
          job = OfficeWorker.CategoryOfJob.NoTitle;
          oRate = nRnd.NextDouble_Standard() * 0.122 + 0.647;
          owNumbers[isMale][isPermanent][1]++;
          np1++;
        }
        else if (rnd < jRates[4]) //事務
        {
          isPermanent = jRates[3] < rnd;  //正規・非正規
          job = OfficeWorker.CategoryOfJob.NoTitle;
          oRate = nRnd.NextDouble_Standard() * 0.115 + 0.694;
          owNumbers[isMale][isPermanent][2]++;
          np2++;
        }
        else //営業
        {
          isPermanent = jRates[5] < rnd;  //正規・非正規
          job = OfficeWorker.CategoryOfJob.NoTitle;
          oRate = nRnd.NextDouble_Standard() * 0.052 + 0.477;
          owNumbers[isMale][isPermanent][3]++;
          np3++;
        }
        //正規分布の裾値対策
        oRate = Math.Min(1.0, Math.Max(0.01, oRate));

        //年齢決定
        int age;
        double[] aRates;
        if (isMale) aRates = ageRates[Industry][0];
        else aRates = ageRates[Industry][1];
        rnd = uRnd.NextDouble();
        if (rnd < aRates[0]) age = 25;
        else if (rnd < aRates[1]) age = 35;
        else if (rnd < aRates[2]) age = 45;
        else if (rnd < aRates[3]) age = 55;
        else age = 65;

        //執務者を作成
        //wks.Add(new OfficeWorker(this, isMale, age, isPermanent, job, oRate, uRnd));
        wks.Add(new OfficeWorker(this, isMale, age, isPermanent, job, oRate, new MersenneTwister(uRnd.Next()))); //2023.07.30 Bugfix

        //床面積確認
        int npSum = np0 + np1 + np2 + np3;
        double myu = (0.162 * (np0 + np2) + 0.149 * np1 + 0.193 * np3) / npSum;
        double dev = (0.055 * (np0 + np2) + 0.053 * np1 + 0.059 * np3) / npSum;
        double ppf = (dev * rateRnd + myu) * 0.83;
        ppf = Math.Max(0.05, Math.Min(0.4, ppf)); //藤井の調査実績から最大最小を設定
        if (FloorArea * ppf < npSum) break;
      }
      workers = wks.ToArray();
    }

    /// <summary>Creates office worker instances according to the workforce composition.</summary>
    /// <returns>The created office worker instance.</returns>
    public OfficeWorker MakeOfficeWorker()
    {
      NormalRandom nRnd = new NormalRandom(uRnd);

      //性別決定
      bool isMale = uRnd.NextDouble() < GetMaleRate(Industry);

      //職業決定
      OfficeWorker.CategoryOfJob job;
      bool isPermanent;
      double oRate;  //在籍者密度[人/m2], 在室率[-]
      double[] jRates;
      if (isMale) jRates = jobRates[Industry][0];
      else jRates = jobRates[Industry][1];
      double rnd = uRnd.NextDouble();
      if (rnd < jRates[0])  //管理
      {
        isPermanent = false;
        job = OfficeWorker.CategoryOfJob.Manager; //役員無し
        oRate = nRnd.NextDouble_Standard() * 0.115 + 0.694;
        owNumbers[isMale][isPermanent][0]++;
      }
      else if (rnd < jRates[2]) //技術
      {
        isPermanent = jRates[1] < rnd;  //正規・非正規
        job = OfficeWorker.CategoryOfJob.NoTitle;
        oRate = nRnd.NextDouble_Standard() * 0.122 + 0.647;
        owNumbers[isMale][isPermanent][1]++;
      }
      else if (rnd < jRates[4]) //事務
      {
        isPermanent = jRates[3] < rnd;  //正規・非正規
        job = OfficeWorker.CategoryOfJob.NoTitle;
        oRate = nRnd.NextDouble_Standard() * 0.115 + 0.694;
        owNumbers[isMale][isPermanent][2]++;
      }
      else //営業
      {
        isPermanent = jRates[5] < rnd;  //正規・非正規
        job = OfficeWorker.CategoryOfJob.NoTitle;
        oRate = nRnd.NextDouble_Standard() * 0.052 + 0.477;
        owNumbers[isMale][isPermanent][3]++;
      }
      //正規分布の裾値対策
      oRate = Math.Min(1.0, Math.Max(0.01, oRate));

      //年齢決定
      int age;
      double[] aRates;
      if (isMale) aRates = ageRates[Industry][0];
      else aRates = ageRates[Industry][1];
      rnd = uRnd.NextDouble();
      if (rnd < aRates[0]) age = 25;
      else if (rnd < aRates[1]) age = 35;
      else if (rnd < aRates[2]) age = 45;
      else if (rnd < aRates[3]) age = 55;
      else age = 65;

      //執務者を作成
      return new OfficeWorker(this, isMale, age, isPermanent, job, oRate, uRnd);
    }

    #endregion

    #region インスタンスメソッド

    /// <summary>Gets the number of workers [persons].</summary>
    /// <param name="isMale">True for male; false for female.</param>
    /// <param name="isPermanent">True for non-permanent employee.</param>
    /// <param name="job">Job category.</param>
    /// <returns>Number of workers [persons].</returns>
    public uint GetNumber(bool isMale, bool isPermanent, CategoryOfJob job)
    {
      switch (job)
      {
        case CategoryOfJob.Management:
          return owNumbers[isMale][isPermanent][0];
        case CategoryOfJob.Technical:
          return owNumbers[isMale][isPermanent][1];
        case CategoryOfJob.Clerical:
          return owNumbers[isMale][isPermanent][2];
        default:
          return owNumbers[isMale][isPermanent][3];
      }
    }

    /// <summary>Updates the daily work schedules for all workers.</summary>
    /// <param name="dTime">Date.</param>
    public void UpdateDailySchedule(DateTime dTime)
    { for (int i = 0; i < workers.Length; i++) workers[i].UpdateDailySchedule(dTime); }

    /// <summary>Updates the in-office presence state for all workers.</summary>
    /// <param name="dTime">Current date and time.</param>
    public void UpdateStatus(DateTime dTime)
    {
      for (int i = 0; i < workers.Length; i++)
        workers[i].UpdateStatus(dTime);

      StayWorkerNumber = 0;
      for (int i = 0; i < workers.Length; i++)
        if (workers[i].StayInOffice) StayWorkerNumber++;
    }

    /// <summary>Adds a special holiday.</summary>
    /// <param name="dTime">Holiday date (only the date portion is used).</param>
    public void AddSpecialHoliday(DateTime dTime)
    { specialHolidays.Add(dTime); }

    /// <summary>Gets the list of special holidays.</summary>
    /// <returns>List of special holidays.</returns>
    public DateTime[] GetSpecialHolidays()
    { return specialHolidays.ToArray(); }

    /// <summary>Clears all special holidays.</summary>
    public void ClearSpecialHolidays()
    { specialHolidays.Clear(); }

    /// <summary>Determines whether the specified date is a holiday.</summary>
    /// <param name="dTime">Date.</param>
    /// <returns>True if the specified date is a holiday.</returns>
    public bool IsHoliday(DateTime dTime)
    {
      //祝日チェック
      foreach (DateTime dt in specialHolidays)
        if (dt.Month == dTime.Month && dt.Day == dTime.Day) return true;

      //一般休日チェック
      switch (dTime.DayOfWeek)
      {
        case DayOfWeek.Saturday:
          return (Holidays & DaysOfWeek.Saturday) != 0;
        case DayOfWeek.Monday:
          return (Holidays & DaysOfWeek.Monday) != 0;
        case DayOfWeek.Tuesday:
          return (Holidays & DaysOfWeek.Tuesday) != 0;
        case DayOfWeek.Wednesday:
          return (Holidays & DaysOfWeek.Wednesday) != 0;
        case DayOfWeek.Thursday:
          return (Holidays & DaysOfWeek.Thursday) != 0;
        case DayOfWeek.Friday:
          return (Holidays & DaysOfWeek.Friday) != 0;
        default:
          return (Holidays & DaysOfWeek.Sunday) != 0;
      }
    }

    /// <summary>Determines whether the specified time is within business hours.</summary>
    /// <param name="dTime">Current date and time.</param>
    /// <returns>True if within business hours.</returns>
    public bool IsBuisinessHours(DateTime dTime)
    {
      //休日の場合は確定的にfalse
      if (IsHoliday(dTime)) return false;

      //平日の場合は始業終業時刻内かを確認
      return (StartHour <= dTime.Hour && StartMinute <= dTime.Minute) && (dTime.Hour <= EndHour && dTime.Minute <= EndMinute);
    }

    /// <summary>Resets the random seed for reproducibility.</summary>
    /// <param name="seed">Random seed.</param>
    public void ResetRandomSeed(uint seed)
    {
      uRnd = new MersenneTwister(seed);
      foreach (OfficeWorker wk in workers) wk.ResetRandomSeed((uint)uRnd.NextInt());
    }

    #endregion

  }

}
