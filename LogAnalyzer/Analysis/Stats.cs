namespace LogAnalyzer.Analysis;

/// <summary>
/// 描述性统计结果
/// </summary>
public class DescriptiveStats
{
    public int Count { get; set; }
    public double Mean { get; set; }
    public double Median { get; set; }
    public double StdDev { get; set; }
    public double Variance { get; set; }
    public double Min { get; set; }
    public double Max { get; set; }
    public double Range { get; set; }
    public double Q1 { get; set; }
    public double Q3 { get; set; }
    public double Iqr { get; set; }
    public double Cv { get; set; }        // 变异系数（%）
    public double Skewness { get; set; }
    public double Kurtosis { get; set; }
}

/// <summary>
/// 统计数学工具（纯函数，无依赖）
/// </summary>
public static class Stats
{
    public static double Mean(IEnumerable<double> v)
    {
        var list = v as IList<double> ?? v.ToList();
        return list.Count == 0 ? 0 : list.Sum() / list.Count;
    }

    public static double StdDev(IEnumerable<double> v, double? mean = null)
    {
        var list = v as IList<double> ?? v.ToList();
        if (list.Count < 2) return 0;
        var m = mean ?? Mean(list);
        var sumSq = list.Sum(x => (x - m) * (x - m));
        return Math.Sqrt(sumSq / (list.Count - 1));
    }

    public static double Quantile(IEnumerable<double> v, double q)
    {
        var list = v.OrderBy(x => x).ToList();
        if (list.Count == 0) return 0;
        if (list.Count == 1) return list[0];
        var pos = (list.Count - 1) * q;
        var low = (int)Math.Floor(pos);
        var high = (int)Math.Ceiling(pos);
        return list[low] + (list[high] - list[low]) * (pos - low);
    }

    public static double Pearson(IEnumerable<double> x, IEnumerable<double> y)
    {
        var xs = x as IList<double> ?? x.ToList();
        var ys = y as IList<double> ?? y.ToList();
        if (xs.Count != ys.Count || xs.Count < 3) return double.NaN;

        var mx = Mean(xs);
        var my = Mean(ys);
        double sxy = 0, sxx = 0, syy = 0;
        for (int i = 0; i < xs.Count; i++)
        {
            var dx = xs[i] - mx;
            var dy = ys[i] - my;
            sxy += dx * dy;
            sxx += dx * dx;
            syy += dy * dy;
        }
        if (sxx == 0 || syy == 0) return double.NaN;
        return sxy / Math.Sqrt(sxx * syy);
    }

    /// <summary>线性回归，返回 (斜率, 截距, R²)</summary>
    public static (double slope, double intercept, double r2) LinearRegression(IEnumerable<double> x, IEnumerable<double> y)
    {
        var xs = x as IList<double> ?? x.ToList();
        var ys = y as IList<double> ?? y.ToList();
        if (xs.Count != ys.Count || xs.Count < 2) return (0, 0, 0);

        var mx = Mean(xs);
        var my = Mean(ys);
        double sxy = 0, sxx = 0, syy = 0;
        for (int i = 0; i < xs.Count; i++)
        {
            var dx = xs[i] - mx;
            var dy = ys[i] - my;
            sxy += dx * dy;
            sxx += dx * dx;
            syy += dy * dy;
        }
        if (sxx == 0) return (0, my, 0);
        var slope = sxy / sxx;
        var intercept = my - slope * mx;
        var r2 = syy == 0 ? 0 : (sxy * sxy) / (sxx * syy);
        return (slope, intercept, r2);
    }

    /// <summary>描述性统计</summary>
    public static DescriptiveStats Describe(IEnumerable<double> v)
    {
        var list = v as IList<double> ?? v.ToList();
        var s = new DescriptiveStats
        {
            Count = list.Count,
            Min = list.Count > 0 ? list.Min() : 0,
            Max = list.Count > 0 ? list.Max() : 0,
        };
        if (list.Count == 0) return s;
        s.Mean = Mean(list);
        s.Median = Quantile(list, 0.5);
        s.Q1 = Quantile(list, 0.25);
        s.Q3 = Quantile(list, 0.75);
        s.Iqr = s.Q3 - s.Q1;
        s.Range = s.Max - s.Min;
        s.Variance = list.Count >= 2 ? list.Sum(x => (x - s.Mean) * (x - s.Mean)) / (list.Count - 1) : 0;
        s.StdDev = Math.Sqrt(s.Variance);
        s.Cv = s.Mean != 0 ? s.StdDev / Math.Abs(s.Mean) * 100 : 0;

        // 偏度
        if (s.StdDev > 0 && list.Count >= 3)
        {
            var n = list.Count;
            s.Skewness = list.Sum(x => Math.Pow((x - s.Mean) / s.StdDev, 3)) * n / ((n - 1.0) * (n - 2.0));
            s.Kurtosis = (list.Sum(x => Math.Pow((x - s.Mean) / s.StdDev, 4)) * n * (n + 1.0)
                / ((n - 1.0) * (n - 2.0) * (n - 3.0)))
                - (3.0 * (n - 1) * (n - 1) / ((n - 2.0) * (n - 3.0)));
        }
        return s;
    }
}
