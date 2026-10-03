using System.ComponentModel;
using UnityEngine;

namespace Qlz;

public sealed class Config
{
    [Description("是否启用插件。")]
    public bool IsEnabled { get; set; } = true;

    [Description("SCP-294 饮料机坐标，生成时贴合下方地面。")]
    public Vector3 MachinePosition { get; set; } = new Vector3(58f, 292f, -43f);

    [Description("SCP-294 饮料机的旋转，正面朝局部负 Z。")]
    public Vector3 MachineRotation { get; set; } = Vector3.zero;

    [Description("SCP-294 模型大小，10 为默认约 2.2 米高，按比例缩放。")]
    public float MachineScale { get; set; } = 10f;

    [Description("玩家与饮料机交互的个人冷却秒数。")]
    public float PersonalCooldown { get; set; } = 2f;

    [Description("是否在每轮开始时自动生成饮料机。")]
    public bool SpawnOnRoundStart { get; set; } = true;

    [Description("回合开始后自动生成 SCP-294? 的延迟秒数。")]
    public float SpawnDelay { get; set; } = 60f;

    [Description("饮料机出现时的顶部公告秒数。")]
    public ushort SpawnBroadcastDuration { get; set; } = 8;

    [Description("巧乐兹成功时的移速效果持续时间。")]
    public float QiaoLeZiDuration { get; set; } = 120f;

    [Description("巧乐兹负面分支的心脏骤停持续秒数。")]
    public float QiaoLeZiCardiacDuration { get; set; } = 6f;

    [Description("Drink drop percentages: two decimal places, all eight must total 100; 0 disables a result.")]
    public double Scp207ChancePercent { get; set; } = 78;
    public double AheadChancePercent { get; set; } = 10;
    public double QiaoLeZiChancePercent { get; set; } = 2.4;
    public double SixtySevenChancePercent { get; set; } = 3.6;
    public double VodkaChancePercent { get; set; } = 3.6;
    public double CompoundVChancePercent { get; set; } = 1.2;
    public double MeteorChancePercent { get; set; } = 0.96;
    public double JiahaoChancePercent { get; set; } = 0.24;

    [Description("QiaoLeZi's cardiac-arrest probability after that drink is selected, 0-100 percent.")]
    public float QiaoLeZiCardiacChancePercent { get; set; } = 50;
    [Description("Jiahao's successful possession probability after that drink is selected, 0-100 percent.")]
    public float JiahaoSuccessChancePercent { get; set; } = 10;

    [Description("Native MovementBoost intensity: bonus movement speed percent, 0-255.")]
    public byte QiaoLeZiSpeedBoostPercent { get; set; } = 100;
    public byte AheadSpeedBoostPercent { get; set; } = 20;
    public byte CompoundVSpeedBoostPercent { get; set; } = 255;
    public byte MeteorSpeedBoostPercent { get; set; } = 150;
    [Description("Ahead's firearm damage bonus percent, clamped to 0-1000.")]
    public float AheadFirearmDamageBonusPercent { get; set; } = 5;
    [Description("Ahead's backlash slowness percent, clamped to 0-100.")]
    public byte AheadBacklashSlownessPercent { get; set; } = 20;
    [Description("Maximum HP retained during Ahead backlash, clamped to 1-100 percent.")]
    public float AheadBacklashMaxHealthPercent { get; set; } = 80;
    [Description("Vodka's slowness percent, clamped to 0-100.")]
    public byte VodkaSlownessPercent { get; set; } = 10;
    [Description("Vodka's actual damage reduction percent, clamped to 0-100 and rounded to native 0.5% steps.")]
    public float VodkaDamageReductionPercent { get; set; } = 7.5f;

    [Description("67 饮料的效果持续时间。")]
    public float SixtySevenDuration { get; set; } = 67f;

    [Description("遥遥领先主效果持续时间。")]
    public float AheadDuration { get; set; } = 90f;

    [Description("遥遥领先主效果结束后的反噬持续秒数。")]
    public float AheadBacklashDuration { get; set; } = 30f;

    [Description("伏特加效果持续时间。")]
    public float VodkaDuration { get; set; } = 120f;

    [Description("Compound V's positive movement effect duration, in seconds.")]
    public float CompoundVDuration { get; set; } = 10f;

    [Description("5号化合物的反噬持续秒数。")]
    public float CompoundVBacklashDuration { get; set; } = 30f;

    [Description("5号化合物反噬的缓慢强度，百分比。")]
    public byte CompoundVBacklashSlowness { get; set; } = 50;

    [Description("美味流星飞行至自爆的秒数。")]
    public float MeteorDuration { get; set; } = 20f;

    [Description("美味流星连续静止超过此秒数即自爆。")]
    public float MeteorStillSeconds { get; set; } = 1f;

    [Description("嘉豪附体效果持续秒数。")]
    public float JiahaoDuration { get; set; } = 90f;

    [Description("嘉豪 OGG BGM 路径。优先使用 DLL 旁的 jh.ogg，缺失时使用内嵌音乐；也可填自定义 OGG 绝对路径。")]
    public string JiahaoMusicPath { get; set; } = "jh.ogg";

    [Description("嘉豪 BGM 的最大听觉半径，米。")]
    public float JiahaoMusicRadius { get; set; } = 10f;

    [Description("嘉豪 BGM 音量。")]
    public float JiahaoMusicVolume { get; set; } = 1f;

    [Description("领取、使用和结束文案显示秒数，到期自动移除。")]
    public float HintMessageDuration { get; set; } = 6f;

    [Description("饮料倒计时纵坐标，0为顶部、1080为底部。")]
    public float BuffHintY { get; set; } = 760f;

    [Description("饮料倒计时相对右边缘的水平偏移，负数向左移。")]
    public float BuffHintX { get; set; } = -260f;

    [Description("手持饮料介绍起始纵坐标，向下排列名称和梗文案。")]
    public float ItemHintY { get; set; } = 790f;

    [Description("临时文案纵坐标，与介绍和倒计时分开。")]
    public float MessageHintY { get; set; } = 560f;
}
