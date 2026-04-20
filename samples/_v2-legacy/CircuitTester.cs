using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;

using Popolo.HVAC.Circuit;
using Popolo.HVAC.Circuit.ControllableFlowSolver;

namespace PopoloTester
{
  class CircuitTester
  {

    #region 回路網テスト

    public static void PipeFlowTest()
    {
      //レイノルズ数[-]
      double[] rn = new double[6 * 9];
      for (int i = 0; i < 6; i++)
        for (int j = 0; j < 9; j++)
          rn[i * 9 + j] = (j + 1) * 100 * Math.Pow(10, i);

      //相対粗度[-]
      double[] rf = new double[]
      { 1e-6, 5e-6, 1e-5, 5e-5, 1e-4, 2e-4, 4e-4, 6e-4, 8e-4,
        1e-3, 2e-3, 4e-3, 6e-3, 8e-3, 0.01, 0.015, 0.02, 0.03, 0.04, 0.05 };

      using (StreamWriter sWriter =
        new StreamWriter("PipeFlowTest.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        for (int i = 0; i < rf.Length; i++) sWriter.Write("," + rf[i]);
        sWriter.WriteLine();
        for (int i = 0; i < rn.Length; i++)
        {
          sWriter.Write(rn[i]);
          for (int j = 0; j < rf.Length; j++)
          {
            double fc = Conduit.GetFrictionFactor(rn[i], rf[j]);
            sWriter.Write("," + fc);
          }
          sWriter.WriteLine();
        }
      }
    }

    public static void CircuitNetworkTest1()
    {
      CircuitNetwork cnetUStrm, cnetDStrm;
      makePumpNetwork1(out cnetUStrm, out cnetDStrm);
      CircuitNode[] nodesUStrm = cnetUStrm.Nodes;
      CircuitNode[] nodesDStrm = cnetDStrm.Nodes;

      //吐出圧一定制御////////////////////////
      //上流側の計算
      cnetUStrm.SetBasePressure(nodesUStrm[0], 250);
      cnetUStrm.Solve();
      //下流側の計算
      cnetDStrm.SetBasePressure(nodesDStrm[0], 0);
      cnetDStrm.Solve();
      //出力
      using (StreamWriter sWriter = new StreamWriter
        ("CircuitNetworkTest1.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        sWriter.WriteLine("節点, 01, 02, 05, 08, 11, 03, 06, 09, 12, 00, 04, 07, 10, 13");
        sWriter.Write("吐出圧一定制御:圧力[kPa]");
        for (int i = 0; i < nodesUStrm.Length; i++) sWriter.Write("," + nodesUStrm[i].Pressure);
        for (int i = 0; i < nodesDStrm.Length; i++) sWriter.Write("," + nodesDStrm[i].Pressure);
        sWriter.WriteLine();
      }

      //末端差圧一定制御////////////////////////
      //下流側の計算
      cnetDStrm.SetBasePressure(nodesDStrm[0], 0);
      cnetDStrm.Solve();
      //上流側の計算
      cnetUStrm.SetBasePressure(nodesUStrm[4], nodesDStrm[4].Pressure + 160);
      cnetUStrm.Solve();
      //出力
      using (StreamWriter sWriter = new StreamWriter
        ("CircuitNetworkTest1.csv", true, Encoding.GetEncoding("Shift_JIS")))
      {
        sWriter.Write("末端差圧一定制御:圧力[kPa]");
        for (int i = 0; i < nodesUStrm.Length; i++) sWriter.Write("," + nodesUStrm[i].Pressure);
        for (int i = 0; i < nodesDStrm.Length; i++) sWriter.Write("," + nodesDStrm[i].Pressure);
      }
    }

    public static void CircuitNetworkTest2()
    {
      CircuitNetwork cnetUStrm, cnetDStrm, cnetDDC;
      makePumpNetwork1(out cnetUStrm, out cnetDStrm);
      makePumpNetwork2(out cnetDDC);
      CentrifugalPump pump = new CentrifugalPump
        (260, 0.03, 250, 0.03, CentrifugalPump.ControlMethod.ConstantPressureWithInverter, 40);
      PumpSystem ps = new PumpSystem(pump, 250, 0.09, 40, 3);
      double[] flows = new double[4];

      using (StreamWriter sWriter = new StreamWriter
        ("CircuitNetworkTest2.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        sWriter.WriteLine("負荷率, ポンプ揚程[kPa], 消費電力[kW]");
        sWriter.WriteLine("負荷率均等低減");
        for (int i = 0; i <= 30; i++)
        {
          //負荷率を均等に低減
          double pl = 1.0 - 0.03 * i;
          sWriter.Write(pl);
          ps.TotalFlowRate = pl * 0.09;
          for (int j = 0; j < flows.Length; j++) flows[j] = pl * 0.0225;
          CircuitNetworkTest2_DDCCntrl(cnetUStrm, cnetDStrm, cnetDDC, flows);
          ps.PressureSetpoint = cnetDDC.Nodes[1].Pressure;
          ps.UpdateState();
          sWriter.WriteLine(", " + cnetDDC.Nodes[1].Pressure + ", " + ps.GetElectricConsumption());
        }
        sWriter.WriteLine("負荷率不均等低減1");
        for (int i = 0; i <= 30; i++)
        {
          //手前側のAHUから負荷を低減
          double pl = 4 * 0.03 * i;
          sWriter.Write(1 - pl / 4);
          ps.TotalFlowRate = (1 - pl / 4) * 0.09;
          for (int j = 0; j < 4; j++)
          {
            flows[j] = Math.Max(0.1, 1 - pl);
            pl = pl - (1 - flows[j]);
            flows[j] *= 0.0225;
          }
          CircuitNetworkTest2_DDCCntrl(cnetUStrm, cnetDStrm, cnetDDC, flows);
          ps.PressureSetpoint = cnetDDC.Nodes[1].Pressure;
          ps.UpdateState();
          sWriter.WriteLine(", " + cnetDDC.Nodes[1].Pressure + ", " + ps.GetElectricConsumption());
        }
        sWriter.WriteLine("負荷率不均等低減2");
        for (int i = 0; i <= 30; i++)
        {
          //末端側のAHUから負荷を低減
          double pl = 4 * 0.03 * i;
          sWriter.Write(1 - pl / 4);
          ps.TotalFlowRate = (1 - pl / 4) * 0.09;
          for (int j = 3; 0 <= j; j--)
          {
            flows[j] = Math.Max(0.1, 1 - pl);
            pl = pl - (1 - flows[j]);
            flows[j] *= 0.0225;
          }
          CircuitNetworkTest2_DDCCntrl(cnetUStrm, cnetDStrm, cnetDDC, flows);
          ps.PressureSetpoint = cnetDDC.Nodes[1].Pressure;
          ps.UpdateState();
          sWriter.WriteLine(", " + cnetDDC.Nodes[1].Pressure + ", " + ps.GetElectricConsumption());
        }
      }
    }

    public static void CircuitNetworkTest2_DDCCntrl
      (CircuitNetwork cnetUStrm, CircuitNetwork cnetDStrm, CircuitNetwork cnetDDC, double[] flows)
    {
      CircuitNode[] ndUStrm = cnetUStrm.Nodes;
      CircuitNode[] ndDStrm = cnetDStrm.Nodes;
      CircuitNode[] ndDDC = cnetDDC.Nodes;
      CircuitNode[] ndBND_CNST = new CircuitNode[]
      { ndUStrm[5], ndUStrm[6], ndUStrm[7], ndUStrm[8], ndDStrm[1], ndDStrm[2], ndDStrm[3], ndDStrm[4] };
      CircuitNode[] ndBND_DDC = new CircuitNode[]
      { ndDDC[3], ndDDC[6], ndDDC[9], ndDDC[12], ndDDC[4], ndDDC[7], ndDDC[10], ndDDC[13] };
      Regulator reg = new Regulator(340, 100, 0.5);

      //AHUの流量を設定
      double sum = 0;
      for (int i = 0; i < 4; i++)
      {
        sum += flows[i];
        ndBND_CNST[i].Inflow = ndBND_DDC[i].Inflow = -flows[i];
        ndBND_CNST[i + 4].Inflow = ndBND_DDC[i + 4].Inflow = flows[i];
      }
      ndUStrm[0].Inflow = ndDDC[1].Inflow = sum;
      ndDStrm[0].Inflow = ndDDC[0].Inflow = -sum;

      //吐出圧一定制御で計算
      cnetUStrm.SetBasePressure(ndUStrm[0], 250);
      cnetDStrm.SetBasePressure(ndDStrm[0], 0);
      cnetUStrm.Solve();
      cnetDStrm.Solve();

      //二方弁開度を計算//最大負荷系統を特定
      int index = 0;
      double max = 0;
      for (int i = 0; i < 4; i++)
      {
        reg.UpdateLift(ndBND_CNST[i].Pressure - ndBND_CNST[i + 4].Pressure);
        if (max < reg.Lift)
        {
          index = i;
          max = reg.Lift;
        }
      }

      //最大負荷系統に開度100%の二方弁を設定して計算
      reg.Lift = 1.0;
      cnetDDC.ConnectNode(reg, ndBND_DDC[index], ndBND_DDC[index + 4]);
      ndBND_DDC[index].Inflow = ndBND_DDC[index + 4].Inflow = 0;
      cnetDDC.SetBasePressure(ndDDC[0], 0);
      cnetDDC.Solve();
      cnetDDC.RemoveBranch(reg);
    }

    public static void makePumpNetwork1
      (out CircuitNetwork cnetUStrm, out CircuitNetwork cnetDStrm)
    {
      double lcf = 1.5; //局部抵抗係数

      //上流型回路
      cnetUStrm = new CircuitNetwork();
      WaterPipe ch01_02 = new WaterPipe(30 * lcf, 0.150, WaterPipe.Material.CarbonSteel);
      WaterPipe ch02_05 = new WaterPipe(5 * lcf, 0.100, WaterPipe.Material.CarbonSteel);
      WaterPipe ch01_08 = new WaterPipe(70 * lcf, 0.150, WaterPipe.Material.CarbonSteel);
      WaterPipe ch08_11 = new WaterPipe(5 * lcf, 0.100, WaterPipe.Material.CarbonSteel);
      SimpleCircuitBranch ch02_03_ahu1 = new SimpleCircuitBranch(0.0225, 150);
      SimpleCircuitBranch ch05_06_ahu2 = new SimpleCircuitBranch(0.0225, 150);
      SimpleCircuitBranch ch08_09_ahu3 = new SimpleCircuitBranch(0.0225, 150);
      SimpleCircuitBranch ch11_12_ahu4 = new SimpleCircuitBranch(0.0225, 150);
      CircuitNode[] nodesUStrm = new CircuitNode[9]; //01, 02, 05, 08, 11, 03, 06, 09, 12の順
      for (int i = 0; i < nodesUStrm.Length; i++) nodesUStrm[i] = cnetUStrm.AddNode();
      cnetUStrm.ConnectNode(ch01_02, nodesUStrm[0], nodesUStrm[1]);
      cnetUStrm.ConnectNode(ch02_05, nodesUStrm[1], nodesUStrm[2]);
      cnetUStrm.ConnectNode(ch01_08, nodesUStrm[0], nodesUStrm[3]);
      cnetUStrm.ConnectNode(ch08_11, nodesUStrm[3], nodesUStrm[4]);
      cnetUStrm.ConnectNode(ch02_03_ahu1, nodesUStrm[1], nodesUStrm[5]);
      cnetUStrm.ConnectNode(ch05_06_ahu2, nodesUStrm[2], nodesUStrm[6]);
      cnetUStrm.ConnectNode(ch08_09_ahu3, nodesUStrm[3], nodesUStrm[7]);
      cnetUStrm.ConnectNode(ch11_12_ahu4, nodesUStrm[4], nodesUStrm[8]);
      //境界条件, 初期値設定     
      nodesUStrm[0].Inflow = 0.0225 * 4;  //流入量
      for (int i = 5; i < 9; i++) nodesUStrm[i].Inflow = -0.0225; //流出量
      for (int i = 0; i < nodesUStrm.Length; i++) nodesUStrm[i].Pressure = 0;

      //下流側回路
      cnetDStrm = new CircuitNetwork();
      WaterPipe ch04_00 = new WaterPipe(30 * lcf, 0.150, WaterPipe.Material.CarbonSteel);
      WaterPipe ch07_04 = new WaterPipe(5 * lcf, 0.100, WaterPipe.Material.CarbonSteel);
      WaterPipe ch10_00 = new WaterPipe(70 * lcf, 0.150, WaterPipe.Material.CarbonSteel);
      WaterPipe ch13_10 = new WaterPipe(5 * lcf, 0.100, WaterPipe.Material.CarbonSteel);
      CircuitNode[] nodesDStrm = new CircuitNode[5]; //00, 04, 07, 10, 13の順
      for (int i = 0; i < nodesDStrm.Length; i++) nodesDStrm[i] = cnetDStrm.AddNode();
      cnetDStrm.ConnectNode(ch04_00, nodesDStrm[1], nodesDStrm[0]);
      cnetDStrm.ConnectNode(ch07_04, nodesDStrm[2], nodesDStrm[1]);
      cnetDStrm.ConnectNode(ch10_00, nodesDStrm[3], nodesDStrm[0]);
      cnetDStrm.ConnectNode(ch13_10, nodesDStrm[4], nodesDStrm[3]);
      //境界条件, 初期値設定  
      for (int i = 1; i < 5; i++) nodesDStrm[i].Inflow = 0.0225; //流出量
      nodesDStrm[0].Inflow = -0.0225 * 4;  //流入量
      for (int i = 0; i < nodesUStrm.Length; i++) nodesUStrm[i].Pressure = 0;
    }

    public static void makePumpNetwork2(out CircuitNetwork cnetDDC)
    {
      double lcf = 1.5; //局部抵抗係数

      cnetDDC = new CircuitNetwork();
      WaterPipe ch01_02_DDC = new WaterPipe(30 * lcf, 0.150, WaterPipe.Material.CarbonSteel);
      WaterPipe ch02_05_DDC = new WaterPipe(5 * lcf, 0.100, WaterPipe.Material.CarbonSteel);
      WaterPipe ch01_08_DDC = new WaterPipe(70 * lcf, 0.150, WaterPipe.Material.CarbonSteel);
      WaterPipe ch08_11_DDC = new WaterPipe(5 * lcf, 0.100, WaterPipe.Material.CarbonSteel);
      WaterPipe ch04_00_DDC = new WaterPipe(30 * lcf, 0.150, WaterPipe.Material.CarbonSteel);
      WaterPipe ch07_04_DDC = new WaterPipe(5 * lcf, 0.100, WaterPipe.Material.CarbonSteel);
      WaterPipe ch10_00_DDC = new WaterPipe(70 * lcf, 0.150, WaterPipe.Material.CarbonSteel);
      WaterPipe ch13_10_DDC = new WaterPipe(5 * lcf, 0.100, WaterPipe.Material.CarbonSteel);
      SimpleCircuitBranch ch02_03_ahu1_DDC = new SimpleCircuitBranch(0.0225, 150);
      SimpleCircuitBranch ch05_06_ahu2_DDC = new SimpleCircuitBranch(0.0225, 150);
      SimpleCircuitBranch ch08_09_ahu3_DDC = new SimpleCircuitBranch(0.0225, 150);
      SimpleCircuitBranch ch11_12_ahu4_DDC = new SimpleCircuitBranch(0.0225, 150);
      CircuitNode[] nodesDDC = new CircuitNode[14];
      for (int i = 0; i < nodesDDC.Length; i++) nodesDDC[i] = cnetDDC.AddNode();
      cnetDDC.ConnectNode(ch01_02_DDC, nodesDDC[1], nodesDDC[2]);
      cnetDDC.ConnectNode(ch02_05_DDC, nodesDDC[2], nodesDDC[5]);
      cnetDDC.ConnectNode(ch01_08_DDC, nodesDDC[1], nodesDDC[8]);
      cnetDDC.ConnectNode(ch08_11_DDC, nodesDDC[8], nodesDDC[11]);
      cnetDDC.ConnectNode(ch04_00_DDC, nodesDDC[4], nodesDDC[0]);
      cnetDDC.ConnectNode(ch07_04_DDC, nodesDDC[7], nodesDDC[4]);
      cnetDDC.ConnectNode(ch10_00_DDC, nodesDDC[10], nodesDDC[0]);
      cnetDDC.ConnectNode(ch13_10_DDC, nodesDDC[13], nodesDDC[10]);
      cnetDDC.ConnectNode(ch02_03_ahu1_DDC, nodesDDC[2], nodesDDC[3]);
      cnetDDC.ConnectNode(ch05_06_ahu2_DDC, nodesDDC[5], nodesDDC[6]);
      cnetDDC.ConnectNode(ch08_09_ahu3_DDC, nodesDDC[8], nodesDDC[9]);
      cnetDDC.ConnectNode(ch11_12_ahu4_DDC, nodesDDC[11], nodesDDC[12]);
      cnetDDC.SetBasePressure(nodesDDC[0], 0);
    }

    #endregion

    #region 回路網テスト2（制御可能回路）

    public static void testControllableFlow()
    {
      ControllableParallelFlow[] floorPFlows = new ControllableParallelFlow[6];
      ControllableSeriesFlow[][] ahus = new ControllableSeriesFlow[6][];
      for (int i = 0; i < floorPFlows.Length; i++) ahus[i] = new ControllableSeriesFlow[4];
      ahus[0][0] = new ControllableSeriesFlow(1.121e6, 2.354e7);
      ahus[0][1] = new ControllableSeriesFlow(1.455e6, 3.055e7);
      ahus[0][2] = new ControllableSeriesFlow(2.324e6, 4.880e7);
      ahus[0][3] = new ControllableSeriesFlow(1.681e6, 3.530e7);
      ahus[1][0] = new ControllableSeriesFlow(1.121e6, 2.130e7);
      ahus[1][1] = new ControllableSeriesFlow(1.455e6, 2.764e7);
      ahus[1][2] = new ControllableSeriesFlow(2.324e6, 4.416e7);
      ahus[1][3] = new ControllableSeriesFlow(1.681e6, 3.193e7);
      ahus[2][0] = new ControllableSeriesFlow(1.121e6, 1.906e7);
      ahus[2][1] = new ControllableSeriesFlow(1.455e6, 2.473e7);
      ahus[2][2] = new ControllableSeriesFlow(2.324e6, 3.951e7);
      ahus[2][3] = new ControllableSeriesFlow(1.681e6, 2.857e7);
      ahus[3][0] = new ControllableSeriesFlow(1.121e6, 1.682e7);
      ahus[3][1] = new ControllableSeriesFlow(1.455e6, 2.182e7);
      ahus[3][2] = new ControllableSeriesFlow(2.324e6, 3.486e7);
      ahus[3][3] = new ControllableSeriesFlow(1.681e6, 2.521e7);
      ahus[4][0] = new ControllableSeriesFlow(1.121e6, 1.457e7);
      ahus[4][1] = new ControllableSeriesFlow(1.455e6, 1.891e7);
      ahus[4][2] = new ControllableSeriesFlow(2.324e6, 3.021e7);
      ahus[4][3] = new ControllableSeriesFlow(1.681e6, 2.185e7);
      ahus[5][0] = new ControllableSeriesFlow(1.121e6, 1.233e7);
      ahus[5][1] = new ControllableSeriesFlow(1.455e6, 1.600e7);
      ahus[5][2] = new ControllableSeriesFlow(2.324e6, 2.556e7);
      ahus[5][3] = new ControllableSeriesFlow(1.681e6, 1.849e7);
      for (int i = 0; i < floorPFlows.Length; i++)
        floorPFlows[i] = new ControllableParallelFlow(ahus[i]);

      ControllableParallelFlow totalSystem = new ControllableParallelFlow(floorPFlows);
      totalSystem.SupplyResistances[0] = totalSystem.ReturnResistances[0] = 2.440e4;
      totalSystem.SupplyResistances[1] = totalSystem.ReturnResistances[1] = 3.904e3;
      totalSystem.SupplyResistances[2] = totalSystem.ReturnResistances[2] = 6.100e3;
      totalSystem.SupplyResistances[3] = totalSystem.ReturnResistances[3] = 1.085e4;
      totalSystem.SupplyResistances[4] = totalSystem.ReturnResistances[4] = 2.440e4;
      totalSystem.SupplyResistances[5] = totalSystem.ReturnResistances[5] = 9.761e4;

      for (int i = 0; i < floorPFlows.Length; i++)
      {
        ahus[i][0].FlowRateSetPoint = 2.11e-3;
        ahus[i][1].FlowRateSetPoint = 1.85e-3;
        ahus[i][2].FlowRateSetPoint = 1.47e-3;
        ahus[i][3].FlowRateSetPoint = 1.72e-3;
      }
      for (int i = 1; i <= 10; i++)
      {
        double pl = 0.1 * i;
        ahus[5][0].FlowRateSetPoint = ahus[4][0].FlowRateSetPoint = ahus[3][0].FlowRateSetPoint = 2.11e-3 * pl;
        ahus[5][1].FlowRateSetPoint = ahus[4][1].FlowRateSetPoint = ahus[3][1].FlowRateSetPoint = 1.85e-3 * pl;
        ahus[5][2].FlowRateSetPoint = ahus[4][2].FlowRateSetPoint = ahus[3][2].FlowRateSetPoint = 1.47e-3 * pl;
        ahus[5][3].FlowRateSetPoint = ahus[4][3].FlowRateSetPoint = ahus[3][3].FlowRateSetPoint = 1.72e-3 * pl;
        Console.WriteLine(pl + ", " + totalSystem.GetMinimumPressure().ToString("F1"));
      }
      for (int i = 1; i <= 10; i++)
      {
        double pl = 0.1 * i;
        ahus[0][0].FlowRateSetPoint = ahus[1][0].FlowRateSetPoint = ahus[2][0].FlowRateSetPoint = 2.11e-3 * pl;
        ahus[0][1].FlowRateSetPoint = ahus[1][1].FlowRateSetPoint = ahus[2][1].FlowRateSetPoint = 1.85e-3 * pl;
        ahus[0][2].FlowRateSetPoint = ahus[1][2].FlowRateSetPoint = ahus[2][2].FlowRateSetPoint = 1.47e-3 * pl;
        ahus[0][3].FlowRateSetPoint = ahus[1][3].FlowRateSetPoint = ahus[2][3].FlowRateSetPoint = 1.72e-3 * pl;
        Console.WriteLine(pl + ", " + totalSystem.GetMinimumPressure().ToString("F1"));
      }
      for (int i = 1; i <= 10; i++)
      {
        double pl = 0.5 + 0.05 * i;
        for (int j = 0; j < ahus.Length; j++)
        {
          ahus[j][0].FlowRateSetPoint = 2.11e-3 * pl;
          ahus[j][1].FlowRateSetPoint = 1.85e-3 * pl;
          ahus[j][2].FlowRateSetPoint = 1.47e-3 * pl;
          ahus[j][3].FlowRateSetPoint = 1.72e-3 * pl;
        }
        Console.WriteLine(pl + ", " + totalSystem.GetMinimumPressure().ToString("F1"));
      }
      //totalSystem.ControlFlowRate(200);
    }

    #endregion

    #region 流体機械テスト

    public static void CentrifugalPumpTest()
    {
      CentrifugalPump p1 = new CentrifugalPump
        (260, 0.03, 250, 0.03, CentrifugalPump.ControlMethod.ConstantPressureWithBypass, 40);
      CentrifugalPump p2 = new CentrifugalPump
        (260, 0.03, 250, 0.03, CentrifugalPump.ControlMethod.ConstantPressureWithInverter, 40);
      CentrifugalPump p3 = new CentrifugalPump
        (260, 0.03, 250, 0.03, CentrifugalPump.ControlMethod.MinimumPressure, 40);
      PumpSystem[] pss = new PumpSystem[3];
      pss[0] = new PumpSystem(p1, 250, 0.09, 40, 3);
      pss[1] = new PumpSystem(p2, 250, 0.09, 40, 3);
      pss[2] = new PumpSystem(p3, 250, 0.09, 40, 3);

      using (StreamWriter sWriter =
          new StreamWriter("CentrifugalPumpTest.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        for (int i = 0; i < 216; i++)
        {
          double wf = (5400 - 25 * i) / 60d / 1000d;
          for (int j = 0; j < pss.Length; j++)
          {
            pss[j].TotalFlowRate = wf;
            pss[j].UpdateState();
          }

          sWriter.Write(wf * 60d * 1000);
          for (int j = 0; j < pss.Length; j++) sWriter.Write(", " + pss[j].GetElectricConsumption());
          for (int j = 0; j < pss.Length; j++) sWriter.Write(", " + pss[j].Pump.GetTotalEfficiency());
          for (int j = 0; j < pss.Length; j++) sWriter.Write(", " + pss[j].BypassFlowRate * (60 * 1000));
          sWriter.WriteLine();
        }
      }
    }

    public static void CentrifugalFanTest()
    {
      double nf = 13500d / 3600;
      double df = 13000d / 3600;
      CentrifugalFan f1 = new CentrifugalFan(0.55, nf, 0.53, df, 4, false);
      CentrifugalFan f2 = new CentrifugalFan(0.55, nf, 0.53, df, 4, true);

      using (StreamWriter sWriter =
          new StreamWriter("CentrifugalFanTest.csv", false, Encoding.GetEncoding("Shift_JIS")))
      {
        for (int i = 0; i < 100; i++)
        {
          double vf = (13000 - 130 * i) / 3600d;
          f1.UpdateState(vf);
          f2.UpdateState(vf);
          sWriter.WriteLine(vf * 3600d +
            ", " + f1.GetElectricConsumption() + ", " + f2.GetElectricConsumption() +
            ", " + f1.GetTotalEfficiency() + ", " + f2.GetTotalEfficiency());
        }
      }
    }

    #endregion

  }
}
