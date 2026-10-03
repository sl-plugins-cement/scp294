using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Qlz;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }

    public static void Main()
    {
        var quota = new LifeBottleQuota();
        Check(quota.CanReceive(1, 100), "新生命可领取");
        Check(quota.CanReceive(1, 100), "仅检查资格/取消交互不消耗额度");
        quota.RecordReceived(1, 100);
        Check(!quota.CanReceive(1, 100), "成功出货后拒绝同生命领取");
        Check(quota.CanReceive(2, 200), "其他玩家独立额度");
        Check(quota.CanReceive(1, 101), "复活后新生命可领取");
        quota.RecordReceived(1, 101);
        Check(!quota.CanReceive(1, 101), "新生命领取后再次锁定");
        quota.RecordReceived(2, 200);
        quota.Forget(1);
        Check(quota.CanReceive(1, 101) && !quota.CanReceive(2, 200), "离开清理仅影响自己");
        quota.Reset();
        Check(quota.CanReceive(1, 101) && quota.CanReceive(2, 200), "回合重置清理所有额度");

        // Compare every code default to the supplied YAML, including vectors and layout.
        var yaml = new Dictionary<string, string>();
        string parent = "";
        foreach (string raw in File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "config.yml")))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            int separator = line.IndexOf(':');
            string key = line.Substring(0, separator);
            string value = line.Substring(separator + 1).Trim();
            if (value.Length == 0) { parent = key; continue; }
            yaml[char.IsWhiteSpace(raw[0]) ? parent + "." + key : key] = value;
        }
        var config = new Config();
        int verifiedKeys = 0;
        foreach (PropertyInfo property in typeof(Config).GetProperties())
        {
            string words = Regex.Replace(property.Name, "([A-Z])([A-Z][a-z])", "$1_$2");
            string key = Regex.Replace(words, "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant();
            object value = property.GetValue(config)!;
            if (value is UnityEngine.Vector3 vector)
            {
                Check(float.Parse(yaml[key + ".x"], CultureInfo.InvariantCulture) == vector.x &&
                    float.Parse(yaml[key + ".y"], CultureInfo.InvariantCulture) == vector.y &&
                    float.Parse(yaml[key + ".z"], CultureInfo.InvariantCulture) == vector.z, "默认向量同步：" + key);
                verifiedKeys += 3;
            }
            else
            {
                object expected = Convert.ChangeType(yaml[key], property.PropertyType, CultureInfo.InvariantCulture);
                Check(value.Equals(expected), "默认配置同步：" + key);
                verifiedKeys++;
            }
        }
        Check(verifiedKeys == yaml.Count, "全部 YAML 配置字段均有代码默认值");

        // A short gameplay walkthrough cannot establish the legendary roll's probability.
        var counts = new Dictionary<Drink, int>();
        for (int ticket = 0; ticket < DrinkRolls.Tickets; ticket++)
        {
            Drink drink = DrinkRolls.Pick(ticket);
            counts.TryGetValue(drink, out int count);
            counts[drink] = count + 1;
        }
        Check(counts.Count == 8 && DrinkRolls.Tickets == 1000, "All drinks represented in the exact 1,000-ticket lottery");
        Check(counts[Drink.Coffee] == 800, "Ordinary coffee 80%");
        Check(counts[Drink.Ahead] == 100, "Mild Ahead 10%");
        Check(counts[Drink.QiaoLeZi] == 20, "QiaoLeZi 2%, with its existing branch roll");
        Check(counts[Drink.SixtySeven] == 30, "67 3%");
        Check(counts[Drink.Vodka] == 30, "Vodka 3%");
        Check(counts[Drink.CompoundV] == 10, "Compound V 1%");
        Check(counts[Drink.Meteor] == 8, "Meteor 0.8%");
        Check(counts[Drink.Jiahao] == 2, "Jiahao 0.2%, with its existing 10% success roll");
        foreach (int invalid in new[] { -1, DrinkRolls.Tickets })
        {
            bool rejected = false;
            try { DrinkRolls.Pick(invalid); }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected, "Lottery rejects an out-of-range ticket");
        }
        Console.WriteLine($"Passed {checks} life quota/default configuration/rarity checks.");
    }
}

namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new(0, 0, 0);
    }
}
