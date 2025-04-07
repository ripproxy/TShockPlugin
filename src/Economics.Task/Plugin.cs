using Economics.Task.DB;
using Economics.Task.Model;
using EconomicsAPI.Configured;
using EconomicsAPI.EventArgs.PlayerEventArgs;
using EconomicsAPI.Events;
using Rests;
using System.Reflection;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;
using System.Timers;
using System.Linq;

namespace Economics.Task;

[ApiVersion(2, 1)]
public class Plugin : TerrariaPlugin
{
    public override string Author => "少司命";
    public override string Description => GetString("提供任务系统!");
    public override string Name => Assembly.GetExecutingAssembly().GetName().Name!;
    public override Version Version => new Version(2, 0, 0, 3);

    internal static Config TaskConfig = new();
    private readonly string PATH = Path.Combine(EconomicsAPI.Economics.SaveDirPath, "Task.json");

    internal static TaskFinishManager TaskFinishManager { get; private set; } = null!;
    internal static TaskKillNPCManager KillNPCManager = null!;
    internal static TaskTallkManager TallkManager = null!;

    private System.Timers.Timer taskResetTimer;

    public Plugin(Main game) : base(game) {}

    public override void Initialize()
    {
        this.LoadConfig();
        TaskFinishManager = new();
        KillNPCManager = new();
        TallkManager = new();
        PlayerHandler.OnPlayerKillNpc += this.OnKillNpc;
        GetDataHandlers.NpcTalk.Register(this.OnNpcTalk);
        GeneralHooks.ReloadEvent += this.LoadConfig;
        TShock.RestApi.Register("/taskFinish", this.Finish);

        // === Auto reset task at 04:00 AM UTC ===
        DateTime now = DateTime.UtcNow;
        DateTime nextReset = now.Date.AddDays(now.Hour >= 4 ? 1 : 0).AddHours(4);
        double initialInterval = (nextReset - now).TotalMilliseconds;

        taskResetTimer = new System.Timers.Timer(initialInterval);
        taskResetTimer.Elapsed += (sender, e) =>
        {
            ResetDailyTask();
            taskResetTimer.Interval = TimeSpan.FromDays(1).TotalMilliseconds;
        };
        taskResetTimer.AutoReset = true;
        taskResetTimer.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            taskResetTimer?.Stop();
            taskResetTimer?.Dispose();

            EconomicsAPI.Economics.RemoveAssemblyCommands(Assembly.GetExecutingAssembly());
            EconomicsAPI.Economics.RemoveAssemblyRest(Assembly.GetExecutingAssembly());
            PlayerHandler.OnPlayerKillNpc -= this.OnKillNpc;
            GetDataHandlers.NpcTalk.UnRegister(this.OnNpcTalk);
            GeneralHooks.ReloadEvent -= this.LoadConfig;
        }
        base.Dispose(disposing);
    }

    private object Finish(RestRequestArgs args)
    {
        if (args.Parameters["name"] == null)
            return new RestObject("201") { Response = GetString("没有检测到玩家名称") };

        if (args.Parameters["taskid"] == null)
            return new RestObject("201") { Response = GetString("没有检测到任务ID") };

        if (!int.TryParse(args.Parameters["taskid"], out var taskid))
            return new RestObject("201") { Response = GetString("非法的任务ID") };

        var task = TaskFinishManager.GetTaksByName(args.Parameters["name"]);
        var finish = task.Any(x => x.TaskID == taskid);
        return new RestObject() { { "response", GetString("查询成功") }, { "code", finish } };
    }

    private void LoadConfig(ReloadEventArgs? args = null)
    {
        if (!File.Exists(this.PATH))
        {
            TaskConfig.Tasks = new()
            {
                new TaskContent()
                {
                    TaskName = "狄拉克的请求",
                    TaskID = 1,
                    Description = "哦，亲爱的朋友，你是来帮我的吗?...",
                    FinishTaskFormat = "哦，感谢你我的朋友,你叫{0}对吧，我记住了!",
                    TaskInfo = new TaskDemand()
                    {
                        TallkNPC = new() { 17 },
                        Items = new List<EconomicsAPI.Model.Item>
                        {
                            new () { netID = 178, Stack = 10, Prefix = 0 },
                            new () { netID = 38, Stack = 2, Prefix = 0 }
                        },
                        KillNPCS = new List<KillNpc>
                        {
                            new () { ID = 2, Count = 2 }
                        }
                    },
                    Reward = new TaskReward()
                    {
                        Commands = new List<string>()
                        {
                            "/permabuff 165",
                            "/i 499"
                        }
                    }
                }
            };
        }
        TaskConfig = ConfigHelper.LoadConfig(this.PATH, TaskConfig);
    }

    private void ResetDailyTask()
    {
        TaskFinishManager.ClearAll();

        foreach (var player in TShock.Players.Where(p => p?.Active == true))
        {
            var random = new Random();
            var newTask = TaskConfig.Tasks[random.Next(TaskConfig.Tasks.Count)];

            UserTaskData.SetUserTask(player.Name, newTask);
            player.SendInfoMessage($"Tugas harian kamu telah diperbarui: {newTask.TaskName}");
        }

        TShock.Log.ConsoleInfo("Tugas harian telah diacak ulang pada pukul 04:00 UTC.");
    }

    public static bool InOfFinishTask(TSPlayer tSPlayer, HashSet<int> tasks)
    {
        if (tasks.Count == 0)
            return true;

        var successtask = TaskFinishManager.GetTaksByName(tSPlayer.Name, TaskStatus.Success);
        if (successtask != null)
        {
            foreach (var task in tasks)
            {
                if (!successtask.Any(x => x.TaskID == task))
                    return false;
            }
        }
        return true;
    }

    private void OnNpcTalk(object? sender, GetDataHandlers.NpcTalkEventArgs e)
    {
        var task = UserTaskData.GetUserTask(e.Player.Name);
        if (task != null && e.NPCTalkTarget != -1)
        {
            if (task.TaskInfo.TallkNPC.Contains(Main.npc[e.NPCTalkTarget].netID))
            {
                TallkManager.AddTallkNPC(e.Player.Name, Main.npc[e.NPCTalkTarget].netID);
            }
        }
    }

    private void OnKillNpc(PlayerKillNpcArgs args)
    {
        if (args.Npc == null)
            return;

        var task = UserTaskData.GetUserTask(args.Player!.Name);
        if (task != null)
        {
            var kill = task.TaskInfo.KillNPCS.Find(x => x.ID == args.Npc.netID);
            if (kill != null)
            {
                if (KillNPCManager.GetKillNpcsCountByID(args.Player.Name, args.Npc.netID) < kill.Count)
                {
                    KillNPCManager.AddKillNpc(args.Player.Name, args.Npc.netID);
                }
            }
        }
    }
}
