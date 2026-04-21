using Newtonsoft.Json;
using Oxide.Core;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Boom Alert", "Matthew x ChatGPT", "4.0.1")]
    class BoomAlert : CovalencePlugin
    {
        private string webhook = "https://discordapp.com/api/webhooks/1492591603049566358/YFdZif3EcKTdyE3NZFYKDw-uuww1e51I7ryGf0LLEQyk2IhykRYXNJJEm_KDG8h7AfMS";

        private List<BuildingPrivlidge> tcCache = new List<BuildingPrivlidge>();

        private class RaidData
        {
            public float startTime;
            public float lastHit;

            public int c4 = 0;
            public int rocket = 0;
            public int satchel = 0;

            public string owner;
            public string grid;
        }

        private Dictionary<string, RaidData> raids = new Dictionary<string, RaidData>();

        private const float RAID_TIMEOUT = 300f; // 5 min bez aktivity = konec

        #region INIT

        void OnServerInitialized()
        {
            tcCache.AddRange(UnityEngine.Object.FindObjectsOfType<BuildingPrivlidge>());
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            var tc = entity as BuildingPrivlidge;
            if (tc != null)
                tcCache.Add(tc);
        }

        void OnEntityKill(BaseNetworkable entity)
        {
            var tc = entity as BuildingPrivlidge;
            if (tc != null)
                tcCache.Remove(tc);
        }

        #endregion

        #region HELPERS

        string GetGrid(Vector3 pos)
        {
            int size = ConVar.Server.worldsize;
            float offset = size / 2f;

            int x = Mathf.FloorToInt((pos.x + offset) / 150f);
            int z = Mathf.FloorToInt((offset - pos.z) / 150f);

            char letter = (char)('A' + x);
            return $"{letter}{z}";
        }

        (string owner, string tcID) GetBaseData(Vector3 pos)
        {
            BuildingPrivlidge closest = null;
            float dist = 999f;

            foreach (var tc in tcCache)
            {
                if (tc == null) continue;

                float d = Vector3.Distance(pos, tc.transform.position);
                if (d < dist && d < 30f)
                {
                    dist = d;
                    closest = tc;
                }
            }

            if (closest == null || closest.authorizedPlayers.Count == 0)
                return ("Neznámý", "none");

            List<string> names = new List<string>();

            foreach (ulong id in closest.authorizedPlayers)
            {
                var player = covalence.Players.FindPlayerById(id.ToString());
                names.Add(player != null ? player.Name : id.ToString());
            }

            return (string.Join(", ", names), closest.net.ID.ToString());
        }

        #endregion

        #region RAID LOGIC

        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (info == null || entity == null) return;

            string weapon = info?.WeaponPrefab?.ShortPrefabName ?? "";

            if (!weapon.Contains("explosive") && !weapon.Contains("rocket"))
                return;

            var attacker = info.InitiatorPlayer;
            if (attacker == null) return;

            Vector3 pos = entity.transform.position;

            var data = GetBaseData(pos);
            string owner = data.owner;
            string tcID = data.tcID;

            string grid = GetGrid(pos);

            float now = UnityEngine.Time.realtimeSinceStartup;

            // 🔥 RAID START
            if (!raids.ContainsKey(tcID))
            {
                raids[tcID] = new RaidData
                {
                    startTime = now,
                    lastHit = now,
                    owner = owner,
                    grid = grid
                };

                Send($"🚨 RAID STARTED\n🏠 {owner}\n📍 {grid}\n👤 {attacker.displayName}");
            }

            var raid = raids[tcID];
            raid.lastHit = now;

            // 📊 počítání
            if (weapon.Contains("explosive.timed")) raid.c4++;
            else if (weapon.Contains("explosive.satchel")) raid.satchel++;
            else if (weapon.Contains("rocket")) raid.rocket++;
        }

        void OnTick()
        {
            float now = UnityEngine.Time.realtimeSinceStartup;

            List<string> toRemove = new List<string>();

            foreach (var kvp in raids)
            {
                var tcID = kvp.Key;
                var raid = kvp.Value;

                if (now - raid.lastHit > RAID_TIMEOUT)
                {
                    float duration = now - raid.startTime;

                    Send(
                        $"🏁 RAID ENDED\n" +
                        $"🏠 {raid.owner}\n" +
                        $"📍 {raid.grid}\n" +
                        $"⏱️ {(int)(duration / 60)} min\n" +
                        $"💣 C4: {raid.c4}\n" +
                        $"🧨 Satchel: {raid.satchel}\n" +
                        $"🚀 Rocket: {raid.rocket}"
                    );

                    toRemove.Add(tcID);
                }
            }

            foreach (var id in toRemove)
                raids.Remove(id);
        }

        #endregion

        #region DISCORD

        private readonly Dictionary<string, string> headers = new Dictionary<string, string>
        {
            { "Content-Type", "application/json" }
        };

        void Send(string msg)
        {
            var payload = new
            {
                username = "RAID DETECTOR",
                content = msg
            };

            webrequest.Enqueue(
                webhook,
                JsonConvert.SerializeObject(payload),
                (code, res) => { },
                this,
                Oxide.Core.Libraries.RequestMethod.POST,
                headers
            );
        }

        #endregion
    }
}
