using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using TerrariaApi.Server;
using TShockAPI;

namespace MultiWeaponPlugin
{
    [ApiVersion(2, 1)]
    public class MultiWeaponPlugin : TerrariaPlugin
    {
        public override string Name => "MultiWeapon (Universal)";
        public override string Author => "NamaKamu";
        public override string Description => "Memungkinkan slot 0, 1, dan 2 untuk memicu serangan secara bersamaan saat slot 0 digunakan, berlaku universal untuk item.";
        public override Version Version => new Version(1, 0, 1, 0);

        public MultiWeaponPlugin(Main game) : base(game) { }

        public override void Initialize()
        {
            // Hook untuk menangkap paket data (misalnya, ketika serangan terjadi)
            ServerApi.Hooks.NetGetData.Register(this, OnGetData);
        }

        private void OnGetData(GetDataEventArgs args)
        {
            /*
             * Pastikan bahwa kita hanya memproses paket yang relevan dengan aksi serangan.
             * Di sini kita anggap paket "ItemAnimation" (misal PacketTypes.ItemAnimation) sebagai indikator,
             * namun periksa struktur dan offset data sesuai dengan versi Terraria v1.4.4.9.
             */
            if (args.MsgId != PacketTypes.ItemAnimation)
                return;

            TSPlayer player = TShock.Players[args.Msg.whoAmI];
            if (player == null || !player.Active)
                return;

            // --- Parsing Data Paket ---
            // Pada contoh ini, secara _placeholder_ asumsikan data paket menyatakan bahwa pemain melakukan aksi pada slot 0.
            // Gantilah dengan parsing yang sesuai struktur data di versi ini.
            int usedSlot = 0; // <-- Misal: data paket mengindikasikan serangan dari slot 0

            if (usedSlot != 0)
                return; // Hanya proses aksi jika yang digunakan adalah slot 0

            // Ambil posisi pemain, misalnya untuk menentukan titik spawn serangan
            Vector2 spawnPosition = player.TPlayer.position;

            // Tentukan arah serangan; bisa disesuaikan dengan arah yang diambil dari data paket atau status pemain.
            Vector2 velocity = new Vector2(10f * player.TPlayer.direction, 0f);

            // Proses serangan untuk item di slot 1 dan slot 2 secara universal
            // Meski item bukan senjata (misal tidak memiliki properti shoot), tetap diproses.
            for (int slot = 1; slot <= 2; slot++)
            {
                Item item = player.TPlayer.inventory[slot];
                if (item == null)
                    continue;

                // Ambil properti dari item. Jika item tidak memiliki properti 'shoot' (biasanya senjata melee),
                // maka item.shoot kemungkinan bernilai 0. Itu artinya, jika ingin memberikan efek khusus untuk item non-senjata,
                // pengaturan tambahan mungkin diperlukan.
                int projType = item.shoot;   // Untuk senjata projectile, ini mendefinisikan jenis proyektilnya.
                int damage   = item.damage;   // Besar damage yang ditimbulkan
                float knockBack = item.knockBack;

                // Lakukan pemanggilan serangan tambahan.
                // Untuk senjata dengan properti projectile, ini akan memunculkan proyektil.
                // Untuk item non-senjata, hasilnya tergantung pada bagaimana engine Terraria menangani 'projType' = 0.
                int projIndex = Projectile.NewProjectile(
                    source: null, 
                    Position: spawnPosition, 
                    Velocity: velocity, 
                    Type: projType, 
                    Damage: damage, 
                    KnockBack: knockBack, 
                    Owner: player.TPlayer.whoAmI
                );

                // Sinkronisasi proyektil tambahan ke semua klien
                NetMessage.SendData(
                    msgType: PacketTypes.ProjectileNew,
                    remoteClient: -1,
                    ignoreClient: player.Index,
                    text: "",
                    number: projIndex
                );
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
            base.Dispose(disposing);
        }
    }
}
