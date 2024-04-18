﻿using TerrariaApi.Server;
using TShockAPI;
using Terraria;
using Terraria.GameContent.Creative;
using Google.Protobuf.WellKnownTypes;
using System.Configuration;
using TShockAPI.Hooks;

namespace InvincibilityPlugin
{
    [ApiVersion(2, 1)]
    public class InvincibilityPlugin : TerrariaPlugin
    {
        public override string Author => "肝帝熙恩";
        public override string Description => "在命令中给予玩家一段时间的无敌状态。";
        public override string Name => "InvincibilityPlugin";
        public override Version Version => new Version(1, 0, 5);
        public static Configuration Config;
        private Dictionary<TSPlayer, DateTime> invincibleStartTime = new Dictionary<TSPlayer, DateTime>();
        private Dictionary<TSPlayer, float> invincibleDurations = new Dictionary<TSPlayer, float>();
        private Dictionary<TSPlayer, float> frameDurations = new Dictionary<TSPlayer, float>();

        public InvincibilityPlugin(Main game) : base(game) 
        {
            LoadConfig();
        }

        private static void LoadConfig()
        {
            Config = Configuration.Read(Configuration.FilePath);
            Config.Write(Configuration.FilePath);

        }
        private static void ReloadConfig(ReloadEventArgs args)
        {
            LoadConfig();
            args.Player?.SendSuccessMessage("[{0}] 重新加载配置完毕。", typeof(InvincibilityPlugin).Name);
        }
        public override void Initialize()
        {
            GeneralHooks.ReloadEvent += ReloadConfig;
            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            ServerApi.Hooks.GameUpdate.Register(this, OnUpdate);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                ServerApi.Hooks.GameUpdate.Deregister(this, OnUpdate);
            }
            base.Dispose(disposing);
        }

        private void OnInitialize(EventArgs args)
        {
            Commands.ChatCommands.Add(new Command("Invincibility.god", InvincibleCommand, "tgod", "限时god无敌"));
            Commands.ChatCommands.Add(new Command("Invincibility.frame", ActivateFrameInvincibility, "tframe", "限时无敌帧无敌"));
        }

        private void OnUpdate(EventArgs args)
        {
            List<TSPlayer> playersToRemove = new List<TSPlayer>();

            // 处理无敌持续时间结束的玩家
            foreach (var pair in invincibleDurations.ToList())
            {
                TSPlayer player = pair.Key;
                float duration = pair.Value;

                if (DateTime.UtcNow - invincibleStartTime[player] >= TimeSpan.FromSeconds(duration))
                {
                    delGodMode(player);
                    playersToRemove.Add(player);
                }
            }

            // 移除无敌持续时间结束的玩家
            foreach (var player in playersToRemove)
            {
                invincibleStartTime.Remove(player);
                invincibleDurations.Remove(player);
            }

            // 处理无敌帧效果
            playersToRemove.Clear(); // 清空玩家列表，用于处理无敌帧结束的玩家
            foreach (var pair in frameDurations.ToList())
            {
                TSPlayer player = pair.Key;
                float duration = pair.Value;

                if (duration >= 1.33f)
                {
                    // 在玩家的客户端发送无敌帧效果
                    player.SendData(PacketTypes.PlayerDodge, "", player.Index, 2f, 0f, 0f, 0);
                    duration -= 0.1f;
                    frameDurations[player] = duration;
                }
                else
                {
                    // 无敌帧结束，添加到待移除玩家列表中
                    playersToRemove.Add(player);
                }
            }

            // 移除无敌帧结束的玩家
            foreach (var player in playersToRemove)
            {
                frameDurations.Remove(player);
                player.SendSuccessMessage($"{Config.CustomEndFrameText}");
            }
        }

        private void InvincibleCommand(CommandArgs args)
        {
            if (!args.Player.HasPermission("限时god无敌"))
            {
                args.Player.SendErrorMessage("你没有执行此命令的权限。");
                return;
            }

            if (args.Parameters.Count < 1)
            {
                args.Player.SendErrorMessage("用法: /限时god无敌或tgod <持续时间秒数>");
                return;
            }

            float duration;
            if (!float.TryParse(args.Parameters[0], out duration) || duration <= 0)
            {
                args.Player.SendErrorMessage("无效的持续时间。请输入一个正数。");
                return;
            }

            TSPlayer player = TShock.Players[args.Player.Index];

            if (player == null || !player.Active)
            {
                args.Player.SendErrorMessage("玩家不在线。");
                return;
            }

            addGodMode(player, duration);
            invincibleStartTime[player] = DateTime.UtcNow;
            invincibleDurations[player] = duration;
        }

        private void addGodMode(TSPlayer player, float duration)
        {
            player.GodMode = true;
            CreativePowerManager.Instance.GetPower<CreativePowers.GodmodePower>().SetEnabledState(player.Index, player.GodMode);
            if (Config.EnableInvincibleReminder)
            {
                player.SendSuccessMessage($"你将在 {duration} 秒内无敌.");
            }

            if (!string.IsNullOrEmpty(Config.CustomInvincibleReminderText))
            {
                player.SendSuccessMessage(Config.CustomInvincibleReminderText);
            }
            NetMessage.SendData((int)PacketTypes.PlayerInfo, -1, -1, null, player.Index, 1f);
        }

        private void delGodMode(TSPlayer player)
        {
            player.GodMode = false;
            CreativePowerManager.Instance.GetPower<CreativePowers.GodmodePower>().SetEnabledState(player.Index, player.GodMode);
            player.SendSuccessMessage($"{Config.CustomInvincibleDisableText}");
            NetMessage.SendData((int)PacketTypes.PlayerInfo, -1, -1, null, player.Index, 1f);
        }

        private void ActivateFrameInvincibility(CommandArgs args)
        {
            if (args.Parameters.Count < 1 || !float.TryParse(args.Parameters[0], out float duration) || duration <= 0)
            {
                args.Player.SendErrorMessage("用法: /限时无敌帧无敌或tframe <持续时间秒数>");
                return;
            }

            TSPlayer player = args.Player;

            if (Config.EnableFrameReminder)
            {
                args.Player.SendSuccessMessage($"你将在 {args.Parameters[0]} 秒内无敌.");
            }
            if (!string.IsNullOrEmpty(Config.CustomStartFrameText))
            {
                player.SendSuccessMessage(Config.CustomStartFrameText);
            }

            frameDurations[player] = duration;
        }
    }
}
