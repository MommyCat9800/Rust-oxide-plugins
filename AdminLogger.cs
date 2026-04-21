using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("AdminLogger", "ChatGPT & MommyCat", "3.2.3")]
    public class AdminLogger : RustPlugin
    {
        [PluginReference]
        Plugin Vanish;

        private string webhookUrl = "https://discord.com/api/webhooks/1492560844251467837/55xTZJQRjfcLslq7JljKHH5wFfcYglpOP-rIaEO1phLbxSW6gOmFBnBFo3zJqCu8MqL_";

        private Dictionary<ulong, Vector3> lastPositions = new Dictionary<ulong, Vector3>();

        void Init()
        {
            PrintWarning("AdminLogger v3.2 loaded!");
            Send("🟢 AdminLogger ONLINE");

            timer.Every(1f, CheckTeleport);
        }

        #region TP CHECK

        void CheckTeleport()
        {
            foreach (var currentPlayer in BasePlayer.activePlayerList)
            {   
                
                if (currentPlayer == null) continue;



                Vector3 currentPPos = currentPlayer.transform.position;

                if (currentPlayer.IsDead())
                {
                    lastPositions.Remove(currentPlayer.userID);
                    continue;
                }

                if (!lastPositions.ContainsKey(currentPlayer.userID))
                {
                    lastPositions[currentPlayer.userID] = currentPPos;
                    continue;
                }

                float dist = Vector3.Distance(lastPositions[currentPlayer.userID], currentPPos);

                if (dist > 50f)
                {
                    if (IsVanished(currentPlayer))
                        Send($"🗺️ {currentPlayer.displayName} VANISH TP: {GetGrid(lastPositions[currentPlayer.userID])} → {GetGrid(currentPPos)}");
                    else
                        Send($"🗺️ {currentPlayer.displayName} TP: {GetGrid(lastPositions[currentPlayer.userID])} → {GetGrid(currentPPos)}");
                }

                lastPositions[currentPlayer.userID] = currentPPos;
            }
        }

        #endregion

        #region HELPERS

        BasePlayer GetPlayer(ConsoleSystem.Arg arg)
        {
            if (arg?.Connection?.player is BasePlayer p)
                return p;

            if (arg?.Player() is BasePlayer p2)
                return p2;

            return null;
        }

        BasePlayer FindPlayer(string input)
        {
            foreach (var p in BasePlayer.activePlayerList)
            {
                if (p.UserIDString == input)
                    return p;
            }
            return null;
        }

        bool IsVanished(BasePlayer player)
        {
            if (Vanish == null) return false;
            return (bool)Vanish.Call("IsInvisible", player);
        }

        string GetGrid(Vector3 pos)
        {
            int size = ConVar.Server.worldsize;
            float offset = size / 2f;

            int x = Mathf.FloorToInt((pos.x + offset) / 150f);
            int z = Mathf.FloorToInt((offset - pos.z) / 150f);

            char letter = (char)('A' + x);
            return $"{letter}{z}";
        }

        #endregion

        #region VANISH

        void OnVanishDisappear(BasePlayer player)
        {
            if (player == null) return;
            Send($"🟡 {player.displayName} zapnul VANISH");
        }

        void OnVanishReappear(BasePlayer player)
        {
            if (player == null) return;
            Send($"🟢 {player.displayName} vypnul VANISH");
        }

        #endregion

        #region COMMANDS

        object OnServerCommand(ConsoleSystem.Arg arg)
        {
            var player = GetPlayer(arg);
            if (player == null) return null;

            string cmd = arg.cmd.FullName.ToLower();
            string[] args = arg.Args;

            // 🎁 GIVE
            if (cmd.Contains("give"))
            {
                string itemInput = args != null && args.Length > 0 ? args[0] : "unknown";
                string amount = args != null && args.Length > 1 ? args[1] : "1";

                string itemName = itemInput;

                var def = ItemManager.FindItemDefinition(itemInput);

                if (def == null && int.TryParse(itemInput, out int itemId))
                {
                    def = ItemManager.FindItemDefinition(itemId);
                }

                if (def != null)
                {
                    itemName = $"{def.displayName.translated} ({def.shortname})";
                }

                Send($"🎁 {player.displayName} dal GIVE → {itemName} x{amount}");
            }

            // 👁️ SPECTATE (FIX NAME)
            if (cmd.Contains("spectate"))
            {
                string target = args != null && args.Length > 0 ? args[0] : "unknown";

                var targetPlayer = FindPlayer(target);

                string name = targetPlayer != null ? targetPlayer.displayName : target;

                Send($"👁️ {player.displayName} spectate → {name}");
            }

            return null;
        }

        #endregion

        #region KICK FIX (SERVER MESSAGE)

        void OnServerMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            if (message.Contains("Kicking"))
            {
                Send($"👢 {message}");
            }
        }

        #endregion

        #region BAN

        void OnUserBanned(string name, string id, string ip, string reason)
        {
            Send($"⛔ BAN: {name} ({id}) → {reason}");
        }

        #endregion

        #region DISCORD

        void Send(string msg)
        {
            if (string.IsNullOrEmpty(webhookUrl)) return;

            var payload = new
            {
                username = "ADMIN LOG",
                content = msg
            };

            webrequest.Enqueue(webhookUrl,
                Newtonsoft.Json.JsonConvert.SerializeObject(payload),
                (code, res) => { },
                this,
                Core.Libraries.RequestMethod.POST,
                new Dictionary<string, string>()
                {
                    ["Content-Type"] = "application/json"
                });
        }

        #endregion
    }
}
