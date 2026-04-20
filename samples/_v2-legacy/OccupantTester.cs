using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;

using Popolo.Numerics;
using Popolo.ThermophysicalProperty;
using Popolo.BuildingOccupant;
using Popolo.HumanBody;
using System.Data.SqlTypes;

namespace PopoloTester
{
  class OccupantTester
  {

    #region 人体モデルテスト

    public static void TwoNodeModelTest()
    {
      double[] activity = new double[] { 45, 65, 100 };
      double[] opTemp = new double[] { 22, 24, 26, 28 };

      Console.WriteLine
        ("22CSH, 22CLH, 22CSET*, 24CSH, 24CLH, 24CSET*, 26CSH, 26CLH, 26CSET*, 28CSH, 28CLH, 28CSET*");
      double st, ct, bt, clt, ss, ls, sr, lr, wd;
      for (int i = 0; i < activity.Length; i++)
      {
        for (int j = 0; j < opTemp.Length; j++)
        {
          TwoNodeModel.GetSteadyState
            (opTemp[j], opTemp[j], 50, 0.3, 0.6, activity[i], 0,
            out st, out ct, out bt, out clt, out ss, out ls, out sr, out lr, out wd);
          double sh = (ss + sr) * 1.8;    //顕熱負荷[W]
          double lh = (ls + lr) * 1.8;    //潜熱負荷[W]
          double setstar = TwoNodeModel.GetSETStar(opTemp[j], activity[i], 0, clt, st, ss, ls, wd);

          Console.Write(sh.ToString("F1") + ", " + lh.ToString("F1") + ", " + setstar.ToString("F1") + ", ");
        }
        Console.WriteLine();
      }
    }

    public static void TwoNodeModelTest2()
    {
      //温度・湿度・着衣条件　0:夏季,1:冬季,2:中間期
      double[] dbt = new double[] { 26, 24, 22 };
      double[] hmd = new double[] { 50, 50, 40 };
      double[] clth = new double[] { 0.6, 0.8, 1.1 };

      for (int i = 0; i < 3; i++)
      {
        //標準条件のSET*
        double stnd = TwoNodeModel.GetSETStarFromAmbientCondition
          (dbt[i], dbt[i], hmd[i], 0.2, clth[i], 65, 0);
        for (int j = 30; j <= 100; j += 10)
        {
          Roots.ErrorFunction eFnc = delegate (double temp)
          {
            return stnd - TwoNodeModel.GetSETStarFromAmbientCondition(temp, temp, j, 0.2, clth[i], 65, 0);
          };
          double set2 = Roots.Newton(eFnc, 24, 0.0001, 0.0001, 0.0001, 20);
          Console.Write(", " + set2.ToString("F3"));
        }
        Console.WriteLine();
      }
    }

    public static void TwoNodeModelTest3()
    {
      int[] age = new int[] { 20, 30, 40, 50, 60 };
      double[] weight = new double[] { 45, 55, 65, 75, 85 };

      double st, ct, bt, clt, ss, ls, sr, lr, wd;
      for (int i = 0; i < age.Length; i++)
      {
        Console.Write(age[i]);
        for (int j = 0; j < weight.Length; j++)
        {
          TwoNodeModel.GetSteadyState(age[i], 1.7, weight[j], 26, 26, 50, 0.1, 0.6, 65, 0,
            out st, out ct, out bt, out clt, out ss, out ls, out sr, out lr, out wd);
          Console.Write(", " + ss.ToString("F4"));
        }
        Console.WriteLine();
      }
    }

    public static void TwoNodeModelTest4()
    {
      double[] AVE_HEIGHT = new double[] { 171.90, 172.04, 171.49, 170.31, 167.39, 158.56, 158.82, 158.67, 157.17, 154.38 };  //平均身長
      double[] SD_HEIGHT = new double[] { 5.64, 5.64, 5.65, 5.50, 5.35, 5.29, 5.10, 5.19, 5.04, 4.89 }; //身長標準偏差
      double[] AVE_WEIGHT = new double[] { 66.34, 68.19, 69.38, 68.14, 65.21, 50.60, 51.35, 52.71, 53.23, 52.24 };  //平均体重
      double[] SD_WEIGHT = new double[] { 9.23, 9.24, 9.45, 8.84, 8.01, 5.78, 6.02, 6.21, 6.58, 6.94 }; //体重標準偏差

      //身長体重初期化
      NormalRandom nRnd = new NormalRandom(1);
      TwoNodeModel[] mdls = new TwoNodeModel[1000];
      int indx = 0;
      for (uint age = 25; age < 70; age += 10)
      {
        int hwInd = 0;
        if (age < 30) hwInd += 0;
        else if (age < 40) hwInd += 1;
        else if (age < 50) hwInd += 2;
        else if (age < 60) hwInd += 3;
        else hwInd += 4;
        for (int i = 0; i < 100; i++) //男性100体
        {
          double height = Math.Round(nRnd.NextDouble() * SD_HEIGHT[hwInd] + AVE_HEIGHT[hwInd], 1);
          double weight = Math.Round(nRnd.NextDouble() * SD_WEIGHT[hwInd] + AVE_WEIGHT[hwInd], 1);
          mdls[indx] = new TwoNodeModel(age, true, 0.01 * height, weight);
          indx++;
        }
        hwInd += 5;
        for (int i = 0; i < 100; i++) //女性100体
        {
          double height = Math.Round(nRnd.NextDouble() * SD_HEIGHT[hwInd] + AVE_HEIGHT[hwInd], 1);
          double weight = Math.Round(nRnd.NextDouble() * SD_WEIGHT[hwInd] + AVE_WEIGHT[hwInd], 1);
          mdls[indx] = new TwoNodeModel(age, false, 0.01 * height, weight);
          indx++;
        }
      }

      using (StreamWriter sWriter = new StreamWriter("TwoNodeModelTest.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        for (int dbt = 10; dbt < 40; dbt++)
        {
          Console.WriteLine(dbt);

          double pmv = ThermalComfort.GetPMV(dbt, dbt, 50, 0.1, 0.9, 1.1, 0);
          double ppd = ThermalComfort.GetPPD(pmv);
          sWriter.Write(pmv + ", " + ppd);

          for (int j = 0; j < mdls.Length; j++)
          {
            mdls[j].UpdateState(3600, dbt, dbt, 50, 0.1, 0.9, 1.1, 0, 101.325);
            double tsv = -6.12
              + 35.86 * (0.5 + Math.Atan((mdls[j].SkinTemperature - 39.48) / 2.06) / Math.PI)
              + 3.69 * (0.5 + Math.Atan((0 + 0.04) / 0.17) / Math.PI);
            tsv += nRnd.NextDouble() * 0.64;
            sWriter.Write(", " + tsv);
          }

          sWriter.WriteLine();
        }
      }
    }

    public static void PMVTest()
    {
      //上下限値
      const int HMD_MAX = 70;
      const int HMD_MIN = 20;

      using (StreamWriter sWriter = new StreamWriter("PMVTest.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        //タイトル行
        for (int hmd = HMD_MIN; hmd <= HMD_MAX; hmd += 10) sWriter.Write("," + hmd.ToString("F0"));
        sWriter.WriteLine();

        sWriter.WriteLine("夏季");
        for (int mrt = 20; mrt <= 40; mrt++)
        {
          sWriter.Write(mrt.ToString("F0"));
          for (int hmd = HMD_MIN; hmd <= HMD_MAX; hmd += 10)
          {
            double pmv = ThermalComfort.GetDrybulbTemperature(0, mrt, hmd, 0.1, 0.65, 1.1, 0.0);
            sWriter.Write("," + pmv.ToString("F2"));
          }
          sWriter.WriteLine();
        }

        sWriter.WriteLine();
        sWriter.WriteLine("冬季");
        for (int mrt = 10; mrt <= 30; mrt++)
        {
          sWriter.Write(mrt.ToString("F0"));
          for (int hmd = HMD_MIN; hmd <= HMD_MAX; hmd += 10)
          {
            double pmv = ThermalComfort.GetDrybulbTemperature(0, mrt, hmd, 0.1, 0.80, 1.1, 0.0);
            sWriter.Write("," + pmv.ToString("F2"));
          }
          sWriter.WriteLine();
        }
      }
    }

    public static double estimateCLOValue
      (double tdb1, double trd1, double hrt1, double clo1, double tdb2, double trd2, double hrt2)
    {
      double rel1 = MoistAir.GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio(tdb1, hrt1, 101.325);
      double rel2 = MoistAir.GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio(tdb2, hrt2, 101.325);
      double pmv1 = ThermalComfort.GetPMV(tdb1, trd1, rel1, 0.2, clo1, 1.1, 0);

      Roots.ErrorFunction eFnc = delegate (double clo2)
      { return pmv1 - ThermalComfort.GetPMV(tdb2, trd2, rel2, 0.2, clo2, 1.1, 0); };

      return Roots.Bisection(eFnc, 0.01, 2.0, 1e-4, 1e-4, 50);
    }

    public static double estimateVelocity
      (double tdb1, double trd1, double hrt1, double clo1, double vel1, double tdb2, double trd2, double hrt2, double clo2)
    {
      double rel1 = MoistAir.GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio(tdb1, hrt1, 101.325);
      double rel2 = MoistAir.GetRelativeHumidityFromDryBulbTemperatureAndHumidityRatio(tdb2, hrt2, 101.325);
      double pmv1 = ThermalComfort.GetPMV(tdb1, trd1, rel1, vel1, clo1, 1.1, 0);

      Roots.ErrorFunction eFnc = delegate (double vel2)
      { return pmv1 - ThermalComfort.GetPMV(tdb2, trd2, rel2, vel2, clo2, 1.1, 0); };

      return Roots.Bisection(eFnc, 0.01, 2.0, 1e-4, 1e-4, 50);
    }

    public static void MultiNodeModelTest2()
    {
      //人体モデル作成
      MultiNodeModel mn = new MultiNodeModel();
      mn.UpdateBoundary(0, 28.8, 28.8, 50);
      mn.UpdateBoundary(MultiNodeModel.Node.RightLeg, 0, 40, 40, 50);
      for (int i = 0; i < 120; i++) mn.Update(600);

      //四肢末端部位
      const MultiNodeModel.Node TERMINAL_NODE =
        MultiNodeModel.Node.LeftHand | MultiNodeModel.Node.RightHand |
        MultiNodeModel.Node.LeftFoot | MultiNodeModel.Node.RightFoot;

      //四肢部位
      const MultiNodeModel.Node LIMBS = TERMINAL_NODE |
        MultiNodeModel.Node.LeftShoulder | MultiNodeModel.Node.RightShoulder |
        MultiNodeModel.Node.LeftArm | MultiNodeModel.Node.RightArm |
        MultiNodeModel.Node.LeftThigh | MultiNodeModel.Node.RightThigh |
        MultiNodeModel.Node.LeftLeg | MultiNodeModel.Node.RightLeg;

      using (StreamWriter sWriter =
        new StreamWriter("MultiNodeTest2.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        sWriter.WriteLine();

        foreach (MultiNodeModel.Node nd in Enum.GetValues(typeof(MultiNodeModel.Node)))
        {
          double tar = mn.GetTemperature(nd, MultiNodeModel.Layer.Artery);
          double tdv = mn.GetTemperature(nd, MultiNodeModel.Layer.DeepVein);
          double tcr = mn.GetTemperature(nd, MultiNodeModel.Layer.Core);
          double tms = mn.GetTemperature(nd, MultiNodeModel.Layer.Muscle);
          double tft = mn.GetTemperature(nd, MultiNodeModel.Layer.Fat);
          double tsk = mn.GetTemperature(nd, MultiNodeModel.Layer.Skin);
          double bfar = mn.GetBloodFlow(nd, MultiNodeModel.Layer.Artery);
          double bfdv = mn.GetBloodFlow(nd, MultiNodeModel.Layer.DeepVein);
          double bfcr = mn.GetBloodFlow(nd, MultiNodeModel.Layer.Core);
          double bfms = mn.GetBloodFlow(nd, MultiNodeModel.Layer.Muscle);
          double bfft = mn.GetBloodFlow(nd, MultiNodeModel.Layer.Fat);
          double bfsk = mn.GetBloodFlow(nd, MultiNodeModel.Layer.Skin);
          double bfava = mn.GetBloodFlow(nd, MultiNodeModel.Layer.AVA);
          double tsv, bfsv;
          if ((nd & LIMBS) == 0)
          {
            tsv = 0;
            bfsv = 0;
          }
          else
          {
            tsv = mn.GetTemperature(nd, MultiNodeModel.Layer.SuperficialVein);
            bfsv = mn.GetBloodFlow(nd, MultiNodeModel.Layer.SuperficialVein);
          }

          //一般情報******************************
          sWriter.WriteLine(nd.ToString() + "核層代謝量," + mn.GetMetabolicRate(nd, MultiNodeModel.Layer.Core));
          sWriter.WriteLine(nd.ToString() + "筋肉層代謝量," + mn.GetMetabolicRate(nd, MultiNodeModel.Layer.Muscle));
          sWriter.WriteLine(nd.ToString() + "脂肪層代謝量," + mn.GetMetabolicRate(nd, MultiNodeModel.Layer.Fat));
          sWriter.WriteLine(nd.ToString() + "皮膚層代謝量," + mn.GetMetabolicRate(nd, MultiNodeModel.Layer.Skin));
          sWriter.WriteLine(nd.ToString() + "核層温度," + tcr);
          sWriter.WriteLine(nd.ToString() + "筋肉層温度," + tms);
          sWriter.WriteLine(nd.ToString() + "脂肪層温度," + tft);
          sWriter.WriteLine(nd.ToString() + "皮膚層温度," + tsk);
          sWriter.WriteLine(nd.ToString() + "動脈温度," + tar);
          sWriter.WriteLine(nd.ToString() + "静脈温度," + tdv);
          sWriter.WriteLine(nd.ToString() + "表在静脈温度," + tsv);
          sWriter.WriteLine(nd.ToString() + "外界相当温度," + mn.GetOperatingTemperature(nd) + "," + mn.GetRelativeHumidity(nd));

          //部位内熱移動******************************
          double[] ht = new double[14];
          ht[0] = (tar - tdv) * mn.GetHeatConductance(nd, MultiNodeModel.Layer.Artery, MultiNodeModel.Layer.DeepVein);
          ht[1] = tar * bfava * MultiNodeModel.BLD_SPECIFICHEAT;
          ht[2] = (tar - tcr) * mn.GetHeatConductance(nd, MultiNodeModel.Layer.Artery, MultiNodeModel.Layer.Core) + tar * bfcr * MultiNodeModel.BLD_SPECIFICHEAT;
          ht[3] = tar * bfms * MultiNodeModel.BLD_SPECIFICHEAT;
          ht[4] = tar * bfft * MultiNodeModel.BLD_SPECIFICHEAT;
          ht[5] = tar * bfsk * MultiNodeModel.BLD_SPECIFICHEAT;
          ht[6] = tcr * bfcr * MultiNodeModel.BLD_SPECIFICHEAT + (tcr - tdv) * mn.GetHeatConductance(nd, MultiNodeModel.Layer.Core, MultiNodeModel.Layer.DeepVein);
          ht[7] = tms * bfms * MultiNodeModel.BLD_SPECIFICHEAT;
          ht[8] = tft * bfft * MultiNodeModel.BLD_SPECIFICHEAT;
          ht[9] = tsk * bfsk * MultiNodeModel.BLD_SPECIFICHEAT;
          ht[10] = (tsk - tsv) * mn.GetHeatConductance(nd, MultiNodeModel.Layer.Skin, MultiNodeModel.Layer.SuperficialVein);
          ht[11] = (tcr - tms) * mn.GetHeatConductance(nd, MultiNodeModel.Layer.Core, MultiNodeModel.Layer.Muscle);
          ht[12] = (tms - tft) * mn.GetHeatConductance(nd, MultiNodeModel.Layer.Muscle, MultiNodeModel.Layer.Fat);
          ht[13] = (tft - tsk) * mn.GetHeatConductance(nd, MultiNodeModel.Layer.Fat, MultiNodeModel.Layer.Skin);
          sWriter.WriteLine(nd.ToString() + "動脈→深部静脈熱移動," + ht[0]);
          sWriter.WriteLine(nd.ToString() + "動脈→表在静脈熱移動," + ht[1]);
          sWriter.WriteLine(nd.ToString() + "動脈→核層熱移動," + ht[2]);
          sWriter.WriteLine(nd.ToString() + "動脈→筋肉層熱移動," + ht[3]);
          sWriter.WriteLine(nd.ToString() + "動脈→脂肪層熱移動," + ht[4]);
          sWriter.WriteLine(nd.ToString() + "動脈→皮膚層熱移動," + ht[5]);
          sWriter.WriteLine(nd.ToString() + "核層→深部静脈熱移動," + ht[6]);
          sWriter.WriteLine(nd.ToString() + "筋肉層→深部静脈熱移動," + ht[7]);
          sWriter.WriteLine(nd.ToString() + "脂肪層→深部静脈熱移動," + ht[8]);
          sWriter.WriteLine(nd.ToString() + "皮膚層→深部静脈熱移動," + ht[9]);
          sWriter.WriteLine(nd.ToString() + "皮膚層→表在静脈熱移動," + ht[10]);
          sWriter.WriteLine(nd.ToString() + "核層→筋肉層熱移動," + ht[11]);
          sWriter.WriteLine(nd.ToString() + "筋肉層→脂肪層熱移動," + ht[12]);
          sWriter.WriteLine(nd.ToString() + "脂肪層→皮膚層熱移動," + ht[13]);
          sWriter.WriteLine(nd.ToString() + "皮膚層→外界熱移動," + (mn.GetSensibleHeatLoss(nd) + mn.GetLatentHeatLoss(nd)) + "," + mn.GetLatentHeatLoss(nd));

          //動脈による熱移動
          double hSum;
          double bf = (bfdv + bfsv);
          MultiNodeModel.Node up = MultiNodeModel.GetUpstreamNode(nd);
          if (up == 0) hSum = mn.CentralBloodTemperature * bf * MultiNodeModel.BLD_SPECIFICHEAT;
          else hSum = mn.GetTemperature(up, MultiNodeModel.Layer.Artery) * bf * MultiNodeModel.BLD_SPECIFICHEAT;
          sWriter.WriteLine(nd.ToString() + "上流動脈からの熱移動," + hSum);
          hSum = 0;
          hSum += tar * bfar * MultiNodeModel.BLD_SPECIFICHEAT;
          sWriter.WriteLine(nd.ToString() + "下流動脈への熱移動," + hSum);

          //深部静脈による熱移動
          hSum = tdv * bfdv * MultiNodeModel.BLD_SPECIFICHEAT;
          sWriter.WriteLine(nd.ToString() + "上流深部静脈への熱移動," + hSum);
          hSum = 0;
          MultiNodeModel.Node[] dn = MultiNodeModel.GetDownStreamNode(nd);
          foreach (MultiNodeModel.Node n in dn)
          {
            hSum += mn.GetTemperature(n, MultiNodeModel.Layer.DeepVein)
              * mn.GetBloodFlow(n, MultiNodeModel.Layer.DeepVein) * MultiNodeModel.BLD_SPECIFICHEAT;
          }
          sWriter.WriteLine(nd.ToString() + "下流深部静脈からの熱移動," + hSum);

          //表在静脈による熱移動
          if ((nd & LIMBS) != 0 || nd == MultiNodeModel.Node.Pelvis)
          {
            hSum = tsv * bfsv * MultiNodeModel.BLD_SPECIFICHEAT;
            sWriter.WriteLine(nd.ToString() + "上流表在静脈への熱移動," + hSum);
            hSum = 0;
            foreach (MultiNodeModel.Node n in dn) hSum += mn.GetTemperature(n, MultiNodeModel.Layer.SuperficialVein) * mn.GetBloodFlow(n, MultiNodeModel.Layer.SuperficialVein) * MultiNodeModel.BLD_SPECIFICHEAT;
            sWriter.WriteLine(nd.ToString() + "下流表在静脈からの熱移動," + hSum);
          }
          else
          {
            sWriter.WriteLine(nd.ToString() + "上流表在静脈への熱移動,0");
            sWriter.WriteLine(nd.ToString() + "下流表在静脈からの熱移動,0");
          }

          sWriter.WriteLine();
        }
        sWriter.WriteLine("呼吸による熱移動," + mn.HeatLossByBreathing);
        sWriter.WriteLine("中央血液溜まりの温度," + mn.CentralBloodTemperature);
      }
    }

    public static void MultiNodeModelTest(double operatingTemperature)
    {
      //人体モデル作成
      MultiNodeModel mn = new MultiNodeModel();
      mn.InitializeTemperature(35);
      mn.UpdateBoundary(0, operatingTemperature, operatingTemperature, 50);

      //四肢部位
      const MultiNodeModel.Node LIMBS =
        MultiNodeModel.Node.LeftHand | MultiNodeModel.Node.RightHand |
        MultiNodeModel.Node.LeftFoot | MultiNodeModel.Node.RightFoot |
        MultiNodeModel.Node.LeftShoulder | MultiNodeModel.Node.RightShoulder |
        MultiNodeModel.Node.LeftArm | MultiNodeModel.Node.RightArm |
        MultiNodeModel.Node.LeftThigh | MultiNodeModel.Node.RightThigh |
        MultiNodeModel.Node.LeftLeg | MultiNodeModel.Node.RightLeg;

      using (StreamWriter sWriter =
        new StreamWriter("MultiNodeTest.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        //タイトル行
        sWriter.Write("中央血液溜まり,");
        foreach (MultiNodeModel.Node nd in Enum.GetValues(typeof(MultiNodeModel.Node)))
          sWriter.Write(nd.ToString() + "核,筋肉,脂肪,皮膚,動脈,静脈,表在静脈,");
        sWriter.WriteLine();

        //時系列データ書き出し
        for (int i = 0; i < 600; i++)
        {
          sWriter.Write(mn.CentralBloodTemperature + ",");
          foreach (MultiNodeModel.Node nd in Enum.GetValues(typeof(MultiNodeModel.Node)))
          {
            sWriter.Write(mn.GetTemperature(nd, MultiNodeModel.Layer.Core) + ",");
            sWriter.Write(mn.GetTemperature(nd, MultiNodeModel.Layer.Muscle) + ",");
            sWriter.Write(mn.GetTemperature(nd, MultiNodeModel.Layer.Fat) + ",");
            sWriter.Write(mn.GetTemperature(nd, MultiNodeModel.Layer.Skin) + ",");
            sWriter.Write(mn.GetTemperature(nd, MultiNodeModel.Layer.Artery) + ",");
            sWriter.Write(mn.GetTemperature(nd, MultiNodeModel.Layer.DeepVein) + ",");
            if ((nd & LIMBS) == 0) sWriter.Write("0,");
            else sWriter.Write
              (mn.GetTemperature(nd, MultiNodeModel.Layer.SuperficialVein) + ",");
          }
          sWriter.WriteLine("");
          //状態更新
          mn.Update(60);
        }
      }
    }

    public static void MultiNodeModelTest3()
    {
      //人体モデル作成
      MultiNodeModel mn = new MultiNodeModel();
      mn.InitializeTemperature(35);

      //四肢部位
      const MultiNodeModel.Node LIMBS =
        MultiNodeModel.Node.LeftHand | MultiNodeModel.Node.RightHand |
        MultiNodeModel.Node.LeftFoot | MultiNodeModel.Node.RightFoot |
        MultiNodeModel.Node.LeftShoulder | MultiNodeModel.Node.RightShoulder |
        MultiNodeModel.Node.LeftArm | MultiNodeModel.Node.RightArm |
        MultiNodeModel.Node.LeftThigh | MultiNodeModel.Node.RightThigh |
        MultiNodeModel.Node.LeftLeg | MultiNodeModel.Node.RightLeg;

      using (StreamWriter sWriter =
        new StreamWriter("MultiNodeTest3.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        //タイトル行
        sWriter.Write("中央血液溜まり,");
        foreach (MultiNodeModel.Node nd in Enum.GetValues(typeof(MultiNodeModel.Node)))
          sWriter.Write(nd.ToString() + "核,筋肉,脂肪,皮膚,動脈,静脈,表在静脈,");
        sWriter.WriteLine();

        //時系列データ書き出し
        mn.UpdateBoundary(0, 15, 15, 50);
        for (int i = 0; i < 600; i++)
        {
          if (i == 300) mn.UpdateBoundary(0, 35, 35, 50);
          sWriter.Write(mn.CentralBloodTemperature + ",");
          foreach (MultiNodeModel.Node nd in Enum.GetValues(typeof(MultiNodeModel.Node)))
          {
            sWriter.Write(mn.GetTemperature(nd, MultiNodeModel.Layer.Core) + ",");
            sWriter.Write(mn.GetTemperature(nd, MultiNodeModel.Layer.Muscle) + ",");
            sWriter.Write(mn.GetTemperature(nd, MultiNodeModel.Layer.Fat) + ",");
            sWriter.Write(mn.GetTemperature(nd, MultiNodeModel.Layer.Skin) + ",");
            sWriter.Write(mn.GetTemperature(nd, MultiNodeModel.Layer.Artery) + ",");
            sWriter.Write(mn.GetTemperature(nd, MultiNodeModel.Layer.DeepVein) + ",");
            if ((nd & LIMBS) == 0) sWriter.Write("0,");
            else sWriter.Write
              (mn.GetTemperature(nd, MultiNodeModel.Layer.SuperficialVein) + ",");
          }
          sWriter.WriteLine("");
          //状態更新
          mn.Update(60);
        }
      }
    }

    #endregion

    #region オフィスワーカーテスト

    public static void officeWorkerTest(int workerNumber, double maleRate, double partRate, uint seed)
    {
      MersenneTwister rnd = new MersenneTwister(seed);
      DateTime dt = new DateTime(1999, 1, 1, 0, 0, 0);
      OfficeTenant.DaysOfWeek holidays = OfficeTenant.DaysOfWeek.Saturday | OfficeTenant.DaysOfWeek.Sunday;
      OfficeTenant.Worker[] workers = new OfficeTenant.Worker[workerNumber];
      int mNum = (int)(workerNumber * maleRate);
      int m20Num = (int)(mNum * 0.08);
      int m30Num = (int)(mNum * 0.18);
      int m40Num = (int)(mNum * 0.24);
      int m50Num = (int)(mNum * 0.22);
      int m60Num = mNum - (m20Num + m30Num + m40Num + m50Num);
      int fNum = workerNumber - mNum;
      int f20Num = (int)(fNum * 0.08);
      int f30Num = (int)(fNum * 0.18);
      int f40Num = (int)(fNum * 0.24);
      int f50Num = (int)(fNum * 0.22);
      int f60Num = fNum - (f20Num + f30Num + f40Num + f50Num);
      int index = 0;
      double oRate = 0.8;

      OfficeTenant tenant = new OfficeTenant(OfficeTenant.CategoryOfIndustry.Construction, 1, holidays, rnd.Next(), 9, 0, 18, 0, 12, 0, 13, 0);

      for (int i = 0; i < m20Num; i++)
      {
        workers[index] = new OfficeTenant.Worker(tenant, true, 25, partRate < rnd.NextDouble(), oRate, rnd);
        index++;
      }
      for (int i = 0; i < m30Num; i++)
      {
        workers[index] = new OfficeTenant.Worker(tenant, true, 35, partRate < rnd.NextDouble(), oRate, rnd);
        index++;
      }
      for (int i = 0; i < m40Num; i++)
      {
        workers[index] = new OfficeTenant.Worker(tenant, true, 45, partRate < rnd.NextDouble(), oRate, rnd);
        index++;
      }
      for (int i = 0; i < m50Num; i++)
      {
        workers[index] = new OfficeTenant.Worker(tenant, true, 55, partRate < rnd.NextDouble(), oRate, rnd);
        index++;
      }
      for (int i = 0; i < m60Num; i++)
      {
        workers[index] = new OfficeTenant.Worker(tenant, true, 65, partRate < rnd.NextDouble(), oRate, rnd);
        index++;
      }
      for (int i = 0; i < f20Num; i++)
      {
        workers[index] = new OfficeTenant.Worker(tenant, false, 25, partRate < rnd.NextDouble(), oRate, rnd);
        index++;
      }
      for (int i = 0; i < f30Num; i++)
      {
        workers[index] = new OfficeTenant.Worker(tenant, false, 35, partRate < rnd.NextDouble(), oRate, rnd);
        index++;
      }
      for (int i = 0; i < f40Num; i++)
      {
        workers[index] = new OfficeTenant.Worker(tenant, false, 45, partRate < rnd.NextDouble(), oRate, rnd);
        index++;
      }
      for (int i = 0; i < f50Num; i++)
      {
        workers[index] = new OfficeTenant.Worker(tenant, false, 55, partRate < rnd.NextDouble(), oRate, rnd);
        index++;
      }
      for (int i = 0; i < f60Num; i++)
      {
        workers[index] = new OfficeTenant.Worker(tenant, false, 65, partRate < rnd.NextDouble(), oRate, rnd);
        index++;
      }

      using (StreamWriter sWriter = new StreamWriter("officeWorkerTest.csv"))
      {
        sWriter.Write(",");
        for (int i = 0; i < 288; i++) sWriter.Write("," + dt.AddMinutes(i * 5).ToShortTimeString());
        sWriter.WriteLine();
        while (dt.Year != 2000)
        {
          if (dt.Hour == 0 && dt.Minute == 0)
          {
            sWriter.Write(dt.ToShortDateString() + "," + dt.DayOfWeek);
            for (int j = 0; j < workers.Length; j++) workers[j].UpdateDailySchedule(dt);
          }
          int stay = 0;
          for (int j = 0; j < workers.Length; j++)
          {
            workers[j].UpdateStatus(dt);
            if (workers[j].StayInOffice) stay++;
          }
          sWriter.Write("," + ((double)stay / workerNumber));
          dt = dt.AddMinutes(5);
          if (dt.Hour == 0 && dt.Minute == 0) sWriter.WriteLine();
        }
      }
    }

    #endregion

    #region 個人差温冷感モデルテスト

    /// <summary>1人の執務者が確率的にどのように不満を表明するか</summary>
    public static void TestOccupantWithThermalPreferences1()
    {
      OccupantWithThermalPreference ocp = new OccupantWithThermalPreference(0, 0);
      for (int i = 0; i < 41; i++)
      {
        double pmv = -2 + 0.1 * i;
        ocp.SetPMV(pmv);

        int numC = 0;
        int numH = 0;
        int numN = 0;
        for (int j = 0; j < 1000; j++)
        {
          OccupantWithThermalPreference.ThermalSensation ss = ocp.UpdateThermalSensationVote();
          if (ss == OccupantWithThermalPreference.ThermalSensation.Hot) numH++;
          else if (ss == OccupantWithThermalPreference.ThermalSensation.Cold) numC++;
          else numN++;
        }

        Console.WriteLine(
          pmv.ToString("F1") + 
          ", " +
          ocp.DissatisfiedProbability_Cold.ToString("F2") +
          ", " +
          ocp.DissatisfiedProbability_Hot.ToString("F2") +
          ", " +
          numC.ToString("F0") +
          ", " +
          numH.ToString("F0") +
          ", " +
          numN.ToString("F0")
          );
      }
    }

    /// <summary>1万人の執務者の不満足者率がPMVに一致するか</summary>
    public static void TestOccupantWithThermalPreferences2()
    {
      OccupantWithThermalPreference[] ocps = new OccupantWithThermalPreference[10000];
      MersenneTwister rnd = new MersenneTwister(0);
      for (int i = 0; i < ocps.Length; i++) 
        ocps[i] = new OccupantWithThermalPreference((uint)(10000 * rnd.NextDouble()));

      for (int i = 0; i < 41; i++)
      {
        double pmv = -2 + 0.1 * i;

        int numC = 0;
        int numH = 0;
        int numN = 0;
        for (int j = 0; j < ocps.Length; j++)
        {
          ocps[j].SetPMV(pmv);
          OccupantWithThermalPreference.ThermalSensation ss = ocps[j].UpdateThermalSensationVote();
          if (ss == OccupantWithThermalPreference.ThermalSensation.Hot) numH++;
          else if (ss == OccupantWithThermalPreference.ThermalSensation.Cold) numC++;
          else numN++;
        }

        Console.WriteLine(
          pmv.ToString("F1") +
          ", " +
          ThermalComfort.GetPPD(pmv).ToString("F1") + 
          ", " +
          numC.ToString("F0") +
          ", " +
          numH.ToString("F0") +
          ", " +
          numN.ToString("F0")
          ); ;
      }
    }

    #endregion

    #region Langevinモデルテスト

    public static void TestLangevinASHVote()
    {
      double[] pmvs = new double[] { -3.0, -2.5, -2.0, -1.5, -1.0, -0.5, 0.0, 0.5, 1.0, 1.5, 2.0, 2.5, 3.0 };

      //HVACあり
      for (int i = 0; i < pmvs.Length; i++)
      {
        Console.Write(pmvs[i]);
        double[] voteD = OccupantModel_Langevin.GetVoteDistribution(pmvs[i], true);
        for (int j = 0; j < voteD.Length; j++)
          Console.Write(", " + voteD[j].ToString("F5"));
        Console.WriteLine();
      }
      
      Console.WriteLine();
      //HVACなし
      for (int i = 0; i < pmvs.Length; i++)
      {
        Console.Write(pmvs[i]);
        double[] voteD = OccupantModel_Langevin.GetVoteDistribution(pmvs[i], false);
        for (int j = 0; j < voteD.Length; j++)
          Console.Write(", " + voteD[j].ToString("F5"));
        Console.WriteLine();
      }
    }

    public static void TestUnAcceptableRate()
    {
      double[] votes = new double[] { -3.0, -2.5, -2.0, -1.5, -1.0, -0.5, 0.0, 0.5, 1.0, 1.5, 2.0, 2.5, 3.0 };

      Console.WriteLine("HVAC_Sum,HVAC_Win,NVNT");
      for (int i = 0; i < votes.Length; i++)
      {
        Console.Write(votes[i] + ",");
        Console.Write((1 - OccupantModel_Langevin.GetAcceptableRateFromVote(votes[i], true, true)).ToString("F4") + ",");
        Console.Write((1 - OccupantModel_Langevin.GetAcceptableRateFromVote(votes[i], true, false)).ToString("F4") + ",");
        Console.Write((1 - OccupantModel_Langevin.GetAcceptableRateFromVote(votes[i], false, false)).ToString("F4"));
        Console.WriteLine();
      }
    }

    public static void TestPersonalUnAcceptableRate()
    {
      double[] pmvs = new double[] { -3.0, -2.5, -2.0, -1.5, -1.0, -0.5, 0.0, 0.5, 1.0, 1.5, 2.0, 2.5, 3.0 };
      OccupantModel_Langevin occupant = new OccupantModel_Langevin(2, true);

      Console.WriteLine("Uncomfortably Cold, Uncomfortably Warm, Uncomfortable");
      for (int i = 0; i < pmvs.Length; i++)
      {
        occupant.Update(pmvs[i]);

        Console.Write(pmvs[i].ToString("F1") + ", ");
        Console.Write(occupant.UncomfortablyColdProbability.ToString("F3") + ", ");
        Console.Write(occupant.UncomfortablyWarmProbability.ToString("F3") + ", ");
        Console.Write(occupant.UncomfortableProbability.ToString("F3"));
        Console.WriteLine();
      }
    }

    public static void TestPPDCurveWithLangevinModel()
    {
      MersenneTwister rnd = new MersenneTwister(1);
      OccupantModel_Langevin[] occupants = new OccupantModel_Langevin[1000];
      int a1, a2, a3, a4, a5, a6, a7;
      a1 = a2 = a3 = a4 = a5 = a6 = a7 = 0;
      for (int i = 0; i < occupants.Length; i++)
      {
        occupants[i] = new OccupantModel_Langevin((uint)rnd.NextInt(), false);
        a1 += (occupants[i].LowAcceptableSensationInWinter > -3 || -3 > occupants[i].HighAcceptableSensationInWinter) ? 1 : 0;
        a2 += (occupants[i].LowAcceptableSensationInWinter > -2 || -2 > occupants[i].HighAcceptableSensationInWinter) ? 1 : 0;
        a3 += (occupants[i].LowAcceptableSensationInWinter > -1 || -1 > occupants[i].HighAcceptableSensationInWinter) ? 1 : 0;
        a4 += (occupants[i].LowAcceptableSensationInWinter > 0 || 0 > occupants[i].HighAcceptableSensationInWinter) ? 1 : 0;
        a5 += (occupants[i].LowAcceptableSensationInWinter > 1 || 1 > occupants[i].HighAcceptableSensationInWinter) ? 1 : 0;
        a6 += (occupants[i].LowAcceptableSensationInWinter > 2 || 2 > occupants[i].HighAcceptableSensationInWinter) ? 1 : 0;
        a7 += (occupants[i].LowAcceptableSensationInWinter > 3 || 3 > occupants[i].HighAcceptableSensationInWinter) ? 1 : 0;
      }

      double[] pmvs = new double[] { -4.0, -3.5, -3.0, -2.5, -2.0, -1.5, -1.0, -0.5, 0.0, 0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 3.5, 4.0 };
      //double[] pmvs = new double[] { -8, -7, -6, -5, -4, -3, -2, -1, 0, 1, 2, 3, 4, 5, 6, 7, 8 };

      for (int i = 0; i < pmvs.Length; i++)
      {
        Console.Write(pmvs[i].ToString("F1") + ", ");

        int disCount = 0;
        int votCount = 0;
        for (int j = 0; j < occupants.Length; j++)
        {
          occupants[j].Update(pmvs[i]);
          votCount += (int)occupants[j].Vote;
          disCount += occupants[j].Comfortable ? 0 : 1;
        }
        Console.WriteLine((0.001 * votCount).ToString("F3") + ", " + (0.001 * disCount).ToString("F3"));
      }
    }

    #endregion

  }
}
