using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.Diagnostics;

using Popolo.Numerics;
using Popolo.Numerics.MatrixOperation;
using Popolo.Weather;

namespace PopoloTester
{
  class NumericsTester
  {

    public static void Test()
    {
      //連立一階微分方程式テスト
      TestODEs();

      //線形代数テスト
      FitAxPlusBTest();
      LUDecompositionTest();
      TridiagonalMatrixSolverTest();
      QRTest();

      //1変数非線形方程式テスト
      RootsTest();
      NewtonRaphsonTest();
      NewtonRaphsonTest2();
      GoldenSectionTest();

      //spline補間テスト
      SplineTest();

      //最小二乗法テスト
      LeastSquareTest();
      LeastSquareTest2();

      //非線形連立方程式テスト
      SimplexTest();
      SimplexTest2();
      LevenbergMarquardtTest();
      MultiMinimizationTest();
    }

    #region 連立一階微分方程式テスト

    public static void TestODEs()
    {
      const double DT = 0.1;
      ODESolver.DifferentialEquations deq = delegate (double t, double[] yt, ref double[] dyt) 
      {
        dyt[0] = yt[0] - 2 * yt[1];
        dyt[1] = yt[0] + 4 * yt[1];
      };

      double[] yt0 = new double[] { 1, -2 };
      double[] yt1 = new double[2];
      for (int i = 0; i < 10; i++)
      {
        double t = DT * i;

        //ルンゲクッタ・ギル法の解
        if (i % 2 == 0)
        {
          ODESolver.SolveRKGill(deq, DT, t, yt0, ref yt1);
          Console.Write(t.ToString("F1") + ", " + yt0[0].ToString("F4") + ", " + yt0[1].ToString("F4"));
        }
        else 
        {
          ODESolver.SolveRKGill(deq, DT, t, yt1, ref yt0);
          Console.Write(t.ToString("F1") + ", " + yt1[0].ToString("F4") + ", " + yt1[1].ToString("F4"));
        }

        //解析解を表示
        Console.WriteLine(
          ", " + (-2 * Math.Exp(2 * t) + 3 * Math.Exp(3 * t)).ToString("F4") + 
          ", " + (Math.Exp(2 * t) - 3 * Math.Exp(3 * t)).ToString("F4")
          );
      }
    }

    public static void TestODEs2()
    {
      const double DS = 0.05; //刻み幅は0.05
      const double TE_UP = 30; //天井面温度[C]
      const double TE_DN = 25; //床面温度[C]
      const double C_HEIGHT = 3.0; //天井高さ[m]
      const double B = 0.65;
      const double G = 9.8;
      const double DTDY = (TE_UP - TE_DN) / C_HEIGHT; //天井高3.0m
      const double KP = 4.8; //空調ハンドブックp.375, 天井線形吹出口の値

      //微分方程式
      double tm = 0;
      ODESolver.DifferentialEquations deq = delegate (double s, double[] yt, ref double[] dyt)
      {
        double mx = yt[0];
        double my = yt[1];
        double ht = yt[2];
        double x = yt[3];
        double y = yt[4];

        double te = TE_DN + y * DTDY;
        double sqmxy = Math.Sqrt(mx * mx + my * my);
        double sqmxy2 = Math.Sqrt(sqmxy);
        tm = te + ht / (s * sqmxy2);
        double beta = 1 / (0.5 * (tm + te) + 273.15);

        dyt[0] = 0;
        dyt[1] = 2 / B * G * beta * ht / sqmxy2 * s;
        dyt[2] = -(1 + B) * s * DTDY * my / sqmxy2;
        dyt[3] = mx / sqmxy;
        dyt[4] = my / sqmxy;
      };

      double sA = 0.1 * 0.8; //吹出面積 10cm x 80cm
      double dO = 2 * Math.Sqrt(sA / Math.PI); //等価直径[m]
      double s = 0.0; //0.1mの地点から開始
      double um = 1.0; //初速[m/s]
      //double q = 1000d / 3600d; //風量[m3/s]
      double tm0 = 40; //初期温度[C]
      double theta = -45 * Math.PI / 180d; //右下45度吹き出し
      double u2s2 = um * um * s * s;
      double mXY = KP * KP * um * um * dO * dO;

      double[] yt0 = new double[]
      {
        mXY * Math.Cos(theta), //mx0
        mXY * Math.Sin(theta), //my0
        0,   //ht0
        0,   //x0
        3.0  //y0
      };
      double[] yt1 = new double[5];
      tm = tm0;
      Console.WriteLine("s,  mx,  my,  ht,  x,  y,  tm,  um,  theta,  te");
      while(0.05 < um)
      {
        s += DS;

        Console.Write(s.ToString("F2"));
        for (int j = 0; j < yt0.Length; j++) Console.Write(", " + yt0[j].ToString("F3"));
        Console.WriteLine(", " + tm.ToString("F2") + ", " + um.ToString("F2") 
          + ", " + (theta / Math.PI * 180d).ToString("F1") + ", " + (TE_DN + yt0[4] * DTDY).ToString("F2"));

        ODESolver.SolveRKGill(deq, DS, s, yt0, ref yt1);
        for (int j = 0; j < yt0.Length; j++) yt0[j] = yt1[j];
        um = Math.Pow(yt0[0] * yt0[0] + yt0[1] * yt0[1], 0.25) / s;
        theta = Math.Atan(yt0[1] / yt0[0]);
      }
    }

    #endregion

    #region 線形代数テスト

    public static void FitAxPlusBTest()
    {
      double[] x = new double[] { 0.1, 0.3, 1.0 };
      double[] y = new double[] { 2, 4, 20 };
      double a, b;
      LinearAlgebra.FitAxPlusB(x, y, out a, out b);

      //エラー確認******************************************************
      if (a != 20.746268656716417 || b != -1.0149253731343275)
        throw new Exception("FitAxPlusBTest Error");
    }

    public static void LUDecompositionTest()
    {
      IMatrix matrix = new Matrix(5, 5); //行列
      IVector b = new Vector(5);        //解ベクトル
      int[] perm = new int[5];     //置換ベクトル
      IVector wArray = new Vector(5);   //作業用記憶領域

      //値を代入
      matrix[0, 0] = -28.9; matrix[0, 1] = 8.9; b[0] = -700;
      matrix[1, 0] = 8.9; matrix[1, 1] = -10.3; matrix[1, 2] = 1.4;
      matrix[2, 1] = 1.4; matrix[2, 2] = -15.7; matrix[2, 3] = 14.3;
      matrix[3, 2] = 14.3; matrix[3, 3] = -32.6; matrix[3, 4] = 18.3;
      matrix[4, 3] = 18.3; matrix[4, 4] = -27.3; b[4] = -234;

      LinearAlgebra.LUDecompose(matrix, perm, wArray);
      LinearAlgebra.FAndBSubstitute(matrix, perm, b);

      for (int i = 0; i < b.Length; i++)
        Console.WriteLine("t" + i.ToString() + " = " + b[i].ToString("F1"));

      //エラー確認******************************************************
      if (
        b[0] != 34.595444254464532 ||
        b[1] != 33.686330219553362 ||
        b[2] != 27.906962426189473 ||
        b[3] != 27.341150194671329 ||
        b[4] != 26.899012767856604)
        throw new Exception("LUDecompositionTest Error");
    }

    public static void TridiagonalMatrixSolverTest()
    {
      Matrix abc = new Matrix(3, 5);   //係数ベクトル
      IVector x = new Vector(5);         //解ベクトル

      abc[0, 0] = 0; abc[1, 0] = -28.9; abc[2, 0] = 8.9;
      abc[0, 1] = 8.9; abc[1, 1] = -10.3; abc[2, 1] = 1.4;
      abc[0, 2] = 1.4; abc[1, 2] = -15.7; abc[2, 2] = 14.3;
      abc[0, 3] = 14.3; abc[1, 3] = -32.6; abc[2, 3] = 18.3;
      abc[0, 4] = 18.3; abc[1, 4] = -27.3; abc[2, 4] = 0;
      x[0] = -700; x[1] = 0; x[2] = 0; x[3] = 0; x[4] = -234;

      LinearAlgebra.SolveTridiagonalMatrix(abc, x);
      for (int i = 0; i < x.Length; i++)
        Console.WriteLine("t" + i.ToString() + " = " + x[i].ToString("F1"));

      //エラー確認******************************************************
      if (
        x[0] != 34.595444254464525 ||
        x[1] != 33.686330219553355 ||
        x[2] != 27.906962426189473 ||
        x[3] != 27.341150194671329 ||
        x[4] != 26.899012767856604)
        throw new Exception("TridiagonalMatrixSolverTest Error");
    }

    public static void QRTest()
    {
      IMatrix mA = new Matrix(5, 5);
      mA[0, 0] = 0.6273; mA[0, 1] = 0.7683; mA[0, 2] = 0.5569; mA[0, 3] = 0.5571; mA[0, 4] = 0.3849;
      mA[1, 0] = 0.7683; mA[1, 1] = 0.3716; mA[1, 2] = 0.4683; mA[1, 3] = 0.7887; mA[1, 4] = 0.6153;
      mA[2, 0] = 0.5569; mA[2, 1] = 0.4683; mA[2, 2] = 0.7764; mA[2, 3] = 0.6480; mA[2, 4] = 0.2756;
      mA[3, 0] = 0.5571; mA[3, 1] = 0.7887; mA[3, 2] = 0.6480; mA[3, 3] = 0.7036; mA[3, 4] = 0.3125;
      mA[4, 0] = 0.3849; mA[4, 1] = 0.6153; mA[4, 2] = 0.2756; mA[4, 3] = 0.3125; mA[4, 4] = 0.5668;

      LinearAlgebra.MakeUpperTriangularMatrix(ref mA);
      for (int i = 0; i < mA.Rows; i++)
      {
        for (int j = 0; j < mA.Columns; j++)
          Console.Write(mA[i, j].ToString("F4") + " , ");
        Console.WriteLine();
      }

      //エラー確認******************************************************
      double[][] ans = new double[][]
      {
        new double[]{ -1.3237961361176425, -1.2875584340340815, -1.2151377512836674, -1.3812965607851724, -0.95174735416211687},
        new double[]{ 0, 0.53899109356992447, 0.15133476377364941, -0.012486693790510085, 0.043076940149720341 },
        new double[]{ 0, 0, -0.35865980354487109, -0.13435091875500288, 0.24491581554495295 },
        new double[]{ 0, 0, 0, -0.13724078938465178, 0.042983530405157767 },
        new double[]{ 0, 0, 0, 0, -0.22826730051106017 }
      };
      for (int i = 0; i < mA.Rows; i++)
        for (int j = 0; j < mA.Columns; j++)
          if (mA[i, j] != ans[i][j]) throw new Exception("QRTest Error");
    }

    #endregion

    #region 1変数非線形方程式テスト

    public static void RootsTest()
    {
      Stopwatch sw = new Stopwatch();
      double g;
      long millisec;

      sw.Start();
      for (int i = 0; i < 10000; i++) Roots.Bisection(fmw, 0, 100, 0.001, 0.00001, 20);
      g = Roots.Bisection(fmw, 0, 100, 0.001, 0.00001, 20);
      sw.Stop();
      millisec = sw.ElapsedMilliseconds;
      Console.WriteLine("二分法：");
      Console.WriteLine(millisec + "mSec");
      Console.WriteLine("G= " + g.ToString("F3"));

      //エラー確認******************************************************
      if (g != 66.604423522949219)
        throw new Exception("RootsTest Error");

      sw.Reset();
      sw.Start();
      for (int i = 0; i < 10000; i++) Roots.Brent(0, 100, 0.001, fmw);
      g = Roots.Brent(0, 100, 0.001, fmw);
      sw.Stop();
      millisec = sw.ElapsedMilliseconds;
      Console.WriteLine("Brent法：");
      Console.WriteLine(millisec + "mSec");
      Console.WriteLine("G= " + g.ToString("F3"));

      //エラー確認******************************************************
      if (g != 66.604635481894064)
        throw new Exception("RootsTest Error");
    }

    public static void NewtonRaphsonTest()
    {
      const int MAX_ITERATION = 100;      //最大反復回数
      const double DELTA = 0.0001;        //微分値計算用微小値
      const double ERR_TOLERANCE = 0.001; //最大許容誤差

      //mwの初期値は0とする
      double mw = 0;

      //誤差を計算
      double fmw1 = fmw(mw);

      //反復回数を記録
      int iterNumber = 0;

      //誤差が許容誤差未満になるまで反復計算
      while (ERR_TOLERANCE < Math.Abs(fmw1) && iterNumber < MAX_ITERATION)
      {
        //数値微分値の計算
        //mwを微小変動させて誤差を再計算
        double fmw2 = fmw(mw + DELTA);
        double fmwd = (fmw2 - fmw1) / DELTA;

        //微分値を利用してmwを更新
        mw -= fmw1 / fmwd;

        //誤差を再計算
        fmw1 = fmw(mw);
      }
      Console.WriteLine("mw= " + mw.ToString("F3"));

      //エラー確認******************************************************
      if (mw != 66.604368555164115)
        throw new Exception("NewtonRaphsonTest Error");
    }

    private static double fmw(double mw)
    {
      //ポンプのPQ特性
      double p1 = mw * (-0.314 + mw * (2.08e-3 + mw * (-4.9e-6 - 1.32e-6 * mw))) + 333;

      //配管の抵抗
      double p2 = mw * (0.567 + 4.66e-2 * mw) + 49.4;

      return p1 - p2;
    }

    public static void NewtonRaphsonTest2()
    {
      //誤差関数を匿名メソッドで定義
      Roots.ErrorFunction eFnc = delegate (double mw)
      {
        //ポンプのPQ特性
        double p1 = mw * (-0.314 + mw * (2.08e-3 + mw * (-4.9e-6 - 1.32e-6 * mw))) + 333;
        //配管の抵抗
        double p2 = mw * (0.567 + 4.66e-2 * mw) + 49.4;
        return p1 - p2;
      };
      //ニュートンラフソン法を適用
      double waterFlowRate = Roots.Newton(eFnc, 0, 0.0001, 0.001, 0.001, 100);

      Console.WriteLine("mw= " + waterFlowRate.ToString("F3"));

      //エラー確認******************************************************
      if (waterFlowRate != 66.604368555164115)
        throw new Exception("NewtonRaphsonTest2 Error");
    }

    public static void GoldenSectionTest()
    {
      Minimization.MinimizeFunction mFnc = delegate (double wTemp)
      {
        double pct = 3.4773e2 + wTemp * (-6.4390e1 + wTemp * (4.775 + wTemp *
          (-1.768e-1 + wTemp * (3.2651e-3 + wTemp * (-2.4048e-5)))));
        double ptb = 4.1472e-1 + wTemp * (5.1299e-3 + wTemp * (4.1126e-4));
        return pct * 15 + ptb * 70;
      };
      double min = 20;
      double gs = Minimization.GoldenSection(ref min, 32, mFnc);
      Console.WriteLine(gs.ToString("F3"));

      //エラー確認******************************************************
      if (gs != 60.135662745472565)
        throw new Exception("GoldenSectionTest Error");
    }

    #endregion

    #region spline補間テスト

    public static void SplineTest()
    {
      double[] x = new double[25];
      for (int i = 0; i < x.Length; i++) x[i] = i;
      double[] y = new double[]
      { 0, 0, 0, 0, 0, 0, 0, 42, 224, 215, 210, 217,
        219, 217, 210, 250, 210, 217, 91, 9, 9, 0, 0, 0, 0 };
      double[] c = CubicSpline.GetParameters(x, y);

      double[] x2 = new double[24 * 4];
      for (int i = 0; i < x2.Length; i++) x2[i] = 0.25 * i;
      double[] y2 = CubicSpline.Interpolate(x, y, c, x2);

      for (int i = 0; i < x2.Length; i++)
      {
        y2[i] = Math.Max(0, y2[i]); //負荷データのため0以上とする
        Console.WriteLine(x2[i].ToString("F2") + "," + y2[i].ToString("F2"));
      }

      //エラー確認******************************************************
      double[] ans = new double[]
      { 0.000000000000000000 ,0.000000000000000000 ,0.000000000000000000 ,0.000000000000000000 ,0.000000000000000000 ,0.002082354106706030 ,0.003844346043149600 ,0.003684164958018370 ,0.000000000000000001 ,0.000000000000000000 ,0.000000000000000000 ,0.000000000000000000 ,0.000000000000000000 ,0.028031689897965800 ,0.052539395923044500 ,0.050777403986601000 ,0.000000000000000000 ,0.000000000000000000 ,0.000000000000000000 ,0.000000000000000000 ,0.000000000000000000 ,0.390361304464816000 ,0.731707196879474000 ,0.707199490854396000 ,0.000000000000000222 ,0.000000000000000000 ,2.519232860682720000 ,15.079446260292600000 ,42.000000000000000000 ,85.718276572609400000 ,137.941361360389000000 ,187.693765467974000000 ,224.000000000000000000 ,239.005615678830000000 ,237.340321697758000000 ,226.754866867807000000 ,215.000000000000000000 ,208.243635712067000000 ,206.322351848574000000 ,207.489892060794000000 ,210.000000000000000000 ,212.394841472898000000 ,214.370270907941000000 ,215.910564889013000000 ,217.000000000000000000 ,217.661373396336000000 ,218.071564519658000000 ,218.445973383151000000 ,219.000000000000000000 ,219.787789941754000000 ,220.218471013423000000 ,219.539916578381000000 ,217.000000000000000000 ,212.421841836644000000 ,207.929551426645000000 ,206.222485303323000000 ,210.000000000000000000 ,220.649842711665000000 ,234.313323279993000000 ,245.820142208323000000 ,250.000000000000000000 ,243.556912316691000000 ,230.692155453381000000 ,217.481320863380000000 ,210.000000000000000000 ,212.106883021567000000 ,218.793054906480000000 ,222.832699338153000000 ,217.000000000000000000 ,196.265555597037000000 ,164.385624920695000000 ,127.312881784005000000 ,91.000000000000000000 ,60.409019590282100000 ,36.539445410738100000 ,19.400148525825000000 ,9.000000000000010000 ,4.942116041833790000 ,5.206593436352120000 ,7.367774112694390000 ,9.000000000000000000 ,8.275641242382660000 ,5.759180843853350000 ,2.613130023397360000 ,0.000000000000000000 ,0.000000000000000000 ,0.000000000000000000 ,0.000000000000000000 ,0.000000000000000000 ,0.324957803075082000 ,0.339086403208781000 ,0.183671801738090000 ,0.000000000000000000 ,0.000000000000000000 ,0.000000000000000000 ,0.000000000000000000 };
      for (int i = 0; i < ans.Length; i++)
        if (0.00000001 < Math.Abs(y2[i] - ans[i])) throw new Exception("SplineTest Error");
    }

    #endregion

    #region BoundaryInterpolatorテスト

    public static void TestBoundaryInterpolator()
    {
      DateTime[] dTimes = {
        new DateTime(1999,1,1,0,0,0),
        new DateTime(1999,1,1,1,0,0),
        new DateTime(1999,1,1,5,0,0),
        new DateTime(1999,1,1,6,0,0),
        new DateTime(1999,1,1,14,0,0),
        new DateTime(1999,1,1,17,0,0),
        new DateTime(1999,1,1,19,0,0),
        new DateTime(1999,1,2,0,0,0)
      };

      BoundaryInterpolator bi = new BoundaryInterpolator(dTimes);

      double[] sv = [2, 4, 5, 2, 10, 8, 5, 1];

      bi.AddSeries(sv);

      DateTime now = new DateTime(1998,12,31,23,0,0);
      while (now < new DateTime(1999, 1, 2, 1, 0, 0))
      {
        Console.WriteLine(now.ToString() + ", " + bi.Interpolate(now, 0).ToString("F2"));
        now = now.AddMinutes(10);
      }
    }

    #endregion

    #region 最小二乗法テスト

    public static void LeastSquareTest()
    {
      List<string[]> bf2 = new List<string[]>();
      using (StreamReader sReader = new StreamReader("lm.csv"))
      {
        sReader.ReadLine();
        string bf1;
        while ((bf1 = sReader.ReadLine()) != null)
          bf2.Add(bf1.Split(','));
      }

      double[,] x = new double[bf2.Count, bf2[0].Length - 1];
      double[] y = new double[bf2.Count];
      for (int i = 0; i < bf2.Count; i++)
      {
        y[i] = double.Parse(bf2[i][0]);
        for (int j = 1; j < bf2[0].Length; j++)
          x[i, j - 1] = double.Parse(bf2[i][j]);
      }
      double[] a = LinearAlgebra.LeastSquareFit(y, x);

      for (int i = 0; i < a.Length; i++)
        Console.Write(a[i].ToString("F1") + " , ");
      Console.WriteLine();

      //エラー確認******************************************************
      if (a[0] != 3.9999999979506327 || a[1] != -2.000000000047804 || a[2] != -7.9999999999018705)
        throw new Exception("RootsTest Error");
    }

    public static void LeastSquareTest2()
    {
      double[,] x = new double[6, 3];
      double[] y = new double[6];
      x[0, 0] = 1.5; x[1, 0] = 2.3; x[2, 0] = 3.8; x[3, 0] = 4.2; x[4, 0] = 5.6; x[5, 0] = 6.3;
      x[0, 1] = 1.0; x[1, 1] = 3.9; x[2, 1] = 8.6; x[3, 1] = 15.25; x[4, 1] = 28.6; x[5, 1] = 32.68;
      x[0, 2] = 1; x[1, 2] = 1; x[2, 2] = 1; x[3, 2] = 1; x[4, 2] = 1; x[5, 2] = 1;
      y[0] = -3.2; y[1] = 2.0; y[2] = 2.2; y[3] = 6.3; y[4] = 5.8; y[5] = 12.5;

      double sig2, aic;
      double[] a = LinearAlgebra.LeastSquareFit(y, x, out sig2, out aic);
      //SIG2=3.6801, AIC=32.845となるはず
      Console.WriteLine("SIG=" + sig2.ToString("F4") + "  AIC=" + aic.ToString("F3"));

      //エラー確認******************************************************
      if (sig2 != 3.6801816985972984 || aic != 32.845035151940777)
        throw new Exception("RootsTest Error");
    }

    public static void MultipleRegressionTest()
    {
      // 観測値
      double[] yd = { 135, 193, 230, 175, 174 };
      Vector y = new Vector(yd);
      // 説明変数行列
      double[][] xm =  {
        new double[] {1, 150, 24, 100},
        new double[] {1, 190, 23, 182},
        new double[] {1, 272, 27, 115},
        new double[] {1, 188, 30, 102},
        new double[] {1, 189, 29, 103}
      };
      double[] weight = LinearAlgebra.EstimateMultipleRegressionCoefficients
        (yd, xm, out double sigma2, out double aic, out double rss2);
    }

    #endregion

    #region 非線形連立方程式テスト

    public static void SimplexTest()
    {
      Random rnd = new Random();
      double[] minX = new double[] { -50, -50 };
      double[] maxX = new double[] { 50, 50 };
      bool suc;
      for (int i = 0; i < 100; i++)
      {
        double[] rslt = NMSimplex.GetSolution(fm_Rosenbrock, minX, maxX, out suc);
        Console.WriteLine(fm_Rosenbrock(rslt).ToString("F4") + ", " +
          rslt[0].ToString("F3") + ", " + rslt[1].ToString("F3") + ", " + suc.ToString());
      }
    }

    public static void SimplexTest2()
    {
      Random rnd = new Random();
      double[] minX = new double[] { -50, -50 };
      double[] maxX = new double[] { 50, 50 };
      bool suc;
      for (int i = 0; i < 100; i++)
      {
        double[] rslt = NMSimplex.GetSolution(fm_Rosenbrock, fc_Rosenbrock, minX, maxX, out suc);
        Console.WriteLine(fm_Rosenbrock(rslt).ToString("F4") + ", " +
          rslt[0].ToString("F3") + ", " + rslt[1].ToString("F3") + ", " + suc.ToString());
      }
    }

    private static double fm_Rosenbrock
      (double[] x)
    {
      return 100 * Math.Pow(x[1] - x[0] * x[0], 2) + Math.Pow(1 - x[0], 2);
    }

    private static double fc_Rosenbrock
      (double[] x)
    {
      return x[0] * x[0] + x[1] * x[1] - 1.5;
    }

    public static void LevenbergMarquardtTest()
    {
      LevenbergMarquardt.ErrorFunction mFnc = delegate (IVector inputs, ref IVector outputs)
      {
        double[] x = new double[] { 0.95, 0.85, 0.75, 0.65, 0.55, 0.45, 0.35, 0.25, 0.15, 0.05 };
        double x1 = inputs[0];
        double x2 = inputs[1];

        outputs[0] = x1 * x1 + 2 * x2 * x2;
        outputs[1] = -0.3 * Math.Cos(3d * Math.PI * x1 + 4 * Math.PI * x2) * (Math.Cos(4 * Math.PI * x2)) + 0.3;
      };

      LevenbergMarquardt lm = new LevenbergMarquardt(mFnc, 2, 2);
      IVector vec = new Vector(2);
      vec[0] = 1.2;
      vec[1] = 1.3;
      IVector op = new Vector(2);
      mFnc(vec, ref op);
      lm.Minimize(ref vec);
      Console.WriteLine(vec[0] + " : " + vec[1]);

      //エラー確認******************************************************
      if (vec[0] != 0.04335597048969896 || vec[1] != 0.21061107305129576)
        throw new Exception("LevenbergMarquardtTest Error");
    }

    public static void MultiMinimizationTest()
    {
      int eqNum = 1;
      MultiMinimization.MinimizeFunction mFnc = delegate (IVector x, int iteration)
      {
        if (eqNum == 0)  //Cragg-Levy:
          return Math.Pow(Math.Exp(x[0]) - x[1], 4) + 100 * Math.Pow(x[1] - x[2], 6)
          + Math.Pow(Math.Tan(x[2] - x[3]), 4) + Math.Pow(x[0], 8) + Math.Pow(x[3] - 1, 2);
        else if (eqNum == 1) //Wood-Colville:
          return 100 * Math.Pow((x[1] - x[0] * x[0]), 2) + Math.Pow(1 - x[0], 2) + 90 * Math.Pow(x[3] - x[2] * x[2], 2)
          + Math.Pow(1 - x[2], 2) + 10.1 * (Math.Pow(x[1] - 1, 2) + Math.Pow(x[3] - 1, 2)) + 19.8 * (x[1] - 1) * (x[3] - 1);
        else //Powell
          return Math.Pow(x[0] + 10 * x[1], 2) + 5 * Math.Pow(x[2] - x[3], 2)
          + Math.Pow(x[1] - 2 * x[2], 4) + 10 * Math.Pow(x[0] - x[3], 4);
      };
      IVector inputs = new Vector(4);
      if (eqNum == 0) { inputs[0] = 0.5; inputs[1] = 2; inputs[2] = 2; inputs[3] = 2; }         //Cragg-Levy
      else if (eqNum == 1) { inputs[0] = -3; inputs[1] = -1; inputs[2] = -3; inputs[3] = -1; }  //Wood-Colville
      else { inputs[0] = -3; inputs[1] = -1; inputs[2] = 0; inputs[3] = 1; }                    //Powell
      int iter;
      MultiMinimization.QuasiNewton(ref inputs, mFnc, 400, 1e-5, 1e-5, 1e-4, out iter);
      //MultiMinimization.Newton(ref inputs, mFnc, 400, 1e-5, 1e-5, 1e-4, out iter);
      double err = mFnc(inputs, 1);
    }

    #endregion

  }
}
