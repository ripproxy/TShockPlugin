using EconomicsAPI.Attributes;
using EconomicsAPI.Extensions;
using TShockAPI;

namespace Economics.Task;

[RegisterSeries]
public class Command
{
    [CommandMap("task", Permission.TaskUse)]
    private void CTask(CommandArgs args)
    {
        void ShowTask(List<string> line)
        {
            if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out var pageNumber))
            {
                return;
            }

            PaginationTools.SendPage(
                args.Player,
                pageNumber,
                line,
                new PaginationTools.Settings
                {
                    MaxLinesPerPage = Plugin.TaskConfig.PageCount,
                    NothingToDisplayString = GetString("Saat ini tidak ada tugas"),
                    HeaderFormat = GetString("Daftar Tugas ({0}/{1}):"),
                    FooterFormat = GetString("Ketik {0}task list {{0}} untuk melihat lebih banyak").SFormat(Commands.Specifier)
                }
            );
        }

        if (args.Parameters.Count >= 1 && args.Parameters[0].ToLower() == "list")
        {
            var line = Plugin.TaskConfig.Tasks.Select(x => $"{x.TaskID.Color(TShockAPI.Utils.PinkHighlight)}.{x.TaskName.Color(TShockAPI.Utils.CyanHighlight)}").ToList();
            ShowTask(line);
        }
        else if (args.Parameters.Count == 2 && args.Parameters[0].ToLower() == "info")
        {
            if (int.TryParse(args.Parameters[1], out var index))
            {
                var task = Plugin.TaskConfig.GetTask(index);
                if (task == null)
                {
                    args.Player.SendErrorMessage(GetString("Tugas ini tidak ada!"));
                }
                else
                {
                    args.Player.SendMessage(GetString($"{task.TaskName} deskripsi: {task.Description}"), Microsoft.Xna.Framework.Color.Wheat);
                }
            }
            else
            {
                args.Player.SendErrorMessage(GetString("Nomor tugas salah!"));
            }
        }
        else if (args.Parameters.Count == 2 && args.Parameters[0].ToLower() == "pick")
        {
            if (UserTaskData.HasTask(args.Player.Name))
            {
                args.Player.SendErrorMessage(GetString("Kamu masih punya tugas aktif, tidak bisa ambil lebih dari satu!"));
            }
            else
            {
                if (int.TryParse(args.Parameters[1], out var index))
                {
                    var task = Plugin.TaskConfig.GetTask(index);
                    if (task != null)
                    {
                        if (Plugin.TaskFinishManager.HasFinishTask(index, args.Player.Name))
                        {
                            args.Player.SendErrorMessage(GetString("Kamu sudah menyelesaikan tugas ini!"));
                            return;
                        }
                        if (!Plugin.InOfFinishTask(args.Player, task.FinishTask))
                        {
                            args.Player.SendErrorMessage(GetString($"Harus menyelesaikan tugas {string.Join(",", task.FinishTask)} terlebih dahulu"));
                            return;
                        }
                        if (!RPG.RPG.InLevel(args.Player.Name, task.LimitLevel))
                        {
                            args.Player.SendErrorMessage(GetString($"Hanya level {string.Join(", ", task.LimitLevel)} yang dapat mengambil tugas ini"));
                            return;
                        }
                        if (!args.Player.InProgress(task.LimitProgress))
                        {
                            args.Player.SendErrorMessage(GetString($"Syarat progres {string.Join(", ", task.LimitLevel)} diperlukan untuk mengambil tugas ini"));
                            return;
                        }

                        UserTaskData.Add(args.Player.Name, index);
                        Plugin.TaskFinishManager.Add(index, args.Player.Name, TaskStatus.Ongoing);
                        args.Player.SendSuccessMessage(GetString("Tugas berhasil diambil!"));
                        args.Player.SendSuccessMessage(GetString($"Nama tugas: {task.TaskName}"));
                        args.Player.SendSuccessMessage(GetString($"Deskripsi tugas: {task.Description}"));
                    }
                    else
                    {
                        args.Player.SendErrorMessage(GetString("Tugas tidak ada!"));
                    }
                }
                else
                {
                    args.Player.SendErrorMessage(GetString("Nomor yang dimasukkan salah!"));
                }
            }
        }
        else if (args.Parameters.Count == 1 && args.Parameters[0] == "prog")
        {
            if (UserTaskData.HasTask(args.Player.Name))
            {
                var progress = UserTaskData.GetTaskProgress(args.Player);
                progress.ForEach(x => args.Player.SendInfoMessage(x));
            }
            else
            {
                args.Player.SendErrorMessage(GetString("Kamu belum mengambil tugas!"));
            }
        }
        else if (args.Parameters.Count == 1 && args.Parameters[0].ToLower() == "del")
        {
            if (UserTaskData.HasTask(args.Player.Name))
            {
                UserTaskData.Remove(args.Player.Name);
                args.Player.SendSuccessMessage(GetString("Kamu telah membatalkan sebuah tugas!"));
            }
            else
            {
                args.Player.SendSuccessMessage(GetString("Kamu belum memulai tugas apa pun!"));
            }
        }
        else if (args.Parameters.Count == 1 && args.Parameters[0].ToLower() == "pr")
        {
            var task = UserTaskData.GetUserTask(args.Player.Name);
            if (task != null)
            {
                if (UserTaskData.DectTaskFinish(args.Player))
                {
                    Plugin.TaskFinishManager.Update(task.TaskID, args.Player.Name, TaskStatus.Success);
                    UserTaskData.FinishTask(args.Player);
                    args.Player.SendSuccessMessage(task.FinishTaskFormat, args.Player.Name);
                }
                else
                {
                    args.Player.SendErrorMessage(GetString("Tugasmu saat ini belum selesai!"));
                }
            }
            else
            {
                args.Player.SendErrorMessage(GetString("Kamu belum mengambil tugas!"));
            }
        }
        else if (args.Parameters.Count == 1 && args.Parameters[0].ToLower() == "reset")
        {
            if (!args.Player.HasPermission(Permission.TaskAdmin))
            {
                args.Player.SendErrorMessage(GetString("Kamu tidak punya izin untuk menggunakan perintah ini!"));
                return;
            }
            Plugin.TaskFinishManager.RemoveAll();
            UserTaskData.Clear();
            Plugin.KillNPCManager.RemoveAll();
            Plugin.TallkManager.RemoveAll();
            args.Player.SendSuccessMessage(GetString("Semua tugas yang selesai telah dibersihkan!"));
        }
        else if (args.Parameters.Count >= 1 && args.Parameters[0].ToLower() == "time")
        {
            // Menampilkan waktu server
            string serverTime = $"Waktu Server: {DateTime.Now:yyyy-MM-dd HH:mm:ss}".Color(TShockAPI.Utils.YellowHighlight);
            args.Player.SendInfoMessage(serverTime);
        }
        else
        {
            args.Player.SendInfoMessage(GetString("/task list untuk melihat daftar tugas"));
            args.Player.SendInfoMessage(GetString("/task info <id> untuk melihat detail tugas"));
            args.Player.SendInfoMessage(GetString("/task pick <id> untuk mengambil tugas"));
            args.Player.SendInfoMessage(GetString("/task prog untuk melihat progres tugas"));
            args.Player.SendInfoMessage(GetString("/task pr untuk mengirim tugas"));
            args.Player.SendInfoMessage(GetString("/task del untuk menghapus tugas"));
            args.Player.SendInfoMessage(GetString("/task reset untuk mereset semua tugas yang selesai"));
        }
    }
}
