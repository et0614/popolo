/* OfficeTenantTests.cs
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
using System.Reflection;
using Xunit;
using Popolo.Core.Numerics;
using Popolo.Core.OccupantBehavior;

namespace Popolo.Core.Tests.OccupantBehavior
{
  /// <summary>OfficeTenant / OfficeWorker のテスト</summary>
  public class OfficeTenantTests
  {
    #region Helpers

    /// <summary>始業 8:30・終業 17:15・昼休み 12:00〜13:00、土日休みのテナントを作る</summary>
    private static OfficeTenant MakeTenant()
      => new OfficeTenant(OfficeTenant.CategoryOfIndustry.Manufacturing, 200,
          OfficeTenant.DaysOfWeek.Saturday | OfficeTenant.DaysOfWeek.Sunday, 1,
          8, 30, 17, 15, 12, 0, 13, 0);

    /// <summary>非公開フィールドの値を取得する</summary>
    private static object GetPrivateField(object obj, string name)
      => obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(obj)!;

    /// <summary>昼休みの取り方（非公開列挙型 LunchBreakTake）を設定する</summary>
    private static void SetLunchBreak(OfficeTenant.OfficeWorker worker, int lunchBreakTake)
    {
      FieldInfo fi = typeof(OfficeTenant.OfficeWorker)
          .GetField("lunchBreak", BindingFlags.NonPublic | BindingFlags.Instance)!;
      fi.SetValue(worker, Enum.ToObject(fi.FieldType, lunchBreakTake));
    }

    #endregion

    #region IsBusinessHours

    /// <summary>
    /// 営業時間内の判定は時刻（時・分）で行う。旧実装は時と分を独立に比較していたため、
    /// 8:30〜17:15 の営業日に 10:00 や 16:45 を営業時間外と誤判定していた
    /// </summary>
    [Theory]
    [InlineData(8, 29, false)]
    [InlineData(8, 30, true)]   //始業時刻を含む
    [InlineData(10, 0, true)]   //旧実装では false
    [InlineData(12, 45, true)]
    [InlineData(16, 45, true)]  //旧実装では false
    [InlineData(17, 15, true)]  //終業時刻を含む
    [InlineData(17, 16, false)]
    [InlineData(20, 0, false)]
    [InlineData(3, 45, false)]
    public void IsBusinessHours_ComparesTimeOfDay(int hour, int minute, bool expected)
    {
      var tenant = MakeTenant();
      var t = new DateTime(2025, 2, 4, hour, minute, 0); //火曜日（祝日でない）
      Assert.Equal(expected, tenant.IsBusinessHours(t));
    }

    /// <summary>終業時刻の分内（秒単位の端数）は営業時間に含む（分単位の判定）</summary>
    [Fact]
    public void IsBusinessHours_EndMinuteInclusive()
    {
      var tenant = MakeTenant();
      Assert.True(tenant.IsBusinessHours(new DateTime(2025, 2, 4, 17, 15, 59)));
    }

    /// <summary>休日は常に営業時間外</summary>
    [Fact]
    public void IsBusinessHours_Holiday_IsFalse()
    {
      var tenant = MakeTenant();
      Assert.False(tenant.IsBusinessHours(new DateTime(2025, 2, 8, 10, 0, 0))); //土曜日
    }

    #endregion

    #region Lunch break

    /// <summary>
    /// 昼休みに「外出しない」(NeverGoesOut) 労働者は昼休みに外出しない。
    /// 旧実装は NeverGoesOut でも 48.8% の確率で外出していた
    /// </summary>
    [Fact]
    public void UpdateDailySchedule_NeverGoesOut_NeverLeavesForLunch()
    {
      var tenant = MakeTenant();
      var worker = new OfficeTenant.OfficeWorker(tenant, true, 35, true,
          OfficeTenant.OfficeWorker.CategoryOfJob.NoTitle, 0.8, new MersenneTwister(7));
      SetLunchBreak(worker, 1); //NeverGoesOut

      DateTime day = new DateTime(2025, 2, 3);
      for (int i = 0; i < 100; i++, day = day.AddDays(1))
      {
        worker.UpdateDailySchedule(day);
        var outGo = (DateTime)GetPrivateField(worker, "lnchOutGoTime");
        var comeBack = (DateTime)GetPrivateField(worker, "lnchComeBackTime");
        Assert.Equal(outGo, comeBack);
      }
    }

    /// <summary>昼休みに「ときどき外出」(SometimeGoesOut) 労働者は外出する日としない日がある</summary>
    [Fact]
    public void UpdateDailySchedule_SometimeGoesOut_SometimesLeavesForLunch()
    {
      var tenant = MakeTenant();
      var worker = new OfficeTenant.OfficeWorker(tenant, true, 35, true,
          OfficeTenant.OfficeWorker.CategoryOfJob.NoTitle, 0.8, new MersenneTwister(7));
      SetLunchBreak(worker, 2); //SometimeGoesOut

      int goOut = 0, stay = 0;
      DateTime day = new DateTime(2025, 2, 3);
      for (int i = 0; i < 200; i++, day = day.AddDays(1))
      {
        worker.UpdateDailySchedule(day);
        var outGo = (DateTime)GetPrivateField(worker, "lnchOutGoTime");
        if (outGo == worker.ArriveTime) stay++;
        else goOut++;
      }
      Assert.True(0 < goOut && 0 < stay, $"goOut={goOut}, stay={stay}");
    }

    #endregion

    #region Automatic job category

    /// <summary>
    /// 職位を自動決定するコンストラクタは性別・年齢階層ごとの管理職・役員比率に従う。
    /// 旧実装は（1）年齢階層を決める前の group（常に 0）を参照、（2）標準正規乱数を確率と比較、
    /// （3）比較の向きが逆、のため職位の分布が大きく歪んでいた
    /// </summary>
    [Theory]
    [InlineData(false, 25, 1 / 72d, 1 / 72d)]   //女性20代
    [InlineData(true, 55, 31 / 83d, 11 / 83d)]  //男性50代
    [InlineData(true, 65, 18 / 59d, 22 / 59d)]  //男性60代
    public void Constructor_AutoJob_FollowsCategoryRates(
        bool isMale, int age, double managerRate, double administratorRate)
    {
      var tenant = MakeTenant();
      const int n = 4000;
      int mng = 0, adm = 0;
      for (uint i = 0; i < n; i++)
      {
        var worker = new OfficeTenant.OfficeWorker(
            tenant, isMale, age, true, 0.8, new MersenneTwister(i + 1));
        Assert.Equal(age, worker.Age);
        Assert.Equal(isMale, worker.IsMale);
        if (worker.Job == OfficeTenant.OfficeWorker.CategoryOfJob.Manager) mng++;
        else if (worker.Job == OfficeTenant.OfficeWorker.CategoryOfJob.Administrator) adm++;
      }
      Assert.InRange(mng / (double)n, managerRate - 0.03, managerRate + 0.03);
      Assert.InRange(adm / (double)n, administratorRate - 0.03, administratorRate + 0.03);
    }

    #endregion
  }
}
